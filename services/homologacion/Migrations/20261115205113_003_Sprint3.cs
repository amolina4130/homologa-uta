using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologaUTA.Homologacion.Migrations
{
    /// <inheritdoc />
    public partial class _003_Sprint3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AreaDestino",
                table: "AnalisisAsignaturas",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AreaOrigen",
                table: "AnalisisAsignaturas",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AsignaturaDestinoId",
                table: "AnalisisAsignaturas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Combinada",
                table: "AnalisisAsignaturas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "MateriasAprobadas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SolicitudId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Nota = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    EstadoSegunDocumento = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Periodo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FechaExtraccion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MateriasAprobadas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MateriasOrigenResultado",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnalisisAsignaturaId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Nota = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MateriasOrigenResultado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MateriasOrigenResultado_AnalisisAsignaturas_AnalisisAsignaturaId",
                        column: x => x.AnalisisAsignaturaId,
                        principalTable: "AnalisisAsignaturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResolucionesConsejo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnalisisSolicitudId = table.Column<int>(type: "int", nullable: false),
                    SolicitudId = table.Column<int>(type: "int", nullable: false),
                    NumeroResolucion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RegistradoPor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResolucionesConsejo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResolucionesConsejo_AnalisisSolicitudes_AnalisisSolicitudId",
                        column: x => x.AnalisisSolicitudId,
                        principalTable: "AnalisisSolicitudes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DecisionesResolucion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResolucionConsejoId = table.Column<int>(type: "int", nullable: false),
                    AsignaturaId = table.Column<int>(type: "int", nullable: false),
                    DecisionFinal = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecisionesResolucion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DecisionesResolucion_ResolucionesConsejo_ResolucionConsejoId",
                        column: x => x.ResolucionConsejoId,
                        principalTable: "ResolucionesConsejo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DecisionesResolucion_ResolucionConsejoId_AsignaturaId",
                table: "DecisionesResolucion",
                columns: new[] { "ResolucionConsejoId", "AsignaturaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MateriasAprobadas_SolicitudId",
                table: "MateriasAprobadas",
                column: "SolicitudId");

            migrationBuilder.CreateIndex(
                name: "IX_MateriasOrigenResultado_AnalisisAsignaturaId",
                table: "MateriasOrigenResultado",
                column: "AnalisisAsignaturaId");

            migrationBuilder.CreateIndex(
                name: "IX_ResolucionesConsejo_AnalisisSolicitudId",
                table: "ResolucionesConsejo",
                column: "AnalisisSolicitudId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResolucionesConsejo_SolicitudId",
                table: "ResolucionesConsejo",
                column: "SolicitudId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DecisionesResolucion");

            migrationBuilder.DropTable(
                name: "MateriasAprobadas");

            migrationBuilder.DropTable(
                name: "MateriasOrigenResultado");

            migrationBuilder.DropTable(
                name: "ResolucionesConsejo");

            migrationBuilder.DropColumn(
                name: "AreaDestino",
                table: "AnalisisAsignaturas");

            migrationBuilder.DropColumn(
                name: "AreaOrigen",
                table: "AnalisisAsignaturas");

            migrationBuilder.DropColumn(
                name: "AsignaturaDestinoId",
                table: "AnalisisAsignaturas");

            migrationBuilder.DropColumn(
                name: "Combinada",
                table: "AnalisisAsignaturas");
        }
    }
}
