# AcademicRegistration en OCI OKE

Guia paso a paso para desplegar AcademicRegistration en Oracle Cloud Infrastructure usando OKE, OCIR, Kubernetes manifests e Ingress NGINX.

Esta guia documenta el flujo usado para `sa-bogota-1`, pero deja parametros para repetirlo en otra region.

## Arquitectura desplegada

Recursos OCI:

- VCN para OKE.
- Internet Gateway.
- Route Table publica.
- Security List con trafico interno, HTTP, HTTPS y API server.
- Subnet publica para endpoint del cluster y LoadBalancers.
- Subnet de nodos.
- OKE Basic cluster.
- Node pool.
- OCIR private repositories.
- LoadBalancer publico creado por `ingress-nginx`.

## Guardrails de costo / Always Free

Para que este despliegue se mantenga dentro de Always Free, valida estos puntos antes de crear o actualizar recursos:

- OKE debe ser `BASIC_CLUSTER`; los clusters Enhanced agregan tarifa de control plane.
- El node pool debe usar `VM.Standard.A1.Flex` en la home region, con maximo total de `2 OCPU` y `12 GB` de memoria para tenancies Always Free.
- El LoadBalancer publico debe quedarse en `flexible` con minimo y maximo `10 Mbps`.
- Boot volumes + block volumes deben sumar como maximo `200 GB` en la home region.
- Object Storage/OCIR deben mantenerse dentro del cupo Always Free de Object Storage.
- OCI Streaming with Apache Kafka no es Always Free. Para costo cero, usa el Kafka in-cluster de `04-datastores.yaml` y no apliques `99-disable-incluster-kafka.yaml`.

Recursos Kubernetes:

- Namespace `academic-registration`.
- ConfigMap de la aplicacion.
- Secrets de app y `imagePullSecret` para OCIR.
- PVCs para MySQL y Kafka.
- Deployments y Services para MySQL, Kafka, API, worker, gateway y frontend.
- Ingress para exponer frontend, gateway y API.

## Valores usados

```text
Region OCI:           sa-bogota-1
OCIR registry:        ocir.sa-bogota-1.oci.oraclecloud.com
OCIR namespace:       ax8nej0w8huf
K8s namespace:        academic-registration
Image prefix:         ocir.sa-bogota-1.oci.oraclecloud.com/ax8nej0w8huf/academic-registration
Tag:                  dev
Ingress public IP:    157.137.225.220
```

Hosts publicados:

```text
academic.157.137.225.220.sslip.io
gateway.157.137.225.220.sslip.io
api.157.137.225.220.sslip.io
```

## 1. Prerrequisitos locales

Herramientas necesarias:

- Docker Desktop.
- `kubectl`.
- OCI CLI.
- PowerShell.

Instala OCI CLI con el script del repo:

```powershell
.\k8s\oci\scripts\install-oci-cli.ps1
```

El script instala OCI CLI en `C:\o` para evitar problemas de rutas largas en Windows/OneDrive.

Verifica Docker:

```powershell
docker version
docker buildx version
```

## 2. Autenticacion segura en OCI

No compartas usuario, password ni OTP por chat. Usa el flujo de navegador:

```powershell
.\k8s\oci\scripts\authenticate-oci.ps1 -Region sa-bogota-1
```

Valida la sesion:

```powershell
& 'C:\o\Scripts\oci.exe' iam region-subscription list `
  --tenancy-id ((Select-String -Path "$env:USERPROFILE\.oci\config" -Pattern '^tenancy=').Line.Split('=')[1]) `
  --auth security_token `
  --region sa-bogota-1
```

## 3. Crear red, cluster OKE y node pool

La forma automatizada es:

```powershell
.\k8s\oci\scripts\provision-oke.ps1 -Region sa-bogota-1
```

Ese script crea o reutiliza:

- VCN `academic-registration-vcn`.
- Internet Gateway.
- Route Table publica.
- Security List publica.
- Subnet publica para endpoint/LoadBalancers.
- Subnet de nodos.
- OKE Basic cluster `academic-registration-oke`.
- Node pool `acadreg-a1-pool` por defecto.

Parametros utiles:

```powershell
.\k8s\oci\scripts\provision-oke.ps1 `
  -Region sa-bogota-1 `
  -NodeShape VM.Standard.A1.Flex `
  -NodeOcpus 2 `
  -NodeMemoryGb 12 `
  -NodeCount 1
```

