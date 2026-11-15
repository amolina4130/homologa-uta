using HomologaUTA.Solicitudes.Models;
using HomologaUTA.Solicitudes.Models.Memoria;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Solicitudes.Data;

public class SolicitudesDbContext : DbContext
{
    public SolicitudesDbContext(DbContextOptions<SolicitudesDbContext> options) : base(options) { }

    // Tablas 1
    public DbSet<Carrera> Carreras => Set<Carrera>();
    public DbSet<Asignatura> Asignaturas => Set<Asignatura>();

    // Catálogo ampliado (Sprint 3)
    public DbSet<PlanAnalitico> PlanesAnaliticos => Set<PlanAnalitico>();
    public DbSet<Equivalencia> Equivalencias => Set<Equivalencia>();

    // Memoria del agente (HU-19)
    public DbSet<MemoriaAsignatura> MemoriaAsignaturas => Set<MemoriaAsignatura>();
    public DbSet<MemoriaEstado> MemoriaEstados => Set<MemoriaEstado>();

    // Tablas 2
    public DbSet<Solicitud> Solicitudes => Set<Solicitud>();
    public DbSet<SolicitudAsignatura> SolicitudAsignaturas => Set<SolicitudAsignatura>();

   public DbSet<HistorialEstado> HistorialEstados => Set<HistorialEstado>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configuración 1
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

        // Configuración 2
        modelBuilder.Entity<Solicitud>(e =>
        {
            e.Property(s => s.NumeroProceso).IsRequired().HasMaxLength(30);
            e.HasIndex(s => s.NumeroProceso).IsUnique();
            e.Property(s => s.Tipo).IsRequired().HasMaxLength(50);
            e.Property(s => s.Modulo).IsRequired().HasMaxLength(20).HasDefaultValue("INTERNO");
            e.Property(s => s.UniversidadOrigen).HasMaxLength(200);
            e.Property(s => s.UniversidadDestino).HasMaxLength(200);
            e.Property(s => s.EstudianteCorreo).IsRequired().HasMaxLength(150);
            e.Property(s => s.Estado).IsRequired().HasMaxLength(50);
            
            e.HasOne(s => s.CarreraOrigen)
             .WithMany()
             .HasForeignKey(s => s.CarreraOrigenId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(s => s.CarreraDestino)
             .WithMany()
             .HasForeignKey(s => s.CarreraDestinoId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SolicitudAsignatura>(e =>
        {
            e.HasOne(sa => sa.Solicitud)
             .WithMany(s => s.Asignaturas)
             .HasForeignKey(sa => sa.SolicitudId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(sa => sa.Asignatura)
             .WithMany()
             .HasForeignKey(sa => sa.AsignaturaId);
        });
        
        modelBuilder.Entity<HistorialEstado>(e =>
        {
            e.HasKey(h => h.Id);
            e.Property(h => h.EstadoAnterior).HasMaxLength(50);
            e.Property(h => h.EstadoNuevo).IsRequired().HasMaxLength(50);
            e.Property(h => h.UsuarioCorreo).IsRequired().HasMaxLength(150);
            e.HasOne(h => h.Solicitud)
             .WithMany()
             .HasForeignKey(h => h.SolicitudId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(h => h.SolicitudId);
        });

        modelBuilder.Entity<PlanAnalitico>(e =>
        {
            e.Property(p => p.Contenido).IsRequired();
            e.Property(p => p.ResultadosAprendizaje).HasMaxLength(4000);
            e.HasOne(p => p.Asignatura)
             .WithMany()
             .HasForeignKey(p => p.AsignaturaId)
             .OnDelete(DeleteBehavior.Cascade);
            // Un solo plan vigente por asignatura
            e.HasIndex(p => p.AsignaturaId).IsUnique();
        });

        modelBuilder.Entity<Equivalencia>(e =>
        {
            e.HasOne(x => x.CarreraOrigen).WithMany().HasForeignKey(x => x.CarreraOrigenId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CarreraDestino).WithMany().HasForeignKey(x => x.CarreraDestinoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AsignaturaOrigen).WithMany().HasForeignKey(x => x.AsignaturaOrigenId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AsignaturaDestino).WithMany().HasForeignKey(x => x.AsignaturaDestinoId).OnDelete(DeleteBehavior.Restrict);
            // No se permite duplicar la equivalencia entre las mismas asignaturas
            e.HasIndex(x => new { x.AsignaturaOrigenId, x.AsignaturaDestinoId }).IsUnique();
        });

        modelBuilder.Entity<MemoriaAsignatura>(e =>
        {
            e.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
            e.Property(x => x.Area).IsRequired().HasMaxLength(80);
            e.Property(x => x.ContenidosMinimos).IsRequired();
            e.HasIndex(x => new { x.CarreraId, x.AsignaturaId }).IsUnique();
            e.HasIndex(x => x.Nivel);
        });

        modelBuilder.Entity<MemoriaEstado>(e =>
        {
            e.Property(x => x.Huella).IsRequired().HasMaxLength(100);
        });

    }
}