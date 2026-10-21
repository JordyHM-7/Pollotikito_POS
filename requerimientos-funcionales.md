# Pollotikito: Requerimientos funcionales

Actualizado: 21 de octubre de 2026

## Contexto técnico (para el agente de código)

- **Backend:** ASP.NET Core (.NET 10) Web API + EF Core, Clean Architecture (Domain, Application, Infrastructure, Api).
- **Base de datos:** PostgreSQL local, fuente de verdad. Llaves primarias UUID en todas las tablas (preparado para sync a la nube en F2).
- **Frontend caja:** React + TypeScript con Vite, servido por la misma API y usado en el navegador de la PC de caja.
- **PDF:** QuestPDF para tiquetes, facturas, proformas y reportes.
- **Correo:** patrón outbox; los envíos se guardan en una tabla y un worker los manda cuando hay internet.
- **Offline:** nada del MVP depende de internet.
- **Nube (F2):** Supabase + dashboard React PWA para el iPhone del dueño; sync de una sola vía (local → nube).
- **Dinero:** colones (CRC), `decimal` en C# y `numeric(18,2)` en PostgreSQL; precios con IVA incluido.
- **Trazabilidad:** referenciar el ID del requerimiento (`RF-XXX-nn`) en tareas, commits y pruebas.

## Alcance y supuestos

Pollotikito es un POS para un restaurante de comida rápida con una sucursal y una caja, que vende sin internet y deja al dueño ver reportes desde su iPhone. Este documento convierte la lista que mandó el cliente en requerimientos numerados, agrega lo que un restaurante necesita y el cliente no mencionó, y marca qué entra en el MVP.

| Supuesto | Valor actual | Impacto si cambia |
| --- | --- | --- |
| Sucursales | 1 | Bajo: todo lleva `sucursal_id` desde el inicio |
| Cajas | 1 PC | Bajo: la UI corre en navegador, otra caja es otro navegador |
| Internet | No requerido para vender | Ninguno: la nube solo recibe datos |
| Reportes remotos | Sí, desde iPhone (PWA) | Medio: requiere el sync a la nube |
| Factura electrónica Hacienda | No por ahora | Alto: se deja el modelo preparado (consecutivos, clave, receptor) |
| Pantalla de cocina | No confirmada | Bajo: la comanda llega en F2 junto con la impresora |
| Inventario | Por insumos con recetas y producción por tandas (ver abajo) | Bajo: el modelo soporta ambos casos |
| Cajeros por turno | 1 (confirmado) | Bajo: cada cierre de caja tiene un solo responsable |
| Métodos de pago | Efectivo, tarjeta y SINPE Móvil (confirmado) | Bajo: agregar otro método es configuración |
| Impresora térmica y gaveta | Fuera del MVP; tiquete en pantalla y PDF | Bajo: se agrega en F2 sin cambiar las ventas |

**Convención:** cada requerimiento tiene un ID `RF-<MÓDULO>-<n>` para referenciarlo en tareas, commits y pruebas. Prioridad **MVP** = necesario para abrir el restaurante; **F2** = fase 2; **F3** = después. La columna Origen marca como Agregado lo que no venía en la lista del cliente y hay que validar con él.

## Inventario: cómo lo planteamos (a validar)

Propuesta: el inventario se lleva por **artículo de inventario** y cada producto del menú tiene una **receta** que dice qué artículos descuenta al venderse. Así el mismo diseño sirve si el cliente controla por producto terminado o por insumo, y cambiar de idea después es configuración, no código.

**Dos conceptos separados:**

- **Producto de venta:** lo que aparece en la caja y tiene precio (Combo 1, 3 piezas, Coca Cola 600 ml).
- **Artículo de inventario:** lo que se compra y se cuenta (pieza de pollo, porción de papas, Coca Cola 600 ml, caja de cartón, vaso). Tiene unidad de compra, unidad de consumo y factor de conversión (1 caja = 24 botellas; 1 bolsa = 10 porciones).

**Ejemplos de receta:**

