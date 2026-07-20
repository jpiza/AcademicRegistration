# Academic Registration

Solucion para la prueba tecnica de registro de estudiantes.

## Requisitos cubiertos

- CRUD de estudiantes.
- Catalogo de 10 materias.
- Cada materia vale 3 creditos.
- Cada estudiante selecciona exactamente 3 materias.
- Cinco profesores dictan dos materias cada uno.
- Un estudiante no puede tener dos clases con el mismo profesor.
- Consulta de registros de otros estudiantes.
- Consulta de nombres de companeros por materia compartida.

## Estructura

- `backend/src/AcademicRegistration.Domain`: entidades, value objects, eventos de dominio, repositorios y unidad de trabajo.
- `backend/src/AcademicRegistration.Application`: CQRS con MediatR, DTOs, validators y pipeline behaviors.
- `backend/src/AcademicRegistration.Infrastructure`: EF Core, proveedores de base de datos, repositorios y semilla de catalogo.
- `backend/src/AcademicRegistration.Infrastructure.Migrations.MySql`: migraciones EF Core para MySQL.
- `backend/src/AcademicRegistration.Infrastructure.Migrations.SqlServer`: migraciones EF Core para SQL Server.
- `backend/src/AcademicRegistration.Api`: API REST con controllers y manejo de errores.
- `backend/src/AcademicRegistration.Gateway`: API gateway con YARP hacia el API.
- `backend/src/AcademicRegistration.Notifications.Worker`: consumidor SQS para notificaciones por correo.
- `frontend`: Angular 22 standalone/zoneless con signals.
- `database`: scripts SQL Server y MySQL.

## Backend

Restaurar y compilar:

```powershell
dotnet restore .\backend\AcademicRegistration.slnx
dotnet build .\backend\AcademicRegistration.slnx
dotnet test .\backend\AcademicRegistration.slnx
```

Ejecutar API y gateway:

```powershell
dotnet run --project .\backend\src\AcademicRegistration.Api\AcademicRegistration.Api.csproj
dotnet run --project .\backend\src\AcademicRegistration.Gateway\AcademicRegistration.Gateway.csproj
dotnet run --project .\backend\src\AcademicRegistration.Notifications.Worker\AcademicRegistration.Notifications.Worker.csproj
```

URLs por defecto:

- API: `http://localhost:5081`
- Swagger API: `http://localhost:5081/swagger`
- Gateway: `http://localhost:5080`

### Eventos y notificaciones

El agregado `Student` levanta `StudentRegisteredEvent` al registrar un estudiante y `StudentEnrollmentChangedEvent` al actualizar su seleccion de materias. La capa Application los traduce a `StudentNotificationRequestedIntegrationEvent`.

Para mantener consistencia entre la base de datos y EventBridge, el API usa el patron transactional outbox:

1. El comando modifica el estudiante y levanta eventos de dominio.
2. Antes de guardar, MediatR maneja esos eventos y `IEventBus` inserta el mensaje en `OutboxMessages`.
3. `Students`, `StudentSubjects` y `OutboxMessages` se guardan en la misma transaccion de EF Core.
4. `OutboxMessageProcessor` lee mensajes pendientes, los publica en EventBridge y marca `ProcessedOnUtc`.
5. Si EventBridge falla, el mensaje queda con `Error` y `RetryCount` para reintento.

Esto evita perder mensajes cuando la base de datos confirma el cambio pero EventBridge no esta disponible. La contraparte es que EventBridge/SQS pueden entregar duplicados si el API publica y se cae antes de marcar el outbox como procesado; por eso los consumidores deben tratar `eventId` como llave de idempotencia.

La configuracion EventBridge del publicador esta en:

```json
"EventBridge": {
  "EventBusName": "academic-registration",
  "Source": "academic-registration.api",
  "Region": "us-east-1",
  "ServiceUrl": "http://localhost:4566"
}
```

El procesador outbox se controla con:

```json
"Outbox": {
  "BatchSize": 20,
  "PollingIntervalSeconds": 5,
  "MaxRetries": 0
}
```

`MaxRetries = 0` significa reintentos indefinidos.

El worker `AcademicRegistration.Notifications.Worker` consume la cola SQS `academic-registration-notifications` y envia la notificacion por correo. Por defecto `Email:Enabled` es `false`, asi que el correo se simula en logs; para envio real configura SMTP con `Email__Enabled=true`, `Email__Host`, `Email__Port`, `Email__From`, `Email__UserName` y `Email__Password`.

La configuracion SQS del worker esta en:

