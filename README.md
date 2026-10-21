# Pollotikito — Incremento 1

POS local para una sucursal, con .NET 10, ASP.NET Core, EF Core/Npgsql, PostgreSQL y React + TypeScript + Vite. Este incremento implementa **RF-SEG-01 a RF-SEG-04 y RF-SIS-02**. El alcance completo está en [docs/requerimientos-funcionales.md](docs/requerimientos-funcionales.md).

## Estructura

```text
Backend/Pollotikito.slnx
  Pollotikito.Domain/           Entidades y enums en español
  Pollotikito.Application/      Casos de uso, contratos y validaciones
  Pollotikito.Infrastructure/   PostgreSQL, auditoría, hashing, logos y migraciones
  Pollotikito.Api/              HTTP, cookies, CSRF, roles y filtro de autorización
  Pollotikito.Tests/            xUnit, integración HTTP y PostgreSQL real
Frontend/                      React + TypeScript + Vite
```

Domain no depende de otros proyectos. Application depende únicamente de Domain. Infrastructure implementa los contratos de Application; Api compone las dependencias y sirve los archivos estáticos.

## Requisitos e instalación

- SDK .NET 10 (el `global.json` permite revisiones de la misma versión principal).
- PostgreSQL local 18 (también se puede usar una versión compatible con Npgsql 10).
- Node.js 22.12+ o 24 y npm.

Desde la raíz del repositorio:

```powershell
dotnet tool restore
dotnet restore Backend/Pollotikito.slnx
Push-Location Frontend
npm ci
Pop-Location
```

La descarga de herramientas y paquetes corresponde a la preparación del entorno. Una instalación publicada funciona sin internet: no usa CDN, fuentes externas, APIs remotas ni servicios en la nube.

## Crear las bases locales

Conectarse con `psql -U postgres -h localhost` o usar pgAdmin. Ejecutar una vez, fuera de una transacción, y elegir contraseñas locales propias:

```sql
CREATE ROLE pollotikito LOGIN PASSWORD 'REEMPLAZAR_CLAVE_LOCAL';
CREATE DATABASE pollotikito OWNER pollotikito;
CREATE ROLE pollotikito_test LOGIN PASSWORD 'REEMPLAZAR_CLAVE_PRUEBAS';
CREATE DATABASE pollotikito_test OWNER pollotikito_test;
```

No hace falta superusuario para la aplicación ni para las pruebas. Las migraciones necesitan permisos DDL sobre su esquema; un despliegue definitivo puede separar el usuario migrador del usuario de ejecución. Usar siempre una base exclusiva para pruebas.

## Configuración y primer arranque

En `Backend/Pollotikito.Api/appsettings.json` están la cadena sin contraseña, el UUID v7 de la sucursal y `Almacenamiento:Logos`. Las rutas relativas de almacenamiento se resuelven respecto al directorio de trabajo; se recomienda una ruta absoluta en producción.

`appsettings.Development.json` define `AdministradorInicial:Usuario` y deja `AdministradorInicial:Password` vacío a propósito. Configurar la contraseña inicial mediante un override local de configuración; nunca subir secretos reales a Git:

```powershell
$env:ConnectionStrings__Pollotikito = 'Host=localhost;Port=5432;Database=pollotikito;Username=pollotikito;Password=REEMPLAZAR_CLAVE_LOCAL'
$env:AdministradorInicial__Password = 'REEMPLAZAR_CON_10_O_MAS_CARACTERES'
dotnet run --project Backend/Pollotikito.Api --launch-profile http
```

La API escucha en `http://localhost:5149`. Al arrancar en Development aplica las migraciones versionadas, crea la configuración y los tres consecutivos, y crea el administrador inicial **solo si nunca se creó uno**. El administrador inicia con `DebeCambiarCredenciales=true`; la API restringe su sesión hasta que cambie la contraseña. Después del primer arranque se puede retirar la variable con la contraseña inicial.

El valor inicial de IVA es 13 %. Los siguientes números parten en 1 y los prefijos vacíos. Todos los IDs de entidades se generan con `Guid.CreateVersion7()`; todas las tablas de negocio incluyen `sucursal_id`, `creado_en` y `actualizado_en` en UTC. El sistema trabaja en CRC; cuando se agreguen importes monetarios se mapearán como `decimal` / `numeric(18,2)`.

