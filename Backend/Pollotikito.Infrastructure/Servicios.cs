using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pollotikito.Application;
using Pollotikito.Domain;

namespace Pollotikito.Infrastructure;

public sealed class Secretos : ISecretos
{
    private readonly PasswordHasher<Usuario> hasher = new();
    public string Hash(Usuario usuario, string secreto) => hasher.HashPassword(usuario, secreto);
    public bool Verificar(Usuario usuario, string hash, string secreto) => hasher.VerifyHashedPassword(usuario, hash, secreto) != PasswordVerificationResult.Failed;
}

public sealed class LogoLocal(IConfiguration configuracion) : ILogoLocal
{
    public string Carpeta => Path.GetFullPath(configuracion["Almacenamiento:Logos"] ?? "storage/logos");
    public async Task<string> Guardar(byte[] contenido, string extension)
    {
        Directory.CreateDirectory(Carpeta);
        var nombre = Guid.CreateVersion7().ToString() + extension;
        await File.WriteAllBytesAsync(Path.Combine(Carpeta, nombre), contenido);
        return nombre;
    }
}

public static class RegistroServicios
{
    public static IServiceCollection AgregarPollotikito(this IServiceCollection s, string conexion)
    {
        s.AddDbContext<PollotikitoDbContext>(o => o.UseNpgsql(conexion));
        s.AddScoped<Almacen>(); s.AddScoped<IAlmacen>(p => p.GetRequiredService<Almacen>());
        s.AddScoped<IConsecutivos>(p => p.GetRequiredService<Almacen>());
        s.AddSingleton<ISecretos, Secretos>(); s.AddScoped<ILogoLocal, LogoLocal>();
        s.AddScoped<UsuariosServicio>(); s.AddScoped<SeguridadServicio>(); s.AddScoped<ConfiguracionServicio>();
        return s;
    }

    public static async Task Inicializar(IServiceProvider servicios, IConfiguration config, bool desarrollo)
    {
        using var scope = servicios.CreateScope(); var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<PollotikitoDbContext>();
        if (desarrollo) await db.Database.MigrateAsync();
        var almacen = sp.GetRequiredService<IAlmacen>(); var contexto = sp.GetRequiredService<IContextoActual>();
        await almacen.Transaccion(async () =>
        {
            if (!await db.Configuraciones.AnyAsync()) db.Add(new ConfiguracionNegocio { SucursalId = contexto.SucursalId });
            foreach (var tipo in Enum.GetValues<TipoDocumento>())
                if (!await db.Consecutivos.AnyAsync(x => x.Tipo == tipo)) db.Add(new Consecutivo { SucursalId = contexto.SucursalId, Tipo = tipo });
            if (desarrollo && !await db.Usuarios.AnyAsync(x => x.Rol == Rol.Administrador))
            {
                var password = config["AdministradorInicial:Password"];
                Validacion.Password(password);
                var login = Validacion.Texto(config["AdministradorInicial:Usuario"], "Administrador inicial", 80, true);
                var u = new Usuario { SucursalId = contexto.SucursalId, Nombre = "Administrador", NombreUsuario = login,
                    NombreUsuarioNormalizado = login.ToUpperInvariant(), Rol = Rol.Administrador, DebeCambiarCredenciales = true };
                u.PasswordHash = sp.GetRequiredService<ISecretos>().Hash(u, password!); db.Add(u);
            }
            await db.SaveChangesAsync(); return true;
        });
    }
}
