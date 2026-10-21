namespace Pollotikito.Domain;

public enum Rol { Administrador, Cajero }
public enum TipoDocumento { TIQUETE, FACTURA, PROFORMA }

public abstract class Entidad
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SucursalId { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public DateTimeOffset ActualizadoEn { get; set; }
}

public sealed class Usuario : Entidad
{
    public string Nombre { get; set; } = "";
    public string NombreUsuario { get; set; } = "";
    public string NombreUsuarioNormalizado { get; set; } = "";
    public Rol Rol { get; set; }
    public bool Activo { get; set; } = true;
    public string? PasswordHash { get; set; }
    public string? PinHash { get; set; }
    public int IntentosFallidos { get; set; }
    public DateTimeOffset? BloqueadoHasta { get; set; }
    public bool DebeCambiarCredenciales { get; set; }
    public Guid VersionSeguridad { get; set; } = Guid.CreateVersion7();
}

public sealed class ConfiguracionNegocio : Entidad
{
    public bool RegistroUnico { get; set; } = true;
    public string NombreComercial { get; set; } = "Pollotikito";
    public string RazonSocial { get; set; } = "";
    public string Cedula { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string Correo { get; set; } = "";
    public string Direccion { get; set; } = "";
    public string? Logo { get; set; }
    public string PieTiquete { get; set; } = "";
    public decimal IvaPorDefecto { get; set; } = 13m;
}

public sealed class Consecutivo : Entidad
{
    public TipoDocumento Tipo { get; set; }
    public string Prefijo { get; set; } = "";
    public long SiguienteNumero { get; set; } = 1;
}

public sealed class Bitacora : Entidad
{
    public Guid? UsuarioId { get; set; }
    public string Accion { get; set; } = "";
    public string NombreEntidad { get; set; } = "";
    public Guid? EntidadId { get; set; }
    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }
    public DateTimeOffset FechaUtc { get; set; }
    public string? Ip { get; set; }
}

public sealed class AutorizacionAdministrador : Entidad
{
    public Guid AdministradorId { get; set; }
    public Guid SolicitanteId { get; set; }
    public string Accion { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTimeOffset VenceEn { get; set; }
    public DateTimeOffset? ConsumidaEn { get; set; }
}