## Ejecutar el frontend en desarrollo

En otra terminal:

```powershell
cd Frontend
npm run dev
```

Abrir la dirección local que imprime Vite (normalmente `http://127.0.0.1:5173`). Vite redirige `/api` a `http://localhost:5149`, manteniendo un único origen para el navegador. No configurar CORS ni JWT.

Pantallas disponibles:

- Acceso con selección de usuario y teclado numérico táctil para PIN, o usuario y contraseña de administrador.
- Cambio obligatorio de contraseña inicial o temporal.
- Administración de usuarios: crear, editar, desactivar y cambiar credenciales. Antes de promover un cajero, asignar su contraseña; antes de cambiar un administrador a cajero, asignar su PIN.
- Datos del negocio, logo y edición de consecutivos.
- Para el cajero: «Caja (próximamente)» y cierre de sesión.

## Publicar y ejecutar con un solo origen

```powershell
dotnet publish Backend/Pollotikito.Api -c Release -o artifacts/publish
```

La publicación ejecuta `npm ci` y `npm run build` e incluye `Frontend/dist` bajo `wwwroot`. Para publicar sin red después de preparar la caché de paquetes, ejecutar previamente `npm ci --offline --no-audit --no-fund` en Frontend y publicar con `-p:SkipFrontendInstall=true`. Para reutilizar un build ya copiado por un proceso propio se puede desactivar toda la etapa con `-p:SkipFrontendBuild=true`.

En producción las migraciones se aplican explícitamente durante el despliegue:

```powershell
dotnet ef database update --project Backend/Pollotikito.Infrastructure
```

Inicializar el administrador durante la preparación local en Development y realizar el cambio de contraseña. Luego ejecutar el resultado publicado desde su carpeta con `ASPNETCORE_ENVIRONMENT=Production`, la cadena de conexión y una dirección HTTPS local configurada. En Production la cookie exige HTTPS. El certificado y la clave de Data Protection deben conservarse para mantener las sesiones entre reinicios. La aplicación no necesita Node.js ni acceso a internet al ejecutarse publicada.

No ejecutar la aplicación publicada desde un directorio distinto sin configurar correctamente la raíz de contenido y la carpeta de logos.

## Ejecutar las pruebas

```powershell
$env:POLLOTIKITO_TEST_CONNECTION = 'Host=localhost;Port=5432;Database=pollotikito_test;Username=pollotikito_test;Password=REEMPLAZAR_CLAVE_PRUEBAS'
dotnet test Backend/Pollotikito.slnx
```

Las pruebas usan PostgreSQL real, sin Docker ni Testcontainers. Crean un esquema aleatorio `test_pollotikito_<uuid>`, aplican las migraciones y eliminan **solo ese esquema** al terminar. El usuario debe ser dueño de la base de pruebas o poder crear y eliminar sus propios esquemas y funciones. No necesitan crear bases ni ser superusuario. Si la variable no está definida, las pruebas de integración se **omiten con un motivo explícito**; las pruebas unitarias siguen ejecutándose. Una conexión configurada pero inválida hace fallar la integración, sin ocultar errores.

Cobertura: PIN correcto/incorrecto, bloqueo de cinco minutos, permisos HTTP 403 del cajero, cookie HttpOnly y CSRF, tokens de administrador ligados a usuario y acción con un solo consumo aun en paralelo y vencimiento exacto a los 60 segundos, auditoría anterior/nueva sin secretos, inmutabilidad de bitácora y usuarios en SQL, cambio inicial obligatorio, invalidación de sesiones, configuración y logos, y 64 solicitudes paralelas al servicio de consecutivos.

Para compilar y verificar la UI:

```powershell
dotnet build Backend/Pollotikito.slnx
Push-Location Frontend
npm run build
Pop-Location
```

## API y decisiones de seguridad

