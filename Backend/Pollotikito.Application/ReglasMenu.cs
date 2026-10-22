using Pollotikito.Domain;
namespace Pollotikito.Application;

public static class ReglasMenu
{
    public static void Disponibilidad(List<Producto> productos,List<Categoria> categorias,DefinicionMenu menu)
    {
        var activos=productos.Where(p=>p.Activo&&categorias.Any(c=>c.Id==p.CategoriaId&&c.Activo)).ToDictionary(p=>p.Id);
        foreach(var p in productos)
        {
            p.Advertencias=[];
            if(!p.Activo)p.Advertencias.Add("Producto inactivo.");
            if(!categorias.Any(c=>c.Id==p.CategoriaId&&c.Activo))p.Advertencias.Add("Categoría inactiva.");
            foreach(var a in menu.Asignaciones.Where(a=>a.Activo&&a.ProductoId==p.Id))
            {
                var g=menu.Grupos.Single(g=>g.Id==a.GrupoModificadorId);
                if(g.Minimo>0 && (!g.Activo||g.Opciones.Count(o=>o.Activo)<g.Minimo))p.Advertencias.Add($"El grupo obligatorio «{g.Nombre}» no tiene suficientes opciones activas (mínimo {g.Minimo}).");
            }
            if(p.EsCombo)
            {
                var componentes=menu.Componentes.Where(c=>c.Activo&&c.ProductoId==p.Id).ToList();
                if(componentes.Count==0)p.Advertencias.Add("El combo no tiene componentes configurados.");
                foreach(var c in componentes)
                {
                    if(c.Tipo==TipoComponente.Fijo && (!activos.TryGetValue(c.ProductoFijoId??Guid.Empty,out var fijo)||fijo.EsCombo))p.Advertencias.Add($"El componente fijo «{c.Nombre}» no está activo/disponible.");
                    if(c.Tipo==TipoComponente.Elegible&&!c.Opciones.Any(o=>o.Activo&&activos.TryGetValue(o.ProductoId,out var opcion)&&!opcion.EsCombo))p.Advertencias.Add($"El componente «{c.Nombre}» no tiene alternativas activas.");
                }
            }
            p.Disponible=p.Advertencias.Count==0;
        }
    }
    public static (decimal Adicional,List<ModificadorLineaVenta> Modificadores,List<ComponenteLineaVenta> Componentes) Elegir(
        LineaSolicitud solicitud,Producto producto,IReadOnlyDictionary<Guid,Producto> productos,DefinicionMenu? menu)
    {
        var seleccion=solicitud.Modificadores??[];var elecciones=solicitud.Componentes??[];
        Validacion.Exigir(seleccion.Count<=100 && seleccion.Distinct().Count()==seleccion.Count,"Cada opción de modificador se puede elegir una sola vez por línea.");
        Validacion.Exigir(elecciones.Count<=500&&elecciones.All(e=>e!=null&&e.Cantidad is >0 and <=100)&&elecciones.Select(e=>(e.ComponenteId,e.ProductoId)).Distinct().Count()==elecciones.Count,"Elecciones de componentes inválidas o repetidas.");
        var modificadores=new List<ModificadorLineaVenta>();var componentes=new List<ComponenteLineaVenta>();
        var productosComponentes=menu?.ProductosComponentes?.ToDictionary(p=>p.Id)??productos;
        var grupos=menu?.Asignaciones.Where(a=>a.Activo&&a.ProductoId==producto.Id).OrderBy(a=>a.Orden).ThenBy(a=>a.Id).Select(a=>menu.Grupos.Single(g=>g.Id==a.GrupoModificadorId)).Where(g=>g.Activo).ToList()??[];
        var opcionesPermitidas=grupos.SelectMany(g=>g.Opciones.Where(o=>o.Activo)).Select(o=>o.Id).ToHashSet();
        Validacion.Exigir(seleccion.All(opcionesPermitidas.Contains),"El modificador no pertenece al producto o está inactivo.");
        foreach(var g in grupos)
        {
            var opciones=g.Opciones.Where(o=>seleccion.Contains(o.Id)).OrderBy(o=>o.Orden).ThenBy(o=>o.Id).ToList();
            Validacion.Exigir(opciones.Count>=g.Minimo&&opciones.Count<=g.Maximo,$"«{g.Nombre}»: elegí entre {g.Minimo} y {g.Maximo} opciones.");
            foreach(var o in opciones)modificadores.Add(new(){GrupoModificadorId=g.Id,OpcionModificadorId=o.Id,NombreGrupo=g.Nombre,NombreOpcion=o.Nombre,PrecioAdicional=o.PrecioAdicional,Orden=modificadores.Count});
        }
        var definiciones=menu?.Componentes.Where(c=>c.Activo&&c.ProductoId==producto.Id).OrderBy(c=>c.Orden).ThenBy(c=>c.Id).ToList()??[];
        Validacion.Exigir(producto.EsCombo||elecciones.Count==0,"Un producto simple no admite elecciones de combo.");
        Validacion.Exigir(elecciones.All(e=>definiciones.Any(c=>c.Id==e.ComponenteId&&c.Tipo==TipoComponente.Elegible)),"El componente no pertenece al combo o no es elegible.");
        foreach(var c in definiciones)
        {
            if(c.Tipo==TipoComponente.Fijo)
            {
                Validacion.Exigir(productosComponentes.ContainsKey(c.ProductoFijoId??Guid.Empty),"Componente fijo no disponible.");
                var p=productosComponentes[c.ProductoFijoId!.Value];
                componentes.Add(new(){ComponenteComboId=c.Id,ProductoId=p.Id,NombreComponente=c.Nombre,NombreProducto=p.Nombre,CantidadPorUnidad=c.Cantidad,Orden=componentes.Count});
            }
            else
            {
                var elegidas=elecciones.Where(e=>e.ComponenteId==c.Id).OrderBy(e=>e.ProductoId).ToList();
                Validacion.Exigir(elegidas.Sum(e=>e.Cantidad)==c.Cantidad,$"«{c.Nombre}»: elegí exactamente {c.Cantidad} unidad(es).");
                foreach(var e in elegidas)
                {
                    var o=c.Opciones.SingleOrDefault(o=>o.Activo&&o.ProductoId==e.ProductoId);
                    Validacion.Exigir(o!=null&&productosComponentes.ContainsKey(e.ProductoId),"La alternativa no pertenece al componente o está inactiva.");
                    componentes.Add(new(){ComponenteComboId=c.Id,ProductoId=e.ProductoId,NombreComponente=c.Nombre,NombreProducto=productosComponentes[e.ProductoId].Nombre,CantidadPorUnidad=e.Cantidad,RecargoUnitario=o!.Recargo,Orden=componentes.Count});
                }
            }
        }
        return (modificadores.Sum(m=>m.PrecioAdicional)+componentes.Sum(c=>c.RecargoUnitario*c.CantidadPorUnidad),modificadores,componentes);
    }
}
