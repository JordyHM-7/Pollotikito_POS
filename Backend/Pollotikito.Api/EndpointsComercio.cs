using Pollotikito.Application;
using Pollotikito.Domain;
using Pollotikito.Infrastructure;

namespace Pollotikito.Api;

public static class EndpointsComercio
{
    public static void Mapear(WebApplication app)
    {
        var admin = app.MapGroup("/api").RequireAuthorization("Administrador");
        admin.MapGet("/categorias", (CatalogoServicio s) => s.Categorias());
        admin.MapPost("/categorias", (DatosCategoria d, CatalogoServicio s) => s.GuardarCategoria(null, d));
        admin.MapPut("/categorias/{id:guid}", (Guid id, DatosCategoria d, CatalogoServicio s) => s.GuardarCategoria(id, d));
        admin.MapPost("/categorias/{id:guid}/desactivar", async (Guid id, CatalogoServicio s) => { await s.DesactivarCategoria(id); return Results.NoContent(); });
        admin.MapGet("/productos", (CatalogoServicio s) => s.Productos());
        admin.MapPost("/productos", (DatosProducto d, CatalogoServicio s) => s.GuardarProducto(null, d));
        admin.MapPut("/productos/{id:guid}", (Guid id, DatosProducto d, CatalogoServicio s) => s.GuardarProducto(id, d));
        admin.MapPost("/productos/{id:guid}/desactivar", async (Guid id, CatalogoServicio s) => { await s.DesactivarProducto(id); return Results.NoContent(); });
        admin.MapPost("/productos/{id:guid}/foto", async (Guid id, HttpRequest request, CatalogoServicio s) => new { ruta = await s.Foto(id, await LeerImagen(request)) });
        admin.MapGet("/caja/sesiones", (CajaServicio s) => s.Sesiones());
        admin.MapPost("/caja/{id:guid}/forzar-cierre", (Guid id, CerrarCaja d, CajaServicio s) => s.Cerrar(id, d, true));

        var cajero = app.MapGroup("/api").RequireAuthorization("Cajero");
        cajero.MapGet("/caja/catalogo", (CatalogoServicio s) => s.Caja());
        cajero.MapGet("/caja/actual", async (CajaServicio s) => new { sesion = await s.Actual() });
        cajero.MapGet("/caja/administradores", async (UsuariosServicio s) => (await s.Listar()).Where(u => u.Activo && u.Rol == Rol.Administrador && u.TienePin && !u.DebeCambiarCredenciales).Select(u => new { u.Id, u.Nombre }));
        cajero.MapPost("/caja/abrir", (AbrirCaja d, CajaServicio s) => s.Abrir(d));
        cajero.MapPost("/caja/{id:guid}/depositos", (Guid id, DatosMovimiento d, CajaServicio s) => s.Movimiento(id, d, TipoMovimiento.Deposito));
        cajero.MapPost("/caja/{id:guid}/retiros", (Guid id, DatosMovimiento d, CajaServicio s) => s.Movimiento(id, d, TipoMovimiento.Retiro))
            .AddEndpointFilter(new AutorizacionAdministradorFiltro("retiro_efectivo"));
        cajero.MapPost("/caja/{id:guid}/cerrar", (Guid id, CerrarCaja d, CajaServicio s) => s.Cerrar(id, d, false));
        cajero.MapPost("/ventas/cotizar", (CotizarVenta d, VentasServicio s) => s.Cotizar(d));
        cajero.MapPost("/ventas", (CobrarVenta d, VentasServicio s) => s.Cobrar(d));
        cajero.MapPost("/ventas/{id:guid}/anular", async (Guid id, AnularVenta d, VentasServicio s) =>
        {
            var venta = await s.Anular(id, d);
            return new { venta, aviso = venta.Pagos.Any(p => p.Metodo != MetodoPago.Efectivo) ? "Recordá reversar el cobro en el datáfono o devolver el SINPE" : null };
        });
        var lectura = app.MapGroup("/api").RequireAuthorization();
        lectura.MapGet("/caja/{id:guid}/reporte", (Guid id, CajaServicio s) => s.Reporte(id));
        lectura.MapGet("/caja/{id:guid}/ventas", (Guid id, string? numero, VentasServicio s) => s.Listar(id, numero));
        lectura.MapGet("/ventas/{id:guid}", (Guid id, VentasServicio s) => s.Obtener(id));
        lectura.MapGet("/ventas/{id:guid}/logo", async (Guid id, VentasServicio s, IConfiguration c) => Archivo(await s.Logo(id), new LogoLocal(c).Carpeta));
        lectura.MapGet("/productos/{id:guid}/foto", async (Guid id, CatalogoServicio s, IConfiguration c) => Archivo(await s.RutaFoto(id), new FotosProductos(c).Carpeta));
    }
    private static IResult Archivo(string? nombre, string carpeta)
    {
        if (nombre == null) return Results.NotFound();
        var ruta = Path.Combine(carpeta, Path.GetFileName(nombre));
        return File.Exists(ruta) ? Results.File(ruta, nombre.EndsWith(".png", StringComparison.Ordinal) ? "image/png" : "image/jpeg") : Results.NotFound();
    }
    private static async Task<byte[]> LeerImagen(HttpRequest request)
    {
        if (request.ContentLength > 1048576) throw new ErrorAplicacion(413, "La foto debe pesar como máximo 1 MB.");
        using var m = new MemoryStream(); var buffer = new byte[8192]; int n;
        while ((n = await request.Body.ReadAsync(buffer)) > 0)
        { if (m.Length + n > 1048576) throw new ErrorAplicacion(413, "La foto debe pesar como máximo 1 MB."); m.Write(buffer, 0, n); }
        return m.ToArray();
    }
}
