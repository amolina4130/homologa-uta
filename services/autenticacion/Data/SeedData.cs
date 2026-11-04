using HomologaUTA.Autenticacion.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace HomologaUTA.Autenticacion.Data;

public static class SeedData
{
    public static async Task InitializeAsync(AuthDbContext context, IPasswordHasher<Usuario> hasher, IConfiguration config)
    {
        // Arreglo con los 5 usuarios
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
            if (!await context.Usuarios.AnyAsync(x => x.Correo == u.Correo))
            {
                var nuevoUsuario = new Usuario
                {
                    Correo = u.Correo,
                    RolId = u.RolId
                };

                // SOLUCIÓN: Validar que la contraseña exista o usar por defecto solo en Desarrollo
                var passwordSegura = u.Pass;
                
                if (string.IsNullOrEmpty(passwordSegura))
                {
                    var entorno = config["ASPNETCORE_ENVIRONMENT"] ?? "Production";
                    
                    if (entorno == "Development")
                    {
                        passwordSegura = "Demo123!";
                    }
                    else
                    {
                        throw new InvalidOperationException($"La variable de entorno para el usuario {u.Correo} no está definida.");
                    }
                }

                nuevoUsuario.PasswordHash = hasher.HashPassword(nuevoUsuario, passwordSegura);
                context.Usuarios.Add(nuevoUsuario);
            }
        }
        await context.SaveChangesAsync();
    }
}