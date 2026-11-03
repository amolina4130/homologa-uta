using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologaUTA.Documentos.Migrations
{
    /// <inheritdoc />
    public partial class AddEstudianteIdToDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EstudianteId",
                table: "Documentos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstudianteId",
                table: "Documentos");
        }
    }
}
