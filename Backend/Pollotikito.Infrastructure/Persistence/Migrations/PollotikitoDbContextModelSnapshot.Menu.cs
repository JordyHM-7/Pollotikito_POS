using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Pollotikito.Infrastructure.Persistence.Migrations;

partial class PollotikitoDbContextModelSnapshot
{
    // Instantánea congelada del Incremento 3; no llama al modelo de producción.
    private static void AgregarMenuIncremento3(ModelBuilder m)
    {
        m.Entity("Pollotikito.Domain.Producto").Property<bool>("EsCombo").HasColumnType("boolean").HasColumnName("es_combo");
        var g=EntidadMenu(m,"GrupoModificador","grupos_modificadores");Texto(g,"Nombre","nombre");Entero(g,"Minimo","minimo");Entero(g,"Maximo","maximo");Activo(g);Orden(g);g.ToTable("grupos_modificadores",t=>t.HasCheckConstraint("ck_grupo_limites","minimo >= 0 AND minimo <= maximo AND maximo >= 1"));
        var o=EntidadMenu(m,"OpcionModificador","opciones_modificadores");Texto(o,"Nombre","nombre");Dinero(o,"PrecioAdicional","precio_adicional");Activo(o);Orden(o);Fk(o,"GrupoModificadorId","grupo_modificador_id","GrupoModificador","Opciones");o.ToTable("opciones_modificadores",t=>t.HasCheckConstraint("ck_opcion_precio","precio_adicional >= 0"));
        var a=EntidadMenu(m,"ProductoGrupoModificador","productos_grupos_modificadores");Activo(a);Orden(a);Fk(a,"ProductoId","producto_id","Producto");Fk(a,"GrupoModificadorId","grupo_modificador_id","GrupoModificador");a.HasIndex("ProductoId","GrupoModificadorId").IsUnique();a.Metadata.RemoveIndex(a.Metadata.FindIndex(a.Metadata.FindProperty("ProductoId")!)!);
        var c=EntidadMenu(m,"ComponenteCombo","componentes_combo");Texto(c,"Nombre","nombre");Texto(c,"Tipo","tipo");Entero(c,"Cantidad","cantidad");Activo(c);Orden(c);Fk(c,"ProductoId","producto_id","Producto");c.Property<Guid?>("ProductoFijoId").HasColumnType("uuid").HasColumnName("producto_fijo_id");c.HasIndex("ProductoFijoId");c.HasOne("Pollotikito.Domain.Producto",null).WithMany().HasForeignKey("ProductoFijoId").OnDelete(DeleteBehavior.Restrict);c.ToTable("componentes_combo",t=>{t.HasCheckConstraint("ck_componente_cantidad","cantidad > 0");t.HasCheckConstraint("ck_componente_tipo","(tipo = 'Fijo' AND producto_fijo_id IS NOT NULL) OR (tipo = 'Elegible' AND producto_fijo_id IS NULL)");});
        var alt=EntidadMenu(m,"OpcionComponenteCombo","opciones_componentes_combo");Dinero(alt,"Recargo","recargo");Orden(alt);Activo(alt);Fk(alt,"ComponenteComboId","componente_combo_id","ComponenteCombo","Opciones");Fk(alt,"ProductoId","producto_id","Producto");alt.HasIndex("ComponenteComboId","ProductoId").IsUnique();alt.Metadata.RemoveIndex(alt.Metadata.FindIndex(alt.Metadata.FindProperty("ComponenteComboId")!)!);alt.ToTable("opciones_componentes_combo",t=>t.HasCheckConstraint("ck_recargo","recargo >= 0"));
        var mod=EntidadMenu(m,"ModificadorLineaVenta","modificadores_linea_venta");Texto(mod,"NombreGrupo","nombre_grupo");Texto(mod,"NombreOpcion","nombre_opcion");Dinero(mod,"PrecioAdicional","precio_adicional");Orden(mod);Fk(mod,"LineaVentaId","linea_venta_id","LineaVenta","Modificadores");Fk(mod,"GrupoModificadorId","grupo_modificador_id","GrupoModificador");Fk(mod,"OpcionModificadorId","opcion_modificador_id","OpcionModificador");mod.HasIndex("LineaVentaId","OpcionModificadorId").IsUnique();mod.Metadata.RemoveIndex(mod.Metadata.FindIndex(mod.Metadata.FindProperty("LineaVentaId")!)!);
        var cv=EntidadMenu(m,"ComponenteLineaVenta","componentes_linea_venta");Texto(cv,"NombreComponente","nombre_componente");Texto(cv,"NombreProducto","nombre_producto");Entero(cv,"CantidadPorUnidad","cantidad_por_unidad");Dinero(cv,"RecargoUnitario","recargo_unitario");Orden(cv);Fk(cv,"LineaVentaId","linea_venta_id","LineaVenta","Componentes");Fk(cv,"ComponenteComboId","componente_combo_id","ComponenteCombo");Fk(cv,"ProductoId","producto_id","Producto");cv.ToTable("componentes_linea_venta",t=>t.HasCheckConstraint("ck_componente_vendido","cantidad_por_unidad > 0 AND recargo_unitario >= 0"));
    }
    private static EntityTypeBuilder EntidadMenu(ModelBuilder m,string nombre,string tabla)
    {
        var b=m.Entity("Pollotikito.Domain."+nombre);b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid").HasColumnName("id");b.HasKey("Id");b.Property<Guid>("SucursalId").HasColumnType("uuid").HasColumnName("sucursal_id");b.Property<DateTimeOffset>("CreadoEn").HasColumnType("timestamp with time zone").HasColumnName("creado_en");b.Property<DateTimeOffset>("ActualizadoEn").HasColumnType("timestamp with time zone").HasColumnName("actualizado_en");b.ToTable(tabla);return b;
    }
    private static void Texto(EntityTypeBuilder b,string p,string columna)=>b.Property<string>(p).IsRequired().HasColumnType("text").HasColumnName(columna);
    private static void Entero(EntityTypeBuilder b,string p,string columna)=>b.Property<int>(p).HasColumnType("integer").HasColumnName(columna);
    private static void Dinero(EntityTypeBuilder b,string p,string columna)=>b.Property<decimal>(p).HasPrecision(18,2).HasColumnType("numeric(18,2)").HasColumnName(columna);
    private static void Activo(EntityTypeBuilder b)=>b.Property<bool>("Activo").HasColumnType("boolean").HasColumnName("activo");
    private static void Orden(EntityTypeBuilder b)=>Entero(b,"Orden","orden");
    private static void Fk(EntityTypeBuilder b,string propiedad,string columna,string destino,string? navegacion=null)
    {b.Property<Guid>(propiedad).HasColumnType("uuid").HasColumnName(columna);b.HasIndex(propiedad);b.HasOne("Pollotikito.Domain."+destino,null).WithMany(navegacion).HasForeignKey(propiedad).OnDelete(DeleteBehavior.Restrict).IsRequired();}
}
