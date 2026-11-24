using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologaUTA.Autenticacion.Migrations
{
    /// <inheritdoc />
    public partial class _004_RolesComiteDocente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Se eliminan los usuarios de los roles que desaparecen para que el seed los
            // recree con los correos y roles vigentes. No se toca coordinador@uta.edu.ec.
            migrationBuilder.Sql(
                "DELETE FROM Usuarios WHERE Correo IN ('estudiante@uta.edu.ec', 'secretaria@uta.edu.ec', 'consejo@uta.edu.ec', 'admin@uta.edu.ec');");

            migrationBuilder.Sql("UPDATE Roles SET Nombre = 'COMITE' WHERE Id = 1;");
            migrationBuilder.Sql("UPDATE Roles SET Nombre = 'DOCENTE' WHERE Id = 3;");

            // El rol 5 se borra antes de renombrar el rol 4 a ADMINISTRADOR para no
            // violar el índice único de Nombre.
            migrationBuilder.Sql("DELETE FROM Roles WHERE Id = 5;");
            migrationBuilder.Sql("UPDATE Roles SET Nombre = 'ADMINISTRADOR' WHERE Id = 4;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM Usuarios WHERE Correo IN ('comite@uta.edu.ec', 'docente@uta.edu.ec', 'docente2@uta.edu.ec', 'admin@uta.edu.ec');");

            // El rol 4 se renombra a CONSEJO solo después de liberar ADMINISTRADOR (id 5).
            migrationBuilder.Sql("SET IDENTITY_INSERT Roles ON;");
            migrationBuilder.Sql("INSERT INTO Roles (Id, Nombre) VALUES (5, 'ADMINISTRADOR');");
            migrationBuilder.Sql("SET IDENTITY_INSERT Roles OFF;");
            migrationBuilder.Sql("UPDATE Roles SET Nombre = 'CONSEJO' WHERE Id = 4;");
            migrationBuilder.Sql("UPDATE Roles SET Nombre = 'SECRETARIA' WHERE Id = 3;");
            migrationBuilder.Sql("UPDATE Roles SET Nombre = 'ESTUDIANTE' WHERE Id = 1;");
        }
    }
}
