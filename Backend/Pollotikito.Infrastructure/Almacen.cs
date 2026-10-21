using Microsoft.EntityFrameworkCore;
using Pollotikito.Application;
using Pollotikito.Domain;

namespace Pollotikito.Infrastructure;

public sealed class Almacen(PollotikitoDbContext db, IContextoActual contexto, TimeProvider reloj) : IAlmacen, IConsecutivos
{
    // Una única sucursal/PC: serializa mutaciones cortas (incluidos intentos de login)
    // y evita carreras entre edición de consecutivos y emisión. El bloqueo vive en PG,
    // no en memoria, y funciona con múltiples procesos de la API.
    public async Task<T> Transaccion<T>(Func<Task<T>> operacion)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(71002101)");
        try { var r = await operacion(); await tx.CommitAsync(); return r; }
        catch { await tx.RollbackAsync(); db.ChangeTracker.Clear(); throw; }
    }
    public Task<List<Usuario>> Usuarios() => db.Usuarios.Where(x => x.SucursalId == contexto.SucursalId).OrderBy(x => x.Nombre).ToListAsync();
    public Task<Usuario?> Usuario(Guid id) => db.Usuarios.SingleOrDefaultAsync(x => x.Id == id && x.SucursalId == contexto.SucursalId);
    public Task<Usuario?> PorNombre(string normalizado) => db.Usuarios.SingleOrDefaultAsync(x => x.NombreUsuarioNormalizado == normalizado && x.SucursalId == contexto.SucursalId);
    public Task<ConfiguracionNegocio> Configuracion() => db.Configuraciones.SingleAsync(x => x.SucursalId == contexto.SucursalId);
    public Task<List<Consecutivo>> Consecutivos() => db.Consecutivos.Where(x => x.SucursalId == contexto.SucursalId).OrderBy(x => x.Tipo).ToListAsync();
    public Task<Consecutivo> Consecutivo(TipoDocumento tipo) => db.Consecutivos.SingleAsync(x => x.Tipo == tipo && x.SucursalId == contexto.SucursalId);
    public void Agregar(Entidad entidad) => db.Add(entidad);
    public async Task Guardar() => await db.SaveChangesAsync();

    public async Task<bool> Consumir(string hash, Guid solicitante, string accion, DateTimeOffset ahora) =>
        await db.Autorizaciones.Where(x => x.TokenHash == hash && x.SolicitanteId == solicitante && x.Accion == accion &&
            x.SucursalId == contexto.SucursalId && x.ConsumidaEn == null && x.VenceEn > ahora &&
            db.Usuarios.Any(u => u.Id == x.AdministradorId && u.Activo && u.Rol == Rol.Administrador && !u.DebeCambiarCredenciales &&
                u.ActualizadoEn <= x.CreadoEn))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumidaEn, ahora).SetProperty(x => x.ActualizadoEn, ahora)) == 1;

    public Task<NumeroDocumento> Siguiente(TipoDocumento tipo) => Transaccion(async () =>
    {
        Validacion.Tipo(tipo);
        var tipoTexto = tipo.ToString(); var ahora = reloj.GetUtcNow(); var sucursal = contexto.SucursalId;
        // Materializar antes de proyectar: PostgreSQL no permite componer UPDATE como subconsulta.
        var filas = await db.Database.SqlQuery<NumeroFila>($"""
            UPDATE consecutivos SET siguiente_numero = siguiente_numero + 1, actualizado_en = {ahora}
            WHERE tipo = {tipoTexto} AND sucursal_id = {sucursal} AND siguiente_numero < 9223372036854775807
            RETURNING prefijo AS "Prefijo", siguiente_numero - 1 AS "Numero"
            """).ToListAsync();
        Validacion.Exigir(filas.Count == 1, "Consecutivo no disponible o agotado.", 409);
        return new NumeroDocumento(filas[0].Prefijo, filas[0].Numero);
    });
    private sealed class NumeroFila { public string Prefijo { get; set; } = ""; public long Numero { get; set; } }
}
