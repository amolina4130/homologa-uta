using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HomologaUTA.Autenticacion.Data;

/// <summary>Permite generar migraciones sin levantar la aplicación ni conectarse a SQL Server.</summary>
public class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlServer("Server=localhost;Database=HomologaUTA_Auth;User Id=sa;Password=placeholder;TrustServerCertificate=True;")
            .Options;
        return new AuthDbContext(options);
    }
}
