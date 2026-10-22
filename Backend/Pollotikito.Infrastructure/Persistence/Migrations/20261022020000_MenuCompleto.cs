using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Pollotikito.Infrastructure.Persistence.Migrations;

// Migración SQL explícita: no requiere ejecutar EF para escribir el esquema.
[DbContext(typeof(PollotikitoDbContext))]
[Migration("20261022020000_MenuCompleto")]
public sealed class MenuCompleto : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<bool>("es_combo", "productos", type:"boolean",nullable:false,defaultValue:false);
        Crear(m,"grupos_modificadores", "nombre text NOT NULL, minimo integer NOT NULL, maximo integer NOT NULL, orden integer NOT NULL, activo boolean NOT NULL, CONSTRAINT ck_grupo_limites CHECK(minimo >= 0 AND minimo <= maximo AND maximo >= 1)");
        Crear(m,"opciones_modificadores", "grupo_modificador_id uuid NOT NULL REFERENCES grupos_modificadores(id) ON DELETE RESTRICT, nombre text NOT NULL, precio_adicional numeric(18,2) NOT NULL, orden integer NOT NULL, activo boolean NOT NULL, CONSTRAINT ck_opcion_precio CHECK(precio_adicional >= 0)");
        Crear(m,"productos_grupos_modificadores", "producto_id uuid NOT NULL REFERENCES productos(id) ON DELETE RESTRICT, grupo_modificador_id uuid NOT NULL REFERENCES grupos_modificadores(id) ON DELETE RESTRICT, orden integer NOT NULL, activo boolean NOT NULL");
        Crear(m,"componentes_combo", "producto_id uuid NOT NULL REFERENCES productos(id) ON DELETE RESTRICT, nombre text NOT NULL, tipo text NOT NULL, cantidad integer NOT NULL, producto_fijo_id uuid REFERENCES productos(id) ON DELETE RESTRICT, orden integer NOT NULL, activo boolean NOT NULL, CONSTRAINT ck_componente_cantidad CHECK(cantidad > 0), CONSTRAINT ck_componente_tipo CHECK((tipo = 'Fijo' AND producto_fijo_id IS NOT NULL) OR (tipo = 'Elegible' AND producto_fijo_id IS NULL))");
        Crear(m,"opciones_componentes_combo", "componente_combo_id uuid NOT NULL REFERENCES componentes_combo(id) ON DELETE RESTRICT, producto_id uuid NOT NULL REFERENCES productos(id) ON DELETE RESTRICT, recargo numeric(18,2) NOT NULL, orden integer NOT NULL, activo boolean NOT NULL, CONSTRAINT ck_recargo CHECK(recargo >= 0)");
        Crear(m,"modificadores_linea_venta", "linea_venta_id uuid NOT NULL REFERENCES lineas_venta(id) ON DELETE RESTRICT, grupo_modificador_id uuid NOT NULL REFERENCES grupos_modificadores(id) ON DELETE RESTRICT, opcion_modificador_id uuid NOT NULL REFERENCES opciones_modificadores(id) ON DELETE RESTRICT, nombre_grupo text NOT NULL, nombre_opcion text NOT NULL, precio_adicional numeric(18,2) NOT NULL, orden integer NOT NULL");
        Crear(m,"componentes_linea_venta", "linea_venta_id uuid NOT NULL REFERENCES lineas_venta(id) ON DELETE RESTRICT, componente_combo_id uuid NOT NULL REFERENCES componentes_combo(id) ON DELETE RESTRICT, producto_id uuid NOT NULL REFERENCES productos(id) ON DELETE RESTRICT, nombre_componente text NOT NULL, nombre_producto text NOT NULL, cantidad_por_unidad integer NOT NULL, recargo_unitario numeric(18,2) NOT NULL, orden integer NOT NULL, CONSTRAINT ck_componente_vendido CHECK(cantidad_por_unidad > 0 AND recargo_unitario >= 0)");
        Indice(m,"opciones_modificadores","grupo_modificador_id");
        Indice(m,"productos_grupos_modificadores","producto_id,grupo_modificador_id",true);Indice(m,"productos_grupos_modificadores","grupo_modificador_id");
        Indice(m,"componentes_combo","producto_id");Indice(m,"componentes_combo","producto_fijo_id");
        Indice(m,"opciones_componentes_combo","componente_combo_id,producto_id",true);Indice(m,"opciones_componentes_combo","producto_id");
        Indice(m,"modificadores_linea_venta","linea_venta_id,opcion_modificador_id",true);Indice(m,"modificadores_linea_venta","grupo_modificador_id");Indice(m,"modificadores_linea_venta","opcion_modificador_id");
        Indice(m,"componentes_linea_venta","linea_venta_id");Indice(m,"componentes_linea_venta","componente_combo_id");Indice(m,"componentes_linea_venta","producto_id");
    }
    private static void Crear(MigrationBuilder m,string tabla,string columnas) => m.Sql($"CREATE TABLE {tabla} (id uuid PRIMARY KEY, sucursal_id uuid NOT NULL, creado_en timestamp with time zone NOT NULL, actualizado_en timestamp with time zone NOT NULL, {columnas});");
    private static void Indice(MigrationBuilder m,string tabla,string columnas,bool unico=false) => m.Sql($"CREATE {(unico?"UNIQUE ":"")}INDEX ix_{tabla}_{columnas.Replace(",","_")} ON {tabla} ({columnas});");
    protected override void Down(MigrationBuilder m)
    {
        foreach(var tabla in new[]{"componentes_linea_venta","modificadores_linea_venta","opciones_componentes_combo","componentes_combo","productos_grupos_modificadores","opciones_modificadores","grupos_modificadores"})m.DropTable(tabla);
        m.DropColumn("es_combo","productos");
    }
}
