using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pollotikito.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VenderYCuadrarCaja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categorias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    sucursal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categorias", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sesiones_caja",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cajero_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_cajero = table.Column<string>(type: "text", nullable: false),
                    abierta_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    monto_inicial = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cerrada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cerrada_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo_cierre_forzado = table.Column<string>(type: "text", nullable: true),
                    cerrada_sin_conteo = table.Column<bool>(type: "boolean", nullable: false),
                    esperado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    contado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    diferencia = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    sucursal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sesiones_caja", x => x.id);
                    table.CheckConstraint("ck_caja_inicial", "monto_inicial >= 0");
                    table.CheckConstraint("ck_cierre_conteo", "(cerrada_en IS NULL AND NOT cerrada_sin_conteo AND contado IS NULL AND diferencia IS NULL AND esperado IS NULL) OR (cerrada_en IS NOT NULL AND esperado IS NOT NULL AND ((cerrada_sin_conteo AND contado IS NULL AND diferencia IS NULL AND motivo_cierre_forzado IS NOT NULL) OR (NOT cerrada_sin_conteo AND contado IS NOT NULL AND diferencia IS NOT NULL)))");
                    table.ForeignKey(
                        name: "FK_sesiones_caja_usuarios_cajero_id",
                        column: x => x.cajero_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "productos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    precio = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tarifa_iva = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    foto = table.Column<string>(type: "text", nullable: true),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    tipo_descuento = table.Column<string>(type: "text", nullable: false),
                    valor_descuento = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    descuento_desde = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    descuento_hasta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sucursal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productos", x => x.id);
                    table.CheckConstraint("ck_producto_iva", "tarifa_iva >= 0 AND tarifa_iva <= 100");
                    table.CheckConstraint("ck_producto_precio", "precio >= 0");
                    table.ForeignKey(
                        name: "FK_productos_categorias_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "categorias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "conteos_denominacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sesion_caja_id = table.Column<Guid>(type: "uuid", nullable: false),
                    denominacion = table.Column<int>(type: "integer", nullable: false),
                    cantidad = table.Column<int>(type: "integer", nullable: false),
                    sucursal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conteos_denominacion", x => x.id);
                    table.CheckConstraint("ck_conteo_cantidad", "cantidad >= 0");
                    table.CheckConstraint("ck_denominacion", "denominacion IN (5,10,25,50,100,500,1000,2000,5000,10000,20000)");
                    table.ForeignKey(
                        name: "FK_conteos_denominacion_sesiones_caja_sesion_caja_id",
                        column: x => x.sesion_caja_id,
                        principalTable: "sesiones_caja",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "movimientos_caja",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sesion_caja_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    motivo = table.Column<string>(type: "text", nullable: false),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sucursal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimientos_caja", x => x.id);
                    table.CheckConstraint("ck_movimiento_monto", "monto > 0");
                    table.ForeignKey(
                        name: "FK_movimientos_caja_sesiones_caja_sesion_caja_id",
                        column: x => x.sesion_caja_id,
                        principalTable: "sesiones_caja",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ventas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sesion_caja_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cajero_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_cajero = table.Column<string>(type: "text", nullable: false),
                    solicitud_hash = table.Column<string>(type: "text", nullable: false),
                    prefijo = table.Column<string>(type: "text", nullable: false),
                    numero = table.Column<long>(type: "bigint", nullable: false),
                    tipo_orden = table.Column<string>(type: "text", nullable: false),
                    estado = table.Column<string>(type: "text", nullable: false),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    descuento = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    @base = table.Column<decimal>(name: "base", type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    impuesto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vuelto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tipo_descuento_global = table.Column<string>(type: "text", nullable: false),
                    valor_descuento_global = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    motivo_descuento_global = table.Column<string>(type: "text", nullable: true),
                    anulada_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    anulada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    motivo_anulacion = table.Column<string>(type: "text", nullable: true),
                    negocio_nombre = table.Column<string>(type: "text", nullable: false),
                    negocio_razon_social = table.Column<string>(type: "text", nullable: false),
                    negocio_cedula = table.Column<string>(type: "text", nullable: false),
                    negocio_telefono = table.Column<string>(type: "text", nullable: false),
                    negocio_correo = table.Column<string>(type: "text", nullable: false),
                    negocio_direccion = table.Column<string>(type: "text", nullable: false),
                    negocio_logo = table.Column<string>(type: "text", nullable: true),
                    pie_tiquete = table.Column<string>(type: "text", nullable: false),
                    sucursal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ventas", x => x.id);
                    table.CheckConstraint("ck_venta_total", "total >= 0 AND base + impuesto = total AND subtotal - descuento = total");
                    table.ForeignKey(
                        name: "FK_ventas_sesiones_caja_sesion_caja_id",
                        column: x => x.sesion_caja_id,
                        principalTable: "sesiones_caja",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lineas_venta",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    venta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    cantidad = table.Column<int>(type: "integer", nullable: false),
                    nota = table.Column<string>(type: "text", nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tarifa_iva = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    descuento_automatico = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    descuento_manual = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tipo_descuento_manual = table.Column<string>(type: "text", nullable: false),
                    valor_descuento_manual = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    motivo_descuento = table.Column<string>(type: "text", nullable: true),
                    descuento_global = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    @base = table.Column<decimal>(name: "base", type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    impuesto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    sucursal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lineas_venta", x => x.id);
                    table.CheckConstraint("ck_linea_importes", "total >= 0 AND base + impuesto = total AND cantidad > 0");
                    table.ForeignKey(
                        name: "FK_lineas_venta_productos_producto_id",
                        column: x => x.producto_id,
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lineas_venta_ventas_venta_id",
                        column: x => x.venta_id,
                        principalTable: "ventas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pagos_venta",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    venta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metodo = table.Column<string>(type: "text", nullable: false),
                    monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    recibido = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    vuelto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    referencia = table.Column<string>(type: "text", nullable: true),
                    sucursal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagos_venta", x => x.id);
                    table.CheckConstraint("ck_pago_importes", "monto >= 0 AND vuelto >= 0 AND ((metodo = 'Efectivo' AND recibido >= monto AND recibido - vuelto = monto) OR (metodo <> 'Efectivo' AND recibido IS NULL AND vuelto = 0))");
                    table.ForeignKey(
                        name: "FK_pagos_venta_ventas_venta_id",
                        column: x => x.venta_id,
                        principalTable: "ventas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_conteos_denominacion_sesion_caja_id_denominacion",
                table: "conteos_denominacion",
                columns: new[] { "sesion_caja_id", "denominacion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lineas_venta_producto_id",
                table: "lineas_venta",
                column: "producto_id");

            migrationBuilder.CreateIndex(
                name: "IX_lineas_venta_venta_id_orden",
                table: "lineas_venta",
                columns: new[] { "venta_id", "orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_caja_sesion_caja_id",
                table: "movimientos_caja",
                column: "sesion_caja_id");

            migrationBuilder.CreateIndex(
                name: "IX_pagos_venta_venta_id_metodo",
                table: "pagos_venta",
                columns: new[] { "venta_id", "metodo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_productos_categoria_id",
                table: "productos",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "IX_sesiones_caja_cajero_id",
                table: "sesiones_caja",
                column: "cajero_id");

            migrationBuilder.CreateIndex(
                name: "IX_sesiones_caja_sucursal_id",
                table: "sesiones_caja",
                column: "sucursal_id",
                unique: true,
                filter: "cerrada_en IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ventas_sesion_caja_id",
                table: "ventas",
                column: "sesion_caja_id");

            migrationBuilder.CreateIndex(
                name: "IX_ventas_sucursal_id_numero",
                table: "ventas",
                columns: new[] { "sucursal_id", "numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "conteos_denominacion");

            migrationBuilder.DropTable(
                name: "lineas_venta");

            migrationBuilder.DropTable(
                name: "movimientos_caja");

            migrationBuilder.DropTable(
                name: "pagos_venta");

            migrationBuilder.DropTable(
                name: "productos");

            migrationBuilder.DropTable(
                name: "ventas");

            migrationBuilder.DropTable(
                name: "categorias");

            migrationBuilder.DropTable(
                name: "sesiones_caja");
        }
    }
}
