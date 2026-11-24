using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologaUTA.Homologacion.Migrations
{
    /// <inheritdoc />
    public partial class _004_DelegacionesDocente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CedulaEstudiante",
                table: "AnalisisSolicitudes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InformeFinalTexto",
                table: "AnalisisSolicitudes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreEstudiante",
                table: "AnalisisSolicitudes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreFirmante",
                table: "AnalisisSolicitudes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroMemorando",
                table: "AnalisisSolicitudes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DelegacionesDocente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SolicitudId = table.Column<int>(type: "int", nullable: false),
                    DocenteCorreo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FechaDelegacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FechaRevision = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InformeTexto = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    EvaluacionesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    NombreDocente = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelegacionesDocente", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DelegacionesDocente_DocenteCorreo",
                table: "DelegacionesDocente",
                column: "DocenteCorreo");

            migrationBuilder.CreateIndex(
                name: "IX_DelegacionesDocente_SolicitudId_DocenteCorreo",
                table: "DelegacionesDocente",
                columns: new[] { "SolicitudId", "DocenteCorreo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DelegacionesDocente");

            migrationBuilder.DropColumn(
                name: "CedulaEstudiante",
                table: "AnalisisSolicitudes");

            migrationBuilder.DropColumn(
                name: "InformeFinalTexto",
                table: "AnalisisSolicitudes");

            migrationBuilder.DropColumn(
                name: "NombreEstudiante",
                table: "AnalisisSolicitudes");

            migrationBuilder.DropColumn(
                name: "NombreFirmante",
                table: "AnalisisSolicitudes");

            migrationBuilder.DropColumn(
                name: "NumeroMemorando",
                table: "AnalisisSolicitudes");
        }
    }
}
