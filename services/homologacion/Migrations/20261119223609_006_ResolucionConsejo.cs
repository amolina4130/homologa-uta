using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologaUTA.Homologacion.Migrations
{
    /// <inheritdoc />
    public partial class _006_ResolucionConsejo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioRegistro = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResolucionesConsejo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DecisionesResolucion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResolucionConsejoId = table.Column<int>(type: "int", nullable: false),
                    AsignaturaId = table.Column<int>(type: "int", nullable: false),
                    DecisionFinal = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
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
                name: "IX_DecisionesResolucion_ResolucionConsejoId",
                table: "DecisionesResolucion",
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
                name: "DecisionesResolucion");

            migrationBuilder.DropTable(
                name: "ResolucionesConsejo");
        }
    }
}
