using Microsoft.EntityFrameworkCore;
using Pollotikito.Application;
using Pollotikito.Domain;
namespace Pollotikito.Infrastructure;

public sealed class MenuRepositorio(PollotikitoDbContext db,IContextoActual contexto) : IMenuRepositorio
{
    public Task<List<GrupoModificador>> Grupos()=>db.Set<GrupoModificador>().Include(g=>g.Opciones).Where(g=>g.SucursalId==contexto.SucursalId).OrderBy(g=>g.Orden).ThenBy(g=>g.Id).ToListAsync();
    public Task<GrupoModificador?> Grupo(Guid id)=>db.Set<GrupoModificador>().Include(g=>g.Opciones).SingleOrDefaultAsync(g=>g.Id==id&&g.SucursalId==contexto.SucursalId);
    public Task<OpcionModificador?> Opcion(Guid id)=>db.Set<OpcionModificador>().SingleOrDefaultAsync(o=>o.Id==id&&o.SucursalId==contexto.SucursalId);
    public Task<List<ProductoGrupoModificador>> Asignaciones()=>db.Set<ProductoGrupoModificador>().Where(a=>a.SucursalId==contexto.SucursalId).OrderBy(a=>a.Orden).ThenBy(a=>a.Id).ToListAsync();
    public Task<List<ComponenteCombo>> Componentes()=>db.Set<ComponenteCombo>().Include(c=>c.Opciones).Where(c=>c.SucursalId==contexto.SucursalId).OrderBy(c=>c.Orden).ThenBy(c=>c.Id).ToListAsync();
    public Task<ComponenteCombo?> Componente(Guid id)=>db.Set<ComponenteCombo>().Include(c=>c.Opciones).SingleOrDefaultAsync(c=>c.Id==id&&c.SucursalId==contexto.SucursalId);
}
