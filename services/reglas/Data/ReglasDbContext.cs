using HomologaUTA.Reglas.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Reglas.Data;

public class ReglasDbContext : DbContext
{
    public ReglasDbContext(DbContextOptions<ReglasDbContext> options) : base(options) { }

    public DbSet<Regla> Reglas => Set<Regla>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Regla>(e =>
        {
            e.Property(r => r.Codigo).IsRequired().HasMaxLength(10);
            e.HasIndex(r => r.Codigo).IsUnique();
            e.Property(r => r.Descripcion).IsRequired().HasMaxLength(500);
            e.Property(r => r.Fuente).IsRequired().HasMaxLength(200);
            e.Property(r => r.Umbral).HasPrecision(10, 2);
        });
    }
}
