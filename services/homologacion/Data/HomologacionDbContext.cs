using HomologaUTA.Homologacion.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Data;

public class HomologacionDbContext : DbContext
{
    public HomologacionDbContext(DbContextOptions<HomologacionDbContext> options) : base(options) { }

    public DbSet<AnalisisSolicitud> AnalisisSolicitudes => Set<AnalisisSolicitud>();
    public DbSet<AnalisisAsignatura> AnalisisAsignaturas => Set<AnalisisAsignatura>();
    public DbSet<MateriaOrigenAnalisis> MateriasOrigenAnalisis => Set<MateriaOrigenAnalisis>();
    public DbSet<MateriaAprobada> MateriasAprobadas => Set<MateriaAprobada>();
    public DbSet<ResolucionConsejo> ResolucionesConsejo => Set<ResolucionConsejo>();
    public DbSet<ResolucionDecision> ResolucionDecisiones => Set<ResolucionDecision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnalisisSolicitud>(e =>
        {
            e.Property(a => a.Estado).IsRequired().HasMaxLength(50);
            e.Property(a => a.MemoriaHuella).HasMaxLength(100);
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
            e.Property(a => a.ReglaAplicada).HasMaxLength(300);
            e.Property(a => a.DecisionCoordinador).HasMaxLength(30);
            e.Property(a => a.ObservacionCoordinador).HasMaxLength(1000);

            e.HasOne(a => a.AnalisisSolicitud)
             .WithMany(s => s.Asignaturas)
             .HasForeignKey(a => a.AnalisisSolicitudId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MateriaOrigenAnalisis>(e =>
        {
            e.Property(m => m.Nombre).IsRequired().HasMaxLength(200);
            e.Property(m => m.EstadoSegunDocumento).HasMaxLength(50);
            e.Property(m => m.Periodo).HasMaxLength(50);
            e.Property(m => m.Nota).HasPrecision(5, 2);

            e.HasOne(m => m.AnalisisAsignatura)
             .WithMany(a => a.MateriasOrigen)
             .HasForeignKey(m => m.AnalisisAsignaturaId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MateriaAprobada>(e =>
        {
            e.Property(m => m.Nombre).IsRequired().HasMaxLength(200);
            e.Property(m => m.EstadoSegunDocumento).HasMaxLength(50);
            e.Property(m => m.Periodo).HasMaxLength(50);
            e.Property(m => m.Nota).HasPrecision(5, 2);
            e.HasIndex(m => m.SolicitudId);
        });

        modelBuilder.Entity<ResolucionConsejo>(e =>
        {
            e.Property(r => r.NumeroResolucion).IsRequired().HasMaxLength(100);
            e.Property(r => r.Observacion).HasMaxLength(2000);
            e.HasIndex(r => r.SolicitudId).IsUnique();
        });

        modelBuilder.Entity<ResolucionDecision>(e =>
        {
            e.Property(d => d.DecisionFinal).IsRequired().HasMaxLength(50);
            e.Property(d => d.Observacion).HasMaxLength(1000);
            e.HasOne(d => d.ResolucionConsejo)
             .WithMany(r => r.Decisiones)
             .HasForeignKey(d => d.ResolucionConsejoId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
