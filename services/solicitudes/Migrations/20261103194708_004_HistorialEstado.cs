using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologaUTA.Solicitudes.Migrations
{
    /// <inheritdoc />
    public partial class _004_HistorialEstado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistorialEstados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SolicitudId = table.Column<int>(type: "int", nullable: false),
                    EstadoAnterior = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EstadoNuevo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UsuarioCorreo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FechaCambio = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialEstados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialEstados_Solicitudes_SolicitudId",
                        column: x => x.SolicitudId,
                        principalTable: "Solicitudes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstados_SolicitudId",
                table: "HistorialEstados",
                column: "SolicitudId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistorialEstados");
        }
    }
}