### Si lo haces desde consola OCI

1. Entra a OCI Console.
2. Ve a Developer Services > Kubernetes Clusters (OKE).
3. Crea un cluster tipo Basic.
4. Usa endpoint publico para pruebas.
5. Usa una VCN con salida a internet.
6. Crea una subnet para servicios/LoadBalancer.
7. Crea una subnet para nodos.
8. Crea un node pool.

En Free Tier con Ampere A1:

```text
Shape: VM.Standard.A1.Flex
```

Si OCI responde `Out of host capacity`, no es error del manifiesto. Significa que OCI no tiene capacidad fisica disponible para ese shape en ese momento. Opciones:

- Reintentar mas tarde.
- Bajar OCPU/RAM.
- Probar otra region.
- Usar un shape pago con creditos de prueba, validando costos.

## 4. Configurar kubeconfig

Cuando el cluster exista, copia el OCID del cluster y ejecuta:

```powershell
.\k8s\oci\scripts\configure-kubeconfig.ps1 `
  -ClusterId "ocid1.cluster.oc1.sa-bogota-1..." `
  -Region sa-bogota-1
```

Verifica nodos y arquitectura:

```powershell
kubectl get nodes -o wide
kubectl get nodes -o jsonpath="{range .items[*]}{.metadata.name}{' '}{.status.nodeInfo.architecture}{'\n'}{end}"
```

La arquitectura importa para las imagenes:

- Node pool Ampere A1: `linux/arm64`.
- Node pool x86: `linux/amd64`.

Si mantienes un pool x86, compila con:

```text
linux/amd64
```

Si usas el default Always Free Ampere A1, compila con:

```text
linux/arm64
```

## 5. Crear repositorios OCIR

Obtiene el namespace de OCIR:

```powershell
& 'C:\o\Scripts\oci.exe' os ns get `
  --auth security_token `
  --region sa-bogota-1
```

Para este ambiente el namespace es:

```text
ax8nej0w8huf
```

Crea los repositorios privados:

```powershell
$compartmentId = ((Select-String -Path "$env:USERPROFILE\.oci\config" -Pattern '^tenancy=').Line.Split('=')[1])
$repos = @(
  'academic-registration/api',
  'academic-registration/gateway',
  'academic-registration/notifications',
  'academic-registration/frontend'
)

foreach ($repo in $repos) {
  & 'C:\o\Scripts\oci.exe' artifacts container repository create `
    --compartment-id $compartmentId `
    --display-name $repo `
    --is-public false `
    --auth security_token `
    --region sa-bogota-1 `
    --wait-for-state AVAILABLE
}
```

Verifica:

```powershell
& 'C:\o\Scripts\oci.exe' artifacts container repository list `
  --compartment-id $compartmentId `
  --auth security_token `
  --region sa-bogota-1 `
  --all
```

## 6. Crear Auth Token y hacer Docker login

El password de Docker para OCIR no es tu password de OCI. Debes generar un Auth Token.

En OCI Console:

1. Arriba a la derecha, abre tu perfil.
2. Entra a User settings o My profile.
3. Ve a Tokens and keys.
4. En Auth Tokens, selecciona Generate token.
5. Usa una descripcion, por ejemplo `academic-registration-ocir`.
6. Copia el token inmediatamente. OCI solo lo muestra una vez.

Haz login:

```powershell
docker login ocir.sa-bogota-1.oci.oraclecloud.com
```

Usuario usado:

```text
ax8nej0w8huf/juliopizag@outlook.com
```

Password:

```text
<Auth Token copiado desde OCI Console>
```

Resultado esperado:

```text
Login Succeeded
```

## 7. Compilar y subir imagenes

El script `build-push-images.ps1` compila y sube:

- API.
- Gateway.
- Notifications worker.
- Frontend.

Para el pool actual `amd64`:

