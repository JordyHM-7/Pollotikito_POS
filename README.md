# Pollotikito — Incrementos 1 y 2

POS local para una sucursal, con .NET 10, ASP.NET Core, EF Core/Npgsql, PostgreSQL y React + TypeScript + Vite. Implementa **RF-SEG-01 a RF-SEG-04, RF-SIS-02, RF-CAT-01 a RF-CAT-04, RF-CAJ-01 a RF-CAJ-04 y RF-VEN-01, 02, 03, 09, 10 y 13**. El alcance completo está en [docs/requerimientos-funcionales.md](docs/requerimientos-funcionales.md).

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

Para que la conexión de desarrollo se conserve al cerrar la terminal, el proyecto tiene habilitado **.NET User Secrets**. Configurarla una vez por perfil, fuera del repositorio:

```powershell
dotnet user-secrets set "ConnectionStrings:Pollotikito" "Host=localhost;Port=5432;Database=pollotikito;Username=pollotikito;Password=REEMPLAZAR_CLAVE_LOCAL" --project Backend/Pollotikito.Api
dotnet run --project Backend/Pollotikito.Api --launch-profile http
```

En Development la API lee esa conexión automáticamente; una variable `ConnectionStrings__Pollotikito` tiene prioridad si está definida. User Secrets solo configura el acceso de la API a PostgreSQL: no cambia la contraseña ni el PIN de usuarios de Pollotikito. Los comandos `dotnet ef` de Infrastructure siguen usando `ConnectionStrings__Pollotikito`; el arranque de desarrollo ya aplica las migraciones con la configuración de la API.

El valor inicial de IVA es 13 %. Los siguientes números parten en 1 y los prefijos vacíos. Todos los IDs de entidades usan UUID v7; la venta se identifica con un UUID v7 generado por el frontend. Todas las tablas de negocio incluyen `sucursal_id`, `creado_en` y `actualizado_en` en UTC. Los importes monetarios se almacenan en CRC como `decimal` / `numeric(18,2)`.

## Ejecutar el frontend en desarrollo

En otra terminal:

```powershell
cd Frontend
npm run dev
```

Abrir la dirección local que imprime Vite (normalmente `http://127.0.0.1:5173`). Vite redirige `/api` a `http://localhost:5149`, manteniendo un único origen para el navegador. No configurar CORS ni JWT.

En desarrollo deben estar ejecutándose **ambos procesos**: la API y Vite. Si solo está abierto Vite, las pantallas pueden cargar pero no se puede iniciar sesión. El proxy devuelve `503 ProblemDetails` con «La API no está disponible» cuando el backend no responde; no significa que la cuenta o el PIN sean incorrectos. Iniciar la API en la terminal que tenga configurada `ConnectionStrings__Pollotikito` y mantenerla abierta.

Pantallas disponibles:

- Acceso con selección de usuario y teclado numérico táctil para PIN, o usuario y contraseña de administrador.
- Cambio obligatorio de contraseña inicial o temporal.
- Administración de usuarios: crear, editar, desactivar y cambiar credenciales. Antes de promover un cajero, asignar su contraseña; antes de cambiar un administrador a cajero, asignar su PIN.
- Datos del negocio, logo y edición de consecutivos.
- Categorías y productos con orden, estado, precio con IVA, tarifa, descuentos automáticos y foto local opcional. Los productos de categorías inactivas se excluyen de la caja aunque sigan activos.
- Para el cajero: apertura, orden táctil, notas y descuentos con motivo, tipo de orden, cobro con efectivo/tarjeta/SINPE o mixto, tiquete, ventas del turno, depósitos, retiros autorizados y cierre por denominaciones.
- Para el administrador: consulta de sesiones y sus ventas/reportes, y cierre forzado con motivo obligatorio, con conteo o sin él.

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

