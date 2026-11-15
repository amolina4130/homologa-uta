using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HomologaUTA.Solicitudes.Data;

/// <summary>Permite generar migraciones sin levantar la aplicación ni conectarse a SQL Server.</summary>
public class SolicitudesDbContextFactory : IDesignTimeDbContextFactory<SolicitudesDbContext>
{
    public SolicitudesDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SolicitudesDbContext>()
            .UseSqlServer("Server=localhost;Database=HomologaUTA_Solicitudes;User Id=sa;Password=placeholder;TrustServerCertificate=True;")
            .Options;
        return new SolicitudesDbContext(options);
    }
}
