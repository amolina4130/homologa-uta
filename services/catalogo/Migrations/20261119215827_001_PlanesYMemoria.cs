using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologaUTA.Catalogo.Migrations
{
    /// <inheritdoc />
    public partial class _001_PlanesYMemoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemoriaCargas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Huella = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalCarreras = table.Column<int>(type: "int", nullable: false),
                    TotalAsignaturas = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoriaCargas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemoriaCarreras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoriaCarreras", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanesAnaliticos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AsignaturaId = table.Column<int>(type: "int", nullable: false),
                    Contenido = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResultadosAprendizaje = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EsVigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanesAnaliticos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemoriaNiveles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CarreraId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoriaNiveles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemoriaNiveles_MemoriaCarreras_CarreraId",
                        column: x => x.CarreraId,
                        principalTable: "MemoriaCarreras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemoriaAsignaturas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CarreraId = table.Column<int>(type: "int", nullable: false),
                    NivelId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Creditos = table.Column<int>(type: "int", nullable: false),
                    Horas = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoriaAsignaturas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemoriaAsignaturas_MemoriaCarreras_CarreraId",
                        column: x => x.CarreraId,
                        principalTable: "MemoriaCarreras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemoriaAsignaturas_MemoriaNiveles_NivelId",
                        column: x => x.NivelId,
                        principalTable: "MemoriaNiveles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemoriaContenidosMinimos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AsignaturaId = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoriaContenidosMinimos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemoriaContenidosMinimos_MemoriaAsignaturas_AsignaturaId",
                        column: x => x.AsignaturaId,
                        principalTable: "MemoriaAsignaturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemoriaAsignaturas_CarreraId",
                table: "MemoriaAsignaturas",
                column: "CarreraId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoriaAsignaturas_NivelId",
                table: "MemoriaAsignaturas",
                column: "NivelId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoriaContenidosMinimos_AsignaturaId_Orden",
                table: "MemoriaContenidosMinimos",
                columns: new[] { "AsignaturaId", "Orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemoriaNiveles_CarreraId_Numero",
                table: "MemoriaNiveles",
                columns: new[] { "CarreraId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanesAnaliticos_AsignaturaId",
                table: "PlanesAnaliticos",
                column: "AsignaturaId",
                unique: true,
                filter: "[EsVigente] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemoriaCargas");

            migrationBuilder.DropTable(
                name: "MemoriaContenidosMinimos");

            migrationBuilder.DropTable(
                name: "PlanesAnaliticos");

            migrationBuilder.DropTable(
                name: "MemoriaAsignaturas");

            migrationBuilder.DropTable(
                name: "MemoriaNiveles");

            migrationBuilder.DropTable(
                name: "MemoriaCarreras");
        }
    }
}
