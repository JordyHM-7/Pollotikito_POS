using System.Security.Cryptography;
using System.Text;
using Pollotikito.Domain;

namespace Pollotikito.Application;

public sealed class SeguridadServicio(IAlmacen db, ISecretos secretos, IContextoActual contexto, TimeProvider reloj)
{
    public async Task<UsuarioDto> Login(LoginPin datos)
    {
        Validacion.Pin(datos.Pin);
        var resultado = await db.Transaccion(async () => await Verificar(await db.Usuario(datos.UsuarioId), datos.Pin, true, false, "LoginPin"));
        return resultado ?? throw new ErrorAplicacion(401, "Credenciales inválidas o cuenta bloqueada.");
    }
    public async Task<UsuarioDto> Login(LoginAdmin datos)
    {
        var nombre = Validacion.Texto(datos.NombreUsuario, "Usuario", 80, true).ToUpperInvariant();
        Validacion.Exigir(datos.Password is { Length: > 0 and <= 128 }, "Contraseña inválida.");
        var resultado = await db.Transaccion(async () => await Verificar(await db.PorNombre(nombre), datos.Password, false, true, "LoginAdmin"));
        return resultado ?? throw new ErrorAplicacion(401, "Credenciales inválidas o cuenta bloqueada.");
    }
    public async Task<AutorizacionDto> Autorizar(PedirAutorizacion datos)
    {
        Validacion.Pin(datos.Pin);
        // Lista explícita: agregar acciones junto con sus endpoints protegidos.
        Validacion.Exigir(datos.Accion is "prueba" or "retiro_efectivo", "Acción no autorizable.");
        var resultado = await db.Transaccion(async () =>
        {
            var admin = await db.Usuario(datos.AdministradorId);
            var valido = await Verificar(admin, datos.Pin, true, true, "AutorizacionAdmin");
            if (valido == null || valido.DebeCambiarCredenciales) return null;
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var vence = reloj.GetUtcNow().AddSeconds(60);
            db.Agregar(new AutorizacionAdministrador { SucursalId = contexto.SucursalId, AdministradorId = valido.Id,
                SolicitanteId = contexto.UsuarioId!.Value, Accion = datos.Accion, TokenHash = HashToken(token), VenceEn = vence });
            await db.Guardar(); return new AutorizacionDto(token, vence);
        });
        return resultado ?? throw new ErrorAplicacion(401, "Credenciales inválidas o cuenta bloqueada.");
    }
    public async Task ExigirAutorizacion(string? token, string accion)
    {
        Validacion.Exigir(token is { Length: 64 }, "Autorización de administrador requerida.", 403);
        Validacion.Exigir(await db.Consumir(HashToken(token!), contexto.UsuarioId!.Value, accion, reloj.GetUtcNow()), "Autorización inválida, usada o vencida.", 403);
    }
    public Task<UsuarioDto?> CambiarPassword(CambiarCredenciales datos) => db.Transaccion<UsuarioDto?>(async () =>
    {
        Validacion.Password(datos.PasswordNueva);
        Validacion.Exigir(datos.PasswordActual is { Length: > 0 and <= 128 }, "Contraseña actual inválida.");
        var u = await db.Usuario(contexto.UsuarioId!.Value) ?? throw new ErrorAplicacion(401, "Sesión inválida.");
        var verificado = await Verificar(u, datos.PasswordActual, false, true, "CambioPassword");
        if (verificado == null) return null;
        Validacion.Exigir(!secretos.Verificar(u, u.PasswordHash!, datos.PasswordNueva), "Usá una contraseña diferente.");
        u.PasswordHash = secretos.Hash(u, datos.PasswordNueva); u.DebeCambiarCredenciales = false;
        u.VersionSeguridad = Guid.CreateVersion7(); await db.Guardar(); return UsuarioDto.De(u);
    });

    private async Task<UsuarioDto?> Verificar(Usuario? u, string secreto, bool pin, bool soloAdmin, string accion)
    {
        var ahora = reloj.GetUtcNow();
        bool valido = false;
        if (u is { Activo: true } && (u.BloqueadoHasta == null || u.BloqueadoHasta <= ahora))
        {
            if (u.BloqueadoHasta != null) { u.IntentosFallidos = 0; u.BloqueadoHasta = null; }
            var hash = pin ? u.PinHash : u.PasswordHash;
            valido = hash != null && secretos.Verificar(u, hash, secreto) && (!soloAdmin || u.Rol == Rol.Administrador);
            if (valido) { u.IntentosFallidos = 0; u.BloqueadoHasta = null; }
            else if (++u.IntentosFallidos >= 5) u.BloqueadoHasta = ahora.AddMinutes(5);
        }
        else
        {
            // Trabajo equivalente aun cuando no existe la cuenta; no se registra el secreto.
            var ficticio = new Usuario(); secretos.Verificar(ficticio, secretos.Hash(ficticio, "verificacion-interna"), secreto);
        }
        db.Agregar(new Bitacora { SucursalId = contexto.SucursalId, UsuarioId = u?.Id, Accion = accion + (valido ? "Exitoso" : "Fallido"),
            NombreEntidad = nameof(Usuario), EntidadId = u?.Id, FechaUtc = ahora, Ip = contexto.Ip });
        await db.Guardar(); return valido ? UsuarioDto.De(u!) : null;
    }
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
