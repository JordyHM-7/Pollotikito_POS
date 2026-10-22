namespace Pollotikito.Domain;

public enum TipoDescuento { Ninguno, Porcentaje, Monto }
public enum TipoOrden { ComerAqui, ParaLlevar, Express }
public enum MetodoPago { Efectivo, Tarjeta, Sinpe }
public enum EstadoVenta { Pagada, Anulada }
public enum TipoMovimiento { Deposito, Retiro }

public sealed class Categoria : Entidad
{
    public string Nombre { get; set; } = "";
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}

public sealed class Producto : Entidad
{
    public bool EsCombo { get; set; }
    public bool Disponible { get; set; } = true;
    public List<string> Advertencias { get; set; } = [];
    public Guid CategoriaId { get; set; }
    public string Nombre { get; set; } = "";
    public decimal Precio { get; set; }
    public decimal TarifaIva { get; set; } = 13m;
    public string? Foto { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public TipoDescuento TipoDescuento { get; set; }
    public decimal ValorDescuento { get; set; }
    public DateTimeOffset? DescuentoDesde { get; set; }
    public DateTimeOffset? DescuentoHasta { get; set; }
}

public sealed class SesionCaja : Entidad
{
    public Guid CajeroId { get; set; }
    public string NombreCajero { get; set; } = "";
    public DateTimeOffset AbiertaEn { get; set; }
    public decimal MontoInicial { get; set; }
    public DateTimeOffset? CerradaEn { get; set; }
    public Guid? CerradaPorId { get; set; }
    public string? MotivoCierreForzado { get; set; }
    public bool CerradaSinConteo { get; set; }
    public decimal? Esperado { get; set; }
    public decimal? Contado { get; set; }
    public decimal? Diferencia { get; set; }
    public List<ConteoDenominacion> Conteo { get; set; } = [];
}

public sealed class ConteoDenominacion : Entidad
{
    public Guid SesionCajaId { get; set; }
    public int Denominacion { get; set; }
    public int Cantidad { get; set; }
}

public sealed class MovimientoCaja : Entidad
{
    public Guid SesionCajaId { get; set; }
    public Guid UsuarioId { get; set; }
    public TipoMovimiento Tipo { get; set; }
    public decimal Monto { get; set; }
    public string Motivo { get; set; } = "";
    public DateTimeOffset Fecha { get; set; }
}

public sealed class Venta : Entidad
{
    public Guid SesionCajaId { get; set; }
    public Guid CajeroId { get; set; }
    public string NombreCajero { get; set; } = "";
    public string SolicitudHash { get; set; } = "";
    public string Prefijo { get; set; } = "";
    public long Numero { get; set; }
    public TipoOrden TipoOrden { get; set; }
    public EstadoVenta Estado { get; set; } = EstadoVenta.Pagada;
    public DateTimeOffset Fecha { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal Base { get; set; }
    public decimal Impuesto { get; set; }
    public decimal Total { get; set; }
    public decimal Vuelto { get; set; }
    public TipoDescuento TipoDescuentoGlobal { get; set; }
    public decimal ValorDescuentoGlobal { get; set; }
    public string? MotivoDescuentoGlobal { get; set; }
    public Guid? AnuladaPorId { get; set; }
    public DateTimeOffset? AnuladaEn { get; set; }
    public string? MotivoAnulacion { get; set; }
    // Instantánea del negocio para que una reimpresión conserve el comprobante original.
    public string NegocioNombre { get; set; } = "";
    public string NegocioRazonSocial { get; set; } = "";
    public string NegocioCedula { get; set; } = "";
    public string NegocioTelefono { get; set; } = "";
    public string NegocioCorreo { get; set; } = "";
    public string NegocioDireccion { get; set; } = "";
    public string? NegocioLogo { get; set; }
    public string PieTiquete { get; set; } = "";
    public List<LineaVenta> Lineas { get; set; } = [];
    public List<PagoVenta> Pagos { get; set; } = [];
}

public sealed class LineaVenta : Entidad
{
    public List<ModificadorLineaVenta> Modificadores { get; set; } = [];
    public List<ComponenteLineaVenta> Componentes { get; set; } = [];
    public Guid VentaId { get; set; }
    public Guid ProductoId { get; set; }
    public int Orden { get; set; }
    public string Nombre { get; set; } = "";
    public int Cantidad { get; set; }
    public string Nota { get; set; } = "";
    public decimal PrecioUnitario { get; set; }
    public decimal TarifaIva { get; set; }
    public decimal DescuentoAutomatico { get; set; }
    public decimal DescuentoManual { get; set; }
    public TipoDescuento TipoDescuentoManual { get; set; }
    public decimal ValorDescuentoManual { get; set; }
    public string? MotivoDescuento { get; set; }
    public decimal DescuentoGlobal { get; set; }
    public decimal Base { get; set; }
    public decimal Impuesto { get; set; }
    public decimal Total { get; set; }
}

public sealed class PagoVenta : Entidad
{
    public Guid VentaId { get; set; }
    public MetodoPago Metodo { get; set; }
    public decimal Monto { get; set; }
    public decimal? Recibido { get; set; }
    public decimal Vuelto { get; set; }
    public string? Referencia { get; set; }
}