```powershell
.\k8s\oci\scripts\build-push-images.ps1 `
  -RegionKey sa-bogota-1 `
  -RegistryHost ocir.sa-bogota-1.oci.oraclecloud.com `
  -TenancyNamespace ax8nej0w8huf `
  -Tag dev `
  -Platform linux/amd64 `
  -Output push
```

Para nodos Ampere A1:

```powershell
.\k8s\oci\scripts\build-push-images.ps1 `
  -RegionKey sa-bogota-1 `
  -RegistryHost ocir.sa-bogota-1.oci.oraclecloud.com `
  -TenancyNamespace ax8nej0w8huf `
  -Tag dev `
  -Platform linux/arm64 `
  -Output push
```

Si solo quieres compilar localmente sin subir:

```powershell
.\k8s\oci\scripts\build-push-images.ps1 `
  -RegionKey sa-bogota-1 `
  -RegistryHost ocir.sa-bogota-1.oci.oraclecloud.com `
  -TenancyNamespace ax8nej0w8huf `
  -Tag dev `
  -Platform linux/amd64 `
  -Output load
```

Imagenes esperadas:

```text
ocir.sa-bogota-1.oci.oraclecloud.com/ax8nej0w8huf/academic-registration/api:dev
ocir.sa-bogota-1.oci.oraclecloud.com/ax8nej0w8huf/academic-registration/gateway:dev
ocir.sa-bogota-1.oci.oraclecloud.com/ax8nej0w8huf/academic-registration/notifications:dev
ocir.sa-bogota-1.oci.oraclecloud.com/ax8nej0w8huf/academic-registration/frontend:dev
```

## 8. Crear secrets de Kubernetes

Aplica el namespace:

```powershell
kubectl apply -f .\k8s\oci\manifests\00-namespace.yaml
```

### Secret de OCIR

Si Docker login ya esta hecho en Docker Desktop, puedes crear el secret desde el credential store local:

```powershell
$registry = 'ocir.sa-bogota-1.oci.oraclecloud.com'
$result = & cmd.exe /c "echo $registry| docker-credential-desktop get"
$cred = ($result | Out-String | ConvertFrom-Json)

kubectl create secret docker-registry ocir-secret `
  --namespace academic-registration `
  --docker-server $registry `
  --docker-username $cred.Username `
  --docker-password $cred.Secret `
  --dry-run=client `
  -o yaml | kubectl apply -f -
```

Verifica:

```powershell
kubectl get secret ocir-secret -n academic-registration
```

### Secret de aplicacion

El manifiesto `02-secrets.yaml` crea `academic-registration-secrets`.

Antes de aplicarlo, revisa y reemplaza valores sensibles. No uses credenciales reales en repos publicos.

```powershell
kubectl apply -f .\k8s\oci\manifests\02-secrets.yaml
```

## 9. Aplicar infraestructura base de Kubernetes

Aplica en este orden:

```powershell
kubectl apply -f .\k8s\oci\manifests\00-namespace.yaml
kubectl apply -f .\k8s\oci\manifests\01-configmap.yaml
kubectl apply -f .\k8s\oci\manifests\02-secrets.yaml
kubectl apply -f .\k8s\oci\manifests\03-storage.yaml
kubectl apply -f .\k8s\oci\manifests\04-datastores.yaml
```

Espera MySQL y Kafka:

```powershell
kubectl rollout status deployment/mysql -n academic-registration --timeout=8m
kubectl rollout status deployment/kafka -n academic-registration --timeout=8m
kubectl get pods,svc -n academic-registration
```

## 10. Desplegar servicios de aplicacion

Aplica API primero:

```powershell
kubectl apply -f .\k8s\oci\manifests\05-api.yaml
kubectl rollout status deployment/api -n academic-registration --timeout=8m
```

Luego worker, gateway y frontend:

```powershell
kubectl apply `
  -f .\k8s\oci\manifests\06-notifications-worker.yaml `
  -f .\k8s\oci\manifests\07-gateway.yaml `
  -f .\k8s\oci\manifests\08-frontend.yaml

