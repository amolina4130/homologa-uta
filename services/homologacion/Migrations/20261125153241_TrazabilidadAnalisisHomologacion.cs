using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologaUTA.Homologacion.Migrations
{
    /// <inheritdoc />
    public partial class TrazabilidadAnalisisHomologacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentacionSolicitada",
                table: "DelegacionesDocente",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaSolicitudDocumentacion",
                table: "DelegacionesDocente",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CambioRespectoDocente",
                table: "AnalisisAsignaturas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ContenidosCoincidentes",
                table: "AnalisisAsignaturas",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreditosDestino",
                table: "AnalisisAsignaturas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreditosOrigen",
                table: "AnalisisAsignaturas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Diferencias",
                table: "AnalisisAsignaturas",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HorasDestino",
                table: "AnalisisAsignaturas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HorasOrigen",
                table: "AnalisisAsignaturas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NivelDestino",
                table: "AnalisisAsignaturas",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NivelOrigen",
                table: "AnalisisAsignaturas",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PropuestaPor",
                table: "AnalisisAsignaturas",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "IA");

            migrationBuilder.AddColumn<string>(
                name: "ReglaAplicada",
                table: "AnalisisAsignaturas",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentacionSolicitada",
                table: "DelegacionesDocente");

            migrationBuilder.DropColumn(
                name: "FechaSolicitudDocumentacion",
                table: "DelegacionesDocente");

            migrationBuilder.DropColumn(
                name: "CambioRespectoDocente",
                table: "AnalisisAsignaturas");

            migrationBuilder.DropColumn(
                name: "ContenidosCoincidentes",
                table: "AnalisisAsignaturas");

            migrationBuilder.DropColumn(
                name: "CreditosDestino",
                table: "AnalisisAsignaturas");

            migrationBuilder.DropColumn(
                name: "CreditosOrigen",
                table: "AnalisisAsignaturas");

            migrationBuilder.DropColumn(
                name: "Diferencias",
                table: "AnalisisAsignaturas");

            migrationBuilder.DropColumn(
                name: "HorasDestino",
                table: "AnalisisAsignaturas");

            migrationBuilder.DropColumn(
                name: "HorasOrigen",
                table: "AnalisisAsignaturas");

            migrationBuilder.DropColumn(
                name: "NivelDestino",
                table: "AnalisisAsignaturas");

            migrationBuilder.DropColumn(
                name: "NivelOrigen",
                table: "AnalisisAsignaturas");

            migrationBuilder.DropColumn(
                name: "PropuestaPor",
                table: "AnalisisAsignaturas");

            migrationBuilder.DropColumn(
                name: "ReglaAplicada",
                table: "AnalisisAsignaturas");
        }
    }
}
