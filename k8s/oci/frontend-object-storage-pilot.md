# Piloto: SPA en OCI Object Storage

Este piloto separa el frontend del cluster OKE:

- El backend, datastores y worker siguen ejecutandose en Kubernetes.
- El SPA Angular se compila como archivos estaticos.
- Los archivos se publican en un bucket publico de OCI Object Storage.
- El SPA consume el API publico expuesto directamente por Ingress NGINX.

## Arquitectura del piloto

```text
Browser
  |
  | HTML/CSS/JS
  v
OCI Object Storage bucket
  |
  | HTTPS API calls
  v
https://api.157.137.225.220.sslip.io/api
  |
  v
OKE Ingress NGINX -> api:5081
```

El frontend de Kubernetes puede seguir desplegado mientras haces la prueba. Para la PoC simplificada, se apaga con:

```powershell
kubectl apply -f k8s\oci\manifests\99-disable-frontend-gateway.yaml
```

## Cambio aplicado al frontend

El frontend ya no depende de tener `/api` fijo en el bundle. Ahora carga `app-config.json` al iniciar:

```json
{
  "apiBaseUrl": "/api"
}
```

Para el piloto, el script sobrescribe ese archivo en el `dist` con:

```json
{
  "apiBaseUrl": "https://api.157.137.225.220.sslip.io/api"
}
```

Esto permite cambiar el backend de destino sin recompilar el Angular.

## Publicar el SPA en Object Storage

Desde la raiz del repositorio:

```powershell
.\k8s\oci\scripts\deploy-frontend-object-storage.ps1 `
  -Region sa-bogota-1 `
  -BucketName academic-registration-spa-pilot `
  -ApiBaseUrl https://api.157.137.225.220.sslip.io/api `
  -UpdateK8sCors
```

El script hace lo siguiente:

1. Ejecuta `npm run build -- --base-href ./` en `frontend`.
2. Escribe `dist/academic-registration-web/browser/app-config.json` con la URL del API.
3. Crea o actualiza el bucket con `ObjectReadWithoutList`.
4. Sube cada archivo con `Content-Type` correcto para HTML, JS, CSS, JSON, imagenes y fuentes.
5. Si usas `-UpdateK8sCors`, agrega el origen de Object Storage al ConfigMap y reinicia el API. Si el gateway sigue activo en otro ambiente, tambien intenta reiniciarlo.

La URL final queda con este formato:

```text
https://objectstorage.sa-bogota-1.oraclecloud.com/n/<namespace>/b/academic-registration-spa-pilot/o/index.html
```

## CORS

Como el navegador servira el SPA desde Object Storage y llamara al API en OKE, el API debe permitir el origen del frontend.

Para la URL path-style de Object Storage, el origen del navegador es:

```text
https://objectstorage.sa-bogota-1.oraclecloud.com
```

El script lo puede agregar automaticamente con `-UpdateK8sCors`. Si usas un dominio propio o CDN delante del bucket, pasa el origen real:

```powershell
.\k8s\oci\scripts\deploy-frontend-object-storage.ps1 `
  -BucketName academic-registration-spa-pilot `
  -ApiBaseUrl https://api.157.137.225.220.sslip.io/api `
  -CorsOrigin https://academic.example.com `
  -UpdateK8sCors
```

## Validacion

Verifica primero el API:

```powershell
curl.exe -i https://api.157.137.225.220.sslip.io/health
```

Luego abre la URL del Object Storage que imprime el script. En DevTools > Network deberias ver:

- `index.html`, `main-*.js`, `styles-*.css` servidos desde Object Storage.
- `app-config.json` servido desde Object Storage con `apiBaseUrl` apuntando al API.
- Requests a `https://api.157.137.225.220.sslip.io/api/subjects` y `/api/students`.

## Limitaciones del piloto

- Object Storage no hace `try_files` ni rewrite a `index.html` como NGINX.
- Este frontend actualmente no usa Angular Router, asi que no hay rutas profundas que recargar.
- Si luego agregas rutas como `/students/123`, conviene poner CDN/custom domain con fallback a `index.html`, o usar hash routing.
- Para produccion, es mejor poner un dominio propio o CDN delante del bucket para tener un origen CORS dedicado, cache control mas fino y HTTPS con nombre estable.

## Retirar el piloto

Para volver al estado anterior no necesitas cambiar el cluster. El frontend en OKE sigue disponible.

Si quieres cerrar el bucket desde OCI CLI:

```powershell
& 'C:\o\Scripts\oci.exe' os object bulk-delete `
  --bucket-name academic-registration-spa-pilot `
  --auth security_token `
  --region sa-bogota-1 `
  --force

& 'C:\o\Scripts\oci.exe' os bucket delete `
  --bucket-name academic-registration-spa-pilot `
  --auth security_token `
  --region sa-bogota-1 `
  --force
```
