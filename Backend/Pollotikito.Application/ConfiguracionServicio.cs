using Pollotikito.Domain;

namespace Pollotikito.Application;

public sealed class ConfiguracionServicio(IAlmacen db, ILogoLocal logos)
{
    public Task<ConfiguracionNegocio> Obtener() => db.Configuracion();
    public Task<List<Consecutivo>> Consecutivos() => db.Consecutivos();
    public Task<ConfiguracionNegocio> Editar(EditarConfiguracion c) => db.Transaccion(async () =>
    {
        Validacion.Configuracion(c);
        var e = await db.Configuracion();
        e.NombreComercial = c.NombreComercial.Trim(); e.RazonSocial = c.RazonSocial.Trim(); e.Cedula = c.Cedula.Trim();
        e.Telefono = c.Telefono.Trim(); e.Correo = c.Correo.Trim(); e.Direccion = c.Direccion.Trim();
        e.PieTiquete = c.PieTiquete.Trim(); e.IvaPorDefecto = c.IvaPorDefecto;
        await db.Guardar(); return e;
    });
    public Task<Consecutivo> EditarConsecutivo(TipoDocumento tipo, EditarConsecutivo c) => db.Transaccion(async () =>
    {
        Validacion.Tipo(tipo);
        var e = await db.Consecutivo(tipo);
        Validacion.Exigir(c.SiguienteNumero >= e.SiguienteNumero && c.SiguienteNumero < long.MaxValue, "El siguiente número solo puede mantenerse o subir y debe dejar espacio para emitir.");
        e.Prefijo = Validacion.Texto(c.Prefijo, "Prefijo", 30); e.SiguienteNumero = c.SiguienteNumero;
        await db.Guardar(); return e;
    });
    public Task<string> Logo(byte[] contenido) => db.Transaccion(async () =>
    {
        var extension = Validacion.Logo(contenido);
        var ruta = await logos.Guardar(contenido, extension);
        var e = await db.Configuracion(); e.Logo = ruta; await db.Guardar(); return ruta;
    });
}
