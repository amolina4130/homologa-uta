using HomologaUTA.Autenticacion.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Autenticacion.Data;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Rol> Roles => Set<Rol>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Rol>(e =>
        {
            e.Property(r => r.Nombre).IsRequired().HasMaxLength(50);
            e.HasIndex(r => r.Nombre).IsUnique();
            e.HasData(
                new Rol { Id = 1, Nombre = "ESTUDIANTE" },
                new Rol { Id = 2, Nombre = "COORDINADOR" },
                new Rol { Id = 3, Nombre = "SECRETARIA" },
                new Rol { Id = 4, Nombre = "CONSEJO" },
                new Rol { Id = 5, Nombre = "ADMINISTRADOR" });
        });

        modelBuilder.Entity<Usuario>(e =>
        {
            e.Property(u => u.Correo).IsRequired().HasMaxLength(150);
            e.HasIndex(u => u.Correo).IsUnique();
            e.Property(u => u.PasswordHash).IsRequired();
            e.HasOne(u => u.Rol)
             .WithMany(r => r.Usuarios)
             .HasForeignKey(u => u.RolId);
        });
    }
}