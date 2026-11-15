using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologaUTA.Solicitudes.Migrations
{
    /// <inheritdoc />
    public partial class _005_ModuloYUniversidades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Modulo",
                table: "Solicitudes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "INTERNO");

            migrationBuilder.AddColumn<string>(
                name: "UniversidadDestino",
                table: "Solicitudes",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UniversidadOrigen",
                table: "Solicitudes",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Modulo",
                table: "Solicitudes");

            migrationBuilder.DropColumn(
                name: "UniversidadDestino",
                table: "Solicitudes");

            migrationBuilder.DropColumn(
                name: "UniversidadOrigen",
                table: "Solicitudes");
        }
    }
}