| Acceso | Método y ruta |
| --- | --- |
| Público | `GET /api/auth/csrf`, `GET /api/auth/cajeros` (solo id y nombre de cuentas activas con PIN) |
| Público | `POST /api/auth/login/pin`, `POST /api/auth/login/admin` |
| Sesión | `GET /api/auth/me`, `POST /api/auth/logout` |
| Administrador, incluso durante cambio inicial | `POST /api/auth/cambiar-password` |
| Administrador | `GET/POST /api/usuarios`, `PUT /api/usuarios/{id}`, `POST /api/usuarios/{id}/desactivar`, `PUT /api/usuarios/{id}/credenciales` |
| Administrador | `GET/PUT /api/configuracion`, `GET/POST /api/configuracion/logo` |
| Administrador | `GET /api/consecutivos`, `PUT /api/consecutivos/{tipo}` |
| Sesión | `POST /api/autorizaciones/administrador`, `POST /api/pruebas/autorizacion-administrador` |

- Todas las mutaciones, incluidos los logins, requieren el header `X-CSRF-TOKEN` obtenido de `/api/auth/csrf`. Renovarlo después de cambiar la identidad (login, logout o cambio obligatorio). Se usan cookies HttpOnly y SameSite Strict, sin JWT ni credenciales en localStorage.
- La cuenta se identifica antes de validar el PIN, permitiendo atribuir y bloquear los intentos incorrectos. Administradores con PIN pueden usar el mismo acceso. Contraseñas de 10–128 caracteres y PIN ASCII de 4–6 dígitos; hashing con `PasswordHasher<Usuario>`. Nunca se devuelven hashes ni se registran secretos.
- Cambiar credenciales, rol o datos de usuario invalida sus sesiones. No se permite eliminar usuarios, desactivar la propia cuenta ni dejar el sistema sin administrador activo. Un reset de contraseña de administrador obliga a cambiarla al ingresar.
- La autorización recibe `{ administradorId, pin, accion: "prueba" }`, devuelve `{ token, venceEn }` y guarda solo su hash. El solicitante envía `X-Autorizacion-Administrador` al endpoint protegido. No cambia su rol ni abre una sesión administrativa. El token solo sirve para la acción y usuario originales; las cuentas con contraseña pendiente no autorizan.
- Para futuras acciones: incluir el nombre en la lista explícita de acciones autorizables de Application y agregar `.AddEndpointFilter(new AutorizacionAdministradorFiltro("nombre_accion"))` a un endpoint autenticado. El filtro consume el token antes de llamar a la acción; si la acción falla, se debe pedir otro token.
- `IConsecutivos.Siguiente(tipo)` usa `UPDATE ... RETURNING` dentro de una transacción PostgreSQL. No hay endpoint para emitir números. La edición administrativa se coordina con la emisión y nunca permite reducir el siguiente número. Los números reservados no se reutilizan aunque una emisión posterior falle; esto puede producir huecos. El servicio de emisión de documentos se implementará en otro incremento.
- Se serializan las mutaciones cortas con un bloqueo transaccional asesor en PostgreSQL, adecuado para una sucursal/PC y efectivo entre distintos procesos. El consumo de autorizaciones es un `UPDATE` condicional atómico independiente. No hay bloqueos en memoria.
- La auditoría de usuarios, configuración y edición de consecutivos se guarda en la misma transacción del cambio. Los accesos exitosos y fallidos registran el usuario identificado (nullable si no existe), acción, entidad, fecha UTC e IP. Las snapshots de credenciales solo indican su existencia; nunca contienen hashes. No se expone una API para alterar la bitácora, y un trigger rechaza UPDATE, DELETE y TRUNCATE.
- Los logos se reciben como cuerpo binario, máximo 1 MB, verificando firmas PNG/JPG. Se guardan con nombre UUID generado en la carpeta local configurable; la base conserva el nombre relativo. No se aceptan rutas ni URLs del cliente. Los archivos solo se sirven desde el endpoint autorizado con tipo de imagen y `nosniff`. Al reemplazar un logo se conserva el anterior para mantener las referencias de la auditoría; prever limpieza/retención en un incremento operativo.
- Errores de entrada se validan en Application y se devuelven como `ProblemDetails`. La API no registra cuerpos de solicitudes ni parámetros SQL.

## Pendientes fuera de este incremento

Productos, ventas, apertura/cierre y movimientos de caja, inventario, clientes, impresión/PDF, correo, sincronización con la nube, reportes y respaldo/restauración operativa. No se incorpora Docker. Tampoco se implementa todavía expiración por inactividad configurable (RF-SEG-05). Nada de esto es necesario para ejecutar las funciones de esta base.
