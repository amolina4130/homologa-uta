using HomologaUTA.Solicitudes.Models;
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
            e.Property(s => s.Tipo).IsRequired().HasMaxLength(50);
            e.Property(s => s.EstudianteCorreo).IsRequired().HasMaxLength(150);
            e.Property(s => s.Estado).IsRequired().HasMaxLength(50);
            e.Property(s => s.Modulo).IsRequired().HasMaxLength(20).HasDefaultValue("INTERNO");
            e.Property(s => s.UniversidadOrigen).HasMaxLength(150);
            e.Property(s => s.UniversidadDestino).HasMaxLength(150);
            
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

    }
}