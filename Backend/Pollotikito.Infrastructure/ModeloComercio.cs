using Microsoft.EntityFrameworkCore;
using Pollotikito.Domain;

namespace Pollotikito.Infrastructure;

internal static class ModeloComercio
{
    public static void Configurar(ModelBuilder b)
    {
        b.Entity<Categoria>().ToTable("categorias");
        b.Entity<Categoria>().Property(x => x.Nombre).HasMaxLength(120);
        b.Entity<Producto>().ToTable("productos", t => { t.HasCheckConstraint("ck_producto_precio", "precio >= 0"); t.HasCheckConstraint("ck_producto_iva", "tarifa_iva >= 0 AND tarifa_iva <= 100"); });
        b.Entity<Producto>().Property(x => x.Nombre).HasMaxLength(150);
        b.Entity<Producto>().Property(x => x.TipoDescuento).HasConversion<string>();
        b.Entity<Producto>().HasOne<Categoria>().WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SesionCaja>().ToTable("sesiones_caja", t => {
            t.HasCheckConstraint("ck_caja_inicial", "monto_inicial >= 0");
            t.HasCheckConstraint("ck_cierre_conteo", "(cerrada_en IS NULL AND NOT cerrada_sin_conteo AND contado IS NULL AND diferencia IS NULL AND esperado IS NULL) OR (cerrada_en IS NOT NULL AND esperado IS NOT NULL AND ((cerrada_sin_conteo AND contado IS NULL AND diferencia IS NULL AND motivo_cierre_forzado IS NOT NULL) OR (NOT cerrada_sin_conteo AND contado IS NOT NULL AND diferencia IS NOT NULL)))");
        });
        b.Entity<SesionCaja>().HasIndex(x => x.SucursalId).IsUnique().HasFilter("cerrada_en IS NULL");
        b.Entity<SesionCaja>().HasOne<Usuario>().WithMany().HasForeignKey(x => x.CajeroId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SesionCaja>().HasMany(x => x.Conteo).WithOne().HasForeignKey(x => x.SesionCajaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ConteoDenominacion>().ToTable("conteos_denominacion", t => {
            t.HasCheckConstraint("ck_conteo_cantidad", "cantidad >= 0");
            t.HasCheckConstraint("ck_denominacion", "denominacion IN (5,10,25,50,100,500,1000,2000,5000,10000,20000)");
        });
        b.Entity<ConteoDenominacion>().HasIndex(x => new { x.SesionCajaId, x.Denominacion }).IsUnique();
        b.Entity<MovimientoCaja>().ToTable("movimientos_caja", t => t.HasCheckConstraint("ck_movimiento_monto", "monto > 0"));
        b.Entity<MovimientoCaja>().Property(x => x.Tipo).HasConversion<string>();
        b.Entity<MovimientoCaja>().HasOne<SesionCaja>().WithMany().HasForeignKey(x => x.SesionCajaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Venta>().ToTable("ventas", t => { t.HasCheckConstraint("ck_venta_total", "total >= 0 AND base + impuesto = total AND subtotal - descuento = total"); });
        b.Entity<Venta>().Property(x => x.TipoOrden).HasConversion<string>(); b.Entity<Venta>().Property(x => x.Estado).HasConversion<string>();
        b.Entity<Venta>().Property(x => x.TipoDescuentoGlobal).HasConversion<string>();
        b.Entity<Venta>().HasIndex(x => new { x.SucursalId, x.Numero }).IsUnique();
        b.Entity<Venta>().HasOne<SesionCaja>().WithMany().HasForeignKey(x => x.SesionCajaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Venta>().HasMany(x => x.Lineas).WithOne().HasForeignKey(x => x.VentaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Venta>().HasMany(x => x.Pagos).WithOne().HasForeignKey(x => x.VentaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LineaVenta>().ToTable("lineas_venta", t => { t.HasCheckConstraint("ck_linea_importes", "total >= 0 AND base + impuesto = total AND cantidad > 0"); });
        b.Entity<LineaVenta>().Property(x => x.TipoDescuentoManual).HasConversion<string>();
        b.Entity<LineaVenta>().HasOne<Producto>().WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LineaVenta>().HasIndex(x => new { x.VentaId, x.Orden }).IsUnique();
        b.Entity<PagoVenta>().ToTable("pagos_venta", t => t.HasCheckConstraint("ck_pago_importes", "monto >= 0 AND vuelto >= 0 AND ((metodo = 'Efectivo' AND recibido >= monto AND recibido - vuelto = monto) OR (metodo <> 'Efectivo' AND recibido IS NULL AND vuelto = 0))"));
        b.Entity<PagoVenta>().Property(x => x.Metodo).HasConversion<string>();
        b.Entity<PagoVenta>().HasIndex(x => new { x.VentaId, x.Metodo }).IsUnique();
        foreach (var e in b.Model.GetEntityTypes().Where(e => e.ClrType == typeof(Producto) || e.ClrType == typeof(SesionCaja) || e.ClrType == typeof(MovimientoCaja) || e.ClrType == typeof(Venta) || e.ClrType == typeof(LineaVenta) || e.ClrType == typeof(PagoVenta)))
            foreach (var p in e.GetProperties().Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            { p.SetPrecision(18); p.SetScale(2); }
    }
}
