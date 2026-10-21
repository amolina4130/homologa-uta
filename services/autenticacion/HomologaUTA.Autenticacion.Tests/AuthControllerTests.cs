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