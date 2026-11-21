using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologaUTA.Solicitudes.Migrations
{
    /// <inheritdoc />
    public partial class _006_NumeroProcesoYEdicion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NumeroProceso",
                table: "Solicitudes",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Detalle",
                table: "HistorialEstados",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            // Las solicitudes anteriores reciben su número de proceso: SOL-0001, SOL-0002, ...
            migrationBuilder.Sql(@"UPDATE Solicitudes SET NumeroProceso = 'SOL-' + CASE WHEN Id < 10000 THEN RIGHT('0000' + CAST(Id AS varchar(10)), 4) ELSE CAST(Id AS varchar(10)) END;");

            migrationBuilder.CreateIndex(
                name: "IX_Solicitudes_NumeroProceso",
                table: "Solicitudes",
                column: "NumeroProceso",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Solicitudes_NumeroProceso",
                table: "Solicitudes");

            migrationBuilder.DropColumn(
                name: "NumeroProceso",
                table: "Solicitudes");

            migrationBuilder.DropColumn(
                name: "Detalle",
                table: "HistorialEstados");
        }
    }
}
