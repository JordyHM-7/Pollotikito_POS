using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pollotikito.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BaseSistema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bitacora",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accion = table.Column<string>(type: "text", nullable: false),
                    nombre_entidad = table.Column<string>(type: "text", nullable: false),
                    entidad_id = table.Column<Guid>(type: "uuid", nullable: true),
                    valor_anterior = table.Column<string>(type: "jsonb", nullable: true),
                    valor_nuevo = table.Column<string>(type: "jsonb", nullable: true),
                    fecha_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ip = table.Column<string>(type: "text", nullable: true),
                    sucursal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bitacora", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "configuracion_negocio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    registro_unico = table.Column<bool>(type: "boolean", nullable: false),
                    nombre_comercial = table.Column<string>(type: "text", nullable: false),
                    razon_social = table.Column<string>(type: "text", nullable: false),
                    cedula = table.Column<string>(type: "text", nullable: false),
                    telefono = table.Column<string>(type: "text", nullable: false),
                    correo = table.Column<string>(type: "text", nullable: false),
                    direccion = table.Column<string>(type: "text", nullable: false),
                    logo = table.Column<string>(type: "text", nullable: true),
                    pie_tiquete = table.Column<string>(type: "text", nullable: false),
                    iva_por_defecto = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    sucursal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracion_negocio", x => x.id);
                    table.CheckConstraint("ck_registro_unico", "registro_unico = true");
                });

            migrationBuilder.CreateTable(
                name: "consecutivos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    prefijo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    siguiente_numero = table.Column<long>(type: "bigint", nullable: false),
                    sucursal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consecutivos", x => x.id);
                    table.CheckConstraint("ck_siguiente_positivo", "siguiente_numero > 0");
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    nombre_usuario = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    nombre_usuario_normalizado = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    rol = table.Column<string>(type: "text", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    pin_hash = table.Column<string>(type: "text", nullable: true),
                    intentos_fallidos = table.Column<int>(type: "integer", nullable: false),
                    bloqueado_hasta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    debe_cambiar_credenciales = table.Column<bool>(type: "boolean", nullable: false),
                    version_seguridad = table.Column<Guid>(type: "uuid", nullable: false),
                    sucursal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "autorizaciones_administrador",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    administrador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitante_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accion = table.Column<string>(type: "text", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    vence_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumida_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sucursal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_autorizaciones_administrador", x => x.id);
                    table.ForeignKey(
                        name: "FK_autorizaciones_administrador_usuarios_administrador_id",
                        column: x => x.administrador_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_autorizaciones_administrador_usuarios_solicitante_id",
                        column: x => x.solicitante_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_autorizaciones_administrador_administrador_id",
                table: "autorizaciones_administrador",
                column: "administrador_id");

            migrationBuilder.CreateIndex(
                name: "IX_autorizaciones_administrador_solicitante_id",
                table: "autorizaciones_administrador",
                column: "solicitante_id");

            migrationBuilder.CreateIndex(
                name: "IX_autorizaciones_administrador_token_hash",
                table: "autorizaciones_administrador",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bitacora_fecha_utc",
                table: "bitacora",
                column: "fecha_utc");

            migrationBuilder.CreateIndex(
                name: "IX_configuracion_negocio_registro_unico",
                table: "configuracion_negocio",
                column: "registro_unico",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_consecutivos_tipo",
                table: "consecutivos",
                column: "tipo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_nombre_usuario_normalizado",
                table: "usuarios",
                column: "nombre_usuario_normalizado",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "autorizaciones_administrador");

            migrationBuilder.DropTable(
                name: "bitacora");

            migrationBuilder.DropTable(
                name: "configuracion_negocio");

            migrationBuilder.DropTable(
                name: "consecutivos");

            migrationBuilder.DropTable(
                name: "usuarios");
        }
    }
}
