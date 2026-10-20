using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Solicitudes.Data;

public class SolicitudesDbContext : DbContext
{
    public SolicitudesDbContext(DbContextOptions<SolicitudesDbContext> options) : base(options) { }
}