```json
"Sqs": {
  "QueueUrl": "http://localhost:4566/000000000000/academic-registration-notifications",
  "Region": "us-east-1",
  "ServiceUrl": "http://localhost:4566",
  "MaxNumberOfMessages": 10,
  "WaitTimeSeconds": 20,
  "VisibilityTimeoutSeconds": 30
}
```

Para Gmail, usa una contrasena de aplicacion, no la contrasena normal de la cuenta:

```powershell
$env:Email__Enabled = "true"
$env:Email__Host = "smtp.gmail.com"
$env:Email__Port = "587"
$env:Email__EnableSsl = "true"
$env:Email__From = "tu-correo@gmail.com"
$env:Email__UserName = "tu-correo@gmail.com"
$env:Email__Password = "tu-contrasena-de-aplicacion"
dotnet run --project .\backend\src\AcademicRegistration.Notifications.Worker\AcademicRegistration.Notifications.Worker.csproj
```

### Proveedor de base de datos

El API puede trabajar con MySQL o SQL Server. El proveedor activo se define con:

```json
{
  "Configuracion": {
    "Conexion": "MySQL"
  }
}
```

Valores validos:

- `MySQL`
- `SQL`

Las cadenas de conexion estan en `backend/src/AcademicRegistration.Api/appsettings.json`:

```json
"CadenasConexion": {
  "ConexionSQL": "Server=(localdb)\\MSSQLLocalDB;Database=AcademicRegistrationDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True",
  "ConexionMySQL": "server=localhost;port=3306;database=AcademicRegistrationDb;user=root;password=123456"
}
```

Tambien se puede cambiar el proveedor desde PowerShell sin editar archivos:

```powershell
$env:Configuracion__Conexion = "SQL"
dotnet run --project .\backend\src\AcademicRegistration.Api\AcademicRegistration.Api.csproj --launch-profile http
```

Para MySQL:

```powershell
$env:Configuracion__Conexion = "MySQL"
dotnet run --project .\backend\src\AcademicRegistration.Api\AcademicRegistration.Api.csproj --launch-profile http
```

Para limpiar la variable de entorno:

```powershell
Remove-Item Env:\Configuracion__Conexion
```

En `Development`, el API ejecuta `Database.Migrate()` al iniciar y despues siembra profesores/materias si no existen. No se debe mezclar `EnsureCreated` con migraciones.

### Migraciones EF Core

Las migraciones estan separadas por proveedor porque EF Core genera tipos, anotaciones y snapshots diferentes para MySQL y SQL Server.

- MySQL: `backend/src/AcademicRegistration.Infrastructure.Migrations.MySql`
- SQL Server: `backend/src/AcademicRegistration.Infrastructure.Migrations.SqlServer`

Antes de usar `dotnet ef`, se recomienda tener la herramienta alineada con la version de EF Core del proyecto:

```powershell
dotnet tool update --global dotnet-ef --version 10.0.9
```

Desde la carpeta `backend`, crear una migracion para MySQL:

```powershell
dotnet ef migrations add NombreMigracion `
  -p .\src\AcademicRegistration.Infrastructure.Migrations.MySql\AcademicRegistration.Infrastructure.Migrations.MySql.csproj `
  -s .\src\AcademicRegistration.Api\AcademicRegistration.Api.csproj `
  -c AcademicRegistrationDbContext `
  -o Persistence\Migrations `
  -- "--Configuracion:Conexion=MySQL"
```

Aplicar migraciones de MySQL:

```powershell
dotnet ef database update `
  -p .\src\AcademicRegistration.Infrastructure.Migrations.MySql\AcademicRegistration.Infrastructure.Migrations.MySql.csproj `
  -s .\src\AcademicRegistration.Api\AcademicRegistration.Api.csproj `
  -c AcademicRegistrationDbContext `
  -- "--Configuracion:Conexion=MySQL"
```

Crear una migracion para SQL Server:

```powershell
dotnet ef migrations add NombreMigracion `
  -p .\src\AcademicRegistration.Infrastructure.Migrations.SqlServer\AcademicRegistration.Infrastructure.Migrations.SqlServer.csproj `
  -s .\src\AcademicRegistration.Api\AcademicRegistration.Api.csproj `
  -c AcademicRegistrationDbContext `
  -o Persistence\Migrations `
  -- "--Configuracion:Conexion=SQL"
```

Aplicar migraciones de SQL Server:

```powershell
dotnet ef database update `
  -p .\src\AcademicRegistration.Infrastructure.Migrations.SqlServer\AcademicRegistration.Infrastructure.Migrations.SqlServer.csproj `
  -s .\src\AcademicRegistration.Api\AcademicRegistration.Api.csproj `
  -c AcademicRegistrationDbContext `
  -- "--Configuracion:Conexion=SQL"
```

