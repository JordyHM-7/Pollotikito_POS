using Pollotikito.Domain;

namespace Pollotikito.Application;

public sealed class CajaServicio(IComercio db, IAlmacen usuarios, IContextoActual contexto, TimeProvider reloj)
{
    private async Task<Usuario> Actor() => await usuarios.Usuario(contexto.UsuarioId!.Value) ?? throw new ErrorAplicacion(401, "Usuario no encontrado.");
    public async Task<SesionCaja?> Actual()
    {
        var s = await db.Abierta(); return s?.CajeroId == contexto.UsuarioId ? s : null;
    }
    public async Task<List<SesionCaja>> Sesiones()
    {
        Validacion.Exigir((await Actor()).Rol == Rol.Administrador, "Acceso denegado.", 403);
        return await db.Sesiones();
    }
    public Task<SesionCaja> Abrir(AbrirCaja datos) => db.Transaccion(async () =>
    {
        var u = await Actor(); Validacion.Exigir(u.Rol == Rol.Cajero, "Solo un cajero puede abrir caja.", 403);
        CalculoVenta.Dinero(datos.MontoInicial, "Monto inicial");
        Validacion.Exigir(await db.Abierta() == null, "Ya hay una caja abierta en la sucursal.", 409);
        var s = new SesionCaja { SucursalId = contexto.SucursalId, CajeroId = u.Id, NombreCajero = u.Nombre,
            AbiertaEn = reloj.GetUtcNow(), MontoInicial = datos.MontoInicial };
        db.Agregar(s); Auditar("AbrirCaja", nameof(SesionCaja), s.Id, null, new { s.MontoInicial, s.CajeroId });
        await db.Guardar(); return s;
    });
    public async Task<SesionCaja> PropiaAbierta(Guid id)
    {
        var s = await db.Sesion(id, true) ?? throw new ErrorAplicacion(404, "Sesión no encontrada.");
        Validacion.Exigir(s.CajeroId == contexto.UsuarioId, "La caja pertenece a otro cajero.", 403);
        Validacion.Exigir(s.CerradaEn == null, "La sesión de caja está cerrada.", 409); return s;
    }
    public Task<MovimientoCaja> Movimiento(Guid id, DatosMovimiento datos, TipoMovimiento tipo) => db.Transaccion(async () =>
    {
        await PropiaAbierta(id); CalculoVenta.Dinero(datos.Monto, "Movimiento", true);
        var m = new MovimientoCaja { SucursalId = contexto.SucursalId, SesionCajaId = id, UsuarioId = contexto.UsuarioId!.Value,
            Tipo = tipo, Monto = datos.Monto, Motivo = Validacion.Texto(datos.Motivo, "Motivo", 500, true), Fecha = reloj.GetUtcNow() };
        db.Agregar(m); Auditar(tipo.ToString(), nameof(MovimientoCaja), m.Id, null, new { m.SesionCajaId, m.Monto, m.Motivo });
        await db.Guardar(); return m;
    });
    public Task<ReporteCaja> Reporte(Guid id) => db.Transaccion(async () =>
    {
        var s = await db.Sesion(id, true) ?? throw new ErrorAplicacion(404, "Sesión no encontrada.");
        await Acceso(s); return await ConstruirReporte(s);
    });
    public async Task Acceso(SesionCaja s)
    {
        var actor = await Actor();
        Validacion.Exigir(actor.Rol == Rol.Administrador || s.CajeroId == actor.Id, "Acceso denegado.", 403);
    }
    public Task<ReporteCaja> Cerrar(Guid id, CerrarCaja datos, bool forzado) => db.Transaccion(async () =>
    {
        var s = await db.Sesion(id, true) ?? throw new ErrorAplicacion(404, "Sesión no encontrada.");
        Validacion.Exigir(s.CerradaEn == null, "La sesión ya está cerrada.", 409);
        if (forzado)
        {
            Validacion.Exigir((await Actor()).Rol == Rol.Administrador, "Acceso denegado.", 403);
            s.MotivoCierreForzado = Validacion.Texto(datos.Motivo, "Motivo de cierre forzado", 500, true);
        }
        else { Validacion.Exigir(s.CajeroId == contexto.UsuarioId, "La caja pertenece a otro cajero.", 403); Validacion.Exigir(datos.Conteo != null, "El cajero debe ingresar el conteo."); }
        decimal? contado = null;
        if (datos.Conteo != null)
        {
            Validacion.Exigir(datos.Conteo.Count <= CalculoVenta.Denominaciones.Length && datos.Conteo.All(x => x != null) && datos.Conteo.Select(x => x.Denominacion).Distinct().Count() == datos.Conteo.Count, "Conteo con denominaciones repetidas o inválidas.");
            foreach (var d in datos.Conteo)
            {
                Validacion.Exigir(CalculoVenta.Denominaciones.Contains(d.Denominacion) && d.Cantidad is >= 0 and <= 1000000, "Denominación o cantidad inválida.");
                var conteo = new ConteoDenominacion { SucursalId = contexto.SucursalId, SesionCajaId = s.Id, Denominacion = d.Denominacion, Cantidad = d.Cantidad };
                db.Agregar(conteo); s.Conteo.Add(conteo);
            }
            contado = datos.Conteo.Sum(x => (decimal)x.Denominacion * x.Cantidad); CalculoVenta.Dinero(contado.Value, "Contado");
        }
        // Persistir el conteo mientras la sesión sigue abierta, dentro de la misma transacción.
        await db.Guardar();
        var reporte = await ConstruirReporte(s);
        s.Contado = contado; s.CerradaSinConteo = datos.Conteo == null;
        s.Esperado = reporte.Esperado; s.Diferencia = s.Contado - s.Esperado; s.CerradaEn = reloj.GetUtcNow(); s.CerradaPorId = contexto.UsuarioId;
        Auditar("CerrarCaja", nameof(SesionCaja), s.Id, new { Estado = "Abierta" }, new { s.CerradaEn, s.CerradaPorId, s.MotivoCierreForzado, s.CerradaSinConteo, s.Esperado, s.Contado, s.Diferencia });
        await db.Guardar(); return reporte with { Contado = s.Contado, Diferencia = s.Diferencia };
    });
    private async Task<ReporteCaja> ConstruirReporte(SesionCaja s)
    {
        var ventas = await db.Ventas(s.Id); var movimientos = await db.Movimientos(s.Id);
        var vigentes = ventas.Where(v => v.Estado == EstadoVenta.Pagada).ToList();
        var anuladas = ventas.Where(v => v.Estado == EstadoVenta.Anulada).ToList();
        static List<ResumenMetodo> Resumen(List<Venta> conjunto) => Enum.GetValues<MetodoPago>().Select(m =>
            new ResumenMetodo(m, conjunto.Count(v => v.Pagos.Any(p => p.Metodo == m)), conjunto.Sum(v => v.Pagos.Where(p => p.Metodo == m).Sum(p => p.Monto)))).ToList();
        var depositos = movimientos.Where(m => m.Tipo == TipoMovimiento.Deposito).Sum(m => m.Monto);
        var retiros = movimientos.Where(m => m.Tipo == TipoMovimiento.Retiro).Sum(m => m.Monto);
        var esperado = s.MontoInicial + vigentes.Sum(v => v.Pagos.Where(p => p.Metodo == MetodoPago.Efectivo).Sum(p => p.Recibido!.Value - p.Vuelto)) + depositos - retiros;
        return new(s, Resumen(vigentes), Resumen(anuladas), vigentes.Count, vigentes.Sum(v => v.Total), anuladas.Count, anuladas.Sum(v => v.Total),
            vigentes.Sum(v => v.Descuento), depositos, retiros, s.Esperado ?? esperado, s.Contado, s.Diferencia, movimientos,ventas.Select(VentaDto.De).ToList());
    }
    public void Auditar(string accion, string entidad, Guid id, object? anterior, object? nuevo) => db.Agregar(new Bitacora {
        SucursalId = contexto.SucursalId, UsuarioId = contexto.UsuarioId, Ip = contexto.Ip, FechaUtc = reloj.GetUtcNow(), Accion = accion,
        NombreEntidad = entidad, EntidadId = id, ValorAnterior = anterior == null ? null : System.Text.Json.JsonSerializer.Serialize(anterior),
        ValorNuevo = nuevo == null ? null : System.Text.Json.JsonSerializer.Serialize(nuevo) });
}
