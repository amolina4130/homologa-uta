using Xunit;
using Microsoft.EntityFrameworkCore;
using HomologaUTA.Autenticacion.Data;
using HomologaUTA.Autenticacion.Models;
using HomologaUTA.Autenticacion.Controllers;
using HomologaUTA.Autenticacion.DTOs;
using HomologaUTA.Autenticacion.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Microsoft.Extensions.Configuration;
using System; // <-- Necesario para Console.WriteLine

namespace HomologaUTA.Autenticacion.Tests
{
    public class AuthControllerTests
    {
        private AuthDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<AuthDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_" + Guid.NewGuid().ToString())
                .Options;
            return new AuthDbContext(options);
        }

        private TokenService GetTokenService()
        {
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(x => x["JWT_SECRET"]).Returns("clave-de-prueba-larga-para-el-test-123456-abcdef");
            mockConfig.Setup(x => x["Jwt:ExpirationHours"]).Returns("8");
            mockConfig.Setup(x => x["Jwt:Issuer"]).Returns("HomologaUTA");
            mockConfig.Setup(x => x["Jwt:Audience"]).Returns("HomologaUTA");
            return new TokenService(mockConfig.Object);
        }

        [Fact]
        public async Task Login_CredencialesInvalidas_DevuelveUnauthorized()
        {
            var context = GetInMemoryDbContext();
            var hasher = new PasswordHasher<Usuario>();
            var tokenService = GetTokenService();
            var controller = new AuthController(context, hasher, tokenService);
            var request = new LoginRequest { Correo = "noexiste@uta.edu.ec", Contrasena = "123" };
            var result = await controller.Login(request);
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task Login_CredencialesValidas_DevuelveOk()
        {
            var context = GetInMemoryDbContext();
            var hasher = new PasswordHasher<Usuario>();
            var tokenService = GetTokenService();
            
            var rol = new Rol { Id = 1, Nombre = "Estudiante" };
            context.Roles.Add(rol);

            var usuario = new Usuario { Id = 1, Correo = "test@uta.edu.ec", RolId = 1, Rol = rol };
            usuario.PasswordHash = hasher.HashPassword(usuario, "Demo123!");
            context.Usuarios.Add(usuario);
            await context.SaveChangesAsync();

            var controller = new AuthController(context, hasher, tokenService);
            var request = new LoginRequest { Correo = "test@uta.edu.ec", Contrasena = "Demo123!" };
            var result = await controller.Login(request);
            
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.IsType<LoginResponse>(okResult.Value);
        }

        // ==============================================================
        // PRUEBAS DEL REGISTRO DE CUENTAS (HU-12)
        // ==============================================================

        [Fact]
        public async Task Registro_DatosValidos_CreaCuentaEstudiante()
        {
            var context = GetInMemoryDbContext();
            var hasher = new PasswordHasher<Usuario>();
            var tokenService = GetTokenService();
            context.Roles.Add(new Rol { Id = 1, Nombre = "ESTUDIANTE" });
            await context.SaveChangesAsync();

            var controller = new AuthController(context, hasher, tokenService);
            var request = new RegistroRequest
            {
                Nombre = "Ana Estudiante",
                Correo = "Ana.Estudiante@UTA.edu.ec",
                Contrasena = "Demo123!"
            };

            var result = await controller.Registrar(request);

            var objectResult = Assert.IsType<CreatedResult>(result);
            Assert.Equal(201, objectResult.StatusCode);
            var respuesta = Assert.IsType<RegistroResponse>(objectResult.Value);
            Assert.Equal("Ana Estudiante", respuesta.Nombre);
            Assert.Equal("ana.estudiante@uta.edu.ec", respuesta.Correo);
            Assert.Equal("Estudiante", respuesta.Rol);

            var guardado = await context.Usuarios.Include(u => u.Rol)
                .SingleAsync(u => u.Correo == "ana.estudiante@uta.edu.ec");
            Assert.Equal("ESTUDIANTE", guardado.Rol.Nombre);
            Assert.Equal("Ana Estudiante", guardado.Nombre);
            Assert.NotEqual("Demo123!", guardado.PasswordHash);
            Assert.Equal(
                PasswordVerificationResult.Success,
                hasher.VerifyHashedPassword(guardado, guardado.PasswordHash, "Demo123!"));
        }

        [Fact]
        public async Task Registro_CuentaCreada_PuedeIniciarSesion()
        {
            var context = GetInMemoryDbContext();
            var hasher = new PasswordHasher<Usuario>();
            var tokenService = GetTokenService();
            context.Roles.Add(new Rol { Id = 1, Nombre = "ESTUDIANTE" });
            await context.SaveChangesAsync();

            var controller = new AuthController(context, hasher, tokenService);
            var registro = await controller.Registrar(new RegistroRequest
            {
                Nombre = "Ana Estudiante",
                Correo = "ana@uta.edu.ec",
                Contrasena = "Demo123!"
            });
            Assert.IsType<CreatedResult>(registro);

            var login = await controller.Login(new LoginRequest
            {
                Correo = "ana@uta.edu.ec",
                Contrasena = "Demo123!"
            });

            var okResult = Assert.IsType<OkObjectResult>(login);
            var respuesta = Assert.IsType<LoginResponse>(okResult.Value);
            Assert.False(string.IsNullOrWhiteSpace(respuesta.Token));
            Assert.Equal("Ana Estudiante", respuesta.Nombre);
            Assert.Equal("ESTUDIANTE", respuesta.Rol);
        }

        [Fact]
        public async Task Registro_CorreoDuplicado_DevuelveConflict()
        {
            var context = GetInMemoryDbContext();
            var hasher = new PasswordHasher<Usuario>();
            var tokenService = GetTokenService();
            var rol = new Rol { Id = 1, Nombre = "ESTUDIANTE" };
            context.Roles.Add(rol);
            var existente = new Usuario { Correo = "duplicado@uta.edu.ec", RolId = 1 };
            existente.PasswordHash = hasher.HashPassword(existente, "Demo123!");
            context.Usuarios.Add(existente);
            await context.SaveChangesAsync();

            var controller = new AuthController(context, hasher, tokenService);
            var request = new RegistroRequest
            {
                Nombre = "Otro Estudiante",
                Correo = "duplicado@uta.edu.ec",
                Contrasena = "Demo123!"
            };

            var result = await controller.Registrar(request);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Equal(409, conflict.StatusCode);
            Assert.Equal(1, await context.Usuarios.CountAsync());
        }

        [Fact]
        public async Task Registro_ContrasenaDebil_DevuelveBadRequest()
        {
            var context = GetInMemoryDbContext();
            var hasher = new PasswordHasher<Usuario>();
            var tokenService = GetTokenService();
            context.Roles.Add(new Rol { Id = 1, Nombre = "ESTUDIANTE" });
            await context.SaveChangesAsync();

            var controller = new AuthController(context, hasher, tokenService);
            var request = new RegistroRequest
            {
                Nombre = "Ana Estudiante",
                Correo = "ana@uta.edu.ec",
                Contrasena = "corta"
            };

            var result = await controller.Registrar(request);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(0, await context.Usuarios.CountAsync());
        }

        [Fact]
        public async Task Registro_SinNombre_DevuelveBadRequest()
        {
            var context = GetInMemoryDbContext();
            var hasher = new PasswordHasher<Usuario>();
            var tokenService = GetTokenService();
            var controller = new AuthController(context, hasher, tokenService);
            var request = new RegistroRequest
            {
                Nombre = "   ",
                Correo = "ana@uta.edu.ec",
                Contrasena = "Demo123!"
            };

            var result = await controller.Registrar(request);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        // ==============================================================
        // PRUEBA TEMPORAL PARA GENERAR EL HASH DE SQL SERVER
        // ==============================================================
        [Fact]
        public void GenerarHashParaSQL()
        {
            var hasher = new PasswordHasher<Usuario>();
            var usuarioFalso = new Usuario();
            var hash = hasher.HashPassword(usuarioFalso, "Demo123!");
            
            // Esto lo imprimirá en la consola
            Console.WriteLine($"\n\n==================================================");
            Console.WriteLine($"EL HASH ES: {hash}");
            Console.WriteLine($"==================================================\n\n");
            
            Assert.True(true);
        }
    }
}