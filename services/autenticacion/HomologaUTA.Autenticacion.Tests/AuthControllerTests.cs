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
            // Creamos un Mock de IConfiguration para simular las variables del .env / appsettings
            var mockConfig = new Mock<IConfiguration>();
            
            // Simulamos las claves que el TokenService busca
            mockConfig.Setup(x => x["JWT_SECRET"]).Returns("clave-de-prueba-larga-para-el-test-123456-abcdef");
            mockConfig.Setup(x => x["Jwt:ExpirationHours"]).Returns("8");
            mockConfig.Setup(x => x["Jwt:Issuer"]).Returns("HomologaUTA");
            mockConfig.Setup(x => x["Jwt:Audience"]).Returns("HomologaUTA");

            return new TokenService(mockConfig.Object);
        }

        [Fact]
        public async Task Login_CredencialesInvalidas_DevuelveUnauthorized()
        {
            // 1. Preparar
            var context = GetInMemoryDbContext();
            var hasher = new PasswordHasher<Usuario>();
            var tokenService = GetTokenService();

            var controller = new AuthController(context, hasher, tokenService);
            var request = new LoginRequest { Correo = "noexiste@uta.edu.ec", Contrasena = "123" };

            // 2. Actuar
            var result = await controller.Login(request);

            // 3. Afirmar
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task Login_CredencialesValidas_DevuelveOk()
        {
            // 1. Preparar
            var context = GetInMemoryDbContext();
            var hasher = new PasswordHasher<Usuario>();
            var tokenService = GetTokenService();
            
            // Creamos un rol y un usuario real en la BD en memoria
            var rol = new Rol { Id = 1, Nombre = "Estudiante" };
            context.Roles.Add(rol);

            var usuario = new Usuario { 
                Id = 1, 
                Correo = "test@uta.edu.ec", 
                RolId = 1,
                Rol = rol
            };
            
            // Hasheamos la contraseña para que sea válida
            usuario.PasswordHash = hasher.HashPassword(usuario, "Demo123!");
            context.Usuarios.Add(usuario);
            await context.SaveChangesAsync();

            var controller = new AuthController(context, hasher, tokenService);
            var request = new LoginRequest { Correo = "test@uta.edu.ec", Contrasena = "Demo123!" };

            // 2. Actuar
            var result = await controller.Login(request);

            // 3. Afirmar
            // Verificamos que sea un OkObjectResult (200 OK)
            var okResult = Assert.IsType<OkObjectResult>(result);
            // Verificamos que el objeto devuelto sea del tipo LoginResponse
            Assert.IsType<LoginResponse>(okResult.Value);
        }
    }
}