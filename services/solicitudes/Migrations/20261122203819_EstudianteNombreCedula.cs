using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologaUTA.Solicitudes.Migrations
{
    /// <inheritdoc />
    public partial class EstudianteNombreCedula : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EstudianteCedula",
                table: "Solicitudes",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EstudianteNombre",
                table: "Solicitudes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Las solicitudes creadas antes de este campo no pueden mostrar el correo del
            // Comité como si fuera el estudiante. Se marcan para que el Coordinador las
            // corrija desde el panel antes de emitir el informe.
            migrationBuilder.Sql(
                "UPDATE [Solicitudes] SET [EstudianteNombre] = N'Estudiante sin identificar', " +
                "[EstudianteCedula] = N'-' WHERE [EstudianteNombre] = N'';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstudianteCedula",
                table: "Solicitudes");

            migrationBuilder.DropColumn(
                name: "EstudianteNombre",
                table: "Solicitudes");
        }
    }
}
