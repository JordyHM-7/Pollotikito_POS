using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Pollotikito.Application;

namespace Pollotikito.Api;
public sealed class ContextoActual(IHttpContextAccessor http, IConfiguration config) : IContextoActual
{
    public Guid SucursalId => Guid.Parse(config["SucursalId"] ?? throw new InvalidOperationException("Falta SucursalId."));
    public Guid? UsuarioId => Guid.TryParse(http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public string? Ip => http.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
public static class Sesion
{
    public static Task Crear(HttpContext http, UsuarioDto u) => http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new Claim(ClaimTypes.Name, u.Nombre), new Claim(ClaimTypes.Role, u.Rol.ToString()),
            new Claim("version", u.VersionSeguridad.ToString()), new Claim("cambiar", u.DebeCambiarCredenciales ? "true" : "false") }, CookieAuthenticationDefaults.AuthenticationScheme)),
        new AuthenticationProperties { IsPersistent = false });
}
/// <summary>Filtro para endpoints autenticados: consume el token antes de ejecutar la acción.</summary>
public sealed class AutorizacionAdministradorFiltro(string accion) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        await context.HttpContext.RequestServices.GetRequiredService<SeguridadServicio>()
            .ExigirAutorizacion(context.HttpContext.Request.Headers["X-Autorizacion-Administrador"].FirstOrDefault(), accion);
        return await next(context);
    }
}
