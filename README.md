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
```

URLs por defecto:

- API: `http://localhost:5081`
- Swagger API: `http://localhost:5081/swagger`
- Gateway: `http://localhost:5080`

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

La configuracion Docker levanta cuatro servicios:

- `frontend`: Angular compilado y servido con Nginx en `http://localhost:4200`.
- `gateway`: API Gateway YARP en `http://localhost:5080`.
- `api`: API REST en `http://localhost:5081` y Swagger en `http://localhost:5081/swagger`.
- `sqlserver`: SQL Server 2022 Express con volumen persistente.

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

Credenciales por defecto de SQL Server:

```text
Usuario: sa
Password: AcademicReg!2026
Base de datos: AcademicRegistrationDb
```

El servicio `api` corre con `ASPNETCORE_ENVIRONMENT=Development` y `Configuracion__Conexion=SQL`, por lo que aplica las migraciones de SQL Server y siembra el catalogo de profesores y materias.

Para reiniciar tambien la base de datos:

```powershell
docker compose down -v
docker compose up --build
```

