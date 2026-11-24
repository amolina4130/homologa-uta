using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologaUTA.Homologacion.Migrations
{
    /// <inheritdoc />
    public partial class DatosFirmaDocenteInforme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocenteCedula",
                table: "DelegacionesDocente",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocenteCorreoInforme",
                table: "DelegacionesDocente",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocenteCedula",
                table: "DelegacionesDocente");

            migrationBuilder.DropColumn(
                name: "DocenteCorreoInforme",
                table: "DelegacionesDocente");
        }
    }
}
