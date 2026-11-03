using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologaUTA.Homologacion.Migrations
{
    /// <inheritdoc />
    public partial class _002_InformeCoordinador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoordinadorCorreo",
                table: "AnalisisSolicitudes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaInforme",
                table: "AnalisisSolicitudes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionCoordinador",
                table: "AnalisisAsignaturas",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservacionCoordinador",
                table: "AnalisisAsignaturas",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalisisSolicitudes_Estado",
                table: "AnalisisSolicitudes",
                column: "Estado");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnalisisSolicitudes_Estado",
                table: "AnalisisSolicitudes");

            migrationBuilder.DropColumn(
                name: "CoordinadorCorreo",
                table: "AnalisisSolicitudes");

            migrationBuilder.DropColumn(
                name: "FechaInforme",
                table: "AnalisisSolicitudes");

            migrationBuilder.DropColumn(
                name: "DecisionCoordinador",
                table: "AnalisisAsignaturas");

            migrationBuilder.DropColumn(
                name: "ObservacionCoordinador",
                table: "AnalisisAsignaturas");
        }
    }
}
