using Pollotikito.Domain;
namespace Pollotikito.Application;

public sealed class MenuServicio(IMenuRepositorio menu, IComercio db, IAlmacen admin, IContextoActual contexto)
{
    public Task<List<GrupoModificador>> Grupos() => menu.Grupos();
    public async Task<DefinicionMenu> Definicion() => new(await menu.Grupos(), await menu.Asignaciones(), await menu.Componentes());
    public Task<GrupoModificador> GuardarGrupo(Guid? id, DatosGrupo d) => admin.Transaccion(async () =>
    {
        Validacion.Exigir(d.Minimo >= 0 && d.Minimo <= d.Maximo && d.Maximo is >= 1 and <= 100, "El grupo requiere 0 ≤ mínimo ≤ máximo y un máximo entre 1 y 100.");
        Validacion.Exigir(d.Orden >= 0, "Orden inválido.");
        var g = id.HasValue ? await menu.Grupo(id.Value) ?? throw new ErrorAplicacion(404,"Grupo no encontrado.") : new GrupoModificador { SucursalId = contexto.SucursalId };
        g.Nombre = Validacion.Texto(d.Nombre,"Nombre",120,true); g.Minimo=d.Minimo; g.Maximo=d.Maximo; g.Orden=d.Orden; g.Activo=d.Activo;
        if(!id.HasValue) db.Agregar(g); await db.Guardar(); return g;
    });
    public Task<OpcionModificador> GuardarOpcion(Guid grupoId, Guid? id, DatosOpcion d) => admin.Transaccion(async () =>
    {
        _ = await menu.Grupo(grupoId) ?? throw new ErrorAplicacion(404,"Grupo no encontrado.");
        CalculoVenta.Dinero(d.PrecioAdicional,"Precio adicional"); Validacion.Exigir(d.Orden>=0,"Orden inválido.");
        var o=id.HasValue ? await menu.Opcion(id.Value) ?? throw new ErrorAplicacion(404,"Opción no encontrada.") : new OpcionModificador { SucursalId=contexto.SucursalId, GrupoModificadorId=grupoId };
        Validacion.Exigir(o.GrupoModificadorId==grupoId,"La opción no pertenece al grupo.");
        o.Nombre=Validacion.Texto(d.Nombre,"Nombre",120,true); o.PrecioAdicional=d.PrecioAdicional; o.Orden=d.Orden; o.Activo=d.Activo;
        if(!id.HasValue) db.Agregar(o); await db.Guardar(); return o;
    });
    public Task<bool> DesactivarGrupo(Guid id) => admin.Transaccion(async () => {var g=await menu.Grupo(id)??throw new ErrorAplicacion(404,"Grupo no encontrado.");g.Activo=false;await db.Guardar();return true;});
    public Task<bool> DesactivarOpcion(Guid id) => admin.Transaccion(async () => {var o=await menu.Opcion(id)??throw new ErrorAplicacion(404,"Opción no encontrada.");o.Activo=false;await db.Guardar();return true;});
    public async Task<List<ProductoGrupoModificador>> Asignaciones(Guid id) => (await menu.Asignaciones()).Where(a=>a.ProductoId==id && a.Activo).ToList();
    public Task<bool> Asignar(Guid id,List<AsignarGrupo> datos) => admin.Transaccion(async () =>
    {
        _=await db.Producto(id)??throw new ErrorAplicacion(404,"Producto no encontrado.");
        Validacion.Exigir(datos is {Count:<=30} && datos.All(x=>x!=null&&x.Orden>=0) && datos.Select(x=>x.GrupoModificadorId).Distinct().Count()==datos.Count,"Asignaciones inválidas o repetidas.");
        var existentes=(await menu.Asignaciones()).Where(x=>x.ProductoId==id).ToList();
        foreach(var x in existentes)x.Activo=false;
        foreach(var a in datos)
        {
            _=await menu.Grupo(a.GrupoModificadorId)??throw new ErrorAplicacion(400,"Grupo no encontrado.");
            var fila=existentes.SingleOrDefault(x=>x.GrupoModificadorId==a.GrupoModificadorId);
            if(fila==null){fila=new(){SucursalId=contexto.SucursalId,ProductoId=id,GrupoModificadorId=a.GrupoModificadorId};db.Agregar(fila);}
            fila.Activo=true;fila.Orden=a.Orden;
        }
        await db.Guardar();return true;
    });
    public async Task<List<ComponenteCombo>> Combo(Guid id) => (await menu.Componentes()).Where(c=>c.ProductoId==id && c.Activo).ToList();
    public Task<bool> GuardarCombo(Guid id,GuardarCombo datos) => admin.Transaccion(async () =>
    {
        var producto=await db.Producto(id)??throw new ErrorAplicacion(404,"Producto no encontrado.");
        Validacion.Exigir(producto.EsCombo,"El producto debe ser de tipo combo.");
        Validacion.Exigir(datos.Componentes is {Count:<=50} && datos.Componentes.All(c=>c!=null),"El combo admite hasta 50 componentes.");
        var conId=datos.Componentes.Where(c=>c.Id!=null).ToList();
        Validacion.Exigir(conId.Select(c=>c.Id).Distinct().Count()==conId.Count,"Componentes repetidos.");
        var existentes=await Combo(id);foreach(var c in existentes)c.Activo=false;
        foreach(var d in datos.Componentes)
        {
            Validacion.Exigir(Enum.IsDefined(d.Tipo)&&d.Cantidad is >0 and <=100 && d.Orden>=0,"Componente inválido.");
            var c=d.Id.HasValue? await menu.Componente(d.Id.Value)??throw new ErrorAplicacion(400,"Componente no encontrado."):new ComponenteCombo{SucursalId=contexto.SucursalId,ProductoId=id};
            Validacion.Exigir(c.ProductoId==id,"El componente pertenece a otro combo.");
            c.Nombre=Validacion.Texto(d.Nombre,"Componente",120,true);c.Tipo=d.Tipo;c.Cantidad=d.Cantidad;c.ProductoFijoId=d.ProductoFijoId;c.Orden=d.Orden;c.Activo=true;
            if(!d.Id.HasValue)db.Agregar(c);
            if(d.Tipo==TipoComponente.Fijo){Validacion.Exigir(d.ProductoFijoId!=null && d.Opciones is {Count:0},"El componente fijo requiere un producto y no admite alternativas.");await ProductoSimple(d.ProductoFijoId!.Value,id);}
            else Validacion.Exigir(d.ProductoFijoId==null && d.Opciones is {Count:>0 and <=100},"El componente elegible requiere alternativas, sin producto fijo.");
            Validacion.Exigir(d.Opciones.All(o=>o!=null&&o.Orden>=0)&&d.Opciones.Select(o=>o.ProductoId).Distinct().Count()==d.Opciones.Count,"Alternativas inválidas o repetidas.");
            foreach(var o in c.Opciones)o.Activo=false;
            foreach(var o in d.Opciones)
            {
                await ProductoSimple(o.ProductoId,id);CalculoVenta.Dinero(o.Recargo,"Recargo");
                var alternativa=c.Opciones.SingleOrDefault(a=>a.ProductoId==o.ProductoId);
                if(alternativa==null){alternativa=new(){SucursalId=contexto.SucursalId,ComponenteComboId=c.Id,ProductoId=o.ProductoId};db.Agregar(alternativa);c.Opciones.Add(alternativa);}
                alternativa.Recargo=o.Recargo;alternativa.Orden=o.Orden;alternativa.Activo=true;
            }
        }
        await db.Guardar();return true;
    });
    private async Task ProductoSimple(Guid id,Guid comboId)
    {var p=await db.Producto(id)??throw new ErrorAplicacion(400,"Producto de componente no encontrado.");Validacion.Exigir(id!=comboId&&!p.EsCombo,"No se permiten combos dentro de combos.");}
}
