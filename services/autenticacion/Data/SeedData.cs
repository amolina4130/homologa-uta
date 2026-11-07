using HomologaUTA.Autenticacion.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace HomologaUTA.Autenticacion.Data;

public static class SeedData
{
    public static async Task InitializeAsync(
        AuthDbContext context,
        IPasswordHasher<Usuario> hasher,
        IConfiguration config,
        ILogger? logger = null)
    {
        // Arreglo con los 5 usuarios, usando los mismos correos de ejemplo
        var usuariosSeed = new[]
        {
            new { Correo = "estudiante@uta.edu.ec", RolId = 1, Pass = config["SEED_ESTUDIANTE_PASSWORD"] },
            new { Correo = "coordinador@uta.edu.ec", RolId = 2, Pass = config["SEED_COORDINADOR_PASSWORD"] },
            new { Correo = "secretaria@uta.edu.ec", RolId = 3, Pass = config["SEED_SECRETARIA_PASSWORD"] },
            new { Correo = "consejo@uta.edu.ec", RolId = 4, Pass = config["SEED_CONSEJO_PASSWORD"] },
            new { Correo = "admin@uta.edu.ec", RolId = 5, Pass = config["SEED_ADMIN_PASSWORD"] }
        };

        foreach (var u in usuariosSeed)
        {
            // Idempotente: solo crea si el correo no existe en la base de datos
            if (!await context.Usuarios.AnyAsync(x => x.Correo == u.Correo))
            {
                if (string.IsNullOrWhiteSpace(u.Pass))
                {
                    // Sin contraseña configurada no se puede crear el usuario; se avisa y se continúa
                    // para que el servicio no entre en un bucle de reinicios.
                    logger?.LogWarning(
                        "No se creó el usuario semilla {Correo}: falta su contraseña en la configuración (SEED_*).",
                        u.Correo);
                    continue;
                }

                var nuevoUsuario = new Usuario
                {
                    Correo = u.Correo,
                    RolId = u.RolId
                };

                // Encriptamos la contraseña
                nuevoUsuario.PasswordHash = hasher.HashPassword(nuevoUsuario, u.Pass);
                context.Usuarios.Add(nuevoUsuario);
            }
        }
        await context.SaveChangesAsync();
    }
}