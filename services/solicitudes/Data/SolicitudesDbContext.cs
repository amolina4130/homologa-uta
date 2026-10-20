using HomologaUTA.Solicitudes.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Solicitudes.Data;

public class SolicitudesDbContext : DbContext
{
    public SolicitudesDbContext(DbContextOptions<SolicitudesDbContext> options) : base(options) { }

    public DbSet<Carrera> Carreras => Set<Carrera>();
    public DbSet<Asignatura> Asignaturas => Set<Asignatura>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Carrera>(e =>
        {
            e.Property(c => c.Nombre).IsRequired().HasMaxLength(150);
            e.HasIndex(c => c.Nombre).IsUnique();
        });

        modelBuilder.Entity<Asignatura>(e =>
        {
            e.Property(a => a.Nombre).IsRequired().HasMaxLength(200);
            e.Property(a => a.Nivel).IsRequired().HasMaxLength(30);
            e.HasOne(a => a.Carrera)
             .WithMany(c => c.Asignaturas)
             .HasForeignKey(a => a.CarreraId);
        });
    }
}