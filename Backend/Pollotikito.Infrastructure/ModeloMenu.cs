using Microsoft.EntityFrameworkCore;
using Pollotikito.Domain;
namespace Pollotikito.Infrastructure;

internal static class ModeloMenu
{
    public static void Configurar(ModelBuilder b)
    {
        b.Entity<Producto>().Ignore(p=>p.Disponible).Ignore(p=>p.Advertencias);
        b.Entity<GrupoModificador>().ToTable("grupos_modificadores",t=>t.HasCheckConstraint("ck_grupo_limites","minimo >= 0 AND minimo <= maximo AND maximo >= 1"));
        b.Entity<GrupoModificador>().HasMany(g=>g.Opciones).WithOne().HasForeignKey(o=>o.GrupoModificadorId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<OpcionModificador>().ToTable("opciones_modificadores",t=>t.HasCheckConstraint("ck_opcion_precio","precio_adicional >= 0"));
        b.Entity<ProductoGrupoModificador>().ToTable("productos_grupos_modificadores");
        b.Entity<ProductoGrupoModificador>().HasIndex(a=>new{a.ProductoId,a.GrupoModificadorId}).IsUnique();
        b.Entity<ProductoGrupoModificador>().HasOne<Producto>().WithMany().HasForeignKey(a=>a.ProductoId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ProductoGrupoModificador>().HasOne<GrupoModificador>().WithMany().HasForeignKey(a=>a.GrupoModificadorId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ComponenteCombo>().ToTable("componentes_combo",t=>{
            t.HasCheckConstraint("ck_componente_cantidad","cantidad > 0");
            t.HasCheckConstraint("ck_componente_tipo","(tipo = 'Fijo' AND producto_fijo_id IS NOT NULL) OR (tipo = 'Elegible' AND producto_fijo_id IS NULL)");
        });
        b.Entity<ComponenteCombo>().Property(c=>c.Tipo).HasConversion<string>();
        b.Entity<ComponenteCombo>().HasOne<Producto>().WithMany().HasForeignKey(c=>c.ProductoId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ComponenteCombo>().HasOne<Producto>().WithMany().HasForeignKey(c=>c.ProductoFijoId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ComponenteCombo>().HasMany(c=>c.Opciones).WithOne().HasForeignKey(o=>o.ComponenteComboId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<OpcionComponenteCombo>().ToTable("opciones_componentes_combo",t=>t.HasCheckConstraint("ck_recargo","recargo >= 0"));
        b.Entity<OpcionComponenteCombo>().HasOne<Producto>().WithMany().HasForeignKey(o=>o.ProductoId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<OpcionComponenteCombo>().HasIndex(o=>new{o.ComponenteComboId,o.ProductoId}).IsUnique();
        b.Entity<ModificadorLineaVenta>().ToTable("modificadores_linea_venta");
        b.Entity<LineaVenta>().HasMany(l=>l.Modificadores).WithOne().HasForeignKey(m=>m.LineaVentaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ModificadorLineaVenta>().HasIndex(m=>new{m.LineaVentaId,m.OpcionModificadorId}).IsUnique();
        b.Entity<ModificadorLineaVenta>().HasOne<GrupoModificador>().WithMany().HasForeignKey(m=>m.GrupoModificadorId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ModificadorLineaVenta>().HasOne<OpcionModificador>().WithMany().HasForeignKey(m=>m.OpcionModificadorId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ComponenteLineaVenta>().ToTable("componentes_linea_venta",t=>t.HasCheckConstraint("ck_componente_vendido","cantidad_por_unidad > 0 AND recargo_unitario >= 0"));
        b.Entity<LineaVenta>().HasMany(l=>l.Componentes).WithOne().HasForeignKey(c=>c.LineaVentaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ComponenteLineaVenta>().HasOne<Producto>().WithMany().HasForeignKey(c=>c.ProductoId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ComponenteLineaVenta>().HasOne<ComponenteCombo>().WithMany().HasForeignKey(c=>c.ComponenteComboId).OnDelete(DeleteBehavior.Restrict);
        foreach(var e in b.Model.GetEntityTypes().Where(e=>new[]{typeof(OpcionModificador),typeof(OpcionComponenteCombo),typeof(ModificadorLineaVenta),typeof(ComponenteLineaVenta)}.Contains(e.ClrType)))
            foreach(var p in e.GetProperties().Where(p=>p.ClrType==typeof(decimal))) {p.SetPrecision(18);p.SetScale(2);}
    }
}
