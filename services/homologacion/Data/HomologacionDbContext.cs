using HomologaUTA.Homologacion.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Data;

public class HomologacionDbContext : DbContext
{
    public HomologacionDbContext(DbContextOptions<HomologacionDbContext> options) : base(options) { }

    public DbSet<AnalisisSolicitud> AnalisisSolicitudes => Set<AnalisisSolicitud>();
    public DbSet<AnalisisAsignatura> AnalisisAsignaturas => Set<AnalisisAsignatura>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnalisisSolicitud>(e =>
        {
            e.Property(a => a.Estado).IsRequired().HasMaxLength(50);

            // Un solo análisis vigente por solicitud
            e.HasIndex(a => a.SolicitudId).IsUnique();
        });

        modelBuilder.Entity<AnalisisAsignatura>(e =>
        {
            e.Property(a => a.AsignaturaOrigen).IsRequired().HasMaxLength(200);
            e.Property(a => a.AsignaturaDestino).IsRequired().HasMaxLength(200);
            e.Property(a => a.Decision).IsRequired().HasMaxLength(30);
            e.Property(a => a.Justificacion).IsRequired().HasMaxLength(2000);

            e.HasOne(a => a.AnalisisSolicitud)
             .WithMany(s => s.Asignaturas)
             .HasForeignKey(a => a.AnalisisSolicitudId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}