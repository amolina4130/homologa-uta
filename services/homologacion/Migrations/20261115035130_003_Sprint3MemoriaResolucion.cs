using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologaUTA.Homologacion.Migrations
{
    /// <inheritdoc />
    public partial class _003_Sprint3MemoriaResolucion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MemoriaHuella",
                table: "AnalisisSolicitudes",
                type: "nvarchar(100)",
                maxLength: 100,
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

            migrationBuilder.AddColumn<string>(
                name: "ReglaAplicada",
                table: "AnalisisAsignaturas",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MateriasAprobadas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SolicitudId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Nota = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    EstadoSegunDocumento = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Periodo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AsignaturaId = table.Column<int>(type: "int", nullable: true),
                    FechaExtraccion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MateriasAprobadas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MateriasOrigenAnalisis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnalisisAsignaturaId = table.Column<int>(type: "int", nullable: false),
                    AsignaturaId = table.Column<int>(type: "int", nullable: true),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Nota = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    EstadoSegunDocumento = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Periodo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MateriasOrigenAnalisis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MateriasOrigenAnalisis_AnalisisAsignaturas_AnalisisAsignaturaId",
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
                    SolicitudId = table.Column<int>(type: "int", nullable: false),
                    NumeroResolucion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResolucionesConsejo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResolucionDecisiones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResolucionConsejoId = table.Column<int>(type: "int", nullable: false),
                    AsignaturaId = table.Column<int>(type: "int", nullable: false),
                    DecisionFinal = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResolucionDecisiones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResolucionDecisiones_ResolucionesConsejo_ResolucionConsejoId",
                        column: x => x.ResolucionConsejoId,
                        principalTable: "ResolucionesConsejo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MateriasAprobadas_SolicitudId",
                table: "MateriasAprobadas",
                column: "SolicitudId");

            migrationBuilder.CreateIndex(
                name: "IX_MateriasOrigenAnalisis_AnalisisAsignaturaId",
                table: "MateriasOrigenAnalisis",
                column: "AnalisisAsignaturaId");

            migrationBuilder.CreateIndex(
                name: "IX_ResolucionDecisiones_ResolucionConsejoId",
                table: "ResolucionDecisiones",
                column: "ResolucionConsejoId");

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
                name: "MateriasAprobadas");

            migrationBuilder.DropTable(
                name: "MateriasOrigenAnalisis");

            migrationBuilder.DropTable(
                name: "ResolucionDecisiones");

            migrationBuilder.DropTable(
                name: "ResolucionesConsejo");

            migrationBuilder.DropColumn(
                name: "MemoriaHuella",
                table: "AnalisisSolicitudes");

            migrationBuilder.DropColumn(
                name: "AsignaturaDestinoId",
                table: "AnalisisAsignaturas");

            migrationBuilder.DropColumn(
                name: "Combinada",
                table: "AnalisisAsignaturas");

            migrationBuilder.DropColumn(
                name: "ReglaAplicada",
                table: "AnalisisAsignaturas");
        }
    }
}
