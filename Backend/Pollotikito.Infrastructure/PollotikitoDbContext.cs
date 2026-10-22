using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Pollotikito.Application;
using Pollotikito.Domain;

namespace Pollotikito.Infrastructure;

public sealed class PollotikitoDbContext(DbContextOptions<PollotikitoDbContext> options, IContextoActual contexto, TimeProvider reloj) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<ConfiguracionNegocio> Configuraciones => Set<ConfiguracionNegocio>();
    public DbSet<Consecutivo> Consecutivos => Set<Consecutivo>();
    public DbSet<Bitacora> Bitacoras => Set<Bitacora>();
    public DbSet<AutorizacionAdministrador> Autorizaciones => Set<AutorizacionAdministrador>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Usuario>().ToTable("usuarios");
        b.Entity<ConfiguracionNegocio>().ToTable("configuracion_negocio", t => t.HasCheckConstraint("ck_registro_unico", "registro_unico = true"));
        b.Entity<Consecutivo>().ToTable("consecutivos", t => t.HasCheckConstraint("ck_siguiente_positivo", "siguiente_numero > 0"));
        b.Entity<Bitacora>().ToTable("bitacora");
        b.Entity<AutorizacionAdministrador>().ToTable("autorizaciones_administrador");
        b.Entity<Usuario>().HasIndex(x => x.NombreUsuarioNormalizado).IsUnique();
        b.Entity<Usuario>().Property(x => x.Nombre).HasMaxLength(120);
        b.Entity<Usuario>().Property(x => x.NombreUsuario).HasMaxLength(80);
        b.Entity<Usuario>().Property(x => x.NombreUsuarioNormalizado).HasMaxLength(80);
        b.Entity<Usuario>().Property(x => x.Rol).HasConversion<string>();
        b.Entity<ConfiguracionNegocio>().HasIndex(x => x.RegistroUnico).IsUnique();
        b.Entity<ConfiguracionNegocio>().Property(x => x.IvaPorDefecto).HasPrecision(5, 2);
        b.Entity<Consecutivo>().HasIndex(x => x.Tipo).IsUnique();
        b.Entity<Consecutivo>().Property(x => x.Tipo).HasConversion<string>();
        b.Entity<Consecutivo>().Property(x => x.Prefijo).HasMaxLength(30);
        b.Entity<Bitacora>().Property(x => x.ValorAnterior).HasColumnType("jsonb");
        b.Entity<Bitacora>().Property(x => x.ValorNuevo).HasColumnType("jsonb");
        b.Entity<Bitacora>().HasIndex(x => x.FechaUtc);
        b.Entity<AutorizacionAdministrador>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<AutorizacionAdministrador>().HasOne<Usuario>().WithMany().HasForeignKey(x => x.AdministradorId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AutorizacionAdministrador>().HasOne<Usuario>().WithMany().HasForeignKey(x => x.SolicitanteId).OnDelete(DeleteBehavior.Restrict);
        ModeloComercio.Configurar(b);
        foreach (var e in b.Model.GetEntityTypes())
        {
            e.SetTableName(Snake(e.GetTableName()!));
            foreach (var p in e.GetProperties()) p.SetColumnName(Snake(p.Name));
        }
    }
    private static string Snake(string nombre) => Regex.Replace(nombre, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();
        var ahora = reloj.GetUtcNow();
        foreach (var e in ChangeTracker.Entries<Entidad>().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList())
        {
            if (e.Entity is Bitacora && e.State != EntityState.Added) throw new InvalidOperationException("La bitácora es de solo inserción.");
            if (e.Entity is Usuario or Producto or Categoria && e.State == EntityState.Deleted) throw new InvalidOperationException("Los registros solo se desactivan.");
            if (e.State == EntityState.Added) e.Entity.CreadoEn = ahora;
            e.Entity.ActualizadoEn = ahora;
            if (e.Entity is not (Usuario or ConfiguracionNegocio or Consecutivo or Categoria or Producto)) continue;
            if (e.State == EntityState.Modified && !e.Properties.Any(p => p.IsModified && p.Metadata.Name is not (nameof(Usuario.IntentosFallidos) or nameof(Usuario.BloqueadoHasta) or nameof(Entidad.ActualizadoEn)))) continue;
            var credenciales = e.Entity is Usuario && e.Properties.Any(p => p.IsModified && p.Metadata.Name is nameof(Usuario.PinHash) or nameof(Usuario.PasswordHash));
            Bitacoras.Add(new Bitacora { SucursalId = e.Entity.SucursalId, UsuarioId = contexto.UsuarioId, Ip = contexto.Ip,
                Accion = e.State == EntityState.Added ? "Crear" : credenciales ? "CambiarCredenciales" : "Editar",
                NombreEntidad = e.Entity.GetType().Name, EntidadId = e.Entity.Id, FechaUtc = ahora, CreadoEn = ahora, ActualizadoEn = ahora,
                ValorAnterior = e.State == EntityState.Added ? null : Snapshot(e, true), ValorNuevo = Snapshot(e, false) });
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
    private static string Snapshot(EntityEntry e, bool original)
    {
        var valores = e.Properties.Where(p => p.Metadata.Name is not (nameof(Usuario.PasswordHash) or nameof(Usuario.PinHash) or nameof(Usuario.VersionSeguridad)))
            .ToDictionary(p => p.Metadata.Name, p => original ? p.OriginalValue : p.CurrentValue);
        if (e.Entity is Usuario)
        {
            valores["TienePin"] = (original ? e.Property(nameof(Usuario.PinHash)).OriginalValue : e.Property(nameof(Usuario.PinHash)).CurrentValue) != null;
            valores["TienePassword"] = (original ? e.Property(nameof(Usuario.PasswordHash)).OriginalValue : e.Property(nameof(Usuario.PasswordHash)).CurrentValue) != null;
        }
        return JsonSerializer.Serialize(valores);
    }
}
