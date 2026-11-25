using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomologaUTA.Homologacion.Migrations
{
    /// <summary>
    /// Sin operaciones. Las tablas ResolucionesConsejo y DecisionesResolucion las crea
    /// 003_Sprint3 con el esquema vigente (RegistradoPor, AnalisisSolicitudId). Esta migración
    /// quedó duplicada al fusionar ramas y volvía a crearlas, lo que fallaba en bases de datos nuevas.
    /// </summary>
    public partial class _006_ResolucionConsejo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
