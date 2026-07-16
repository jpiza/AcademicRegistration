# AcademicRegistration en Kubernetes

Estos manifiestos levantan el stack completo en el namespace `academic-registration`:

- `sqlserver` y `kafka` con PVCs locales de Minikube.
- `api`, `gateway`, `frontend` y `notifications-worker` como Deployments.
- Services internos para cada componente.
- Un Ingress llamado `academic-registration-routes` para entrada externa.

Los ReplicaSets no se definen a mano: Kubernetes los crea y administra a partir de cada Deployment. Puedes verlos con:

```powershell
kubectl get rs -n academic-registration
```

## Despliegue rapido en Minikube

Desde la raiz del repositorio:

```powershell
.\k8s\deploy-minikube.ps1
```

El script construye las imagenes dentro de Minikube, habilita el addon de Ingress y aplica los manifiestos.

Si ya construiste las imagenes:

```powershell
.\k8s\deploy-minikube.ps1 -SkipBuild
```

## Despliegue manual

Construir las imagenes dentro de Minikube:

```powershell
minikube image build -t academic-registration-api:dev -f Dockerfile.api backend
minikube image build -t academic-registration-gateway:dev -f Dockerfile.gateway backend
minikube image build -t academic-registration-notifications:dev -f Dockerfile.notifications backend
minikube image build -t academic-registration-frontend:dev -f Dockerfile frontend
```

Aplicar los recursos:

```powershell
minikube addons enable ingress
kubectl apply -f .\k8s
kubectl get pods,svc,ingress,rs -n academic-registration
```

## Acceso externo

Como este cluster de Minikube corre con el driver Docker en Windows, el addon de Ingress normalmente requiere un tunel. En una terminal aparte:

```powershell
minikube tunnel
```

Mientras el tunel este activo, agrega estas entradas al archivo `hosts` de Windows:

```text
127.0.0.1 academic-registration.local
127.0.0.1 gateway.academic-registration.local
127.0.0.1 api.academic-registration.local
```

Si usas otro driver de Minikube que expone la IP del nodo directamente, puedes obtenerla con:

```powershell
minikube ip
```

Y mapear los hosts a esa IP:

```text
<MINIKUBE_IP> academic-registration.local
<MINIKUBE_IP> gateway.academic-registration.local
<MINIKUBE_IP> api.academic-registration.local
```

URLs:

- Frontend: `http://academic-registration.local`
- Gateway: `http://gateway.academic-registration.local`
- API: `http://api.academic-registration.local`
- Swagger API: `http://api.academic-registration.local/swagger`

## Configuracion sensible

Los valores de `k8s/02-secrets.yaml` son para desarrollo local. Antes de usar SMTP real, cambia:

- `email-username`
- `email-password`
- `Email__Enabled` en `k8s/01-configmap.yaml`

SQL Server usa por defecto `AcademicReg!2026`, igual que el entorno Docker local.