El Incremento 2 agrega pruebas de IVA incluido y redondeo, vigencia de descuentos, descuento por unidad y prorrateo de centavos, pagos mixtos y vuelto, rechazo de precios y pagos adulterados, instantáneas, idempotencia idéntica y conflictos de contenido, cobros paralelos, ausencia de bloqueo global, apertura simultánea, retiros autorizados, esperado de caja y anulaciones por método, ambos cierres forzados, fotos y permisos. También se inyecta un fallo al insertar la venta para comprobar el rollback de venta, líneas, pagos, bitácora y consecutivo. Verificación de entrega: **44 pruebas ejecutadas, 44 aprobadas y 0 omitidas** con PostgreSQL configurado (20 casos unitarios y 24 de integración).

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
- `IConsecutivos.Siguiente(tipo)` usa `UPDATE ... RETURNING` y reutiliza la transacción del cobro, sin tomar el bloqueo asesor global ni abrir una transacción anidada. Si el cobro falla, el número también se revierte; si se confirmó, una anulación nunca lo libera. Para llamadas internas independientes abre una transacción local. No hay endpoint para emitir números. La edición administrativa y el trigger de monotonía impiden bajar el siguiente número.
- Usuarios, configuración y catálogo conservan el bloqueo asesor administrativo. **Cobros, movimientos, anulaciones y cierres no lo toman**: `SELECT ... FOR UPDATE` sobre la sesión serializa sus operaciones. La apertura está protegida por un índice único parcial sobre `sucursal_id WHERE cerrada_en IS NULL`. El consumo de autorizaciones sigue siendo un `UPDATE` condicional atómico. No hay bloqueos en memoria.
- La auditoría de usuarios, configuración y edición de consecutivos se guarda en la misma transacción del cambio. Los accesos exitosos y fallidos registran el usuario identificado (nullable si no existe), acción, entidad, fecha UTC e IP. Las snapshots de credenciales solo indican su existencia; nunca contienen hashes. No se expone una API para alterar la bitácora, y un trigger rechaza UPDATE, DELETE y TRUNCATE.
- Los logos se reciben como cuerpo binario, máximo 1 MB, verificando firmas PNG/JPG. Se guardan con nombre UUID generado en la carpeta local configurable; la base conserva el nombre relativo. No se aceptan rutas ni URLs del cliente. Los archivos solo se sirven desde el endpoint autorizado con tipo de imagen y `nosniff`. Al reemplazar un logo se conserva el anterior para mantener las referencias de la auditoría; prever limpieza/retención en un incremento operativo.
- Errores de entrada se validan en Application y se devuelven como `ProblemDetails`. La API no registra cuerpos de solicitudes ni parámetros SQL.

## Operación de venta y cierre (Incremento 2)

1. Como administrador, crear categorías y productos; establecer precios con IVA incluido, tarifa, orden, descuentos y vigencia. `Almacenamiento:FotosProductos` configura la carpeta local (por defecto `storage/productos`). Las fotos siguen el mismo límite de 1 MB y firmas PNG/JPG que los logos, con nombre UUID y ruta relativa en la base.
2. Como cajero, abrir caja con el monto inicial. Solo el propietario de la sesión puede vender, registrar movimientos, anular y realizar el cierre normal. Un administrador consulta y puede forzar el cierre desde su pantalla; no cobra a nombre del cajero.
3. Armar la orden tocando productos. La orden vive en memoria del frontend. El servidor proporciona una cotización y recalcula nuevamente al cobrar usando el catálogo actual; ningún precio o total del cliente se usa como autoridad.
4. Orden de descuentos: automático **por unidad** (porcentaje redondeado a centavos o monto fijo limitado al precio), manual de línea y global. Los manuales requieren motivo. La vigencia automática incluye ambos extremos UTC. El global se prorratea proporcionalmente, tomando cuotas inferiores en centavos y distribuyendo los centavos restantes por mayor resto, con desempate por orden de línea. No hay líneas negativas. Un descuento manual que excede el disponible se rechaza.
5. Para cada línea: `base = round(total / (1 + tarifa_iva / 100), 2, AwayFromZero)`; `impuesto = total - base`. Los totales de venta son sumas de las líneas guardadas. Nombres, precios, descuentos, impuestos, datos del negocio y referencia del logo quedan como instantáneas. Modificar el catálogo o el negocio no cambia el tiquete histórico.
6. En el cobro, `Monto` es la parte **aplicada** de cada método y debe sumar exactamente el total. Solo efectivo admite `Recibido` y `Vuelto = Recibido - Monto`. Tarjeta requiere autorización y SINPE requiere comprobante. Se permite un registro de cada método por venta. Los botones de billetes establecen el recibido; «Exacto» usa la parte en efectivo.
7. El frontend genera el UUID v7 antes de enviar el cobro. Se guarda una huella canónica de sesión, tipo de orden, líneas, notas, descuentos y pagos; respeta el orden de las listas y normaliza la representación numérica. Solo el mismo contenido y cajero recuperan la venta existente. Cambiar contenido, sesión o usuario con el mismo UUID devuelve **409**. Un reintento idéntico recupera el comprobante aun si luego cambió el catálogo o se cerró la sesión.
8. Si se pierde una respuesta, el formulario conserva exactamente esa solicitud en memoria y, si está disponible, en `sessionStorage` de la pestaña, sin credenciales. Bloquea la edición y ofrece reintentar o consultar el UUID. No repetir un cobro externo en el datáfono/SINPE por un fallo de red local: primero consultar su resultado. La orden sin cobrar no se persiste.
9. El retiro solicita un administrador y su PIN; se emite y consume un token `retiro_efectivo` del flujo existente. Un token de `prueba`, de otro usuario, vencido o usado no sirve. El depósito no requiere token. Registrar siempre el motivo. La autorización se consume antes de ejecutar la acción; si el movimiento falla, pedir una nueva.
10. Anular requiere motivo y sesión propia abierta. No borra comprobantes ni pagos: registra quién, cuándo y por qué. Para tarjeta o SINPE se muestra **«Recordá reversar el cobro en el datáfono o devolver el SINPE»**. Pollotikito no efectúa ninguna reversión externa. El reporte desglosa las partes anuladas por método, incluidos pagos mixtos.
11. Esperado de efectivo: `monto inicial + Σ(recibido - vuelto de pagos en efectivo de ventas no anuladas) + depósitos - retiros`. El reporte separa ventas vigentes, descuentos vigentes, anulaciones y movimientos. Una venta mixta cuenta una vez en cada método utilizado; los montos por método son solo la parte aplicada, no el total repetido.
12. Cerrar contando monedas de 5, 10, 25, 50, 100 y 500 y billetes de 1000, 2000, 5000, 10000 y 20000. `Diferencia = Contado - Esperado`. El administrador puede forzar con conteo o **sin conteo**, siempre con motivo. Sin conteo se guarda el indicador explícito y tanto contado como diferencia son `null`, no cero. La sesión y sus operaciones quedan protegidas contra modificaciones después del cierre.