| Producto de venta | Descuenta |
| --- | --- |
| Coca Cola 600 ml | 1 Coca Cola 600 ml (receta 1 a 1) |
| 3 piezas | 3 piezas de pollo, 1 caja chica |
| Combo 1 | 2 piezas de pollo, 1 porción de papas, 1 refresco elegido, 1 caja |

**Cómo se mueve el stock:**

1. **Entra** por facturas de compra a proveedores (lo pide el cliente: "alimente su inventario con el registro de facturas de compra") o por órdenes de entrada manuales.
2. **Sale** automáticamente al cobrar una venta, según la receta. Si la venta se anula, el stock regresa.
3. **Se ajusta** con órdenes de salida con motivo (merma, quemado, consumo de empleados, cortesía) y con el conteo físico, que registra la diferencia entre lo que dice el sistema y lo contado.
4. Cada movimiento queda en un **kardex** (fecha, tipo, cantidad, costo, usuario, documento origen), que es la base del "reporte de movimientos".

**Si el cliente dice que no es así:** si solo controla producto terminado, cada receta es 1 a 1 y listo. Si no quiere controlar ciertos artículos (aceite, sal), simplemente no se ponen en la receta.

**Producción por tandas (confirmado):** el cajero registra una orden de producción, por ejemplo 40 piezas. La orden descuenta el pollo crudo (y el aceite o el empanizado, si se controlan) y suma 40 piezas listas al inventario. Las ventas descuentan piezas listas. Al cierre, las piezas que sobran se registran como merma o quedan para el día siguiente, y el reporte muestra producido, vendido y desperdiciado por día.

**Regla de stock negativo:** el sistema permite vender aunque el stock quede negativo y lo señala en reportes. Bloquear una venta en hora pico porque alguien no registró una compra es peor que un número negativo.

## Requerimientos funcionales por módulo

Son 11 módulos. La columna Origen dice si lo pidió el cliente o si lo agregamos nosotros y hay que validarlo.

### 1. Usuarios y seguridad (SEG)

| ID | Requerimiento | Prioridad | Origen |
| --- | --- | --- | --- |
| RF-SEG-01 | Crear, editar y desactivar usuarios con rol Administrador o Cajero. | MVP | Agregado |
| RF-SEG-02 | El cajero entra con PIN de 4 a 6 dígitos; el administrador con usuario y contraseña. | MVP | Agregado |
| RF-SEG-03 | Retirar efectivo y cambiar precios requiere PIN de administrador. Por ahora el cajero puede anular ventas y aplicar descuentos manuales sin autorización, con motivo obligatorio. | MVP | Agregado |
| RF-SEG-04 | Toda acción sensible queda en una bitácora de auditoría (quién, qué, cuándo, valor anterior y nuevo). | MVP | Agregado |
| RF-SEG-05 | Cierre de sesión automático de la caja tras un tiempo configurable sin uso. | F2 | Agregado |

### 2. Catálogo (CAT)

| ID | Requerimiento | Prioridad | Origen |
| --- | --- | --- | --- |
| RF-CAT-01 | Crear, editar y desactivar productos de venta con nombre, precio, categoría, foto opcional e impuesto. | MVP | Cliente |
| RF-CAT-02 | Organizar productos por categorías, con orden configurable para los botones de la caja. | MVP | Cliente |
| RF-CAT-03 | Los precios se registran con impuesto incluido (IVA 13% por defecto) y el sistema desglosa base e impuesto en cada comprobante. | MVP | Cliente |
| RF-CAT-04 | Descuentos por producto (porcentaje o monto) con vigencia opcional, y descuento manual en la venta según RF-SEG-03. | MVP | Cliente |
| RF-CAT-05 | Modificadores por producto: opciones sin costo (sin cebolla) y con costo (extra papas). | MVP | Agregado |
| RF-CAT-06 | Combos con componentes a elegir (refresco a escoger entre varios). | MVP | Agregado |
| RF-CAT-07 | Cada producto tiene una receta que define qué artículos de inventario descuenta (ver sección de inventario). | MVP | Agregado |

