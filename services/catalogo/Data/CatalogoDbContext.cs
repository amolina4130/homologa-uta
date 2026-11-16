using Microsoft.EntityFrameworkCore;
using HomologaUTA.Catalogo.Models;

namespace HomologaUTA.Catalogo.Data;

public class CatalogoDbContext : DbContext
{
    public CatalogoDbContext(DbContextOptions<CatalogoDbContext> options) : base(options) { }

    public DbSet<PlanAnalitico> PlanesAnaliticos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Un solo plan vigente por asignatura (índice único filtrado)
        modelBuilder.Entity<PlanAnalitico>()
            .HasIndex(p => p.AsignaturaId)
            .IsUnique()
            .HasFilter("[EsVigente] = 1");
    }
}