Para verificar que el modelo no tiene cambios pendientes:

```powershell
dotnet ef migrations has-pending-model-changes `
  -p .\src\AcademicRegistration.Infrastructure.Migrations.SqlServer\AcademicRegistration.Infrastructure.Migrations.SqlServer.csproj `
  -s .\src\AcademicRegistration.Api\AcademicRegistration.Api.csproj `
  -c AcademicRegistrationDbContext `
  -- "--Configuracion:Conexion=SQL"
```

Usa el proyecto `AcademicRegistration.Infrastructure.Migrations.MySql` y `-- "--Configuracion:Conexion=MySQL"` para hacer la misma validacion en MySQL.

Importante: al cambiar de proveedor, no reutilices las migraciones del otro motor. Cada proveedor debe crear y aplicar sus migraciones desde su propio proyecto.

SQL Server LocalDB de desarrollo:

```text
Server=(localdb)\MSSQLLocalDB;Database=AcademicRegistrationDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
```

Tambien estan disponibles scripts manuales en:

- `database/sqlserver/001_create_academic_registration.sql`
- `database/mysql/001_create_academic_registration.sql`

## Endpoints

- `GET /api/subjects`
- `GET /api/students`
- `GET /api/students/{studentId}`
- `GET /api/students/{studentId}/classmates`
- `POST /api/students`
- `PUT /api/students/{studentId}`
- `DELETE /api/students/{studentId}`

## Frontend

Requiere Node compatible con Angular 22. Se valido con Node `24.15.0`.

```powershell
cd .\frontend
npm install
npm run build
npm start
```

Angular usa proxy local para enviar `/api` al gateway `http://localhost:5080`.

## Verificacion realizada

- `dotnet restore backend/AcademicRegistration.slnx`
- `dotnet build backend/AcademicRegistration.slnx --no-restore`
- `dotnet test backend/AcademicRegistration.slnx --no-restore`
- `npm install`
- `npm run build`

## Docker

La configuracion Docker levanta seis servicios:

- `frontend`: Angular compilado y servido con Nginx en `http://localhost:4200`.
- `gateway`: API Gateway YARP en `http://localhost:5080`.
- `api`: API REST en `http://localhost:5081` y Swagger en `http://localhost:5081/swagger`.
- `sqlserver`: SQL Server 2022 Express con volumen persistente.
- `localstack`: EventBridge y SQS locales en `http://localhost:4566`.
- `notifications-worker`: consumidor de la cola `academic-registration-notifications` para enviar correos.

Crear el archivo de entorno:

```powershell
copy .env.example .env
```

Levantar todo el stack:

```powershell
docker compose up --build
```

URLs principales:

- Frontend: `http://localhost:4200`
- Gateway: `http://localhost:5080`
- API: `http://localhost:5081`
- Swagger: `http://localhost:5081/swagger`
- SQL Server: `localhost,1433`
- LocalStack: `http://localhost:4566`

Credenciales por defecto de SQL Server:

```text
Usuario: sa
Password: AcademicReg!2026
Base de datos: AcademicRegistrationDb
```

El servicio `api` corre con `ASPNETCORE_ENVIRONMENT=Development` y `Configuracion__Conexion=SQL`, por lo que aplica las migraciones de SQL Server y siembra el catalogo de profesores y materias.

El servicio `notifications-worker` queda con `EMAIL_ENABLED=false` por defecto. Con ese valor, veras el contenido del correo en los logs del contenedor. Para enviar correos reales, actualiza las variables SMTP en `.env`. Si usas Gmail, `EMAIL_PASSWORD` debe ser una contrasena de aplicacion.

LocalStack inicializa automaticamente:

- Event bus `academic-registration`.
- Cola `academic-registration-notifications`.
- DLQ `academic-registration-notifications-dlq`.
- Regla EventBridge que enruta `student.registered` y `student.enrollment.changed` hacia SQS.

Para reiniciar tambien la base de datos:

```powershell
docker compose down -v
docker compose up --build
```

## AWS PoC

La POC AWS esta en `infrastructure/aws-poc`.

- Terraform crea VPC, ALB, ECS Fargate, Cloud Map, RDS SQL Server, EventBridge, SQS + DLQ, Secrets Manager, KMS, CloudWatch, X-Ray daemon y frontend S3 + CloudFront.
- GitHub Actions valida backend/frontend/Terraform y puede desplegar manualmente con `.github/workflows/aws-poc-ci-cd.yml`.
- El frontend usa `/api`; CloudFront enruta `/api/*` al ALB y el gateway YARP enruta al API por Cloud Map.

Ver detalles y pasos en `infrastructure/aws-poc/README.md`.

