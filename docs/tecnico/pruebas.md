# Pruebas y capturas

## xUnit y PostgreSQL local

```powershell
$env:POLLOTIKITO_TEST_CONNECTION = 'Host=localhost;Port=5432;Database=pollotikito_test;Username=pollotikito_test;Password=REEMPLAZAR'
dotnet test Backend/Pollotikito.slnx -c Release
```

La base es exclusiva para pruebas. El rol debe poder crear/eliminar sus propios esquemas y funciones; no necesita superusuario. `PostgresFixture` crea `test_pollotikito_<uuid>`, aplica migraciones, prepara cuentas ficticias y borra solo su esquema y archivos temporales. No Docker/Testcontainers. Sin variable, integración se omite con explicación; conexión definida pero incorrecta falla. La variable debe estar en el entorno de la terminal actual; si se guardó como variable de usuario, abrir una terminal nueva o cargarla explícitamente.

| Suite | Casos | Cobertura |
| --- | --- | --- |
| Unitarias previas | 20 | Validaciones, PIN, IVA, descuentos y pagos. |
| Integración previa | 24 | Acceso/bloqueo, permisos, CSRF, tokens, auditoría, imágenes, consecutivos paralelos, caja/cobros/idempotencia/rollback. |
| MenuTests | 10 | Obligatorios, opciones ajenas/repetidas/inactivas, máximos, precio completo con todos los descuentos/redondeos, instantáneas, disponibilidad y cantidades de componentes. |
| MenuIntegracionTests | 2 | Combo con modificadores, huella, historial y reporte, rechazo por disponibilidad, permisos, límites y anidación en ambos sentidos. |
| **Total definido** | **56** | **30 unitarias y 26 de integración.** |

## Cobertura por requerimiento

| ID | Comprobaciones |
| --- | --- |
| RF-SEG-01 | Crear/editar/desactivar, permisos 403, protección de último administrador e invalidación de sesión. |
| RF-SEG-02 | PIN correcto/incorrecto, ASCII, cinco fallos y desbloqueo; cambio inicial y contraseña. |
| RF-SEG-03 | Acción/usuario, consumo único incluso paralelo, expiración exacta a 60 segundos y filtro de prueba. |
| RF-SEG-04 | Anterior/nuevo sin secretos, login y cambios, inmutabilidad SQL. |
| RF-SIS-02 | Configuración, imagen válida/límite, consecutivos paralelos, no bajar número y rollback junto con venta. |
| RF-CAT-01 / RF-CAT-02 | Catálogo, permisos, categoría inactiva excluye producto y fotos. |
| RF-CAT-03 / RF-CAT-04 | IVA incluido, redondeo por unidad/línea, vigencia, manual/global, prorrateo de centavos. |
| RF-CAT-05 | Mínimo/máximo, obligatorios, pertenencia/actividad, no repetición, adicionales con descuentos, auditoría y snapshots. |
| RF-CAT-06 | Fijos/elegibles, cantidades/recargos, productos ajenos, disponibilidad, anidación en ambos sentidos, snapshots e idempotencia con elecciones. |
| RF-CAJ-01 a RF-CAJ-04 | Apertura única, propiedad, depósitos/retiros autorizados, esperado, conteo, cierre forzado con/sin conteo e inmutabilidad. |
| RF-VEN-01 / RF-VEN-02 / RF-VEN-03 | Líneas/notas/orden, servidor recalcula, pagos mixtos, vuelto, reintentos/conflictos y concurrencia. |
| RF-VEN-09 | Anulación, motivo, efectos en esperado y reporte por método, aviso de reversión externa. |
| RF-VEN-10 / RF-VEN-13 | Consulta histórica y datos del comprobante; presentación/impresión y recorrido visual cubiertos por capturas, sin automatizar hardware. |

Verificación local actual: API, pruebas y helper demo compilan sin errores/advertencias; `npm run build` compila. **56 pruebas aprobadas, 0 fallidas y 0 omitidas** con PostgreSQL configurado. EF confirma que no hay diferencias entre el modelo y la última migración. Una compilación anterior fue bloqueada por Windows al cargar `Pollotikito.Application.dll` (`0x800711C7`); una nueva publicación pudo ejecutar migraciones y pruebas sin modificar ninguna configuración de Windows. No se determinó la causa del bloqueo anterior.

Para repetir la verificación, incluidos los modelos de migración:

```powershell
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project Backend/Pollotikito.Infrastructure --startup-project Backend/Pollotikito.Api
dotnet test Backend/Pollotikito.slnx -c Release
cd Frontend
npm run build
npm run capturas
```

## Capturas repetibles

Preparación única: `npm ci`. El script intenta Chromium de Playwright y, si no está disponible, Edge y Chrome instalados. También se puede instalar Chromium con `npx playwright install chromium`, o elegir navegador mediante `$env:POLLOTIKITO_BROWSER_CHANNEL='chrome'` o `'msedge'`. Solo son dependencias de desarrollo. Luego, desde Frontend:

```powershell
npm run capturas
```

El script:

1. Exige `POLLOTIKITO_TEST_CONNECTION` y el helper rechaza cualquier base distinta de `pollotikito_test`.
2. Crea un esquema `demo_pollotikito_<uuid>`; nunca usa User Secrets ni conexión de producción/desarrollo.
3. Siembra Pollotikito Demo, cuentas ficticias con contraseña/PIN efímeros y hashes, categorías, productos, grupos y combos del ejemplo.
4. Publica y ejecuta su propia API en puerto libre con frontend del mismo origen. No detiene la API ni Vite del usuario.
5. Playwright recorre pantallas con viewport 1366×768, locale es-CR y zona horaria de Costa Rica. Antes de cada PNG comprueba que los campos de contraseña/PIN estén vacíos. No guarda trazas/videos con secretos.
6. Guarda PNG en `docs/manual-usuario/img`. Cierra solo sus procesos, elimina su esquema y carpeta temporal en `finally`. Si no logra limpiar, informa el esquema y conserva el helper para recuperación.

Se generaron **27 capturas** con Edge instalado, viewport 1366×768 y datos ficticios, verificando el recorrido hasta cierre normal, cierre forzado sin conteo y sus reportes. El esquema y la API temporales se eliminaron al finalizar. Las capturas son material del manual y un recorrido funcional; no sustituyen las pruebas monetarias/concurrentes.
