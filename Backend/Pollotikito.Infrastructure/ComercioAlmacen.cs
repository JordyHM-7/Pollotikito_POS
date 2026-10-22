using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pollotikito.Application;
using Pollotikito.Domain;

namespace Pollotikito.Infrastructure;

public sealed class ComercioAlmacen(PollotikitoDbContext db, IContextoActual contexto) : IComercio
{
    public async Task<T> Transaccion<T>(Func<Task<T>> operacion)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        try { var resultado = await operacion(); await tx.CommitAsync(); return resultado; }
        catch { await tx.RollbackAsync(); db.ChangeTracker.Clear(); throw; }
    }
    public Task<List<Categoria>> Categorias() => db.Set<Categoria>().AsNoTracking().Where(x => x.SucursalId == contexto.SucursalId).OrderBy(x => x.Orden).ThenBy(x => x.Nombre).ThenBy(x => x.Id).ToListAsync();
    public Task<Categoria?> Categoria(Guid id) => db.Set<Categoria>().SingleOrDefaultAsync(x => x.Id == id && x.SucursalId == contexto.SucursalId);
    public Task<List<Producto>> Productos() => db.Set<Producto>().AsNoTracking().Where(x => x.SucursalId == contexto.SucursalId).OrderBy(x => x.Orden).ThenBy(x => x.Nombre).ThenBy(x => x.Id).ToListAsync();
    public Task<Producto?> Producto(Guid id) => db.Set<Producto>().SingleOrDefaultAsync(x => x.Id == id && x.SucursalId == contexto.SucursalId);
    public async Task<SesionCaja?> Sesion(Guid id, bool bloquear = false)
    {
        if (!bloquear) return await db.Set<SesionCaja>().AsNoTracking().Include(s => s.Conteo).SingleOrDefaultAsync(x => x.Id == id && x.SucursalId == contexto.SucursalId);
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("El bloqueo de sesión requiere una transacción.");
        var sucursal = contexto.SucursalId;
        var s = (await db.Set<SesionCaja>().FromSqlInterpolated($"SELECT * FROM sesiones_caja WHERE id = {id} AND sucursal_id = {sucursal} FOR UPDATE").ToListAsync()).SingleOrDefault();
        if (s != null) await db.Entry(s).Collection(x => x.Conteo).LoadAsync();
        return s;
    }
    public Task<SesionCaja?> Abierta() => db.Set<SesionCaja>().AsNoTracking().SingleOrDefaultAsync(x => x.SucursalId == contexto.SucursalId && x.CerradaEn == null);
    public Task<List<SesionCaja>> Sesiones() => db.Set<SesionCaja>().AsNoTracking().Include(s => s.Conteo).Where(x => x.SucursalId == contexto.SucursalId).OrderByDescending(x => x.AbiertaEn).ToListAsync();
    public Task<Venta?> Venta(Guid id, bool seguir = false)
    {
        var q = db.Set<Venta>().Include(x => x.Lineas).Include(x => x.Pagos).AsSplitQuery().Where(x => x.Id == id && x.SucursalId == contexto.SucursalId);
        return (seguir ? q : q.AsNoTracking()).SingleOrDefaultAsync();
    }
    public Task<List<Venta>> Ventas(Guid id) => db.Set<Venta>().AsNoTracking().Include(x => x.Lineas).Include(x => x.Pagos).AsSplitQuery()
        .Where(x => x.SesionCajaId == id && x.SucursalId == contexto.SucursalId).OrderByDescending(x => x.Numero).ToListAsync();
    public Task<List<MovimientoCaja>> Movimientos(Guid id) => db.Set<MovimientoCaja>().AsNoTracking().Where(x => x.SesionCajaId == id && x.SucursalId == contexto.SucursalId).OrderBy(x => x.Fecha).ToListAsync();
    public void Agregar(Entidad e) => db.Add(e);
    public async Task Guardar() => await db.SaveChangesAsync();
}

public sealed class FotosProductos(IConfiguration config) : IFotosProductos
{
    public string Carpeta => Path.GetFullPath(config["Almacenamiento:FotosProductos"] ?? "storage/productos");
    public async Task<string> Guardar(byte[] contenido, string extension)
    {
        Directory.CreateDirectory(Carpeta); var nombre = Guid.CreateVersion7() + extension;
        await File.WriteAllBytesAsync(Path.Combine(Carpeta, nombre), contenido); return nombre;
    }
}