kubectl rollout status deployment/notifications-worker -n academic-registration --timeout=6m
kubectl rollout status deployment/gateway -n academic-registration --timeout=6m
kubectl rollout status deployment/frontend -n academic-registration --timeout=6m
```

Verifica:

```powershell
kubectl get pods,svc -n academic-registration
```

Estado esperado:

```text
api                     1/1 Running
mysql                   1/1 Running
kafka                   1/1 Running
notifications-worker    1/1 Running
gateway                 2/2 Running
frontend                2/2 Running
```

## 11. Instalar Ingress NGINX

Instala el controller:

```powershell
.\k8s\oci\scripts\install-ingress-nginx.ps1
```

El script:

- Aplica el manifiesto oficial de `ingress-nginx`.
- Crea `ingressClassName: nginx`.
- Crea un Service `LoadBalancer`.
- Anota el LoadBalancer OCI como flexible de 10 Mbps.

Obtiene la IP publica:

```powershell
kubectl get svc ingress-nginx-controller -n ingress-nginx
```

Ejemplo del despliegue actual:

```text
157.137.225.220
```

## 12. Aplicar rutas externas

Primero instala `cert-manager` para emitir certificados TLS con Let's Encrypt:

```powershell
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.21.0/cert-manager.yaml

kubectl wait --namespace cert-manager `
  --for=condition=ready pod `
  --selector=app.kubernetes.io/instance=cert-manager `
  --timeout=300s
```

Aplica el `ClusterIssuer` de Let's Encrypt:

```powershell
kubectl apply -f .\k8s\oci\manifests\10-letsencrypt-clusterissuer.yaml
kubectl get clusterissuer letsencrypt-prod
```

Aplica el Ingress:

```powershell
kubectl apply -f .\k8s\oci\manifests\09-routes.yaml
kubectl get ingress -n academic-registration
```

Hosts:

```text
academic.157.137.225.220.sslip.io
gateway.157.137.225.220.sslip.io
api.157.137.225.220.sslip.io
```

## 13. Dominio dummy con sslip.io

Para pruebas publicas sin comprar dominio y sin editar `hosts`, este despliegue usa `sslip.io`.

`sslip.io` resuelve automaticamente cualquier hostname que tenga una IP embebida. Por ejemplo:

```text
academic.157.137.225.220.sslip.io          -> 157.137.225.220
api.157.137.225.220.sslip.io               -> 157.137.225.220
gateway.157.137.225.220.sslip.io           -> 157.137.225.220
```

Esto no requiere Oracle DNS. Si la IP del LoadBalancer cambia, actualiza `09-routes.yaml` con la nueva IP embebida y aplica de nuevo el Ingress.

Para produccion, usa un dominio real y crea registros DNS publicos tipo `A` en OCI DNS o en tu proveedor DNS.

## 14. HTTPS con Let's Encrypt

El Ingress usa:

- `cert-manager.io/cluster-issuer: letsencrypt-prod`
- `nginx.ingress.kubernetes.io/ssl-redirect: "true"`
- TLS secret `academic-registration-tls`

Verifica el certificado:

```powershell
kubectl get certificate -n academic-registration
kubectl describe certificate academic-registration-tls -n academic-registration
```

Estado esperado:

```text
academic-registration-tls   True   academic-registration-tls
```

Si el certificado queda pendiente, revisa:

```powershell
kubectl get certificaterequest,order,challenge -n academic-registration
kubectl get events -n academic-registration --sort-by=.lastTimestamp
```

## 15. Verificacion externa

Prueba las URLs publicas directamente:

```powershell
curl.exe -I http://academic.157.137.225.220.sslip.io/
curl.exe -I https://academic.157.137.225.220.sslip.io/
curl.exe -i https://api.157.137.225.220.sslip.io/health
curl.exe -i https://gateway.157.137.225.220.sslip.io/health
```

Respuestas esperadas:

```text
HTTP:     308 Permanent Redirect hacia HTTPS
Frontend: 200 OK por HTTPS
API:      {"status":"Healthy","service":"AcademicRegistration.Api"}
Gateway:  {"status":"Healthy","service":"AcademicRegistration.Gateway"}
```

URLs:

```text
https://academic.157.137.225.220.sslip.io
https://api.157.137.225.220.sslip.io/health
https://gateway.157.137.225.220.sslip.io/health
```

