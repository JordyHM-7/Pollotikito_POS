using System.Net.Mail;
using Pollotikito.Domain;

namespace Pollotikito.Application;

public static class Validacion
{
    public static void Exigir(bool condicion, string mensaje, int estado = 400)
    {
        if (!condicion) throw new ErrorAplicacion(estado, mensaje);
    }
    public static string Texto(string? valor, string campo, int max, bool requerido = false)
    {
        var texto = valor?.Trim() ?? "";
        Exigir(texto.Length <= max && (!requerido || texto.Length > 0), $"{campo}: longitud inválida (máximo {max}).");
        return texto;
    }
    public static void Pin(string? pin) => Exigir(pin is { Length: >= 4 and <= 6 } && pin.All(c => c is >= '0' and <= '9'), "El PIN debe tener de 4 a 6 dígitos.");
    public static void Password(string? password) => Exigir(password is { Length: >= 10 and <= 128 }, "La contraseña debe tener de 10 a 128 caracteres.");
    public static void Rol(Rol rol) => Exigir(Enum.IsDefined(rol), "Rol inválido.");
    public static void Tipo(TipoDocumento tipo) => Exigir(Enum.IsDefined(tipo), "Tipo de documento inválido.");
    public static void Configuracion(EditarConfiguracion c)
    {
        Texto(c.NombreComercial, "Nombre comercial", 150, true);
        Texto(c.RazonSocial, "Razón social", 200); Texto(c.Cedula, "Cédula", 30);
        Texto(c.Telefono, "Teléfono", 30); Texto(c.Correo, "Correo", 254);
        Texto(c.Direccion, "Dirección", 1000); Texto(c.PieTiquete, "Pie de tiquete", 1000);
        Exigir(string.IsNullOrEmpty(c.Correo) || MailAddress.TryCreate(c.Correo, out _), "Correo inválido.");
        Exigir(c.IvaPorDefecto >= 0 && c.IvaPorDefecto <= 100 && decimal.Round(c.IvaPorDefecto, 2) == c.IvaPorDefecto, "IVA inválido.");
    }
    public static string Logo(byte[] contenido)
    {
        Exigir(contenido.Length is > 0 and <= 1048576, "El logo debe pesar como máximo 1 MB.");
        if (contenido.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return ".png";
        if (contenido.AsSpan().StartsWith(new byte[] { 255, 216, 255 }) && contenido.AsSpan().EndsWith(new byte[] { 255, 217 })) return ".jpg";
        throw new ErrorAplicacion(400, "El logo debe ser PNG o JPG.");
    }
}
