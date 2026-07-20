# Academic Registration AWS PoC

Esta carpeta adapta el proyecto a la arquitectura propuesta en la presentacion:

- Canal web: Angular en S3 + CloudFront.
- Borde y aplicacion: CloudFront hacia ALB, gateway YARP y API .NET en ECS Fargate.
- Datos: RDS MySQL privado por defecto para cuentas/free plan. SQL Server queda disponible con `db_engine = "sqlserver"` si tu cuenta lo permite.
- Eventos: transactional outbox en el API, EventBridge como bus y SQS + DLQ para procesamiento asincrono.
- Secretos: Secrets Manager + KMS, inyectados como variables seguras en ECS.
- Observabilidad: CloudWatch Logs, alarmas basicas y X-Ray daemon sidecar.
- CI/CD: GitHub Actions para build/test/validate y despliegue automatico desde `main`.

## Estructura

- `terraform/`: infraestructura AWS de la PoC.
- `.github/workflows/aws-poc-ci-cd.yml`: pipeline de validacion y despliegue.

## Flujo de eventos

1. El API guarda el cambio de estudiante y el mensaje outbox en la misma transaccion de RDS.
2. `OutboxMessageProcessor` publica el payload en EventBridge.
3. Una regla de EventBridge enruta `student.registered` y `student.enrollment.changed` a SQS.
4. El worker lee SQS, envia la notificacion y borra el mensaje.
5. Si el worker falla, SQS reintenta y luego mueve el mensaje a la DLQ.

## Despliegue continuo con GitHub Actions

El workflow `.github/workflows/aws-poc-ci-cd.yml` queda con este comportamiento:

- Pull request a `main`: compila backend, ejecuta tests, compila frontend y valida Terraform.
- Push a `main`: hace lo anterior y despues despliega automaticamente en AWS.
- Ejecucion manual: permite redeploy desde `Actions > AWS PoC CI/CD > Run workflow` usando `deploy=true`.

Configura estos `secrets` del repositorio:

- `AWS_ACCESS_KEY_ID`: access key para Terraform, ECR, S3 y CloudFront.
- `AWS_SECRET_ACCESS_KEY`: secret key para Terraform, ECR, S3 y CloudFront.
- `TF_STATE_BUCKET`: bucket S3 para el estado remoto de Terraform.
- `TF_STATE_LOCK_TABLE`: tabla DynamoDB para lock del estado.
- `SMTP_PASSWORD`: opcional si `EMAIL_ENABLED=true`.

Configura estos `variables` del repositorio si quieres cambiar defaults:

- `AWS_REGION`: default `us-east-1`.
- `PROJECT_NAME`: default `academic-registration`.
- `ENVIRONMENT`: default `poc`.
- `DB_ENGINE`: default `mysql`. Usa `sqlserver` solo si tu cuenta AWS lo permite.
- `DB_INSTANCE_CLASS`: default `db.t3.micro`.
- `DB_STORAGE_TYPE`: default `gp2`.
- `EMAIL_ENABLED`: default `false`.
- `SMTP_HOST`, `SMTP_USERNAME`, `SMTP_FROM`: opcionales.

El bucket `TF_STATE_BUCKET` y la tabla `TF_STATE_LOCK_TABLE` deben existir antes del primer deploy si vas a usar estado remoto desde GitHub Actions. Si ya estas ejecutando Terraform localmente con estado local, migra primero el estado a S3 o el workflow creara recursos como si fuera un estado nuevo.

Despues de configurar secrets y variables:

1. Sube la rama a GitHub.
2. Abre un pull request contra `main` para validar.
3. Haz merge a `main`.
4. GitHub Actions construira imagenes, aplicara Terraform, publicara Angular en S3 e invalidara CloudFront.

El workflow:

1. Valida .NET, Angular y Terraform.
2. Crea o actualiza los repositorios ECR.
3. Construye y sube imagenes de API, gateway y worker.
4. Aplica Terraform con el tag de imagen.
5. Compila Angular y publica los archivos a S3.
6. Invalida CloudFront.

## Despliegue manual

```powershell
cd infrastructure\aws-poc\terraform
copy terraform.tfvars.example terraform.tfvars
terraform init
terraform apply `
  -target=aws_ecr_repository.api `
  -target=aws_ecr_repository.gateway `
  -target=aws_ecr_repository.notifications
```

Despues construye y sube imagenes a ECR con el tag que quieras usar, por ejemplo `poc-001`.

```powershell
$accountId = aws sts get-caller-identity --query Account --output text
$region = "us-east-1"
$registry = "$accountId.dkr.ecr.$region.amazonaws.com"
$tag = "poc-001"
aws ecr get-login-password --region $region | docker login --username AWS --password-stdin $registry

docker build -f backend\Dockerfile.api -t "$registry/academic-registration-poc/api:$tag" backend
docker push "$registry/academic-registration-poc/api:$tag"

docker build -f backend\Dockerfile.gateway -t "$registry/academic-registration-poc/gateway:$tag" backend
docker push "$registry/academic-registration-poc/gateway:$tag"

docker build -f backend\Dockerfile.notifications -t "$registry/academic-registration-poc/notifications:$tag" backend
docker push "$registry/academic-registration-poc/notifications:$tag"
```

Aplica el resto de la infraestructura:

```powershell
terraform apply -var "image_tag=$tag"
terraform output frontend_url
terraform output api_base_url
```

Publica el frontend:

```powershell
cd ..\..\..\frontend
npm ci
npm run build
cd ..\infrastructure\aws-poc\terraform

$bucket = terraform output -raw frontend_bucket
$distributionId = terraform output -raw cloudfront_distribution_id
aws s3 sync ..\..\..\frontend\dist\academic-registration-web\browser "s3://$bucket" --delete
aws cloudfront create-invalidation --distribution-id $distributionId --paths "/*"
```

Si tu build de Angular emite directamente en `dist\academic-registration-web`, usa esa carpeta en el `aws s3 sync`.

## Notas de seguridad y costo

- RDS, ECS, ALB, CloudFront, Secrets Manager y KMS pueden generar costo aunque la base use MySQL micro. Destruye la PoC cuando termines:

```powershell
terraform destroy
```

- La cadena de conexion de RDS y la clave SMTP quedan en Secrets Manager. Terraform tambien guarda valores sensibles en el state; usa backend cifrado y acceso limitado.
- La PoC usa tasks ECS con IP publica para evitar NAT Gateway y bajar costo. Para produccion, mueve ECS a subredes privadas con NAT o VPC endpoints.
- `enable_waf=false` por defecto para controlar costo; activalo si quieres probar WAF en CloudFront.
- Para permitir solo IPs autorizadas por WAF, configura `terraform.tfvars` asi:

```hcl
enable_waf                 = true
attach_waf_to_cloudfront   = true
enable_waf_ip_allowlist    = true
waf_allowed_ipv4_cidrs     = ["203.0.113.10/32"]
restrict_alb_to_cloudfront = true
```

  Usa `/32` para una IP publica individual. `restrict_alb_to_cloudfront=true` evita que alguien salte WAF usando el DNS directo del ALB.
- Para eliminar WAF sin el error `WAFAssociatedItemException`, hazlo en dos fases:

```hcl
enable_waf               = true
attach_waf_to_cloudfront = false
```

  Aplica Terraform y espera a que CloudFront quede `Deployed`. Luego cambia `enable_waf=false` y vuelve a aplicar para destruir el Web ACL.
- La arquitectura puede incorporar API Gateway HTTP API delante del ALB en una segunda iteracion. Esta PoC usa CloudFront + ALB + gateway YARP para mantener el despliegue simple y verificable.