### 3. Ventas en caja (VEN)

| ID | Requerimiento | Prioridad | Origen |
| --- | --- | --- | --- |
| RF-VEN-01 | Armar una orden tocando productos, con cantidades, modificadores y notas por línea. | MVP | Agregado |
| RF-VEN-02 | Indicar el tipo de orden: comer aquí, para llevar o express. | MVP | Agregado |
| RF-VEN-03 | Cobrar con efectivo (calcula vuelto), tarjeta (datáfono externo, se registra el número de autorización), SINPE Móvil (se registra el comprobante) o pago mixto. | MVP | Cliente |
| RF-VEN-04 | Imprimir tiquete de venta en impresora térmica de 80 mm y abrir la gaveta al cobrar en efectivo. | F2 | Cliente |
| RF-VEN-05 | Imprimir comanda para cocina al confirmar la orden. | F2 | Agregado |
| RF-VEN-06 | Generar factura de venta digital (no electrónica) con consecutivo propio, en PDF tamaño carta, asociada a un cliente. | MVP | Cliente |
| RF-VEN-07 | Enviar la factura digital al correo del cliente; si no hay internet, queda en cola y se envía al volver la conexión. | MVP | Cliente |
| RF-VEN-08 | Vender a crédito a un cliente autorizado, generando una cuenta por cobrar. | MVP | Cliente |
| RF-VEN-09 | Anular una venta con motivo; revierte inventario, caja y cuenta por cobrar. | MVP | Agregado |
| RF-VEN-10 | Reimprimir tiquetes y facturas anteriores. | MVP | Agregado |
| RF-VEN-11 | Dejar órdenes en espera y retomarlas. | F2 | Agregado |
| RF-VEN-12 | Pantalla de cocina en tablet en lugar de comanda impresa. | F3 | Agregado |
| RF-VEN-13 | Mostrar el tiquete en pantalla al cobrar y permitir imprimirlo o guardarlo en PDF desde el navegador, mientras no haya impresora térmica. | MVP | Cliente |

### 4. Control de caja (CAJ)

| ID | Requerimiento | Prioridad | Origen |
| --- | --- | --- | --- |
| RF-CAJ-01 | Abrir caja con monto inicial de efectivo; no se puede vender sin caja abierta. Cada turno tiene una sola caja abierta a nombre de un solo cajero, que responde por el cierre. | MVP | Cliente |
| RF-CAJ-02 | Registrar depósitos (entradas) y retiros (salidas) de efectivo con motivo. | MVP | Cliente |
| RF-CAJ-03 | Cerrar caja con conteo de efectivo por denominación; el sistema calcula esperado, contado y diferencia. | MVP | Cliente |
| RF-CAJ-04 | Generar el reporte diario de caja en PDF imprimible: ventas por método de pago, depósitos, retiros, anulaciones, descuentos y diferencia. | MVP | Cliente |

### 5. Proformas (PRF)

| ID | Requerimiento | Prioridad | Origen |
| --- | --- | --- | --- |
| RF-PRF-01 | Crear proformas con cliente, productos, precios y fecha de vencimiento. | MVP | Cliente |
| RF-PRF-02 | Imprimir la proforma en tamaño carta y enviarla por correo como PDF. | MVP | Cliente |
| RF-PRF-03 | Convertir una proforma aceptada en venta sin volver a digitar. | F2 | Agregado |

### 6. Inventario (INV)

| ID | Requerimiento | Prioridad | Origen |
| --- | --- | --- | --- |
| RF-INV-01 | Crear artículos de inventario con cantidad inicial, unidad de compra, unidad de consumo, factor de conversión, costo y stock mínimo. | MVP | Cliente |
| RF-INV-02 | Descontar inventario automáticamente al cobrar según la receta del producto. | MVP | Agregado |
| RF-INV-03 | Registrar órdenes de entrada y de salida manuales con motivo (merma, consumo interno, cortesía). | MVP | Cliente |
| RF-INV-04 | Registrar conteo físico y generar el ajuste por diferencia. | MVP | Agregado |
| RF-INV-05 | Reporte de movimientos (kardex) por artículo y rango de fechas. | MVP | Cliente |
| RF-INV-06 | Alerta de artículos bajo el stock mínimo. | F2 | Agregado |
| RF-INV-07 | Costo de cada producto de venta calculado desde su receta, y margen. | F2 | Agregado |
| RF-INV-08 | Órdenes de producción por tandas: descuentan insumos (pollo crudo) y suman piezas listas; las ventas descuentan piezas listas y lo sobrante al cierre se registra como merma o se traslada. | MVP | Cliente |

