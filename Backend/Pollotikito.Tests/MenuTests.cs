using Pollotikito.Application;
using Pollotikito.Domain;

namespace Pollotikito.Tests;

public sealed class MenuTests
{
    private static readonly DateTimeOffset Ahora=DateTimeOffset.UtcNow;
    private static (Producto P,GrupoModificador G,DefinicionMenu M) Preparar(int minimo=1,int maximo=1)
    {
        var p=new Producto{Precio=1000};var g=new GrupoModificador{Nombre="Extras",Minimo=minimo,Maximo=maximo};
        g.Opciones.Add(new(){GrupoModificadorId=g.Id,Nombre="Queso",PrecioAdicional=500});
        return(p,g,new([g],[new(){ProductoId=p.Id,GrupoModificadorId=g.Id}],[]));
    }
    [Fact,Trait("Requerimiento","RF-CAT-05")]
    public void Rechaza_omision_grupo_obligatorio_y_opcion_ajena()
    {
        var(p,_,m)=Preparar();var ps=new Dictionary<Guid,Producto>{{p.Id,p}};
        Assert.Throws<ErrorAplicacion>(()=>ReglasMenu.Elegir(new(p.Id,1),p,ps,m));
        Assert.Throws<ErrorAplicacion>(()=>ReglasMenu.Elegir(new(p.Id,1,Modificadores:[Guid.CreateVersion7()]),p,ps,m));
    }
    [Fact,Trait("Requerimiento","RF-CAT-05")]
    public void Opcion_no_se_puede_repetir_aunque_maximo_permita_dos()
    {
        var(p,g,m)=Preparar(0,2);var id=g.Opciones[0].Id;
        Assert.Throws<ErrorAplicacion>(()=>ReglasMenu.Elegir(new(p.Id,1,Modificadores:[id,id]),p,new Dictionary<Guid,Producto>{{p.Id,p}},m));
    }
    [Fact,Trait("Requerimiento","RF-CAT-05")]
    public void Adicional_se_suma_antes_de_descuentos_y_se_guarda_nombre_y_precio()
    {
        var(p,g,m)=Preparar();p.TipoDescuento=TipoDescuento.Porcentaje;p.ValorDescuento=10;
        var l=Assert.Single(CalculoVenta.Lineas([new(p.Id,2,Modificadores:[g.Opciones[0].Id])],new Dictionary<Guid,Producto>{{p.Id,p}},null,Ahora,m));
        Assert.Equal(1500,l.PrecioUnitario);Assert.Equal(300,l.DescuentoAutomatico);Assert.Equal(2700,l.Total);
        var mod=Assert.Single(l.Modificadores);Assert.Equal("Queso",mod.NombreOpcion);Assert.Equal(500,mod.PrecioAdicional);Assert.Equal(l.Id,mod.LineaVentaId);
        g.Opciones[0].Nombre="Otro";g.Opciones[0].PrecioAdicional=800;Assert.Equal("Queso",mod.NombreOpcion);Assert.Equal(500,mod.PrecioAdicional);
    }
    [Fact,Trait("Requerimiento","RF-CAT-05")]
    public void No_disponible_si_grupo_obligatorio_sin_opciones_suficientes()
    {
        var(p,g,m)=Preparar(2,3);var c=new Categoria();p.CategoriaId=c.Id;
        ReglasMenu.Disponibilidad([p],[c],m);Assert.False(p.Disponible);Assert.Contains(p.Advertencias,a=>a.Contains("Extras"));
        g.Minimo=1;ReglasMenu.Disponibilidad([p],[c],m);Assert.True(p.Disponible);
        g.Activo=false;ReglasMenu.Disponibilidad([p],[c],m);Assert.False(p.Disponible);
    }
    [Theory,Trait("Requerimiento","RF-CAT-06")]
    [InlineData(false,true)] [InlineData(true,false)]
    public void Combo_no_disponible_si_fijo_inactivo_o_elegible_sin_alternativas(bool fijoActivo,bool alternativaActiva)
    {
        var cat=new Categoria();var combo=new Producto{CategoriaId=cat.Id,EsCombo=true};var fijo=new Producto{CategoriaId=cat.Id,Activo=fijoActivo};var bebida=new Producto{CategoriaId=cat.Id,Activo=alternativaActiva};
        var m=new DefinicionMenu([],[],[new(){ProductoId=combo.Id,Nombre="Pollo",Tipo=TipoComponente.Fijo,ProductoFijoId=fijo.Id},new(){ProductoId=combo.Id,Nombre="Bebida",Tipo=TipoComponente.Elegible,Opciones=[new(){ProductoId=bebida.Id}]}]);
        ReglasMenu.Disponibilidad([combo,fijo,bebida],[cat],m);Assert.False(combo.Disponible);Assert.NotEmpty(combo.Advertencias);
    }
    [Fact,Trait("Requerimiento","RF-CAT-06")]
    public void Snapshot_componentes_guarda_cantidad_por_unidad_y_recargo_para_inventario()
    {
        var combo=new Producto{Precio=4500,EsCombo=true};var pollo=new Producto{Nombre="Pieza"};var bebida=new Producto{Nombre="1 litro"};
        var fijo=new ComponenteCombo{ProductoId=combo.Id,Nombre="Pollo",Tipo=TipoComponente.Fijo,ProductoFijoId=pollo.Id,Cantidad=2};
        var elegible=new ComponenteCombo{ProductoId=combo.Id,Nombre="Refresco",Tipo=TipoComponente.Elegible,Cantidad=1,Opciones=[new(){ProductoId=bebida.Id,Recargo=600}]};
        var m=new DefinicionMenu([],[],[fijo,elegible],[pollo,bebida]);var solicitud=new LineaSolicitud(combo.Id,3,Componentes:[new(elegible.Id,bebida.Id,1)]);
        var l=Assert.Single(CalculoVenta.Lineas([solicitud],new Dictionary<Guid,Producto>{{combo.Id,combo}},null,Ahora,m));
        Assert.Equal(5100,l.PrecioUnitario);Assert.Equal(15300,l.Total);Assert.Equal(6,l.Componentes.Single(c=>c.ProductoId==pollo.Id).CantidadPorUnidad*l.Cantidad);Assert.Equal(600,l.Componentes.Single(c=>c.ProductoId==bebida.Id).RecargoUnitario);
        Assert.Throws<ErrorAplicacion>(()=>ReglasMenu.Elegir(solicitud with{Componentes=[]},combo,new Dictionary<Guid,Producto>(),m));
    }
    [Fact,Trait("Requerimiento","RF-CAT-05")]
    public void Maximo_y_opciones_inactivas_se_validan()
    {
        var(p,g,m)=Preparar();var o=new OpcionModificador{GrupoModificadorId=g.Id};g.Opciones.Add(o);
        var ps=new Dictionary<Guid,Producto>{{p.Id,p}};Assert.Throws<ErrorAplicacion>(()=>ReglasMenu.Elegir(new(p.Id,1,Modificadores:g.Opciones.Select(o=>o.Id).ToList()),p,ps,m));
        o.Activo=false;Assert.Throws<ErrorAplicacion>(()=>ReglasMenu.Elegir(new(p.Id,1,Modificadores:[o.Id]),p,ps,m));
    }
}
