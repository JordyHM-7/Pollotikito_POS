# Instalación y operación

## Preparar el entorno

SDK .NET 10, PostgreSQL local 18 o compatible con Npgsql 10, Node.js 22.12+ o 24 y npm. Sin Docker. Desde la raíz:

```powershell
dotnet tool restore
dotnet restore Backend/Pollotikito.slnx
Push-Location Frontend
npm ci
Pop-Location
```

Estas descargas son preparación del entorno. La aplicación publicada funciona sin internet y no necesita Node ni Playwright.

## Crear bases y roles

Conectarse mediante pgAdmin o `psql -U postgres -h localhost`. Ejecutar una vez, fuera de una transacción, reemplazando las claves:

```sql
CREATE ROLE pollotikito LOGIN PASSWORD 'REEMPLAZAR_CLAVE_LOCAL';
CREATE DATABASE pollotikito OWNER pollotikito;
CREATE ROLE pollotikito_test LOGIN PASSWORD 'REEMPLAZAR_CLAVE_PRUEBAS';
CREATE DATABASE pollotikito_test OWNER pollotikito_test;
```

La aplicación y las pruebas no necesitan superusuario. Los roles deben poder crear objetos en sus esquemas para migraciones. Nunca usar la base real para pruebas/capturas. El helper de capturas exige el nombre `pollotikito_test`.

## Conexión y administrador inicial

`appsettings.json` contiene cadena sin contraseña, SucursalId y carpetas de almacenamiento. `appsettings.Development.json` contiene usuario inicial y contraseña vacía. Definir overrides locales; no guardar secretos reales en Git:

```powershell
$env:ConnectionStrings__Pollotikito = 'Host=localhost;Port=5432;Database=pollotikito;Username=pollotikito;Password=REEMPLAZAR_CLAVE_LOCAL'
$env:AdministradorInicial__Password = 'REEMPLAZAR_CON_10_O_MAS_CARACTERES'
dotnet run --project Backend/Pollotikito.Api --launch-profile http
```

Para conservar la conexión entre terminales, configurar una vez User Secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:Pollotikito" "Host=localhost;Port=5432;Database=pollotikito;Username=pollotikito;Password=REEMPLAZAR_CLAVE_LOCAL" --project Backend/Pollotikito.Api
```

Development lee User Secrets; una variable `ConnectionStrings__Pollotikito` tiene prioridad. La contraseña de PostgreSQL es distinta del PIN/contraseña de ingreso a Pollotikito. Los comandos de EF de Infrastructure usan la variable de conexión, no necesariamente los secretos del proyecto Api.

Al arrancar en Development se aplican migraciones, se inicializa negocio/consecutivos y se crea administrador **solo si nunca hubo uno**. Completá cambio obligatorio de contraseña y luego retirá el override de contraseña inicial. Un administrador existente no se reemplaza por cambiar appsettings. SucursalId debe ser estable; no cambiarlo en una instalación con datos.

## Desarrollo: dos terminales

Terminal 1, raíz del repositorio:

```powershell
dotnet run --project Backend/Pollotikito.Api --launch-profile http
```

API: `http://localhost:5149`. Terminal 2:

```powershell
cd Frontend
npm run dev
```

Abrir la dirección indicada por Vite, normalmente `http://127.0.0.1:5173`. Mantener ambas terminales abiertas. Vite redirige `/api` a la API, sin CORS. Si falta API, el proxy responde 503 ProblemDetails «La API no está disponible»; no cambiar la contraseña de la cuenta para corregirlo.

## Build y publicación

```powershell
dotnet build Backend/Pollotikito.slnx -c Release
dotnet publish Backend/Pollotikito.Api -c Release -o artifacts/publicado
```

Publicar ejecuta npm ci/build y copia dist a wwwroot. Para publicar sin red después de preparar paquetes: ejecutar `npm ci --offline --no-audit --no-fund` en Frontend y publicar con `-p:SkipFrontendInstall=true`. `-p:SkipFrontendBuild=true` solo cuando el proceso propio ya incorporó los archivos estáticos.

En producción, respaldar y aplicar migraciones explícitamente con la conexión correcta:

```powershell
dotnet ef database update --project Backend/Pollotikito.Infrastructure
```

Preparar administrador y cambiar su contraseña durante la instalación local en Development. Después ejecutar desde la carpeta publicada con `ASPNETCORE_ENVIRONMENT=Production`, conexión y dirección HTTPS local configuradas. La cookie exige HTTPS en Production. Conservar certificado y claves de Data Protection. No usar Testing en producción: ese entorno existe para pruebas y omite inicialización.

## Archivos y respaldos

`Almacenamiento:Logos` y `Almacenamiento:FotosProductos` definen carpetas locales; por defecto storage/logos y storage/productos. Rutas relativas dependen del directorio de trabajo; usar absolutas en producción y dar permisos a la cuenta de ejecución. BD conserva rutas relativas generadas. No borrar imágenes antiguas: comprobantes históricos pueden referenciarlas.

Un respaldo recuperable debe incluir PostgreSQL, ambas carpetas de imágenes, configuración local, SucursalId, secretos protegidos, certificados y Data Protection. Planificar backup/restore con herramientas PostgreSQL y verificar restauración en un entorno separado. Automatización y procedimiento operativo definitivo pertenecen a un incremento posterior; no están implementados aquí.

## Diagnóstico

- Conexión rechazada: comprobar servicio PostgreSQL, host/puerto, base y rol, sin imprimir la cadena con contraseña en logs.
- Error interno: revisar hora y TraceId, disponibilidad de BD y estado de migraciones. API no expone detalles SQL al usuario.
- DLL bloqueada `0x800711C7`: ocurrió con una compilación de `Pollotikito.Application.dll`; una publicación posterior se ejecutó sin modificar Windows. Si reaparece, solicitar diagnóstico autorizado de Code Integrity antes de cualquier cambio de políticas. Ver [reporte de la incidencia](incidencia-control-aplicaciones.md). No desactivar seguridad desde scripts del repo.
- DLL ocupada al compilar: otro proceso puede estar usando el build. Coordinar la terminal de ese proceso o compilar Release/otra salida; no detener procesos ajenos automáticamente.
- Imágenes faltantes: comprobar carpeta configurada y respaldo; no sustituir con URLs externas.

Pruebas y capturas: [pruebas.md](pruebas.md). Uso diario: [manual](../manual-usuario/README.md).
