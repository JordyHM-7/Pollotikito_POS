using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Pollotikito.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PollotikitoDbContext))]
[Migration("20261022010000_ProteccionesCaja")]
public sealed class ProteccionesCaja : Migration
{
    protected override void Up(MigrationBuilder m) => m.Sql("""
        CREATE FUNCTION proteger_sesion_cerrada() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP = 'DELETE' OR OLD.cerrada_en IS NOT NULL THEN
            RAISE EXCEPTION 'La sesión no se puede borrar ni modificar después del cierre';
          END IF;
          RETURN NEW;
        END; $$;
        CREATE TRIGGER sesion_inmutable BEFORE UPDATE OR DELETE ON sesiones_caja
          FOR EACH ROW EXECUTE FUNCTION proteger_sesion_cerrada();

        CREATE FUNCTION proteger_operacion_caja() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE sesion uuid; cerrada timestamptz;
        BEGIN
          IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Los movimientos y comprobantes no se borran'; END IF;
          IF TG_TABLE_NAME IN ('ventas', 'movimientos_caja', 'conteos_denominacion') THEN
            sesion := NEW.sesion_caja_id;
            IF TG_OP = 'UPDATE' AND OLD.sesion_caja_id <> NEW.sesion_caja_id THEN
              RAISE EXCEPTION 'No se puede mover una operación entre sesiones';
            END IF;
          ELSE
            SELECT sesion_caja_id INTO sesion FROM ventas WHERE id = NEW.venta_id;
            IF TG_OP = 'UPDATE' AND OLD.venta_id <> NEW.venta_id THEN
              RAISE EXCEPTION 'No se puede mover una línea o pago entre ventas';
            END IF;
          END IF;
          SELECT cerrada_en INTO cerrada FROM sesiones_caja WHERE id = sesion FOR UPDATE;
          IF cerrada IS NOT NULL THEN RAISE EXCEPTION 'La sesión está cerrada'; END IF;
          RETURN NEW;
        END; $$;
        CREATE TRIGGER venta_caja_abierta BEFORE INSERT OR UPDATE OR DELETE ON ventas
          FOR EACH ROW EXECUTE FUNCTION proteger_operacion_caja();
        CREATE TRIGGER movimiento_caja_abierta BEFORE INSERT OR UPDATE OR DELETE ON movimientos_caja
          FOR EACH ROW EXECUTE FUNCTION proteger_operacion_caja();
        CREATE TRIGGER conteo_caja_abierta BEFORE INSERT OR UPDATE OR DELETE ON conteos_denominacion
          FOR EACH ROW EXECUTE FUNCTION proteger_operacion_caja();
        CREATE TRIGGER linea_caja_abierta BEFORE INSERT OR UPDATE OR DELETE ON lineas_venta
          FOR EACH ROW EXECUTE FUNCTION proteger_operacion_caja();
        CREATE TRIGGER pago_caja_abierta BEFORE INSERT OR UPDATE OR DELETE ON pagos_venta
          FOR EACH ROW EXECUTE FUNCTION proteger_operacion_caja();
        CREATE FUNCTION no_borrar_catalogo() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'El catálogo solo se desactiva'; END; $$;
        CREATE TRIGGER producto_no_borrar BEFORE DELETE OR TRUNCATE ON productos FOR EACH STATEMENT EXECUTE FUNCTION no_borrar_catalogo();
        CREATE TRIGGER categoria_no_borrar BEFORE DELETE OR TRUNCATE ON categorias FOR EACH STATEMENT EXECUTE FUNCTION no_borrar_catalogo();
        """);
    protected override void Down(MigrationBuilder m) => m.Sql("""
        DROP TRIGGER categoria_no_borrar ON categorias;
        DROP TRIGGER producto_no_borrar ON productos;
        DROP FUNCTION no_borrar_catalogo();
        DROP TRIGGER pago_caja_abierta ON pagos_venta;
        DROP TRIGGER linea_caja_abierta ON lineas_venta;
        DROP TRIGGER conteo_caja_abierta ON conteos_denominacion;
        DROP TRIGGER movimiento_caja_abierta ON movimientos_caja;
        DROP TRIGGER venta_caja_abierta ON ventas;
        DROP FUNCTION proteger_operacion_caja();
        DROP TRIGGER sesion_inmutable ON sesiones_caja;
        DROP FUNCTION proteger_sesion_cerrada();
        """);
}
