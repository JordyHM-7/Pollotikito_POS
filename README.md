# Pollotikito

POS local para restaurante: .NET 10, ASP.NET Core, EF Core/Npgsql, PostgreSQL y React + TypeScript + Vite. Incrementos 1–3: seguridad, auditoría, negocio/consecutivos, catálogo, caja/cobros, modificadores y combos. La API publicada sirve el frontend en el mismo origen; funciona sin internet.

- [Manual de usuario](docs/manual-usuario/README.md)
- [Arquitectura](docs/tecnico/arquitectura.md) · [Seguridad](docs/tecnico/seguridad.md) · [Base de datos](docs/tecnico/base-de-datos.md)
- [Ventas, caja y guía para inventario](docs/tecnico/ventas-y-caja.md) · [Decisiones y pendientes](docs/tecnico/decisiones.md)
- [Instalación, configuración y operación](docs/tecnico/operacion.md) · [Pruebas y capturas](docs/tecnico/pruebas.md)
- [Requerimientos funcionales](docs/requerimientos-funcionales.md)

## Preparación

Requiere SDK .NET 10, PostgreSQL local, Node.js 22.12+ o 24 y npm. Descargar dependencias una vez:

```powershell
dotnet tool restore
dotnet restore Backend/Pollotikito.slnx
Push-Location Frontend
npm ci
Pop-Location
```

En pgAdmin o psql, crear base y rol locales (reemplazar claves):

```sql
CREATE ROLE pollotikito LOGIN PASSWORD 'REEMPLAZAR_CLAVE_LOCAL';
CREATE DATABASE pollotikito OWNER pollotikito;
CREATE ROLE pollotikito_test LOGIN PASSWORD 'REEMPLAZAR_CLAVE_PRUEBAS';
CREATE DATABASE pollotikito_test OWNER pollotikito_test;
```

No se necesita superusuario para ejecutar la app ni pruebas. No Docker ni Testcontainers.

## Desarrollo

Desde la raíz, terminal 1:

```powershell
$env:ConnectionStrings__Pollotikito = 'Host=localhost;Port=5432;Database=pollotikito;Username=pollotikito;Password=REEMPLAZAR_CLAVE_LOCAL'
$env:AdministradorInicial__Password = 'REEMPLAZAR_CON_10_O_MAS_CARACTERES'
dotnet run --project Backend/Pollotikito.Api --launch-profile http
```

API en `http://localhost:5149`. Development aplica migraciones y crea el administrador inicial solo si no existe ninguno; obliga a cambiar contraseña. Si ya está configurado User Secrets y existe administrador, solo ejecutar `dotnet run`. Ver [configuración persistente](docs/tecnico/operacion.md).

Terminal 2:

```powershell
cd Frontend
npm run dev
```

Abrir la dirección que muestra Vite, normalmente `http://127.0.0.1:5173`. Mantener API y Vite activos. El proxy devuelve «La API no está disponible» si falta el backend.

## Verificación y publicación

```powershell
$env:POLLOTIKITO_TEST_CONNECTION = 'Host=localhost;Port=5432;Database=pollotikito_test;Username=pollotikito_test;Password=REEMPLAZAR_CLAVE_PRUEBAS'
dotnet test Backend/Pollotikito.slnx -c Release
dotnet publish Backend/Pollotikito.Api -c Release -o artifacts/publicado
```

Pruebas en esquemas temporales de PostgreSQL. Sin variable se omite integración con mensaje claro. Publicar incorpora el frontend en wwwroot. Producción requiere HTTPS local, conexión, migración explícita y almacenamiento persistente; [procedimiento](docs/tecnico/operacion.md).

Capturas ficticias reproducibles, desde Frontend: `npm run capturas` con la variable de pruebas y Chromium de Playwright preparado. Nunca usa la base de desarrollo. [Detalles y requisitos](docs/tecnico/pruebas.md).

**Verificación I3:** frontend y backend compilan; **56 pruebas aprobadas, 0 fallidas y 0 omitidas** con PostgreSQL local. Migraciones ejecutadas en esquemas temporales y modelo sin diferencias pendientes. Ver [pruebas y capturas](docs/tecnico/pruebas.md).
