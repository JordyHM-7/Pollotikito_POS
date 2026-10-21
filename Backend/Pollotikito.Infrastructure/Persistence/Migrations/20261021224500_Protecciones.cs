using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Pollotikito.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PollotikitoDbContext))]
[Migration("20261021224500_Protecciones")]
public sealed class Protecciones : Migration
{
    protected override void Up(MigrationBuilder m) => m.Sql("""
        CREATE FUNCTION impedir_cambio_bitacora() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'La bitácora es de solo inserción'; END; $$;
        CREATE TRIGGER bitacora_inmutable BEFORE UPDATE OR DELETE OR TRUNCATE ON bitacora
        FOR EACH STATEMENT EXECUTE FUNCTION impedir_cambio_bitacora();
        CREATE FUNCTION impedir_borrado_usuario() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'Los usuarios solo se desactivan'; END; $$;
        CREATE TRIGGER usuarios_no_borrar BEFORE DELETE OR TRUNCATE ON usuarios
        FOR EACH STATEMENT EXECUTE FUNCTION impedir_borrado_usuario();
        CREATE FUNCTION consecutivo_solo_sube() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF NEW.siguiente_numero < OLD.siguiente_numero THEN
            RAISE EXCEPTION 'El consecutivo no puede bajar';
          END IF;
          RETURN NEW;
        END; $$;
        CREATE TRIGGER consecutivo_monotono BEFORE UPDATE ON consecutivos
        FOR EACH ROW EXECUTE FUNCTION consecutivo_solo_sube();
        """);
    protected override void Down(MigrationBuilder m) => m.Sql("""
        DROP TRIGGER consecutivo_monotono ON consecutivos;
        DROP FUNCTION consecutivo_solo_sube();
        DROP TRIGGER usuarios_no_borrar ON usuarios;
        DROP FUNCTION impedir_borrado_usuario();
        DROP TRIGGER bitacora_inmutable ON bitacora;
        DROP FUNCTION impedir_cambio_bitacora();
        """);
}
