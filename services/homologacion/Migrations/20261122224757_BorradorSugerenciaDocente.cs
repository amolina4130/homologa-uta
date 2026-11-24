using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologaUTA.Homologacion.Migrations
{
    /// <inheritdoc />
    public partial class BorradorSugerenciaDocente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaSugerencia",
                table: "DelegacionesDocente",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SugerenciaJson",
                table: "DelegacionesDocente",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaSugerencia",
                table: "DelegacionesDocente");

            migrationBuilder.DropColumn(
                name: "SugerenciaJson",
                table: "DelegacionesDocente");
        }
    }
}
