using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Autenticacion.Data;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }
}