using Pollotikito.Domain;

namespace Pollotikito.Application;

public interface IComercio
{
    Task<T> Transaccion<T>(Func<Task<T>> operacion);
    Task<List<Categoria>> Categorias();
    Task<Categoria?> Categoria(Guid id);
    Task<List<Producto>> Productos();
    Task<Producto?> Producto(Guid id);
    Task<SesionCaja?> Sesion(Guid id, bool bloquear = false);
    Task<SesionCaja?> Abierta();
    Task<List<SesionCaja>> Sesiones();
    Task<Venta?> Venta(Guid id, bool seguir = false);
    Task<List<Venta>> Ventas(Guid sesionId);
    Task<List<MovimientoCaja>> Movimientos(Guid sesionId);
    void Agregar(Entidad entidad);
    Task Guardar();
}

public interface IFotosProductos
{
    Task<string> Guardar(byte[] contenido, string extension);
}

public sealed record DatosCategoria(string Nombre, int Orden, bool Activo = true);
public sealed record DatosProducto(Guid CategoriaId, string Nombre, decimal Precio, decimal? TarifaIva, int Orden,
    bool Activo = true, TipoDescuento TipoDescuento = TipoDescuento.Ninguno, decimal ValorDescuento = 0,
    DateTimeOffset? DescuentoDesde = null, DateTimeOffset? DescuentoHasta = null);
public sealed record CatalogoDto(List<Categoria> Categorias, List<Producto> Productos);
public sealed record DescuentoSolicitud(TipoDescuento Tipo = TipoDescuento.Ninguno, decimal Valor = 0, string? Motivo = null);
public sealed record LineaSolicitud(Guid ProductoId, int Cantidad, string? Nota = null, DescuentoSolicitud? Descuento = null);
public sealed record PagoSolicitud(MetodoPago Metodo, decimal Monto, decimal? Recibido = null, string? Referencia = null);
public sealed record CobrarVenta(Guid Id, Guid SesionCajaId, TipoOrden TipoOrden, List<LineaSolicitud> Lineas,
    List<PagoSolicitud> Pagos, DescuentoSolicitud? Descuento = null);
public sealed record CotizarVenta(TipoOrden TipoOrden, List<LineaSolicitud> Lineas, DescuentoSolicitud? Descuento = null);
public sealed record AbrirCaja(decimal MontoInicial);
public sealed record DatosMovimiento(decimal Monto, string Motivo);
public sealed record DatosConteo(int Denominacion, int Cantidad);
public sealed record CerrarCaja(List<DatosConteo>? Conteo, string? Motivo = null);
public sealed record AnularVenta(string Motivo);
public sealed record ResumenMetodo(MetodoPago Metodo, int Cantidad, decimal Monto);
public sealed record ReporteCaja(SesionCaja Sesion, List<ResumenMetodo> VentasPorMetodo, List<ResumenMetodo> AnulacionesPorMetodo,
    int CantidadVentas, decimal TotalVentas, int CantidadAnulaciones, decimal TotalAnulaciones, decimal TotalDescuentos,
    decimal Depositos, decimal Retiros, decimal Esperado, decimal? Contado, decimal? Diferencia, List<MovimientoCaja> Movimientos);

// DTO explícito: la huella de idempotencia queda dentro de la persistencia.
public sealed record VentaDto(Guid Id, Guid SesionCajaId, string NombreCajero, string Prefijo, long Numero,
    TipoOrden TipoOrden, EstadoVenta Estado, DateTimeOffset Fecha, decimal Subtotal, decimal Descuento,
    decimal Base, decimal Impuesto, decimal Total, decimal Vuelto, string? MotivoDescuentoGlobal,
    Guid? AnuladaPorId, DateTimeOffset? AnuladaEn, string? MotivoAnulacion,
    string NegocioNombre, string NegocioRazonSocial, string NegocioCedula, string NegocioTelefono,
    string NegocioCorreo, string NegocioDireccion, bool TieneLogo, string PieTiquete, List<LineaVenta> Lineas, List<PagoVenta> Pagos)
{
    public static VentaDto De(Venta v) => new(v.Id, v.SesionCajaId, v.NombreCajero, v.Prefijo, v.Numero, v.TipoOrden, v.Estado,
        v.Fecha, v.Subtotal, v.Descuento, v.Base, v.Impuesto, v.Total, v.Vuelto, v.MotivoDescuentoGlobal,
        v.AnuladaPorId, v.AnuladaEn, v.MotivoAnulacion, v.NegocioNombre, v.NegocioRazonSocial, v.NegocioCedula,
        v.NegocioTelefono, v.NegocioCorreo, v.NegocioDireccion, v.NegocioLogo != null, v.PieTiquete,
        v.Lineas.OrderBy(x => x.Orden).ToList(), v.Pagos);
}
