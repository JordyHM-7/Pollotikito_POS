using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Pollotikito.Infrastructure.Persistence.Migrations;
[DbContext(typeof(PollotikitoDbContext))]
[Migration("20261023000000_ProteccionesMenu")]
public sealed class ProteccionesMenu : Migration
{
    protected override void Up(MigrationBuilder m)=>m.Sql("""
        CREATE FUNCTION validar_componentes_simples() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_TABLE_NAME = 'productos' THEN
            IF NEW.es_combo AND EXISTS (
              SELECT 1 FROM componentes_combo c LEFT JOIN opciones_componentes_combo o ON o.componente_combo_id=c.id AND o.activo
              WHERE c.activo AND (c.producto_fijo_id=NEW.id OR o.producto_id=NEW.id)
            ) THEN RAISE EXCEPTION 'Un componente no puede convertirse en combo'; END IF;
          ELSIF TG_TABLE_NAME = 'componentes_combo' THEN
            IF NEW.activo AND (NOT EXISTS(SELECT 1 FROM productos WHERE id=NEW.producto_id AND es_combo)
              OR NEW.producto_id=NEW.producto_fijo_id
              OR EXISTS(SELECT 1 FROM productos WHERE id=NEW.producto_fijo_id AND es_combo))
              THEN RAISE EXCEPTION 'No se permiten combos anidados'; END IF;
          ELSE
            IF NEW.activo AND EXISTS(SELECT 1 FROM productos WHERE id=NEW.producto_id AND es_combo)
              THEN RAISE EXCEPTION 'No se permiten alternativas de tipo combo'; END IF;
          END IF;
          RETURN NEW;
        END; $$;
        CREATE TRIGGER producto_combo_no_anidado BEFORE UPDATE OF es_combo ON productos FOR EACH ROW EXECUTE FUNCTION validar_componentes_simples();
        CREATE TRIGGER componente_no_anidado BEFORE INSERT OR UPDATE ON componentes_combo FOR EACH ROW EXECUTE FUNCTION validar_componentes_simples();
        CREATE TRIGGER alternativa_no_anidada BEFORE INSERT OR UPDATE ON opciones_componentes_combo FOR EACH ROW EXECUTE FUNCTION validar_componentes_simples();
        CREATE FUNCTION proteger_detalle_linea() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE sesion uuid; cerrada timestamptz;
        BEGIN
          IF TG_OP <> 'INSERT' THEN RAISE EXCEPTION 'Las instantáneas de elecciones son inmutables'; END IF;
          SELECT v.sesion_caja_id INTO sesion FROM lineas_venta l JOIN ventas v ON v.id=l.venta_id WHERE l.id=NEW.linea_venta_id;
          SELECT cerrada_en INTO cerrada FROM sesiones_caja WHERE id=sesion FOR UPDATE;
          IF cerrada IS NOT NULL THEN RAISE EXCEPTION 'La sesión está cerrada'; END IF;
          RETURN NEW;
        END; $$;
        CREATE TRIGGER modificador_vendido_inmutable BEFORE INSERT OR UPDATE OR DELETE ON modificadores_linea_venta FOR EACH ROW EXECUTE FUNCTION proteger_detalle_linea();
        CREATE TRIGGER componente_vendido_inmutable BEFORE INSERT OR UPDATE OR DELETE ON componentes_linea_venta FOR EACH ROW EXECUTE FUNCTION proteger_detalle_linea();
        """);
    protected override void Down(MigrationBuilder m)=>m.Sql("""
        DROP TRIGGER componente_vendido_inmutable ON componentes_linea_venta;
        DROP TRIGGER modificador_vendido_inmutable ON modificadores_linea_venta;
        DROP FUNCTION proteger_detalle_linea();
        DROP TRIGGER alternativa_no_anidada ON opciones_componentes_combo;
        DROP TRIGGER componente_no_anidado ON componentes_combo;
        DROP TRIGGER producto_combo_no_anidado ON productos;
        DROP FUNCTION validar_componentes_simples();
        """);
}
