using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pollotikito.Domain;

namespace Pollotikito.Application;

public sealed class VentasServicio(IComercio db, IAlmacen administracion, IConsecutivos consecutivos,
    CajaServicio caja, CatalogoServicio catalogo, IContextoActual contexto, TimeProvider reloj)
{
    public async Task<VentaDto> Cotizar(CotizarVenta datos)
    {
        Validacion.Exigir(Enum.IsDefined(datos.TipoOrden), "Tipo de orden inválido.");
        var disponibles = await catalogo.Caja();
        var productos = disponibles.Productos.ToDictionary(p => p.Id);
        var v = new Venta { TipoOrden = datos.TipoOrden, Lineas = CalculoVenta.Lineas(datos.Lineas, productos, datos.Descuento, reloj.GetUtcNow(),disponibles.Menu) };
        Totales(v); return VentaDto.De(v);
    }
    public Task<VentaDto> Cobrar(CobrarVenta datos) => db.Transaccion(async () =>
    {
        Validacion.Exigir(datos.Id.Version == 7, "El identificador de venta debe ser UUID v7.");
        Validacion.Exigir(Enum.IsDefined(datos.TipoOrden), "Tipo de orden inválido.");
        Validacion.Exigir(datos.Lineas is { Count: > 0 and <= 200 } && datos.Lineas.All(x => x != null) && datos.Pagos is { Count: > 0 and <= 3 } && datos.Pagos.All(x => x != null), "Líneas o pagos inválidos.");
        var huella = Huella(datos);
        var previa = await db.Venta(datos.Id);
        if (previa != null) return await Reintento(previa, huella);
        var sesion = await caja.PropiaAbierta(datos.SesionCajaId);
        previa = await db.Venta(datos.Id);
        if (previa != null) return await Reintento(previa, huella);
        var disponibles = await catalogo.Caja();var productos = disponibles.Productos.ToDictionary(p => p.Id);
        var ahora = reloj.GetUtcNow(); var negocio = await administracion.Configuracion();
        var v = new Venta { Id = datos.Id, SucursalId = contexto.SucursalId, SesionCajaId = sesion.Id, CajeroId = sesion.CajeroId,
            NombreCajero = sesion.NombreCajero, SolicitudHash = huella, Fecha = ahora, TipoOrden = datos.TipoOrden,
            TipoDescuentoGlobal = datos.Descuento?.Tipo ?? TipoDescuento.Ninguno, ValorDescuentoGlobal = datos.Descuento?.Valor ?? 0,
            MotivoDescuentoGlobal = datos.Descuento?.Motivo?.Trim(), NegocioNombre = negocio.NombreComercial, NegocioRazonSocial = negocio.RazonSocial,
            NegocioCedula = negocio.Cedula, NegocioTelefono = negocio.Telefono, NegocioCorreo = negocio.Correo,
            NegocioDireccion = negocio.Direccion, NegocioLogo = negocio.Logo, PieTiquete = negocio.PieTiquete,
            Lineas = CalculoVenta.Lineas(datos.Lineas, productos, datos.Descuento, ahora,disponibles.Menu) };
        Totales(v); v.Pagos = CalculoVenta.Pagos(datos.Pagos, v.Total); v.Vuelto = v.Pagos.Sum(p => p.Vuelto);
        var numero = await consecutivos.Siguiente(TipoDocumento.TIQUETE); v.Prefijo = numero.Prefijo; v.Numero = numero.Numero;
        foreach (var l in v.Lineas) { l.SucursalId = contexto.SucursalId; l.VentaId = v.Id;
            foreach(var m in l.Modificadores)m.SucursalId=contexto.SucursalId;
            foreach(var c in l.Componentes)c.SucursalId=contexto.SucursalId;
        }
        foreach (var p in v.Pagos) { p.SucursalId = contexto.SucursalId; p.VentaId = v.Id; }
        db.Agregar(v);
        if (v.Lineas.Any(l => l.DescuentoManual > 0 || l.DescuentoGlobal > 0))
            caja.Auditar("DescuentoManual", nameof(Venta), v.Id, new { Total = v.Subtotal - v.Lineas.Sum(l => l.DescuentoAutomatico) },
                new { v.Total, v.MotivoDescuentoGlobal, Lineas = v.Lineas.Select(l => new { l.ProductoId, l.DescuentoManual, l.DescuentoGlobal, l.MotivoDescuento }) });
        await db.Guardar(); return VentaDto.De(v);
    });
    private async Task<VentaDto> Reintento(Venta venta, string huella)
    {
        // No se revelan ventas de otro usuario aunque conozca el UUID.
        Validacion.Exigir(venta.CajeroId == contexto.UsuarioId && venta.SolicitudHash == huella, "El identificador ya se usó con otro contenido o usuario.", 409);
        await Task.CompletedTask; return VentaDto.De(venta);
    }
    public async Task<VentaDto> Obtener(Guid id)
    {
        var v = await Requerida(id); var s = await db.Sesion(v.SesionCajaId) ?? throw new ErrorAplicacion(404, "Sesión no encontrada.");
        await caja.Acceso(s); return VentaDto.De(v);
    }
    public async Task<string?> Logo(Guid id) { await Obtener(id); return (await Requerida(id)).NegocioLogo; }
    public async Task<List<VentaDto>> Listar(Guid sesionId, string? numero)
    {
        var s = await db.Sesion(sesionId) ?? throw new ErrorAplicacion(404, "Sesión no encontrada."); await caja.Acceso(s);
        var ventas = await db.Ventas(sesionId);
        return ventas.Where(v => string.IsNullOrWhiteSpace(numero) || (v.Prefijo + v.Numero.ToString(CultureInfo.InvariantCulture)).Contains(numero, StringComparison.OrdinalIgnoreCase))
            .Select(VentaDto.De).ToList();
    }
    public Task<VentaDto> Anular(Guid id, AnularVenta datos) => db.Transaccion(async () =>
    {
        var anterior = await Requerida(id);
        await caja.PropiaAbierta(anterior.SesionCajaId);
        var v = await db.Venta(id, true) ?? throw new ErrorAplicacion(404, "Venta no encontrada.");
        Validacion.Exigir(v.Estado == EstadoVenta.Pagada, "La venta ya está anulada.", 409);
        v.MotivoAnulacion = Validacion.Texto(datos.Motivo, "Motivo de anulación", 500, true);
        v.Estado = EstadoVenta.Anulada; v.AnuladaEn = reloj.GetUtcNow(); v.AnuladaPorId = contexto.UsuarioId;
        caja.Auditar("AnularVenta", nameof(Venta), v.Id, new { Estado = EstadoVenta.Pagada, v.Total },
            new { v.Estado, v.Total, v.MotivoAnulacion, v.AnuladaPorId, v.AnuladaEn, Metodos = v.Pagos.Select(p => p.Metodo) });
        await db.Guardar(); return VentaDto.De(v);
    });
    private async Task<Venta> Requerida(Guid id) => await db.Venta(id) ?? throw new ErrorAplicacion(404, "Venta no encontrada.");
    private static void Totales(Venta v)
    {
        v.Subtotal = v.Lineas.Sum(l => l.PrecioUnitario * l.Cantidad);
        v.Descuento = v.Lineas.Sum(l => l.DescuentoAutomatico + l.DescuentoManual + l.DescuentoGlobal);
        v.Base = v.Lineas.Sum(l => l.Base); v.Impuesto = v.Lineas.Sum(l => l.Impuesto); v.Total = v.Lineas.Sum(l => l.Total);
        CalculoVenta.Dinero(v.Subtotal, "Subtotal"); CalculoVenta.Dinero(v.Total, "Total");
    }
    private static string Huella(CobrarVenta d)
    {
        Validacion.Exigir(d.Lineas.All(l => (l.Modificadores?.Count ?? 0) <= 100 && (l.Componentes?.Count ?? 0) <= 500 && (l.Componentes?.All(c => c != null) ?? true)), "Elecciones inválidas.");
        static string N(decimal n) => n.ToString("G29", CultureInfo.InvariantCulture);
        static object D(DescuentoSolicitud? x) => new { Tipo = x?.Tipo ?? TipoDescuento.Ninguno, Valor = N(x?.Valor ?? 0), Motivo = x?.Motivo ?? "" };
        var canonico = new { d.SesionCajaId, d.TipoOrden, Lineas = d.Lineas.Select(l => new { l.ProductoId, l.Cantidad, Nota = l.Nota ?? "", Descuento = D(l.Descuento) }),
            Pagos = d.Pagos.Select(p => new { p.Metodo, Monto = N(p.Monto), Recibido = p.Recibido.HasValue ? N(p.Recibido.Value) : null, Referencia = p.Referencia ?? "" }), Descuento = D(d.Descuento) };
        // Conservar la huella histórica para solicitudes del Incremento 2 sin elecciones.
        var serializado = d.Lineas.All(l=>(l.Modificadores?.Count??0)==0&&(l.Componentes?.Count??0)==0) ? JsonSerializer.Serialize(canonico) :
            JsonSerializer.Serialize(new { Solicitud=canonico, Elecciones=d.Lineas.Select(l=>new { Modificadores=(l.Modificadores??[]).Order().ToArray(),
                Componentes=(l.Componentes??[]).OrderBy(c=>c.ComponenteId).ThenBy(c=>c.ProductoId).Select(c=>new{c.ComponenteId,c.ProductoId,c.Cantidad}) }) });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serializado)));
    }
}