## 16. Flujo alternativo todo-en-uno

Si quieres que el script cree secrets y aplique todos los manifests:

```powershell
$token = Read-Host "OCIR auth token" -AsSecureString

.\k8s\oci\scripts\deploy-app.ps1 `
  -RegionKey sa-bogota-1 `
  -RegistryHost ocir.sa-bogota-1.oci.oraclecloud.com `
  -TenancyNamespace ax8nej0w8huf `
  -DockerUsername "ax8nej0w8huf/juliopizag@outlook.com" `
  -DockerAuthToken $token `
  -Tag dev
```

Este flujo es util en despliegues limpios. Si ya aplicaste recursos manualmente, puedes seguir el flujo por manifiestos de las secciones anteriores.

## Troubleshooting

### `Out of host capacity`

Ocurre al crear nodos `VM.Standard.A1.Flex`.

Soluciones:

- Reintentar mas tarde.
- Bajar OCPU/RAM.
- Usar otra region.
- Usar otro shape validando costos.

### `Docker login Unauthorized`

Verifica:

- Registry: `ocir.sa-bogota-1.oci.oraclecloud.com`.
- Namespace: `ax8nej0w8huf`.
- Usuario: `ax8nej0w8huf/juliopizag@outlook.com`.
- Password: Auth Token de OCI, no password de cuenta.
- El token fue copiado al momento de generarlo.

### `ImagePullBackOff` por arquitectura

Ejemplo de error:

```text
no image found in image index for architecture "amd64"
```

Significa que el nodo es `amd64`, pero el tag tiene solo imagen `arm64`, o al contrario.

Verifica arquitectura:

```powershell
kubectl get nodes -o jsonpath="{range .items[*]}{.metadata.name}{' '}{.status.nodeInfo.architecture}{'\n'}{end}"
```

Recompila con la plataforma correcta:

```powershell
.\k8s\oci\scripts\build-push-images.ps1 `
  -RegionKey sa-bogota-1 `
  -RegistryHost ocir.sa-bogota-1.oci.oraclecloud.com `
  -TenancyNamespace ax8nej0w8huf `
  -Tag dev `
  -Platform linux/amd64 `
  -Output push
```

Recrea el pod:

```powershell
kubectl delete pod -n academic-registration -l app.kubernetes.io/component=api
```

### `kubectl` dice que `oci` no existe

Ejecuta de nuevo:

```powershell
.\k8s\oci\scripts\configure-kubeconfig.ps1 `
  -ClusterId "ocid1.cluster.oc1.sa-bogota-1..." `
  -Region sa-bogota-1
```

El script ajusta el kubeconfig para usar `C:\o\Scripts\oci.exe`.

### Sesion OCI expirada

Renueva:

```powershell
.\k8s\oci\scripts\authenticate-oci.ps1 -Region sa-bogota-1
```

### Ver logs

```powershell
kubectl logs -f deployment/api -n academic-registration
kubectl logs -f deployment/gateway -n academic-registration
kubectl logs -f deployment/notifications-worker -n academic-registration
```

### Ver eventos

```powershell
kubectl get events -n academic-registration --sort-by=.lastTimestamp
```

## Limpieza opcional

Para borrar solo la aplicacion Kubernetes:

```powershell
kubectl delete namespace academic-registration
```

Para borrar Ingress NGINX:

```powershell
kubectl delete namespace ingress-nginx
```

Para cortar costos de recursos pagados, revisa especialmente:

- Node pools que no sean `VM.Standard.A1.Flex` dentro de `2 OCPU / 12 GB`.
- Clusters de OCI Streaming with Apache Kafka.
- LoadBalancers por encima de `10 Mbps` o balanceadores adicionales.
- Block/boot volumes por encima de `200 GB` combinados.

Borra recursos OCI como OKE, VCN, subnets, repos OCIR, Kafka gestionado o LoadBalancers con cuidado desde la consola OCI o con CLI, revisando dependencias antes de eliminar.

## Variante: SPA en Object Storage

Para probar el frontend fuera del cluster, usando OCI Object Storage como hosting estatico y el gateway de OKE como backend, ver:

```text
k8s/oci/frontend-object-storage-pilot.md
```
