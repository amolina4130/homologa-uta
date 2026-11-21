using HomologaUTA.Homologacion.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Data;

public class HomologacionDbContext : DbContext
{
    public HomologacionDbContext(DbContextOptions<HomologacionDbContext> options) : base(options) { }

    public DbSet<AnalisisSolicitud> AnalisisSolicitudes => Set<AnalisisSolicitud>();
    public DbSet<AnalisisAsignatura> AnalisisAsignaturas => Set<AnalisisAsignatura>();
    public DbSet<ResolucionConsejo> ResolucionesConsejo { get; set; }
    public DbSet<DecisionAsignaturaResolucion> DecisionesResolucion { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnalisisSolicitud>(e =>
        {
            e.Property(a => a.Estado).IsRequired().HasMaxLength(50);
            e.Property(a => a.CoordinadorCorreo).HasMaxLength(200);

            // Un solo análisis vigente por solicitud
            e.HasIndex(a => a.SolicitudId).IsUnique();

            // La lista del Coordinador filtra por estado
            e.HasIndex(a => a.Estado);
        });

        modelBuilder.Entity<AnalisisAsignatura>(e =>
        {
            e.Property(a => a.AsignaturaOrigen).IsRequired().HasMaxLength(200);
            e.Property(a => a.AsignaturaDestino).IsRequired().HasMaxLength(200);
            e.Property(a => a.Decision).IsRequired().HasMaxLength(30);
            e.Property(a => a.Justificacion).IsRequired().HasMaxLength(2000);
            e.Property(a => a.DecisionCoordinador).HasMaxLength(30);
            e.Property(a => a.ObservacionCoordinador).HasMaxLength(1000);

            e.HasOne(a => a.AnalisisSolicitud)
             .WithMany(s => s.Asignaturas)
             .HasForeignKey(a => a.AnalisisSolicitudId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // --- CONFIGURACIÓN NUEVA PARA LA RESOLUCIÓN ---
        modelBuilder.Entity<ResolucionConsejo>(e =>
        {
            e.Property(r => r.NumeroResolucion).IsRequired().HasMaxLength(100);
            e.Property(r => r.UsuarioRegistro).IsRequired().HasMaxLength(200);
            
            // Una resolución por solicitud
            e.HasIndex(r => r.SolicitudId).IsUnique();

            // Relación con las decisiones
            e.HasMany(r => r.Decisiones)
             .WithOne()
             .HasForeignKey(d => d.ResolucionConsejoId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DecisionAsignaturaResolucion>(e =>
        {
            e.Property(d => d.DecisionFinal).IsRequired().HasMaxLength(50);
        });
    }
}