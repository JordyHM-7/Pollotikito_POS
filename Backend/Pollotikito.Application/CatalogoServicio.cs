using Pollotikito.Domain;

namespace Pollotikito.Application;

public sealed class CatalogoServicio(IComercio db, IAlmacen administracion, IFotosProductos fotos, IContextoActual contexto)
{
    public Task<List<Categoria>> Categorias() => db.Categorias();
    public Task<List<Producto>> Productos() => db.Productos();
    public async Task<CatalogoDto> Caja()
    {
        var categorias = (await db.Categorias()).Where(x => x.Activo).ToList();
        var ids = categorias.Select(x => x.Id).ToHashSet();
        return new(categorias, (await db.Productos()).Where(x => x.Activo && ids.Contains(x.CategoriaId)).ToList());
    }
    public Task<Categoria> GuardarCategoria(Guid? id, DatosCategoria d) => administracion.Transaccion(async () =>
    {
        var c = id.HasValue ? await db.Categoria(id.Value) ?? throw new ErrorAplicacion(404, "Categoría no encontrada.") : new Categoria { SucursalId = contexto.SucursalId };
        c.Nombre = Validacion.Texto(d.Nombre, "Nombre", 120, true); c.Orden = d.Orden; c.Activo = d.Activo;
        Validacion.Exigir(d.Orden >= 0, "El orden no puede ser negativo.");
        if (!id.HasValue) db.Agregar(c); await db.Guardar(); return c;
    });
    public Task<bool> DesactivarCategoria(Guid id) => administracion.Transaccion(async () =>
    {
        var c = await db.Categoria(id) ?? throw new ErrorAplicacion(404, "Categoría no encontrada.");
        c.Activo = false; await db.Guardar(); return true;
    });
    public Task<Producto> GuardarProducto(Guid? id, DatosProducto d) => administracion.Transaccion(async () =>
    {
        var p = id.HasValue ? await db.Producto(id.Value) ?? throw new ErrorAplicacion(404, "Producto no encontrado.") : new Producto { SucursalId = contexto.SucursalId };
        Validacion.Exigir(await db.Categoria(d.CategoriaId) != null, "Categoría no encontrada.");
        CalculoVenta.Dinero(d.Precio, "Precio"); CalculoVenta.ValidarDescuento(d.TipoDescuento, d.ValorDescuento);
        var iva = d.TarifaIva ?? (await administracion.Configuracion()).IvaPorDefecto;
        Validacion.Exigir(iva is >= 0 and <= 100 && CalculoVenta.Redondear(iva) == iva, "Tarifa de IVA inválida.");
        Validacion.Exigir(d.Orden >= 0, "El orden no puede ser negativo.");
        Validacion.Exigir(d.DescuentoDesde == null || d.DescuentoHasta == null || d.DescuentoDesde <= d.DescuentoHasta, "La vigencia del descuento es inválida.");
        p.Nombre = Validacion.Texto(d.Nombre, "Nombre", 150, true); p.CategoriaId = d.CategoriaId; p.Precio = d.Precio;
        p.TarifaIva = iva; p.Orden = d.Orden; p.Activo = d.Activo; p.TipoDescuento = d.TipoDescuento;
        p.ValorDescuento = d.ValorDescuento; p.DescuentoDesde = d.DescuentoDesde?.ToUniversalTime(); p.DescuentoHasta = d.DescuentoHasta?.ToUniversalTime();
        if (!id.HasValue) db.Agregar(p); await db.Guardar(); return p;
    });
    public Task<bool> DesactivarProducto(Guid id) => administracion.Transaccion(async () =>
    {
        var p = await db.Producto(id) ?? throw new ErrorAplicacion(404, "Producto no encontrado.");
        p.Activo = false; await db.Guardar(); return true;
    });
    public Task<string> Foto(Guid id, byte[] contenido) => administracion.Transaccion(async () =>
    {
        var p = await db.Producto(id) ?? throw new ErrorAplicacion(404, "Producto no encontrado.");
        var extension = Validacion.Logo(contenido); p.Foto = await fotos.Guardar(contenido, extension);
        await db.Guardar(); return p.Foto;
    });
    public async Task<string?> RutaFoto(Guid id) => (await db.Producto(id) ?? throw new ErrorAplicacion(404, "Producto no encontrado.")).Foto;
}
