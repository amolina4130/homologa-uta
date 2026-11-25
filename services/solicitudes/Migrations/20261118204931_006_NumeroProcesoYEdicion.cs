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
            // NumeroProceso y su índice también los crea 006_Sprint3_CatalogoMemoria
            // (ambas ramas lo agregaron y se fusionaron). Se crean solo si faltan,
            // para que funcione en BD nuevas y en BD que aplicaron cualquiera de las dos.
            migrationBuilder.Sql(@"
IF COL_LENGTH('Solicitudes', 'NumeroProceso') IS NULL
    ALTER TABLE [Solicitudes] ADD [NumeroProceso] nvarchar(30) NOT NULL DEFAULT N'';");

            migrationBuilder.AddColumn<string>(
                name: "Detalle",
                table: "HistorialEstados",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            // Las solicitudes anteriores reciben su número de proceso: SOL-0001, SOL-0002, ...
            migrationBuilder.Sql(@"
UPDATE Solicitudes SET NumeroProceso = 'SOL-' + CASE WHEN Id < 10000 THEN RIGHT('0000' + CAST(Id AS varchar(10)), 4) ELSE CAST(Id AS varchar(10)) END
WHERE NumeroProceso = '' OR NumeroProceso IS NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Solicitudes_NumeroProceso' AND object_id = OBJECT_ID('Solicitudes'))
    CREATE UNIQUE INDEX [IX_Solicitudes_NumeroProceso] ON [Solicitudes] ([NumeroProceso]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // NumeroProceso pertenece a 006_Sprint3_CatalogoMemoria (anterior); su Down lo elimina.
            migrationBuilder.DropColumn(
                name: "Detalle",
                table: "HistorialEstados");
        }
    }
}