### 7. Clientes y cuentas por cobrar (CLI)

| ID | Requerimiento | Prioridad | Origen |
| --- | --- | --- | --- |
| RF-CLI-01 | Crear y editar clientes: nombre, identificación, teléfono y correo. | MVP | Cliente |
| RF-CLI-02 | Habilitar crédito por cliente con límite y plazo en días. | MVP | Cliente |
| RF-CLI-03 | Registrar abonos a una o varias facturas pendientes, con método de pago. | MVP | Cliente |
| RF-CLI-04 | Estado de cuenta del cliente (facturas, abonos, saldo), imprimible y enviable por correo. | MVP | Cliente |
| RF-CLI-05 | Listado de facturas vencidas según el plazo del cliente. | MVP | Cliente |
| RF-CLI-06 | Historial de compras por cliente. | MVP | Cliente |
| RF-CLI-07 | Reporte de abonos por rango de fechas. | MVP | Cliente |

### 8. Proveedores, compras y cuentas por pagar (PRV)

| ID | Requerimiento | Prioridad | Origen |
| --- | --- | --- | --- |
| RF-PRV-01 | Crear y editar proveedores con datos de contacto y plazo de crédito. | MVP | Cliente |
| RF-PRV-02 | Registrar facturas de compra (contado o crédito) con sus líneas; cada línea suma al inventario y actualiza el costo. | MVP | Cliente |
| RF-PRV-03 | Registrar abonos a facturas de proveedores. | MVP | Cliente |
| RF-PRV-04 | Estado de cuenta por proveedor y listado de facturas vencidas. | MVP | Cliente |
| RF-PRV-05 | Historial de compras por proveedor y reporte de abonos. | MVP | Cliente |

### 9. Reportes (REP)

| ID | Requerimiento | Prioridad | Origen |
| --- | --- | --- | --- |
| RF-REP-01 | Ventas por día, rango de fechas, categoría, producto y método de pago. | MVP | Agregado |
| RF-REP-02 | Productos más vendidos y ventas por hora del día. | F2 | Agregado |
| RF-REP-03 | Facturas vencidas consolidadas (por cobrar y por pagar). | MVP | Cliente |
| RF-REP-04 | Historial de compras general. | MVP | Cliente |
| RF-REP-05 | Anulaciones y descuentos por usuario. | MVP | Agregado |
| RF-REP-06 | Exportar cualquier reporte a PDF y Excel. | F2 | Agregado |

### 10. Nube y reportes remotos (NUB)

| ID | Requerimiento | Prioridad | Origen |
| --- | --- | --- | --- |
| RF-NUB-01 | Subir ventas, movimientos de caja y cierres a la nube cuando haya internet, sin duplicar ni perder registros. | F2 | Agregado |
| RF-NUB-02 | Dashboard para el dueño en el iPhone (PWA) con ventas del día, cierres y comparativos. | F2 | Agregado |
| RF-NUB-03 | Respaldo automático cifrado de la base de datos local en la nube, varias veces al día. | MVP | Agregado |

### 11. Operación y configuración (SIS)

| ID | Requerimiento | Prioridad | Origen |
| --- | --- | --- | --- |
| RF-SIS-01 | Todas las funciones de venta, caja, inventario y cuentas funcionan sin internet. | MVP | Cliente |
| RF-SIS-02 | Configurar datos del negocio (nombre, cédula, teléfono, logo, pie de tiquete) y consecutivos de documentos. | MVP | Agregado |
| RF-SIS-03 | Configurar impresoras de tiquete y comanda. | F2 | Agregado |
| RF-SIS-04 | Restaurar el sistema en otra PC desde un respaldo siguiendo un procedimiento documentado. | MVP | Agregado |
| RF-SIS-05 | Emitir comprobantes electrónicos ante Hacienda. | F3 | Agregado |

