using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pollotikito.Application;
using Pollotikito.Domain;
using Pollotikito.Infrastructure;

namespace Pollotikito.Tests;

public sealed class IntegracionTests(PostgresFixture f) : IClassFixture<PostgresFixture>
{
    [PostgresFact, Trait("Requerimiento", "RF-SEG-02")]
    public async Task Pin_correcto_incorrecto_bloqueo_y_desbloqueo_a_los_cinco_minutos()
    {
        using var a = await f.Admin(); var u = await f.CrearCajero(a);
        using var c = await f.Cajero(u.Id);
        var cookie = (await c.GetAsync("/api/auth/me")); Assert.Equal(HttpStatusCode.OK, cookie.StatusCode);
        await c.PostAsync("/api/auth/logout", null); await PostgresFixture.Csrf(c);
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/login/pin", new LoginPin(u.Id, "9999"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/login/pin", new LoginPin(u.Id, "1234"))).StatusCode);
        f.Reloj.Avanzar(TimeSpan.FromMinutes(5));
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/auth/login/pin", new LoginPin(u.Id, "1234"))).StatusCode);
        using var scope = f.Api.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<PollotikitoDbContext>();
        Assert.Equal(0, (await db.Usuarios.SingleAsync(x => x.Id == u.Id)).IntentosFallidos);
        Assert.True(await db.Bitacoras.AnyAsync(x => x.UsuarioId == u.Id && x.Accion == "LoginPinExitoso"));
        Assert.Equal(6, await db.Bitacoras.CountAsync(x => x.UsuarioId == u.Id && x.Accion == "LoginPinFallido"));
    }

    [PostgresFact, Trait("Requerimiento", "RF-SEG-01")]
    public async Task Cajero_recibe_403_en_todos_los_endpoints_administrativos()
    {
        using var a = await f.Admin(); var u = await f.CrearCajero(a); using var c = await f.Cajero(u.Id);
        foreach (var ruta in new[] { "/api/usuarios", "/api/configuracion", "/api/consecutivos", "/api/configuracion/logo" })
            Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(ruta)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/usuarios", new CrearUsuario("Otro", "otro", Rol.Cajero, null, "2222"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PutAsJsonAsync($"/api/usuarios/{u.Id}", new EditarUsuario("Otro", "otro", Rol.Administrador))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsync($"/api/usuarios/{u.Id}/desactivar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PutAsJsonAsync($"/api/usuarios/{u.Id}/credenciales", new Credenciales(null,"1111"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PutAsJsonAsync("/api/configuracion", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PutAsJsonAsync("/api/consecutivos/TIQUETE", new EditarConsecutivo("X", 100))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsync("/api/configuracion/logo", new ByteArrayContent([1,2]))).StatusCode);
    }

    private async Task<AutorizacionDto> Token(HttpClient c)
    {
        var r = await c.PostAsJsonAsync("/api/autorizaciones/administrador", new PedirAutorizacion(f.AdminId, "8642", "prueba"));
        r.EnsureSuccessStatusCode(); return (await r.Content.ReadFromJsonAsync<AutorizacionDto>())!;
    }
    private static async Task<HttpResponseMessage> Usar(HttpClient c, string token)
    {
        var r = new HttpRequestMessage(HttpMethod.Post, "/api/pruebas/autorizacion-administrador");
        r.Headers.Add("X-Autorizacion-Administrador", token); return await c.SendAsync(r);
    }

    [PostgresFact, Trait("Requerimiento", "RF-SEG-03")]
    public async Task Autorizacion_solo_un_uso_incluso_en_paralelo_y_vence_exactamente_a_60_segundos()
    {
        using var a = await f.Admin(); var u = await f.CrearCajero(a); using var c = await f.Cajero(u.Id);
        var t = await Token(c);
        var respuestas = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Usar(c, t.Token)));
        Assert.Single(respuestas, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Equal(7, respuestas.Count(x => x.StatusCode == HttpStatusCode.Forbidden));
        var vigente = await Token(c); f.Reloj.Avanzar(TimeSpan.FromSeconds(59));
        Assert.Equal(HttpStatusCode.OK, (await Usar(c, vigente.Token)).StatusCode);
        var vencido = await Token(c); f.Reloj.Avanzar(TimeSpan.FromSeconds(60));
        Assert.Equal(HttpStatusCode.Forbidden, (await Usar(c, vencido.Token)).StatusCode);
    }

    [PostgresFact, Trait("Requerimiento", "RF-SEG-03")]
    public async Task Autorizacion_no_sirve_para_otro_usuario_ni_otra_accion()
    {
        using var a = await f.Admin(); var u = await f.CrearCajero(a); using var c = await f.Cajero(u.Id);
        var t = await Token(c);
        Assert.Equal(HttpStatusCode.Forbidden, (await Usar(a, t.Token)).StatusCode);
        using (var scope = f.Api.Services.CreateScope())
        {
            var almacen = scope.ServiceProvider.GetRequiredService<IAlmacen>();
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(t.Token)));
            Assert.False(await almacen.Consumir(hash, u.Id, "retiro_efectivo", f.Reloj.GetUtcNow()));
        }
        Assert.Equal(HttpStatusCode.OK, (await Usar(c, t.Token)).StatusCode);
    }

    [PostgresFact, Trait("Requerimiento", "RF-SEG-04")]
    public async Task Cambios_auditados_con_anterior_nuevo_actor_e_ip_sin_secretos_y_bitacora_inmutable()
    {
        using var a = await f.Admin(); var u = await f.CrearCajero(a);
        (await a.PutAsJsonAsync($"/api/usuarios/{u.Id}", new EditarUsuario("Nombre editado", u.NombreUsuario, Rol.Cajero))).EnsureSuccessStatusCode();
        (await a.PutAsJsonAsync($"/api/usuarios/{u.Id}/credenciales", new Credenciales(null, "4321"))).EnsureSuccessStatusCode();
        using var scope = f.Api.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<PollotikitoDbContext>();
        var b = await db.Bitacoras.SingleAsync(x => x.EntidadId == u.Id && x.Accion == "Editar");
        Assert.Equal(f.AdminId, b.UsuarioId); Assert.Equal(TimeSpan.Zero, b.FechaUtc.Offset); Assert.Equal("127.0.0.1", b.Ip);
        Assert.Equal("Cajero", JsonDocument.Parse(b.ValorAnterior!).RootElement.GetProperty("Nombre").GetString());
        Assert.Equal("Nombre editado", JsonDocument.Parse(b.ValorNuevo!).RootElement.GetProperty("Nombre").GetString());
        var json = string.Join("", await db.Bitacoras.Where(x => x.EntidadId == u.Id).Select(x => x.ValorNuevo).ToListAsync());
        Assert.DoesNotContain("4321", json); Assert.DoesNotContain("PinHash", json); Assert.DoesNotContain("PasswordHash", json);
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE bitacora SET accion='alterada'"));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM bitacora"));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("TRUNCATE bitacora"));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM usuarios"));
    }

    [PostgresFact, Trait("Requerimiento", "RF-SIS-02")]
    public async Task Servicio_de_consecutivos_no_repite_numeros_en_paralelo()
    {
        var resultados = await Task.WhenAll(Enumerable.Range(0, 64).Select(async _ =>
        {
            using var scope = f.Api.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IConsecutivos>().Siguiente(TipoDocumento.PROFORMA);
        }));
        Assert.Equal(64, resultados.Select(x => x.Numero).Distinct().Count());
        var ordenados = resultados.Select(x => x.Numero).Order().ToArray();
        Assert.Equal(Enumerable.Range(0,64).Select(i => ordenados[0] + i), ordenados);
        using var a = await f.Admin();
        Assert.Equal(HttpStatusCode.NotFound, (await a.PostAsync("/api/consecutivos/PROFORMA/siguiente", null)).StatusCode);
    }

    [PostgresFact, Trait("Requerimiento", "RF-SIS-02")]
    public async Task Prefijo_y_numero_se_auditan_y_numero_no_puede_bajar()
    {
        using var a = await f.Admin();
        (await a.PutAsJsonAsync("/api/consecutivos/FACTURA", new EditarConsecutivo("F-", 500))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PutAsJsonAsync("/api/consecutivos/FACTURA", new EditarConsecutivo("F-", 499))).StatusCode);
        using var scope = f.Api.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<PollotikitoDbContext>();
        var b = await db.Bitacoras.SingleAsync(x => x.NombreEntidad == nameof(Consecutivo) && x.Accion == "Editar");
        Assert.Equal(500, JsonDocument.Parse(b.ValorNuevo!).RootElement.GetProperty("SiguienteNumero").GetInt64());
        Assert.NotNull(b.ValorAnterior);
        var siguiente = await scope.ServiceProvider.GetRequiredService<IConsecutivos>().Siguiente(TipoDocumento.FACTURA);
        Assert.Equal(new NumeroDocumento("F-", 500), siguiente);
    }

    [PostgresFact, Trait("Requerimiento", "RF-SEG-02")]
    public async Task Primer_inicio_obliga_cambio_password_y_el_cambio_invalida_otras_sesiones()
    {
        using var a = await f.Admin();
        var nombre = "admin" + Guid.NewGuid().ToString("N");
        var r = await a.PostAsJsonAsync("/api/usuarios", new CrearUsuario("Otro admin", nombre, Rol.Administrador, PostgresFixture.Password, null));
        var u = (await r.Content.ReadFromJsonAsync<UsuarioDto>(PostgresFixture.Json))!;
        (await a.PutAsJsonAsync($"/api/usuarios/{u.Id}/credenciales", new Credenciales(PostgresFixture.Password, null))).EnsureSuccessStatusCode();
        using var c = f.Cliente(); await PostgresFixture.Csrf(c);
        var login = await c.PostAsJsonAsync("/api/auth/login/admin", new LoginAdmin(nombre, PostgresFixture.Password));
        Assert.True((await login.Content.ReadFromJsonAsync<UsuarioDto>(PostgresFixture.Json))!.DebeCambiarCredenciales);
        await PostgresFixture.Csrf(c);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/usuarios")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/configuracion")).StatusCode);
        (await c.PostAsJsonAsync("/api/auth/cambiar-password", new CambiarCredenciales(PostgresFixture.Password, "MiNuevaClave2026!"))).EnsureSuccessStatusCode();
        await PostgresFixture.Csrf(c);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/usuarios")).StatusCode);
        (await a.PostAsync($"/api/usuarios/{u.Id}/desactivar", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode);
    }

    [PostgresFact, Trait("Requerimiento", "RF-SIS-02")]
    public async Task Configuracion_unica_auditada_logo_local_y_limites()
    {
        using var a = await f.Admin();
        var d = new EditarConfiguracion("Pollotikito Test", "Razón", "123", "22223333", "local@example.com", "Dirección", "Gracias", 13m);
        (await a.PutAsJsonAsync("/api/configuracion", d)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsync("/api/configuracion/logo", new ByteArrayContent([1,2,3]))).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await a.PostAsync("/api/configuracion/logo", new ByteArrayContent(new byte[1048577]))).StatusCode);
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a2ioAAAAASUVORK5CYII=");
        (await a.PostAsync("/api/configuracion/logo", new ByteArrayContent(png))).EnsureSuccessStatusCode();
        var archivo = await a.GetAsync("/api/configuracion/logo"); archivo.EnsureSuccessStatusCode();
        Assert.Equal("image/png", archivo.Content.Headers.ContentType!.MediaType);
        using var scope = f.Api.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<PollotikitoDbContext>();
        var c = await db.Configuraciones.SingleAsync(); Assert.False(Path.IsPathRooted(c.Logo)); Assert.EndsWith(".png", c.Logo);
        Assert.True(await db.Bitacoras.AnyAsync(x => x.NombreEntidad == nameof(ConfiguracionNegocio) && x.ValorAnterior != null && x.UsuarioId == f.AdminId));
    }

    [PostgresFact, Trait("Requerimiento", "RF-SEG-02")]
    public async Task Mutaciones_exigen_csrf_y_la_cookie_es_HttpOnly()
    {
        using var c = f.Cliente();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/auth/login/admin", new LoginAdmin("admin", PostgresFixture.Password))).StatusCode);
        await PostgresFixture.Csrf(c);
        var r = await c.PostAsJsonAsync("/api/auth/login/admin", new LoginAdmin("admin", PostgresFixture.Password));
        r.EnsureSuccessStatusCode();
        Assert.Contains(r.Headers.GetValues("Set-Cookie"), x => x.StartsWith("Pollotikito.Sesion=") && x.Contains("httponly", StringComparison.OrdinalIgnoreCase) && x.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));
    }
}
