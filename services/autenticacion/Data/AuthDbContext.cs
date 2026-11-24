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
                new Rol { Id = 1, Nombre = "COMITE" },
                new Rol { Id = 2, Nombre = "COORDINADOR" },
                new Rol { Id = 3, Nombre = "DOCENTE" },
                new Rol { Id = 4, Nombre = "ADMINISTRADOR" });
        });

        modelBuilder.Entity<Usuario>(e =>
        {
            e.Property(u => u.Nombre).IsRequired().HasMaxLength(200).HasDefaultValue("");
            e.Property(u => u.Correo).IsRequired().HasMaxLength(150);
            e.HasIndex(u => u.Correo).IsUnique();
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.Tipo).HasMaxLength(80).HasDefaultValue("");
            e.Property(u => u.Titulo).HasMaxLength(80).HasDefaultValue("");
            e.Property(u => u.Cedula).HasMaxLength(50).HasDefaultValue("");
            e.Property(u => u.Telefono).HasMaxLength(50).HasDefaultValue("");
            e.HasOne(u => u.Rol)
             .WithMany(r => r.Usuarios)
             .HasForeignKey(u => u.RolId);
        });
    }
}