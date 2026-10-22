# Seguridad

## Identidad y autorización

Cookie HttpOnly, SameSite Strict, mismo origen, sin JWT. En Production exige HTTPS. PIN ASCII 4–6 dígitos; contraseña 10–128 caracteres. Ambos se guardan con `PasswordHasher<Usuario>` de ASP.NET Core; nunca texto plano. Cinco fallos bloquean la cuenta cinco minutos. Los mensajes de acceso son genéricos. El selector público expone ID/nombre de cuentas activas con PIN; permite identificar la cuenta antes de comprobar el PIN.

Administrador inicial y resets administrativos de contraseña obligan a cambiarla. La sesión queda restringida a consultar identidad, CSRF, cambiar contraseña y salir. Cambios de credenciales/rol/datos invalidan las sesiones por versión de seguridad. No se permite desactivar la propia cuenta ni dejar sin administrador activo. Usuarios no se eliminan.

Toda mutación, incluidos logins, valida `X-CSRF-TOKEN` obtenido de `GET /api/auth/csrf`. Renovar tras login, logout o cambio de contraseña. La cookie de autenticación no se lee desde JavaScript.

## Matriz HTTP

| Acceso | Endpoints |
| --- | --- |
| Público | `GET /api/auth/csrf`, `GET /api/auth/cajeros`; `POST /api/auth/login/pin`, `POST /api/auth/login/admin` |
| Autenticado | `GET /api/auth/me`, `POST /api/auth/logout`; `POST /api/autorizaciones/administrador` |
| Administrador incluso con cambio pendiente | `POST /api/auth/cambiar-password` |
| Administrador | `GET/POST /api/usuarios`; `PUT /api/usuarios/{id}`; `PUT .../{id}/credenciales`; `POST .../{id}/desactivar` |
| Administrador | `GET/PUT /api/configuracion`; `GET/POST /api/configuracion/logo`; `GET /api/consecutivos`; `PUT /api/consecutivos/{tipo}` |
| Administrador | `GET/POST /api/categorias`, `GET/POST /api/productos`; `PUT /api/categorias/{id}`, `PUT /api/productos/{id}`; sus `POST .../{id}/desactivar`; `POST /api/productos/{id}/foto` |
| Administrador | `GET/POST /api/modificadores/grupos`; `PUT /api/modificadores/grupos/{id}`; `POST .../{id}/desactivar` |
| Administrador | `POST /api/modificadores/grupos/{grupo}/opciones`; `PUT .../{grupo}/opciones/{id}`; `POST /api/modificadores/opciones/{id}/desactivar` |
| Administrador | `GET/PUT /api/productos/{id}/grupos-modificadores`; `GET/PUT /api/productos/{id}/combo` |
| Cajero | `GET /api/caja/catalogo`, `/api/caja/actual`, `/api/caja/administradores`; `POST /api/caja/abrir`; `POST /api/ventas/cotizar` |
| Cajero propietario con sesión abierta | `POST /api/ventas`; `POST /api/ventas/{id}/anular`; `POST /api/caja/{id}/depositos`, `/retiros`, `/cerrar` |
| Administrador | `GET /api/caja/sesiones`; `POST /api/caja/{id}/forzar-cierre` |
| Propietario o administrador | `GET /api/caja/{id}/ventas?numero=...`, `/reporte`; `GET /api/ventas/{id}`, `/logo` |
| Autenticado | `GET /api/productos/{id}/foto` |
| Autenticado + token acción `prueba` | `POST /api/pruebas/autorizacion-administrador` |

El rol se comprueba en endpoint y la propiedad de la sesión en Application. Ser administrador permite consultar/forzar cierre; no vender en nombre del cajero. El retiro además consume token de acción `retiro_efectivo`. El endpoint de prueba es infraestructura de RF-SEG-03, no una operación comercial.

## Autorización de un uso

Solicitud `{ administradorId, pin, accion }`. Solo acciones explícitas `prueba` y `retiro_efectivo`. Administrador activo, con PIN y sin cambio de contraseña pendiente. Devuelve secreto aleatorio y vencimiento de 60 segundos; guarda hash del token, administrador, solicitante y acción. Consumo atómico condicional por hash/usuario/acción/vencimiento/no consumido. A los 60 segundos ya vence. Otro usuario u otra acción no sirven.

Enviar `X-Autorizacion-Administrador`. Reutilización rechazada aun con solicitudes paralelas. `AutorizacionAdministradorFiltro("accion")` es reutilizable: registrar una acción permitida en Application, añadir filtro a endpoint autenticado y pruebas. El token se consume antes de ejecutar el endpoint; si este falla hay que pedir otro.

## Auditoría y archivos

Cambios de usuarios, negocio, consecutivos y catálogo completo registran anterior/nuevo en la misma transacción. Logins correctos e incorrectos también dejan auditoría; usuario nullable cuando no se identifica. Aperturas/cierres, movimientos, descuentos manuales y anulaciones tienen auditoría explícita. Valores de credenciales solo indican presencia; ni hashes ni PIN/contraseña aparecen en snapshots. API no registra cuerpos ni parámetros SQL. Errores internos solo exponen TraceId mediante ProblemDetails.

Bitácora sin API de modificación y con trigger que rechaza UPDATE/DELETE/TRUNCATE. Las imágenes se reciben como binario máximo 1 MB, con validación de firmas PNG/JPG; nombre UUID generado localmente, ruta relativa en BD. No se aceptan rutas/URLs del cliente. Lectura autenticada desde carpeta configurada, `nosniff`. Se conservan imágenes antiguas para las referencias históricas.

## Límites conocidos

Este diseño no impide que el dueño del equipo o un administrador de PostgreSQL altere archivos, permisos o triggers. Restringir acceso físico y credenciales de BD; separar migrador/ejecución es una decisión de despliegue. Un PIN corto depende del bloqueo de intentos y no reemplaza protección del equipo. HttpOnly/CSRF no resuelven una vulnerabilidad XSS: no introducir HTML sin sanitizar ni scripts externos. HTTPS local y persistencia de Data Protection deben configurarse al desplegar. Timeout por inactividad configurable y retención de auditoría/imágenes quedan pendientes. No se modifica la protección de Windows desde el proyecto.
