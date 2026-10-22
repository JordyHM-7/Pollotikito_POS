using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pollotikito.Application;
using Pollotikito.Domain;
using Pollotikito.Infrastructure;

namespace Pollotikito.Tests;
public sealed class MenuIntegracionTests(PostgresFixture f):IClassFixture<PostgresFixture>
{
    private static async Task<T> Leer<T>(HttpResponseMessage r)
    {Assert.True(r.IsSuccessStatusCode,$"HTTP {(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");return(await r.Content.ReadFromJsonAsync<T>(PostgresFixture.Json))!;}
    [PostgresFact,Trait("Requerimiento","RF-CAT-05"),Trait("Requerimiento","RF-CAT-06")]
    public async Task Vender_combo_con_modificadores_idempotencia_snapshot_reporte_y_disponibilidad()
    {
        using var admin=await f.Admin();var cat=await Leer<Categoria>(await admin.PostAsJsonAsync("/api/categorias",new DatosCategoria("Menú",0)));
        async Task<Producto> Producto(string nombre,decimal precio,bool combo=false)=>await Leer<Producto>(await admin.PostAsJsonAsync("/api/productos",new DatosProducto(cat.Id,nombre,precio,13,0,EsCombo:combo)));
        var pollo=await Producto("Pollo",1000);var bebida=await Producto("Bebida",500);var combo=await Producto("Combo",4500,true);
        var g=await Leer<GrupoModificador>(await admin.PostAsJsonAsync("/api/modificadores/grupos",new DatosGrupo("Extra",1,1)));
        var opcion=await Leer<OpcionModificador>(await admin.PostAsJsonAsync($"/api/modificadores/grupos/{g.Id}/opciones",new DatosOpcion("Papas",800)));
        (await admin.PutAsJsonAsync($"/api/productos/{combo.Id}/grupos-modificadores",new[]{new AsignarGrupo(g.Id,0)})).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/productos/{combo.Id}/combo",new GuardarCombo([new(null,"Pollo",TipoComponente.Fijo,2,pollo.Id,0,[]),new(null,"Refresco",TipoComponente.Elegible,1,null,1,[new(bebida.Id,600)])]))).EnsureSuccessStatusCode();
        var comps=await Leer<List<ComponenteCombo>>(await admin.GetAsync($"/api/productos/{combo.Id}/combo"));var elegible=comps.Single(c=>c.Tipo==TipoComponente.Elegible);
        var u=await f.CrearCajero(admin);using var cajero=await f.Cajero(u.Id);var sesion=await Leer<SesionCaja>(await cajero.PostAsJsonAsync("/api/caja/abrir",new AbrirCaja(0)));
        try
        {
            Assert.Equal(HttpStatusCode.Forbidden,(await cajero.GetAsync("/api/modificadores/grupos")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await cajero.GetAsync($"/api/productos/{combo.Id}/combo")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await cajero.PutAsJsonAsync($"/api/productos/{combo.Id}/grupos-modificadores",Array.Empty<AsignarGrupo>())).StatusCode);
            var d=new CobrarVenta(Guid.CreateVersion7(),sesion.Id,TipoOrden.ComerAqui,[new(combo.Id,2,Modificadores:[opcion.Id],Componentes:[new(elegible.Id,bebida.Id,1)])],[new(MetodoPago.Efectivo,11800,12000)]);
            var v=await Leer<VentaDto>(await cajero.PostAsJsonAsync("/api/ventas",d));Assert.Equal(11800,v.Total);Assert.Equal(5900,Assert.Single(v.Lineas).PrecioUnitario);
            Assert.Equal(v.Id,(await Leer<VentaDto>(await cajero.PostAsJsonAsync("/api/ventas",d))).Id);
            Assert.Equal(HttpStatusCode.Conflict,(await cajero.PostAsJsonAsync("/api/ventas",d with{Lineas=[d.Lineas[0] with{Modificadores=[]}]})).StatusCode);
            (await admin.PutAsJsonAsync($"/api/modificadores/grupos/{g.Id}/opciones/{opcion.Id}",new DatosOpcion("Renombrado",900))).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Conflict,(await cajero.PostAsJsonAsync("/api/ventas",d with{Lineas=[d.Lineas[0] with{Componentes=[new(elegible.Id,bebida.Id,2)]}]})).StatusCode);
            (await admin.PutAsJsonAsync($"/api/productos/{combo.Id}/combo",new GuardarCombo(comps.Select(c=>new DatosComponente(c.Id,c.Nombre,c.Tipo,c.Cantidad,c.ProductoFijoId,c.Orden,c.Opciones.Where(o=>o.Activo).Select(o=>new DatosAlternativa(o.ProductoId,1000,o.Orden)).ToList())).ToList()))).EnsureSuccessStatusCode();
            var guardada=await Leer<VentaDto>(await cajero.GetAsync($"/api/ventas/{v.Id}"));Assert.Equal("Papas",Assert.Single(Assert.Single(guardada.Lineas).Modificadores).NombreOpcion);
            Assert.Equal(5900,Assert.Single(guardada.Lineas).PrecioUnitario);Assert.Equal(600,Assert.Single(guardada.Lineas).Componentes.Single(c=>c.ProductoId==bebida.Id).RecargoUnitario);
            var reporte=await Leer<ReporteCaja>(await cajero.GetAsync($"/api/caja/{sesion.Id}/reporte"));Assert.Equal(2,Assert.Single(Assert.Single(reporte.DetalleVentas!).Lineas).Componentes.Count);
            (await admin.PostAsync($"/api/productos/{pollo.Id}/desactivar",null)).EnsureSuccessStatusCode();
            var catalogo=await Leer<CatalogoDto>(await cajero.GetAsync("/api/caja/catalogo"));Assert.DoesNotContain(catalogo.Productos,p=>p.Id==combo.Id);
            Assert.Equal(HttpStatusCode.BadRequest,(await cajero.PostAsJsonAsync("/api/ventas/cotizar",new CotizarVenta(d.TipoOrden,d.Lineas))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,(await cajero.PostAsJsonAsync("/api/ventas",d with{Id=Guid.CreateVersion7()})).StatusCode);
            using var scope=f.Api.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<PollotikitoDbContext>();Assert.True(await db.Bitacoras.AnyAsync(b=>b.NombreEntidad==nameof(OpcionModificador)&&b.EntidadId==opcion.Id&&b.ValorAnterior!=null));
        }
        finally{(await admin.PostAsJsonAsync($"/api/caja/{sesion.Id}/forzar-cierre",new CerrarCaja(null,"Fin prueba"))).EnsureSuccessStatusCode();}
    }
    [PostgresFact,Trait("Requerimiento","RF-CAT-05"),Trait("Requerimiento","RF-CAT-06")]
    public async Task Limites_anidacion_ambos_sentidos_y_minimo_insatisfecho()
    {
        using var a=await f.Admin();var cat=await Leer<Categoria>(await a.PostAsJsonAsync("/api/categorias",new DatosCategoria("Pruebas",0)));
        async Task<Producto> Crear(bool combo)=>await Leer<Producto>(await a.PostAsJsonAsync("/api/productos",new DatosProducto(cat.Id,"Producto",100,13,0,EsCombo:combo)));
        var p=await Crear(false);var combo=await Crear(true);var otro=await Crear(true);
        foreach(var limites in new[]{new DatosGrupo("Inválido",-1,1),new DatosGrupo("Inválido",2,1),new DatosGrupo("Inválido",0,0)})Assert.Equal(HttpStatusCode.BadRequest,(await a.PostAsJsonAsync("/api/modificadores/grupos",limites)).StatusCode);
        GuardarCombo Fijo(Guid id)=>new([new(null,"Fijo",TipoComponente.Fijo,1,id,0,[])]);
        Assert.Equal(HttpStatusCode.BadRequest,(await a.PutAsJsonAsync($"/api/productos/{combo.Id}/combo",Fijo(otro.Id))).StatusCode);
        (await a.PutAsJsonAsync($"/api/productos/{combo.Id}/combo",Fijo(p.Id))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest,(await a.PutAsJsonAsync($"/api/productos/{p.Id}",new DatosProducto(cat.Id,"Producto",100,13,0,EsCombo:true))).StatusCode);
        var g=await Leer<GrupoModificador>(await a.PostAsJsonAsync("/api/modificadores/grupos",new DatosGrupo("Obligatorio",1,1)));
        (await a.PutAsJsonAsync($"/api/productos/{p.Id}/grupos-modificadores",new[]{new AsignarGrupo(g.Id,0)})).EnsureSuccessStatusCode();
        var productos=await Leer<List<Producto>>(await a.GetAsync("/api/productos"));Assert.False(productos.Single(x=>x.Id==p.Id).Disponible);Assert.Contains(productos.Single(x=>x.Id==p.Id).Advertencias,x=>x.Contains("Obligatorio"));
    }
}