El tiquete se muestra después de cobrar y desde el historial. «Imprimir / guardar PDF» abre la impresión del navegador con página de 80 mm de ancho (alto de página 297 mm; los tiquetes largos pueden continuar en otra página). El reporte usa carta. Desactivar encabezados/pies propios del navegador y respetar el tamaño indicado en la vista previa. No se usa biblioteca PDF ni controlador de impresora térmica. Los reportes de sesiones cerradas se consultan desde la administración.

### Endpoints del Incremento 2

Todos requieren sesión y CSRF en mutaciones, además de los permisos indicados.

| Acceso | Método y ruta |
| --- | --- |
| Administrador | `GET/POST /api/categorias`, `PUT /api/categorias/{id}`, `POST /api/categorias/{id}/desactivar` |
| Administrador | `GET/POST /api/productos`, `PUT /api/productos/{id}`, `POST /api/productos/{id}/desactivar`, `POST /api/productos/{id}/foto` (cuerpo binario) |
| Cajero | `GET /api/caja/catalogo`, `GET /api/caja/actual` (devuelve `{ sesion: null }` si no tiene una abierta), `GET /api/caja/administradores`, `POST /api/caja/abrir` |
| Cajero propietario | `POST /api/caja/{id}/depositos`, `POST /api/caja/{id}/retiros` (header `X-Autorizacion-Administrador`), `POST /api/caja/{id}/cerrar` |
| Cajero | `POST /api/ventas/cotizar` (solo cálculo, no guarda), `POST /api/ventas` (sesión propia abierta), `POST /api/ventas/{id}/anular` |
| Propietario o administrador | `GET /api/caja/{id}/ventas?numero=...`, `GET /api/caja/{id}/reporte`, `GET /api/ventas/{id}`, `GET /api/ventas/{id}/logo` |
| Autenticado | `GET /api/productos/{id}/foto` |
| Administrador | `GET /api/caja/sesiones`, `POST /api/caja/{id}/forzar-cierre` |

La edición de catálogo amplía la auditoría automática, incluyendo anterior/nuevo de precios, descuentos, vigencias, fotos y estados. Aperturas, cierres, depósitos, retiros, descuentos manuales y anulaciones agregan auditoría explícita dentro de su transacción. `LineaVenta` contiene solo columnas tipadas; modificadores y combos se agregarán como tablas relacionadas en el Incremento 3.

## Pendientes fuera de estos incrementos

Modificadores, combos, recetas, inventario, clientes, crédito, factura digital, proformas, correo, impresora térmica/gaveta/comanda, sincronización con la nube, respaldo/restauración operativa y Docker. También quedan pendientes expiración por inactividad configurable (RF-SEG-05), ventas en espera y la política de retención de imágenes/auditoría. Estos módulos no son necesarios para ejecutar los dos incrementos entregados.
