using HomologaUTA.Homologacion.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Data;

public class HomologacionDbContext : DbContext
{
    public HomologacionDbContext(DbContextOptions<HomologacionDbContext> options) : base(options) { }

    public DbSet<AnalisisSolicitud> AnalisisSolicitudes => Set<AnalisisSolicitud>();
    public DbSet<AnalisisAsignatura> AnalisisAsignaturas => Set<AnalisisAsignatura>();
    public DbSet<MateriaOrigenResultado> MateriasOrigenResultado => Set<MateriaOrigenResultado>();

    // Sprint 3
    public DbSet<MateriaAprobada> MateriasAprobadas => Set<MateriaAprobada>();
    public DbSet<ResolucionConsejo> ResolucionesConsejo => Set<ResolucionConsejo>();
    public DbSet<DecisionResolucion> DecisionesResolucion => Set<DecisionResolucion>();

    // Delegación a docentes y emisión del informe final
    public DbSet<DelegacionDocente> DelegacionesDocente => Set<DelegacionDocente>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnalisisSolicitud>(e =>
        {
            e.Property(a => a.Estado).IsRequired().HasMaxLength(50);
            e.Property(a => a.CoordinadorCorreo).HasMaxLength(200);
            e.Property(a => a.NombreEstudiante).HasMaxLength(200);
            e.Property(a => a.CedulaEstudiante).HasMaxLength(50);
            e.Property(a => a.NumeroMemorando).HasMaxLength(100);
            e.Property(a => a.NombreFirmante).HasMaxLength(200);

            // Un solo análisis vigente por solicitud
            e.HasIndex(a => a.SolicitudId).IsUnique();

            // La lista del Coordinador filtra por estado
            e.HasIndex(a => a.Estado);

            e.HasOne(a => a.Resolucion)
             .WithOne(r => r.AnalisisSolicitud)
             .HasForeignKey<ResolucionConsejo>(r => r.AnalisisSolicitudId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AnalisisAsignatura>(e =>
        {
            e.Property(a => a.AsignaturaOrigen).IsRequired().HasMaxLength(200);
            e.Property(a => a.AsignaturaDestino).IsRequired().HasMaxLength(200);
            e.Property(a => a.AreaOrigen).HasMaxLength(80);
            e.Property(a => a.AreaDestino).HasMaxLength(80);
            e.Property(a => a.Decision).IsRequired().HasMaxLength(30);
            e.Property(a => a.Justificacion).IsRequired().HasMaxLength(2000);
            e.Property(a => a.DecisionCoordinador).HasMaxLength(30);
            e.Property(a => a.ObservacionCoordinador).HasMaxLength(1000);
            e.Property(a => a.NivelOrigen).HasMaxLength(30);
            e.Property(a => a.NivelDestino).HasMaxLength(30);
            e.Property(a => a.ContenidosCoincidentes).HasMaxLength(2000);
            e.Property(a => a.Diferencias).HasMaxLength(2000);
            e.Property(a => a.ReglaAplicada).HasMaxLength(500);
            e.Property(a => a.PropuestaPor).IsRequired().HasMaxLength(20).HasDefaultValue("IA");

            e.HasOne(a => a.AnalisisSolicitud)
             .WithMany(s => s.Asignaturas)
             .HasForeignKey(a => a.AnalisisSolicitudId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MateriaOrigenResultado>(e =>
        {
            e.Property(m => m.Nombre).IsRequired().HasMaxLength(200);
            e.Property(m => m.Nota).HasPrecision(5, 2);

            e.HasOne(m => m.AnalisisAsignatura)
             .WithMany(a => a.MateriasOrigen)
             .HasForeignKey(m => m.AnalisisAsignaturaId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MateriaAprobada>(e =>
        {
            e.Property(m => m.Nombre).IsRequired().HasMaxLength(200);
            e.Property(m => m.EstadoSegunDocumento).IsRequired().HasMaxLength(50);
            e.Property(m => m.Periodo).HasMaxLength(50);
            e.Property(m => m.Nota).HasPrecision(5, 2);

            // Un estudiante puede tener varias materias; se consultan por solicitud.
            e.HasIndex(m => m.SolicitudId);
        });

        modelBuilder.Entity<ResolucionConsejo>(e =>
        {
            e.Property(r => r.NumeroResolucion).IsRequired().HasMaxLength(100);
            e.Property(r => r.Observacion).HasMaxLength(2000);
            e.Property(r => r.RegistradoPor).IsRequired().HasMaxLength(200);

            // No se puede registrar dos veces la misma resolución para una solicitud.
            e.HasIndex(r => r.SolicitudId).IsUnique();
            e.HasIndex(r => r.AnalisisSolicitudId).IsUnique();
        });

        modelBuilder.Entity<DecisionResolucion>(e =>
        {
            e.Property(d => d.DecisionFinal).IsRequired().HasMaxLength(30);

            e.HasOne(d => d.ResolucionConsejo)
             .WithMany(r => r.Decisiones)
             .HasForeignKey(d => d.ResolucionConsejoId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(d => new { d.ResolucionConsejoId, d.AsignaturaId }).IsUnique();
        });

        modelBuilder.Entity<DelegacionDocente>(e =>
        {
            e.Property(d => d.DocenteCorreo).IsRequired().HasMaxLength(200);
            e.Property(d => d.CoordinadorCorreo).IsRequired().HasMaxLength(200).HasDefaultValue("");
            e.Property(d => d.Estado).IsRequired().HasMaxLength(30);
            e.Property(d => d.NombreDocente).HasMaxLength(200);
            e.Property(d => d.DocenteCedula).HasMaxLength(50);
            e.Property(d => d.DocenteCorreoInforme).HasMaxLength(200);
            e.Property(d => d.DocumentacionSolicitada).HasMaxLength(1000);
            e.Property(d => d.InformeTexto).HasMaxLength(8000);
            e.Property(d => d.EvaluacionesJson).HasMaxLength(8000);
            e.Property(d => d.SugerenciaJson).HasColumnType("nvarchar(max)");

            // Un mismo docente no puede recibir dos veces la misma solicitud.
            e.HasIndex(d => new { d.SolicitudId, d.DocenteCorreo }).IsUnique();

            e.HasIndex(d => d.DocenteCorreo);
        });
    }
}
