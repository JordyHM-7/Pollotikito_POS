using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Pollotikito.Application;
using Pollotikito.Domain;
using Pollotikito.Infrastructure;

namespace Pollotikito.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POLLOTIKITO_TEST_CONNECTION")))
            Skip = "Integración omitida: definí POLLOTIKITO_TEST_CONNECTION con una base PostgreSQL local de pruebas.";
    }
}
public sealed class RelojPrueba : TimeProvider
{
    private long ticks = DateTimeOffset.UtcNow.UtcTicks;
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
    public void Avanzar(TimeSpan tiempo) => Interlocked.Add(ref ticks, tiempo.Ticks);
}
public sealed class ApiPrueba(string conexion, RelojPrueba reloj, string logos) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:Pollotikito"] = conexion, ["AllowedHosts"] = "*", ["Almacenamiento:Logos"] = logos }));
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<DbContextOptions<PollotikitoDbContext>>();
            s.AddDbContext<PollotikitoDbContext>(o => o.UseNpgsql(conexion));
            s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(reloj);
            s.AddTransient<IStartupFilter, IpDePrueba>();
        });
    }
}

public sealed class IpDePrueba : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> siguiente) => app =>
    {
        app.Use((http, next) => { http.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback; return next(); });
        siguiente(app);
    };
}

public sealed class PostgresFixture : IAsyncLifetime
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    public const string Password = "ClaveDePrueba2026!";
    public string Schema { get; } = "test_pollotikito_" + Guid.NewGuid().ToString("N");
    private readonly string logos = Path.Combine(Path.GetTempPath(), "pollotikito_test_" + Guid.NewGuid().ToString("N"));
    public ApiPrueba Api { get; private set; } = null!;
    public string Conexion { get; private set; } = "";
    public RelojPrueba Reloj { get; } = new();
    public Guid AdminId { get; private set; }
    public async Task InitializeAsync()
    {
        var original = Environment.GetEnvironmentVariable("POLLOTIKITO_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(original)) return;
        var cadena = new NpgsqlConnectionStringBuilder(original) { SearchPath = Schema, IncludeErrorDetail = false };
        Conexion = cadena.ConnectionString;
        await using (var c = new NpgsqlConnection(original))
        { await c.OpenAsync(); await using var cmd = new NpgsqlCommand($"CREATE SCHEMA {Schema}", c); await cmd.ExecuteNonQueryAsync(); }
        Api = new ApiPrueba(Conexion, Reloj, logos);
        using var scope = Api.Services.CreateScope(); var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<PollotikitoDbContext>(); await db.Database.MigrateAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["AdministradorInicial:Usuario"] = "admin", ["AdministradorInicial:Password"] = Password }).Build();
        await RegistroServicios.Inicializar(Api.Services, config, true);
        db.ChangeTracker.Clear(); var admin = await db.Usuarios.SingleAsync();
        // La semilla real debe forzar el cambio y no contener el secreto.
        Assert.True(admin.DebeCambiarCredenciales); Assert.NotEqual(Password, admin.PasswordHash);
        AdminId = admin.Id;
        admin.DebeCambiarCredenciales = false;
        admin.PinHash = sp.GetRequiredService<ISecretos>().Hash(admin, "8642");
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync()
    {
        if (Api == null) return;
        await Api.DisposeAsync();
        // Únicamente el esquema aleatorio creado por esta fixture; jamás DROP DATABASE.
        if (!System.Text.RegularExpressions.Regex.IsMatch(Schema, "^test_pollotikito_[a-f0-9]{32}$")) throw new InvalidOperationException();
        await using var c = new NpgsqlConnection(Conexion); await c.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP SCHEMA {Schema} CASCADE", c); await cmd.ExecuteNonQueryAsync();
        if (Directory.Exists(logos)) Directory.Delete(logos, true);
    }
    public HttpClient Cliente() => Api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    public static async Task Csrf(HttpClient c)
    {
        var token = await c.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        c.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }
    public async Task<HttpClient> Admin()
    {
        var c = Cliente(); await Csrf(c);
        (await c.PostAsJsonAsync("/api/auth/login/admin", new LoginAdmin("admin", Password))).EnsureSuccessStatusCode();
        await Csrf(c); return c;
    }
    public async Task<UsuarioDto> CrearCajero(HttpClient admin)
    {
        var r = await admin.PostAsJsonAsync("/api/usuarios", new CrearUsuario("Cajero", "c" + Guid.NewGuid().ToString("N"), Rol.Cajero, null, "1234"));
        r.EnsureSuccessStatusCode(); return (await r.Content.ReadFromJsonAsync<UsuarioDto>(Json))!;
    }
    public async Task<HttpClient> Cajero(Guid id)
    {
        var c = Cliente(); await Csrf(c);
        (await c.PostAsJsonAsync("/api/auth/login/pin", new LoginPin(id, "1234"))).EnsureSuccessStatusCode();
        await Csrf(c); return c;
    }
}
