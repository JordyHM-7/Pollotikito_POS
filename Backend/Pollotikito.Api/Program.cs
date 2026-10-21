using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Pollotikito.Application;
using Pollotikito.Domain;
using Pollotikito.Infrastructure;
using Pollotikito.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IContextoActual, ContextoActual>();
builder.Services.AgregarPollotikito(builder.Configuration.GetConnectionString("Pollotikito") ?? throw new InvalidOperationException("Falta ConnectionStrings:Pollotikito."));
builder.Services.AddAntiforgery(o => { o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.Name = "Pollotikito.Csrf"; o.Cookie.SameSite = SameSiteMode.Strict; });
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "Pollotikito.Sesion"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromHours(12); o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
    o.Events.OnValidatePrincipal = async c =>
    {
        var db = c.HttpContext.RequestServices.GetRequiredService<PollotikitoDbContext>();
        var id = Guid.TryParse(c.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var valor) ? valor : Guid.Empty;
        var sucursal = c.HttpContext.RequestServices.GetRequiredService<IContextoActual>().SucursalId;
        var u = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.SucursalId == sucursal);
        if (u == null || !u.Activo || u.VersionSeguridad.ToString() != c.Principal?.FindFirstValue("version"))
        { c.RejectPrincipal(); await c.HttpContext.SignOutAsync(); }
    };
});
builder.Services.AddAuthorization(o => o.AddPolicy("Administrador", p => p.RequireRole(nameof(Rol.Administrador))));
var app = builder.Build();
app.Use(async (http, next) =>
{
    http.Response.Headers.XContentTypeOptions = "nosniff";
    http.Response.Headers.ContentSecurityPolicy = "default-src 'self'; img-src 'self' blob:; style-src 'self'; script-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    if (http.Request.Path.StartsWithSegments("/api")) http.Response.Headers.CacheControl = "no-store";
    try { await next(); }
    catch (ErrorAplicacion e) { await Results.Problem(statusCode: e.Estado, title: e.Message).ExecuteAsync(http); }
    catch (AntiforgeryValidationException) { await Results.Problem(statusCode: 400, title: "Token CSRF inválido. Recargá la página.").ExecuteAsync(http); }
    catch (BadHttpRequestException e) { await Results.Problem(statusCode: e.StatusCode, title: "Solicitud inválida.").ExecuteAsync(http); }
    catch (DbUpdateException) { await Results.Problem(statusCode: 409, title: "Conflicto de datos al guardar el cambio.").ExecuteAsync(http); }
    catch (Exception)
    {
        app.Logger.LogError("Error interno. TraceId: {TraceId}", http.TraceIdentifier);
        await Results.Problem(statusCode: 500, title: "Error interno.", extensions: new Dictionary<string, object?> { ["traceId"] = http.TraceIdentifier }).ExecuteAsync(http);
    }
});
app.UseStatusCodePages(async c => await Results.Problem(statusCode: c.HttpContext.Response.StatusCode).ExecuteAsync(c.HttpContext));
app.UseDefaultFiles(); app.UseStaticFiles();
app.UseAuthentication(); app.UseAuthorization();
app.Use(async (http, next) =>
{
    if (http.Request.Path.StartsWithSegments("/api"))
    {
        if (http.User.Identity?.IsAuthenticated == true && http.User.FindFirstValue("cambiar") == "true" &&
            http.Request.Path.Value is not ("/api/auth/me" or "/api/auth/cambiar-password" or "/api/auth/logout" or "/api/auth/csrf"))
        { await Results.Problem(statusCode: 403, title: "Primero debés cambiar tu contraseña.").ExecuteAsync(http); return; }
        if (http.Request.Method is not ("GET" or "HEAD" or "OPTIONS"))
            await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http);
    }
    await next();
});
app.MapGet("/api/auth/csrf", (HttpContext h, IAntiforgery csrf) => new { token = csrf.GetAndStoreTokens(h).RequestToken });
app.MapGet("/api/auth/cajeros", async (UsuariosServicio s) => (await s.Listar()).Where(x => x.Activo && x.TienePin).Select(x => new { x.Id, x.Nombre }));
app.MapPost("/api/auth/login/pin", async (LoginPin d, SeguridadServicio s, HttpContext h) => { var u = await s.Login(d); await Sesion.Crear(h, u); return u; });
app.MapPost("/api/auth/login/admin", async (LoginAdmin d, SeguridadServicio s, HttpContext h) => { var u = await s.Login(d); await Sesion.Crear(h, u); return u; });
app.MapGet("/api/auth/me", async (IContextoActual c, UsuariosServicio s) => await s.Obtener(c.UsuarioId!.Value)).RequireAuthorization();
app.MapPost("/api/auth/logout", async (HttpContext h) => { await h.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
app.MapPost("/api/auth/cambiar-password", async (CambiarCredenciales d, SeguridadServicio s, HttpContext h) =>
{
    var u = await s.CambiarPassword(d);
    if (u == null) throw new ErrorAplicacion(401, "Credenciales inválidas o cuenta bloqueada.");
    await Sesion.Crear(h, u); return u;
}).RequireAuthorization("Administrador");
var admin = app.MapGroup("/api").RequireAuthorization("Administrador");
admin.MapGet("/usuarios", (UsuariosServicio s) => s.Listar());
admin.MapPost("/usuarios", async (CrearUsuario d, UsuariosServicio s) => { var u = await s.Crear(d); return Results.Created($"/api/usuarios/{u.Id}", u); });
admin.MapPut("/usuarios/{id:guid}", (Guid id, EditarUsuario d, UsuariosServicio s) => s.Editar(id, d));
admin.MapPost("/usuarios/{id:guid}/desactivar", async (Guid id, UsuariosServicio s) => { await s.Desactivar(id); return Results.NoContent(); });
admin.MapPut("/usuarios/{id:guid}/credenciales", (Guid id, Credenciales d, UsuariosServicio s) => s.Credenciales(id, d));
admin.MapGet("/configuracion", (ConfiguracionServicio s) => s.Obtener());
admin.MapPut("/configuracion", (EditarConfiguracion d, ConfiguracionServicio s) => s.Editar(d));
admin.MapGet("/consecutivos", (ConfiguracionServicio s) => s.Consecutivos());
admin.MapPut("/consecutivos/{tipo}", (TipoDocumento tipo, EditarConsecutivo d, ConfiguracionServicio s) => s.EditarConsecutivo(tipo, d));
admin.MapPost("/configuracion/logo", async (HttpRequest request, ConfiguracionServicio s) =>
{
    if (request.ContentLength > 1048576) throw new ErrorAplicacion(413, "El logo debe pesar como máximo 1 MB.");
    using var contenido = new MemoryStream(); var buffer = new byte[8192]; int leidos;
    while ((leidos = await request.Body.ReadAsync(buffer)) > 0)
    {
        if (contenido.Length + leidos > 1048576) throw new ErrorAplicacion(413, "El logo debe pesar como máximo 1 MB.");
        contenido.Write(buffer, 0, leidos);
    }
    return new { ruta = await s.Logo(contenido.ToArray()) };
});
admin.MapGet("/configuracion/logo", async (ConfiguracionServicio s, IConfiguration config) =>
{
    var ruta = (await s.Obtener()).Logo;
    if (ruta == null) return Results.NotFound();
    var archivo = Path.Combine(new LogoLocal(config).Carpeta, Path.GetFileName(ruta));
    return File.Exists(archivo) ? Results.File(archivo, ruta.EndsWith(".png") ? "image/png" : "image/jpeg") : Results.NotFound();
});
app.MapPost("/api/autorizaciones/administrador", (PedirAutorizacion d, SeguridadServicio s) => s.Autorizar(d)).RequireAuthorization();
app.MapPost("/api/pruebas/autorizacion-administrador", () => Results.Ok(new { autorizado = true }))
    .RequireAuthorization().AddEndpointFilter(new AutorizacionAdministradorFiltro("prueba"));
app.MapMethods("/api/{**path}", ["GET", "POST", "PUT", "DELETE", "PATCH"], () => Results.NotFound());
app.MapFallbackToFile("index.html");
if (!app.Environment.IsEnvironment("Testing")) await RegistroServicios.Inicializar(app.Services, app.Configuration, app.Environment.IsDevelopment());
app.Run();
public partial class Program;
