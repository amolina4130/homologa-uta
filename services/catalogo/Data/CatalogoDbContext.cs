using Microsoft.EntityFrameworkCore;
using HomologaUTA.Catalogo.Models;
using HomologaUTA.Catalogo.Models.Memoria;

namespace HomologaUTA.Catalogo.Data;

public class CatalogoDbContext : DbContext
{
    public CatalogoDbContext(DbContextOptions<CatalogoDbContext> options) : base(options) { }

    public DbSet<PlanAnalitico> PlanesAnaliticos { get; set; }

    // Memoria del agente (TA-19.1)
    public DbSet<CarreraMemoria> MemoriaCarreras => Set<CarreraMemoria>();
    public DbSet<NivelMemoria> MemoriaNiveles => Set<NivelMemoria>();
    public DbSet<AsignaturaMemoria> MemoriaAsignaturas => Set<AsignaturaMemoria>();
    public DbSet<ContenidoMinimo> MemoriaContenidosMinimos => Set<ContenidoMinimo>();
    public DbSet<CargaMemoria> MemoriaCargas => Set<CargaMemoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Un solo plan vigente por asignatura (índice único filtrado)
        modelBuilder.Entity<PlanAnalitico>()
            .HasIndex(p => p.AsignaturaId)
            .IsUnique()
            .HasFilter("[EsVigente] = 1");

        modelBuilder.Entity<CarreraMemoria>(e =>
        {
            e.ToTable("MemoriaCarreras");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
        });

        modelBuilder.Entity<NivelMemoria>(e =>
        {
            e.ToTable("MemoriaNiveles");
            e.Property(x => x.Nombre).IsRequired().HasMaxLength(30);
            e.HasOne(x => x.Carrera).WithMany(c => c.Niveles)
                .HasForeignKey(x => x.CarreraId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.CarreraId, x.Numero }).IsUnique();
        });

        modelBuilder.Entity<AsignaturaMemoria>(e =>
        {
            e.ToTable("MemoriaAsignaturas");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
            e.Property(x => x.Area).IsRequired().HasMaxLength(60);
            e.HasOne(x => x.Nivel).WithMany(n => n.Asignaturas)
                .HasForeignKey(x => x.NivelId).OnDelete(DeleteBehavior.Cascade);
            // Restrict: evita dos rutas de borrado en cascada (carrera -> nivel -> asignatura)
            e.HasOne(x => x.Carrera).WithMany(c => c.Asignaturas)
                .HasForeignKey(x => x.CarreraId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.CarreraId);
        });

        modelBuilder.Entity<ContenidoMinimo>(e =>
        {
            e.ToTable("MemoriaContenidosMinimos");
            e.Property(x => x.Descripcion).IsRequired().HasMaxLength(400);
            e.HasOne(x => x.Asignatura).WithMany(a => a.ContenidosMinimos)
                .HasForeignKey(x => x.AsignaturaId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.AsignaturaId, x.Orden }).IsUnique();
        });

        modelBuilder.Entity<CargaMemoria>(e =>
        {
            e.ToTable("MemoriaCargas");
            e.Property(x => x.Huella).IsRequired().HasMaxLength(64);
        });
    }
}
