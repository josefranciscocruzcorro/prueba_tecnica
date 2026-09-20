using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clientes.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class EsquemaInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "clientes");

            migrationBuilder.CreateTable(
                name: "actividad_cliente",
                schema: "clientes",
                columns: table => new
                {
                    evento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ocurrido_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_actividad_cliente", x => x.evento_id);
                });

            migrationBuilder.CreateTable(
                name: "bandeja_salida",
                schema: "clientes",
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
                name: "personas",
                schema: "clientes",
                columns: table => new
                {
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    genero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    edad = table.Column<int>(type: "integer", nullable: false),
                    identificacion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    direccion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personas", x => x.persona_id);
                });

            migrationBuilder.CreateTable(
                name: "clientes",
                schema: "clientes",
                columns: table => new
                {
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    contrasena_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    estado = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clientes", x => x.persona_id);
                    table.ForeignKey(
                        name: "FK_clientes_personas_persona_id",
                        column: x => x.persona_id,
                        principalSchema: "clientes",
                        principalTable: "personas",
                        principalColumn: "persona_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_actividad_cliente_fecha",
                schema: "clientes",
                table: "actividad_cliente",
                columns: new[] { "cliente_id", "ocurrido_en" });

            migrationBuilder.CreateIndex(
                name: "ix_bandeja_salida_pendientes",
                schema: "clientes",
                table: "bandeja_salida",
                column: "creado_en",
                filter: "publicado_en IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_clientes_estado",
                schema: "clientes",
                table: "clientes",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ux_clientes_cliente_id",
                schema: "clientes",
                table: "clientes",
                column: "cliente_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personas_nombre",
                schema: "clientes",
                table: "personas",
                column: "nombre");

            migrationBuilder.CreateIndex(
                name: "ux_personas_identificacion",
                schema: "clientes",
                table: "personas",
                column: "identificacion",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "actividad_cliente",
                schema: "clientes");

            migrationBuilder.DropTable(
                name: "bandeja_salida",
                schema: "clientes");

            migrationBuilder.DropTable(
                name: "clientes",
                schema: "clientes");

            migrationBuilder.DropTable(
                name: "personas",
                schema: "clientes");
        }
    }
}
