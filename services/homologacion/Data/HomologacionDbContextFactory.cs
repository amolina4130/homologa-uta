using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HomologaUTA.Homologacion.Data;

/// <summary>Permite generar migraciones sin levantar la aplicación ni conectarse a SQL Server.</summary>
public class HomologacionDbContextFactory : IDesignTimeDbContextFactory<HomologacionDbContext>
{
    public HomologacionDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<HomologacionDbContext>()
            .UseSqlServer("Server=localhost;Database=HomologaUTA_Homologacion;User Id=sa;Password=placeholder;TrustServerCertificate=True;")
            .Options;
        return new HomologacionDbContext(options);
    }
}
