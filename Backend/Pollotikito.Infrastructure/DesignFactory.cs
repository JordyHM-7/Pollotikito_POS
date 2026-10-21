using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pollotikito.Application;

namespace Pollotikito.Infrastructure;
public sealed class DesignFactory : IDesignTimeDbContextFactory<PollotikitoDbContext>
{
    public PollotikitoDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<PollotikitoDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Pollotikito") ??
            "Host=localhost;Database=pollotikito;Username=pollotikito").Options, new ContextoDiseno(), TimeProvider.System);
    private sealed class ContextoDiseno : IContextoActual
    {
        public Guid SucursalId => Guid.Empty;
        public Guid? UsuarioId => null;
        public string? Ip => null;
    }
}
