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

    // Tablas 2
    public DbSet<Solicitud> Solicitudes => Set<Solicitud>();
    public DbSet<SolicitudAsignatura> SolicitudAsignaturas => Set<SolicitudAsignatura>();

   public DbSet<HistorialEstado> HistorialEstados => Set<HistorialEstado>();

    // Sprint 3
    public DbSet<PlanAnalitico> PlanesAnaliticos => Set<PlanAnalitico>();
    public DbSet<Equivalencia> Equivalencias => Set<Equivalencia>();
    public DbSet<MemoriaAsignatura> MemoriaAsignaturas => Set<MemoriaAsignatura>();
    public DbSet<MemoriaEstado> MemoriaEstados => Set<MemoriaEstado>();

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
            e.Property(a => a.Area).HasMaxLength(80).HasDefaultValue("");
            e.HasOne(a => a.Carrera)
             .WithMany(c => c.Asignaturas)
             .HasForeignKey(a => a.CarreraId);
        });

        modelBuilder.Entity<PlanAnalitico>(e =>
        {
            e.Property(p => p.Contenido).IsRequired();
            e.Property(p => p.ResultadosAprendizaje).HasMaxLength(4000);
            e.HasIndex(p => p.AsignaturaId).IsUnique();
            e.HasOne(p => p.Asignatura)
             .WithMany()
             .HasForeignKey(p => p.AsignaturaId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Equivalencia>(e =>
        {
            e.HasIndex(x => new { x.CarreraOrigenId, x.AsignaturaOrigenId, x.CarreraDestinoId, x.AsignaturaDestinoId })
             .IsUnique();

            e.HasOne(x => x.CarreraOrigen).WithMany()
             .HasForeignKey(x => x.CarreraOrigenId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CarreraDestino).WithMany()
             .HasForeignKey(x => x.CarreraDestinoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AsignaturaOrigen).WithMany()
             .HasForeignKey(x => x.AsignaturaOrigenId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AsignaturaDestino).WithMany()
             .HasForeignKey(x => x.AsignaturaDestinoId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MemoriaAsignatura>(e =>
        {
            e.Property(m => m.Carrera).IsRequired().HasMaxLength(150);
            e.Property(m => m.Nombre).IsRequired().HasMaxLength(200);
            e.Property(m => m.Nivel).IsRequired().HasMaxLength(30);
            e.Property(m => m.Area).IsRequired().HasMaxLength(80);
            e.Property(m => m.ContenidosMinimos).IsRequired();
            e.HasIndex(m => new { m.CarreraId, m.AsignaturaId }).IsUnique();
            e.HasIndex(m => m.Area);
            e.HasIndex(m => m.NivelNumerico);
        });

        modelBuilder.Entity<MemoriaEstado>(e =>
        {
            e.Property(m => m.Huella).IsRequired().HasMaxLength(80);
        });

        // Configuración 2
        modelBuilder.Entity<Solicitud>(e =>
        {
            e.Property(s => s.NumeroProceso).IsRequired().HasMaxLength(30).HasDefaultValue("");
            e.HasIndex(s => s.NumeroProceso).IsUnique();
            e.Property(s => s.Tipo).IsRequired().HasMaxLength(50);
            e.Property(s => s.EstudianteCorreo).IsRequired().HasMaxLength(150);
            e.Property(s => s.EstudianteNombre).IsRequired().HasMaxLength(200);
            e.Property(s => s.EstudianteCedula).IsRequired().HasMaxLength(30);
            e.Property(s => s.Estado).IsRequired().HasMaxLength(50);
            e.Property(s => s.Modulo).IsRequired().HasMaxLength(20).HasDefaultValue("INTERNO");
            e.Property(s => s.UniversidadOrigen).HasMaxLength(150);
            e.Property(s => s.UniversidadDestino).HasMaxLength(150);
            e.Property(s => s.NumeroProceso).IsRequired().HasMaxLength(30);
            e.HasIndex(s => s.NumeroProceso).IsUnique();
            
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
            e.Property(h => h.Detalle).HasMaxLength(200);
            e.HasOne(h => h.Solicitud)
             .WithMany()
             .HasForeignKey(h => h.SolicitudId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(h => h.SolicitudId);
        });

    }
}