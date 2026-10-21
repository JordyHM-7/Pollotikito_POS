using Pollotikito.Domain;

namespace Pollotikito.Application;

public sealed class ErrorAplicacion(int estado, string mensaje) : Exception(mensaje)
{
    public int Estado { get; } = estado;
}

public interface IContextoActual
{
    Guid SucursalId { get; }
    Guid? UsuarioId { get; }
    string? Ip { get; }
}

public interface ISecretos
{
    string Hash(Usuario usuario, string secreto);
    bool Verificar(Usuario usuario, string hash, string secreto);
}

public interface IAlmacen
{
    Task<T> Transaccion<T>(Func<Task<T>> operacion);
    Task<List<Usuario>> Usuarios();
    Task<Usuario?> Usuario(Guid id);
    Task<Usuario?> PorNombre(string normalizado);
    Task<ConfiguracionNegocio> Configuracion();
    Task<List<Consecutivo>> Consecutivos();
    Task<Consecutivo> Consecutivo(TipoDocumento tipo);
    void Agregar(Entidad entidad);
    Task Guardar();
    Task<bool> Consumir(string tokenHash, Guid solicitanteId, string accion, DateTimeOffset ahora);
    Task<NumeroDocumento> Siguiente(TipoDocumento tipo);
}

public interface ILogoLocal
{
    Task<string> Guardar(byte[] contenido, string extension);
}

public interface IConsecutivos
{
    Task<NumeroDocumento> Siguiente(TipoDocumento tipo);
}

public sealed record NumeroDocumento(string Prefijo, long Numero);
public sealed record UsuarioDto(Guid Id, string Nombre, string NombreUsuario, Rol Rol, bool Activo,
    bool TienePin, bool DebeCambiarCredenciales, Guid VersionSeguridad)
{
    public static UsuarioDto De(Usuario u) => new(u.Id, u.Nombre, u.NombreUsuario, u.Rol, u.Activo,
        u.PinHash != null, u.DebeCambiarCredenciales, u.VersionSeguridad);
}
public sealed record CrearUsuario(string Nombre, string NombreUsuario, Rol Rol, string? Password, string? Pin);
public sealed record EditarUsuario(string Nombre, string NombreUsuario, Rol Rol);
public sealed record Credenciales(string? Password, string? Pin, bool QuitarPin = false);
public sealed record CambiarCredenciales(string PasswordActual, string PasswordNueva);
public sealed record LoginPin(Guid UsuarioId, string Pin);
public sealed record LoginAdmin(string NombreUsuario, string Password);
public sealed record PedirAutorizacion(Guid AdministradorId, string Pin, string Accion);
public sealed record AutorizacionDto(string Token, DateTimeOffset VenceEn);
public sealed record EditarConsecutivo(string Prefijo, long SiguienteNumero);
public sealed record EditarConfiguracion(string NombreComercial, string RazonSocial, string Cedula,
    string Telefono, string Correo, string Direccion, string PieTiquete, decimal IvaPorDefecto);
