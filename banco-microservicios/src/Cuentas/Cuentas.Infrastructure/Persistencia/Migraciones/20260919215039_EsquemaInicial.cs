using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cuentas.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class EsquemaInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cuentas");

            migrationBuilder.CreateTable(
                name: "bandeja_salida",
                schema: "cuentas",
                columns: table => new
                {
                    mensaje_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    contenido = table.Column<string>(type: "text", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    publicado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    intentos = table.Column<int>(type: "integer", nullable: false),
                    ultimo_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bandeja_salida", x => x.mensaje_id);
                });

            migrationBuilder.CreateTable(
                name: "clientes_replicados",
                schema: "cuentas",
                columns: table => new
                {
                    cliente_id = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    identificacion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    estado = table.Column<bool>(type: "boolean", nullable: false),
                    sincronizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clientes_replicados", x => x.cliente_id);
                });

            migrationBuilder.CreateTable(
                name: "cuentas",
                schema: "cuentas",
                columns: table => new
                {
                    cuenta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_cuenta = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tipo_cuenta = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    saldo_inicial = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    saldo_disponible = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    estado = table.Column<bool>(type: "boolean", nullable: false),
                    cliente_id = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    creada_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cuentas", x => x.cuenta_id);
                });

            migrationBuilder.CreateTable(
                name: "movimientos",
                schema: "cuentas",
                columns: table => new
                {
                    movimiento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cuenta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tipo_movimiento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    saldo_disponible = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    secuencia = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimientos", x => x.movimiento_id);
                    table.ForeignKey(
                        name: "FK_movimientos_cuentas_cuenta_id",
                        column: x => x.cuenta_id,
                        principalSchema: "cuentas",
                        principalTable: "cuentas",
                        principalColumn: "cuenta_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bandeja_salida_pendientes",
                schema: "cuentas",
                table: "bandeja_salida",
                column: "creado_en",
                filter: "publicado_en IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_cuentas_cliente",
                schema: "cuentas",
                table: "cuentas",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ux_cuentas_numero",
                schema: "cuentas",
                table: "cuentas",
                column: "numero_cuenta",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_movimientos_cuenta_fecha",
                schema: "cuentas",
                table: "movimientos",
                columns: new[] { "cuenta_id", "fecha", "secuencia" });

            migrationBuilder.CreateIndex(
                name: "ix_movimientos_fecha",
                schema: "cuentas",
                table: "movimientos",
                column: "fecha");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bandeja_salida",
                schema: "cuentas");

            migrationBuilder.DropTable(
                name: "clientes_replicados",
                schema: "cuentas");

            migrationBuilder.DropTable(
                name: "movimientos",
                schema: "cuentas");

            migrationBuilder.DropTable(
                name: "cuentas",
                schema: "cuentas");
        }
    }
}
