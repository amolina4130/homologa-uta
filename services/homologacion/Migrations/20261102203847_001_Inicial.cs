using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologaUTA.Homologacion.Migrations
{
    /// <inheritdoc />
    public partial class _001_Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalisisSolicitudes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SolicitudId = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FechaAnalisis = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalisisSolicitudes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnalisisAsignaturas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnalisisSolicitudId = table.Column<int>(type: "int", nullable: false),
                    AsignaturaOrigen = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AsignaturaDestino = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PorcentajeContenido = table.Column<int>(type: "int", nullable: false),
                    PorcentajeProfundidad = table.Column<int>(type: "int", nullable: false),
                    PorcentajeHoras = table.Column<int>(type: "int", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Justificacion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RequiereRevision = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalisisAsignaturas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalisisAsignaturas_AnalisisSolicitudes_AnalisisSolicitudId",
                        column: x => x.AnalisisSolicitudId,
                        principalTable: "AnalisisSolicitudes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalisisAsignaturas_AnalisisSolicitudId",
                table: "AnalisisAsignaturas",
                column: "AnalisisSolicitudId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalisisSolicitudes_SolicitudId",
                table: "AnalisisSolicitudes",
                column: "SolicitudId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalisisAsignaturas");

            migrationBuilder.DropTable(
                name: "AnalisisSolicitudes");
        }
    }
}
