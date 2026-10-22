import { spawn } from "node:child_process";
import { randomBytes } from "node:crypto";
import { mkdir, mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve, dirname, relative, isAbsolute } from "node:path";
import { fileURLToPath } from "node:url";
import net from "node:net";
import { chromium, expect } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
if (!process.env.POLLOTIKITO_TEST_CONNECTION)
  throw new Error(
    "Definí POLLOTIKITO_TEST_CONNECTION para la base exclusiva pollotikito_test. Nunca se utiliza la conexión de desarrollo.",
  );
const schema = "demo_pollotikito_" + randomBytes(16).toString("hex");
const password = "Demo!" + randomBytes(18).toString("hex"),
  nueva = "Nueva!" + randomBytes(18).toString("hex");
const pin = String(100000 + Math.floor(Math.random() * 900000));
const env = {
  ...process.env,
  POLLOTIKITO_DEMO_SCHEMA: schema,
  POLLOTIKITO_DEMO_PASSWORD: password,
  POLLOTIKITO_DEMO_PIN: pin,
};
const output = await mkdtemp(join(tmpdir(), "pollotikito-capturas-"));
const demo = join(output, "demo", "Pollotikito.Demo.dll");
let api,
  browser,
  preparado = false;
function ejecutar(args, opciones = {}) {
  return new Promise((ok, fail) => {
    let salida = "";
    const child = spawn("dotnet", args, {
      cwd: root,
      env,
      windowsHide: true,
      ...opciones,
    });
    child.stdout.on("data", (d) => (salida += d));
    child.stderr.on("data", (d) => (salida += d));
    child.once("error", fail);
    child.once("exit", (code) =>
      code === 0
        ? ok(salida)
        : fail(
            new Error(
              `Falló dotnet ${args[0]} (código ${code}). ${salida.replaceAll(password, "[secreto]").replaceAll(pin, "[PIN]").slice(-2500)}`,
            ),
          ),
    );
  });
}
const port = await new Promise((ok, fail) => {
  const server = net.createServer();
  server.once("error", fail);
  server.listen(0, "127.0.0.1", () => {
    const port = server.address().port;
    server.close(() => ok(port));
  });
});
const url = `http://127.0.0.1:${port}`;
const imgs = join(root, "docs", "manual-usuario", "img");
await mkdir(imgs, { recursive: true });
try {
  console.log("Compilando demo y publicando API en una carpeta temporal…");
  await ejecutar([
    "publish",
    "Backend/Pollotikito.Demo",
    "-c",
    "Release",
    "-o",
    join(output, "demo"),
  ]);
  await ejecutar([
    "publish",
    "Backend/Pollotikito.Api",
    "-c",
    "Release",
    "-o",
    join(output, "api"),
    "-p:SkipFrontendInstall=true",
  ]);
  preparado = true;
  const seed = await ejecutar([demo]);
  const datos = JSON.parse(seed.trim().split(/\r?\n/).at(-1));
  api = spawn("dotnet", [join(output, "api", "Pollotikito.Api.dll")], {
    cwd: join(output, "api"),
    windowsHide: true,
    stdio: "ignore",
    env: {
      ...env,
      ASPNETCORE_ENVIRONMENT: "Testing",
      ASPNETCORE_URLS: url,
      ConnectionStrings__Pollotikito:
        process.env.POLLOTIKITO_TEST_CONNECTION +
        `;Search Path=${schema};Include Error Detail=false`,
      SucursalId: datos.sucursalId,
      Almacenamiento__Logos: join(output, "logos"),
      Almacenamiento__FotosProductos: join(output, "fotos"),
    },
  });
  api.on("error", () => {});
  let lista = false;
  for (let n = 0; n < 100; n++) {
    try {
      const r = await fetch(url + "/api/auth/csrf");
      if (r.ok) {
        lista = true;
        break;
      }
    } catch {}
    if (api.exitCode !== null) break;
    await new Promise((r) => setTimeout(r, 200));
  }
  if (!lista)
    throw new Error(
      "La API aislada no arrancó. Revisá permisos de ejecución de las DLL y acceso a PostgreSQL.",
    );
  for (const channel of process.env.POLLOTIKITO_BROWSER_CHANNEL
    ? [process.env.POLLOTIKITO_BROWSER_CHANNEL]
    : [undefined, "msedge", "chrome"]) {
    try {
      browser = await chromium.launch(channel ? { channel } : {});
      break;
    } catch (e) {
      if (process.env.POLLOTIKITO_BROWSER_CHANNEL) throw e;
    }
  }
  if (!browser)
    throw new Error(
      "No hay navegador compatible. Prepará Chromium con npx playwright install chromium o indicá POLLOTIKITO_BROWSER_CHANNEL.",
    );
  const context = await browser.newContext({
    viewport: { width: 1366, height: 768 },
    locale: "es-CR",
    timezoneId: "America/Costa_Rica",
  });
  const page = await context.newPage();
  const captura = async (nombre, foco) => {
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.locator(".modal").evaluateAll((modales) =>
      modales.forEach((m) => {
        m.scrollTop = 0;
      }),
    );
    if (foco) await foco.scrollIntoViewIfNeeded();
    await page.locator('input[type="password"]').evaluateAll((inputs) => {
      if (inputs.some((i) => i.value))
        throw new Error(
          "No se captura una pantalla con credenciales ingresadas.",
        );
    });
    await page.screenshot({
      path: join(imgs, nombre + ".png"),
      fullPage: false,
    });
    console.log(`Captura: ${nombre}`);
  };
  const boton = (nombre) =>
    page.getByRole("button", { name: nombre, exact: true });
  async function llamar(ruta, body) {
    return page.evaluate(
      async ({ ruta, body }) => {
        const token = await (await fetch("/api/auth/csrf")).json();
        const r = await fetch("/api" + ruta, {
          method: body === undefined ? "GET" : "POST",
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": token.token,
          },
          body: body === undefined ? undefined : JSON.stringify(body),
        });
        if (!r.ok) throw new Error(`API demo ${ruta}: HTTP ${r.status}`);
        return r.status === 204 ? null : r.json();
      },
      { ruta, body },
    );
  }
  await page.goto(url);
  await expect(
    page.getByRole("heading", { name: "Iniciar sesión" }),
  ).toBeVisible();
  await captura("01-login-pin");
  await boton("Administrador").click();
  await captura("02-login-administrador");
  await page.getByLabel("Usuario", { exact: true }).fill("admin_demo");
  await page.getByLabel("Contraseña", { exact: true }).fill(password);
  await boton("Ingresar").click();
  await expect(
    page.getByRole("heading", { name: "Cambiá tu contraseña" }),
  ).toBeVisible();
  await captura("03-cambio-inicial");
  await page.getByLabel("Contraseña actual").fill(password);
  await page.getByLabel("Nueva contraseña", { exact: true }).fill(nueva);
  await page.getByLabel("Confirmar nueva contraseña").fill(nueva);
  await boton("Guardar y continuar").click();
  await expect(
    page.getByRole("heading", { name: "Usuarios", exact: true }),
  ).toBeVisible();
  await captura("04-usuarios");
  await boton("Crear usuario").click();
  await captura("05-crear-usuario");
  await boton("Cancelar").click();
  await boton("Negocio y consecutivos").click();
  await expect(page.getByLabel("Nombre comercial")).toHaveValue(
    "Pollotikito Demo",
  );
  await captura("06-negocio");
  await page
    .getByRole("heading", { name: "Consecutivos", exact: true })
    .scrollIntoViewIfNeeded();
  await captura(
    "07-consecutivos",
    page.getByRole("heading", { name: "Consecutivos", exact: true }),
  );
  await boton("Catálogo").click();
  await expect(
    page.getByRole("cell", { name: "Combo 1 · Combo", exact: true }),
  ).toBeVisible();
  await captura("08-productos");
  await boton("Categorías").click();
  await captura("09-categorias");
  await boton("Productos").click();
  await page
    .getByRole("row")
    .filter({
      has: page.getByRole("cell", { name: "Combo 1 · Combo", exact: true }),
    })
    .getByRole("button", { name: "Grupos / componentes", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Elecciones de Combo 1" }),
  ).toBeVisible();
  await captura(
    "10-componentes-combo",
    page.getByRole("heading", {
      name: "Componentes por una unidad de combo",
      exact: true,
    }),
  );
  await boton("Cerrar elecciones").click();
  await boton("Modificadores").click();
  await expect(
    page.getByRole("heading", { name: "Extras", exact: true }),
  ).toBeVisible();
  await captura("11-modificadores");
  await page
    .locator("article")
    .filter({ has: page.getByRole("heading", { name: "Extras", exact: true }) })
    .getByRole("button")
    .click();
  await captura("12-editar-grupo");
  await boton("Cerrar").click();
  await boton("Sesiones de caja").click();
  await expect(
    page.getByRole("heading", { name: "Sesiones de caja", exact: true }),
  ).toBeVisible();
  await captura("25-sesiones-administracion");
  await boton("Cerrar sesión").click();
  await expect(
    page.getByRole("heading", { name: "Iniciar sesión", exact: true }),
  ).toBeVisible();
  await llamar("/auth/login/pin", { usuarioId: datos.cajeroId, pin });
  await page.reload();
  await expect(
    page.getByRole("heading", { name: "Abrir caja", exact: true }),
  ).toBeVisible();
  await captura("13-abrir-caja");
  await page.getByLabel("Monto inicial en efectivo").fill("10000");
  await boton("Abrir mi caja").click();
  await expect(
    page.getByRole("heading", { name: "Caja — Caja Demo" }),
  ).toBeVisible();
  await captura("14-catalogo-caja");
  await boton("Combos").click();
  await page.getByRole("button", { name: /Combo 1/ }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await page.getByLabel("Extra papas").check();
  await page
    .getByLabel("Refresco a elegir 1")
    .selectOption({ label: "Refresco 1 litro · +₡600,00" });
  await captura("15-personalizar-combo");
  await boton("Confirmar elecciones").click();
  await expect(boton("Cobrar")).toBeEnabled();
  await captura("16-orden");
  await boton("Cobrar").click();
  await page.getByLabel("Método de pago").selectOption("Mixto");
  await page.getByLabel("Parte en efectivo").fill("900");
  await page.getByLabel("Parte en tarjeta").fill("5000");
  await page.getByLabel("Efectivo recibido").fill("1000");
  await page
    .getByLabel("Autorización del datáfono (obligatoria)")
    .fill("DEMO-001");
  await captura("17-cobro-mixto");
  await boton("Confirmar cobro").click();
  await expect(page.getByRole("heading", { name: /Tiquete/ })).toBeVisible();
  await captura("18-tiquete");
  await boton("Volver / Nueva venta").click();
  await boton("Ventas del turno").click();
  await expect(boton("Anular")).toBeVisible();
  await captura("19-ventas-turno");
  await boton("Anular").click();
  await page
    .getByLabel("Motivo obligatorio")
    .fill("Pedido de demostración cancelado");
  await captura("20-anulacion-aviso");
  await boton("Confirmar anulación").click();
  await expect(page.getByRole("status")).toBeVisible();
  await boton("Depósito").click();
  await page.getByLabel("Monto", { exact: true }).fill("500");
  await page.getByLabel("Motivo obligatorio").fill("Cambio de demostración");
  await captura("21-deposito");
  await boton("Registrar depósito").click();
  await expect(
    page.getByRole("heading", { name: "Depósito de efectivo", exact: true }),
  ).not.toBeVisible();
  await boton("Retiro").click();
  await expect(page.getByLabel("Administrador que autoriza")).toHaveValue(
    datos.adminId,
  );
  await page.getByLabel("Monto", { exact: true }).fill("200");
  await page.getByLabel("Motivo obligatorio").fill("Retiro de demostración");
  await captura("22-retiro");
  await page.getByLabel("PIN de administrador").fill(pin);
  await boton("Registrar retiro").click();
  await expect(
    page.getByRole("heading", { name: "Retiro de efectivo", exact: true }),
  ).not.toBeVisible();
  await boton("Cerrar caja").click();
  await page.getByLabel("Cantidad de 10000", { exact: true }).fill("1");
  await page.getByLabel("Cantidad de 100", { exact: true }).fill("3");
  await captura("23-conteo-cierre");
  page.once("dialog", (d) => d.accept());
  await boton("Confirmar cierre").click();
  await expect(
    page.getByRole("heading", { name: "Pollotikito — Reporte de sesión" }),
  ).toBeVisible();
  await captura("24-reporte");
  await llamar("/caja/abrir", { montoInicial: 5000 });
  await llamar("/auth/logout", {});
  await llamar("/auth/login/admin", {
    nombreUsuario: "admin_demo",
    password: nueva,
  });
  await page.reload();
  await expect(
    page.getByRole("heading", { name: "Usuarios", exact: true }),
  ).toBeVisible();
  await boton("Sesiones de caja").click();
  await expect(
    page.getByRole("cell", { name: "Abierta", exact: true }),
  ).toBeVisible();
  await captura("25-sesiones-administracion");
  await boton("Forzar cierre").click();
  await page
    .getByLabel("Motivo obligatorio")
    .fill("Cambio de turno de demostración");
  await page.getByLabel("Cerrar sin conteo").check();
  await captura("26-cierre-forzado");
  page.once("dialog", (d) => d.accept());
  await boton("Confirmar cierre").click();
  await expect(
    page.getByText(
      "CERRADA SIN CONTEO — no se registraron contado ni diferencia.",
      { exact: true },
    ),
  ).toBeVisible();
  await captura("27-reporte-sin-conteo");
  console.log("Capturas completas. Datos exclusivamente ficticios.");
} catch (error) {
  const mensaje = error instanceof Error ? error.message : String(error);
  console.error(
    mensaje
      .replaceAll(password, "[contraseña]")
      .replaceAll(nueva, "[contraseña]")
      .replaceAll(pin, "[PIN]"),
  );
  process.exitCode = 1;
} finally {
  if (browser) await browser.close();
  if (api && api.exitCode === null) {
    api.kill();
    await Promise.race([
      new Promise((r) => api.once("exit", r)),
      new Promise((r) => setTimeout(r, 3000)),
    ]);
  }
  let limpio = !preparado;
  if (preparado) {
    try {
      await ejecutar([demo, "limpiar"]);
      limpio = true;
    } catch {
      console.error(
        `No se pudo limpiar el esquema temporal ${schema}. Revisá PostgreSQL; se conservó el helper en ${output}.`,
      );
    }
  }
  if (limpio) {
    const destino = relative(resolve(tmpdir()), resolve(output));
    if (
      isAbsolute(destino) ||
      destino.startsWith("..") ||
      !destino.startsWith("pollotikito-capturas-")
    )
      throw new Error(
        "Ruta temporal fuera del destino esperado; no se elimina.",
      );
    await rm(output, { recursive: true, force: true });
  }
}
