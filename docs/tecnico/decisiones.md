# Decisiones

Fecha del registro: **2026-10-21**. Contexto común: una sucursal de comida rápida, operación local sin internet y entregas incrementales; todavía no se conocen recetas ni menú real.

| Decisión | Motivo / límite |
| --- | --- |
| PostgreSQL local y mismo origen | Venta sin internet; API publicada sirve React. |
| Cookie HttpOnly + CSRF | Sesión local sin JWT ni secretos en almacenamiento del navegador. |
| UUID v7 en cada tabla | Preparación para sincronización futura, aún no implementada. |
| Desactivar en vez de borrar | Conservar referencias e historia; auditoría de anterior/nuevo. |
| Precios IVA incluido y decimal | Cobro CRC exacto con redondeo AwayFromZero por línea. |
| Automático, manual de línea y global | Orden explícito; global reparte centavos sin perder dinero. |
| Consecutivo dentro del cobro | Rollback conjunto; sin endpoint de emisión ni bloqueo global. |
| Una sesión abierta por sucursal | Índice parcial; serialización por fila de sesión. |
| Elecciones tipadas con instantáneas | Historia estable y productos/cantidades disponibles para I4; sin JSON reservado. |
| Grupos compartidos y orden de asignación | Reutilización sin duplicar configuración por producto. |
| `0 ≤ mínimo ≤ máximo`, máximo de 1 a 100 | Grupo obligatorio cuando mínimo > 0; opciones activas suficientes para vender. |
| **Cada opción se elige como máximo una vez por línea** | No se permite «extra queso ×2». Posible mejora futura: cantidad tipada por opción con límites, cálculo e inventario explícitos. |
| Elecciones por unidad de línea | Cantidad multiplica toda la configuración; elecciones distintas requieren líneas separadas. |
| Componentes simples, sin combos anidados | Se bloquea incluir combos y convertir en combo un producto usado como componente activo. |
| Grupos propios de componentes no se heredan | Las elecciones vendidas son las del producto padre; componentes son cantidades de productos simples. |
| Catálogo excluye lo no vendible | Advertencia precisa en administración y rechazo adicional del servidor al cotizar/cobrar. |
| Anulación conserva pago externo | Recordatorio para devolución manual, desglose por método. |
| Cierre forzado sin conteo explícito | Contado y diferencia nulos; nunca inventar cero. |
| Imágenes locales con UUID | Máximo 1 MB, firmas PNG/JPG, ruta relativa en BD. |
| Capturas en esquema demo de base de pruebas | Nunca sembrar negocio real; secretos efímeros sin capturarlos ni registrarlos. |
| Migración de I3 escrita en SQL con snapshot congelado | Se escribió durante un bloqueo de carga de DLL. Luego se verificó su ejecución en PostgreSQL y EF confirmó que el snapshot coincide con el modelo. |

Pendientes: inventario/recetas y compras (I4), clientes/documentos/crédito/correo (I5), reportes ampliados/cuentas por pagar, operación de respaldos automáticos, nube, ventas en espera, timeout por inactividad configurable, impresora térmica/gaveta/comanda, retención de imágenes y auditoría. Docker no forma parte de este trabajo.

## Alternativas consideradas

| Fecha | Contexto | Decisión | Alternativa considerada | Motivo |
| --- | --- | --- | --- | --- |
| 2026-10-21 | Navegador y API en la misma PC/origen | Cookie HttpOnly + CSRF | JWT en almacenamiento del navegador | Sesión revocable, evita exponer el token a JavaScript y no necesita autenticación distribuida. |
| 2026-10-21 | Venta sin internet, concurrencia local | PostgreSQL local, sin Docker | Servicio cloud o contenedores | Requisito de instalación local y transacciones/bloqueos reales; no depender de infraestructura externa. |
| 2026-10-21 | Aún no hay integración con hardware | Tiquete por impresión del navegador | Controlador térmico y apertura de gaveta | Mantener el alcance; permite imprimir/guardar PDF desde ahora. |
| 2026-10-21 | Operación del cajero en su turno | Cajero descuenta y anula **sin PIN administrativo**, con motivo y auditoría | Exigir autorización para cada ajuste | Flujo aprobado de I2; permisos limitados a sesión propia abierta. |
| 2026-10-21 | Salida de efectivo | Retiro con PIN administrativo y token de un uso por acción/usuario | Reutilizar sesión admin o token genérico | Autoriza solo ese retiro durante 60 segundos, sin elevar rol del cajero. |
| 2026-10-21 | Venta histórica y futuro inventario | Elecciones en tablas tipadas con IDs y cantidades | JSON reservado o releer el combo actual | Historia inmutable y expansión de consumo verificable en I4. |
| 2026-10-21 | Elecciones táctiles por unidad | Una opción de modificador como máximo una vez; líneas separadas por configuración | Cantidad libre por opción / elección por cada unidad de una misma línea | Reduce ambigüedad; cantidades de extras quedan como mejora futura. |
| 2026-10-21 | Datos del restaurante deben preservarse | Capturas en esquema temporal de base de pruebas | Sembrar la base de desarrollo | Aislamiento, credenciales efímeras y limpieza reproducible. |