## Orden de construcción

El MVP se arma en 7 incrementos; al terminar el 2 ya se puede vender y enseñárselo al cliente para recibir correcciones temprano. Inventario va en el 4 para dar tiempo a que el cliente confirme cómo lo maneja.

1. **Base del sistema:** solución .NET con Clean Architecture, PostgreSQL, migraciones, usuarios con PIN, roles, bitácora y configuración del negocio. Cubre RF-SEG-01 a 04 y RF-SIS-02.
2. **Vender y cuadrar caja:** catálogo básico, pantalla de caja, cobro con efectivo, tarjeta y SINPE, tiquete en pantalla y PDF, anulación, apertura y cierre de caja con reporte diario. Cubre RF-CAT-01 a 04, RF-VEN-01 a 03, 09, 10 y 13, y RF-CAJ-01 a 04.
3. **Menú completo:** modificadores y combos. Cubre RF-CAT-05 y 06.
4. **Inventario y compras:** artículos, recetas, descuento automático, producción por tandas, entradas, salidas, conteo, kardex, proveedores y facturas de compra. Cubre RF-INV-01 a 05 y 08, RF-CAT-07 y RF-PRV-01 y 02.
5. **Clientes y documentos:** clientes, crédito, abonos, estados de cuenta, factura digital en PDF, envío por correo con cola offline y proformas. Cubre RF-CLI-01 a 07, RF-VEN-06 a 08 y RF-PRF-01 y 02.
6. **Cuentas por pagar y reportes:** abonos a proveedores, estados de cuenta, vencidas y reportes de ventas. Cubre RF-PRV-03 a 05 y RF-REP-01, 03, 04 y 05.
7. **Puesta en producción:** respaldos automáticos, procedimiento de restauración, instalación en la PC del cliente y prueba completa sin internet. Cubre RF-NUB-03 y RF-SIS-01 y 04.

Después del MVP, la fase 2 empieza por el sync a la nube y el dashboard del iPhone (RF-NUB-01 y 02), seguido de la impresora térmica con gaveta y comanda (RF-VEN-04 y 05, RF-SIS-03).

## Preguntas abiertas para el cliente

Las respuestas pueden cambiar prioridades, pero ninguna bloquea empezar los incrementos 1 a 3.

- [ ] ¿Controla inventario por producto terminado, por insumo o no lo controla hoy? ¿Cuenta físico al cierre del día o de la semana?
- [x] ¿Fríe por tandas (por ejemplo 40 piezas en la mañana) o al momento de cada pedido?
- [ ] ¿Qué combos y modificadores tiene el menú actual? Pedir el menú con precios.
- [x] ¿Acepta tarjeta y SINPE Móvil? ¿Cobra servicio de express?
- [ ] ¿Quiénes le compran a crédito y con qué plazo? ¿Qué proveedores le dan crédito?
- [ ] ¿Para qué usa las proformas en un restaurante (eventos, pedidos grandes)?
- [ ] ¿Qué datos debe llevar el tiquete y la factura digital (cédula, razón social, leyendas)?
- [x] ¿Quién puede anular ventas y dar descuentos? ¿Habrá más de un cajero por turno?
- [ ] ¿Qué impresora térmica y gaveta va a comprar, o ya tiene? ¿Tiene UPS?
- [x] Confirmar con su contador que no está obligado a factura electrónica ante Hacienda.

**Respondido el 21 de octubre:** acepta tarjeta y SINPE Móvil; un solo cajero por turno; por ahora el cajero puede anular y dar descuentos; no está obligado a factura electrónica; fríe por tandas. La impresora térmica queda fuera del MVP. Siguen abiertas las demás preguntas.
