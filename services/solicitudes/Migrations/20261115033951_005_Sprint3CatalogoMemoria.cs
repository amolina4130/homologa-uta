using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologaUTA.Solicitudes.Migrations
{
    /// <inheritdoc />
    public partial class _005_Sprint3CatalogoMemoria : Migration
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
                name: "NumeroProceso",
                table: "Solicitudes",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UniversidadDestino",
                table: "Solicitudes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UniversidadOrigen",
                table: "Solicitudes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Equivalencias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CarreraOrigenId = table.Column<int>(type: "int", nullable: false),
                    AsignaturaOrigenId = table.Column<int>(type: "int", nullable: false),
                    CarreraDestinoId = table.Column<int>(type: "int", nullable: false),
                    AsignaturaDestinoId = table.Column<int>(type: "int", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equivalencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Equivalencias_Asignaturas_AsignaturaDestinoId",
                        column: x => x.AsignaturaDestinoId,
                        principalTable: "Asignaturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Equivalencias_Asignaturas_AsignaturaOrigenId",
                        column: x => x.AsignaturaOrigenId,
                        principalTable: "Asignaturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Equivalencias_Carreras_CarreraDestinoId",
                        column: x => x.CarreraDestinoId,
                        principalTable: "Carreras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Equivalencias_Carreras_CarreraOrigenId",
                        column: x => x.CarreraOrigenId,
                        principalTable: "Carreras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MemoriaAsignaturas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CarreraId = table.Column<int>(type: "int", nullable: false),
                    AsignaturaId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Nivel = table.Column<int>(type: "int", nullable: false),
                    Area = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Creditos = table.Column<int>(type: "int", nullable: false),
                    Horas = table.Column<int>(type: "int", nullable: false),
                    ContenidosMinimos = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoriaAsignaturas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemoriaEstados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Huella = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalCarreras = table.Column<int>(type: "int", nullable: false),
                    TotalAsignaturas = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoriaEstados", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanesAnaliticos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AsignaturaId = table.Column<int>(type: "int", nullable: false),
                    Contenido = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResultadosAprendizaje = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanesAnaliticos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanesAnaliticos_Asignaturas_AsignaturaId",
                        column: x => x.AsignaturaId,
                        principalTable: "Asignaturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Las solicitudes anteriores reciben su número de proceso antes de crear el índice único.
            migrationBuilder.Sql(@"
                UPDATE Solicitudes
                SET NumeroProceso = 'SOL-' + RIGHT('0000' + CAST(Id AS VARCHAR(10)), 4)
                WHERE NumeroProceso = '' OR NumeroProceso IS NULL;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Solicitudes_NumeroProceso",
                table: "Solicitudes",
                column: "NumeroProceso",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Equivalencias_AsignaturaDestinoId",
                table: "Equivalencias",
                column: "AsignaturaDestinoId");

            migrationBuilder.CreateIndex(
                name: "IX_Equivalencias_AsignaturaOrigenId_AsignaturaDestinoId",
                table: "Equivalencias",
                columns: new[] { "AsignaturaOrigenId", "AsignaturaDestinoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Equivalencias_CarreraDestinoId",
                table: "Equivalencias",
                column: "CarreraDestinoId");

            migrationBuilder.CreateIndex(
                name: "IX_Equivalencias_CarreraOrigenId",
                table: "Equivalencias",
                column: "CarreraOrigenId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoriaAsignaturas_CarreraId_AsignaturaId",
                table: "MemoriaAsignaturas",
                columns: new[] { "CarreraId", "AsignaturaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemoriaAsignaturas_Nivel",
                table: "MemoriaAsignaturas",
                column: "Nivel");

            migrationBuilder.CreateIndex(
                name: "IX_PlanesAnaliticos_AsignaturaId",
                table: "PlanesAnaliticos",
                column: "AsignaturaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Equivalencias");

            migrationBuilder.DropTable(
                name: "MemoriaAsignaturas");

            migrationBuilder.DropTable(
                name: "MemoriaEstados");

            migrationBuilder.DropTable(
                name: "PlanesAnaliticos");

            migrationBuilder.DropIndex(
                name: "IX_Solicitudes_NumeroProceso",
                table: "Solicitudes");

            migrationBuilder.DropColumn(
                name: "Modulo",
                table: "Solicitudes");

            migrationBuilder.DropColumn(
                name: "NumeroProceso",
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
