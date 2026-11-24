using HomologaUTA.Autenticacion.Controllers;
using HomologaUTA.Autenticacion.Data;
using HomologaUTA.Autenticacion.DTOs;
using HomologaUTA.Autenticacion.Models;
using HomologaUTA.Autenticacion.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;

namespace HomologaUTA.Autenticacion.Tests;

public class RegistroTests
{
    private static AuthDbContext GetDb() =>
        new(new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase("Registro_" + Guid.NewGuid())
            .Options);

    private static TokenService GetTokens()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(x => x["JWT_SECRET"]).Returns("clave-de-prueba-larga-para-el-test-123456-abcdef");
        config.Setup(x => x["Jwt:ExpirationHours"]).Returns("8");
        config.Setup(x => x["Jwt:Issuer"]).Returns("HomologaUTA");
        config.Setup(x => x["Jwt:Audience"]).Returns("HomologaUTA");
        return new TokenService(config.Object);
    }

    private static async Task<AuthDbContext> GetDbConRolAsync()
    {
        var db = GetDb();
        db.Roles.AddRange(
            new Rol { Id = 1, Nombre = "COMITE" },
            new Rol { Id = 3, Nombre = "DOCENTE" });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Registro_Valido_CreaCuentaConRolComite()
    {
        using var db = await GetDbConRolAsync();
        var hasher = new PasswordHasher<Usuario>();
        var controller = new AuthController(db, hasher, GetTokens());

        var result = await controller.Registro(new RegistroRequest
        {
            Nombre = "Ana Pérez",
            Correo = "ana@uta.edu.ec",
            Contrasena = "Demo123!"
        });

        var created = Assert.IsType<CreatedResult>(result);
        var respuesta = Assert.IsType<RegistroResponse>(created.Value);
        Assert.Equal("COMITE", respuesta.Rol);

        var guardado = await db.Usuarios.SingleAsync();
        Assert.Equal("Ana Pérez", guardado.Nombre);
        Assert.Equal(1, guardado.RolId);
        Assert.NotEqual("Demo123!", guardado.PasswordHash);
    }

    [Fact]
    public async Task Registro_RolDocente_CreaCuentaConRolDocente()
    {
        using var db = await GetDbConRolAsync();
        var controller = new AuthController(db, new PasswordHasher<Usuario>(), GetTokens());

        var result = await controller.Registro(new RegistroRequest
        {
            Nombre = "Carlos Docente",
            Correo = "docente3@uta.edu.ec",
            Contrasena = "Demo123!",
            Rol = "DOCENTE"
        });

        var created = Assert.IsType<CreatedResult>(result);
        var respuesta = Assert.IsType<RegistroResponse>(created.Value);
        Assert.Equal("DOCENTE", respuesta.Rol);

        var guardado = await db.Usuarios.SingleAsync();
        Assert.Equal(3, guardado.RolId);
    }

    [Fact]
    public async Task Registro_CorreoRepetido_DevuelveConflict()
    {
        using var db = await GetDbConRolAsync();
        var hasher = new PasswordHasher<Usuario>();
        var controller = new AuthController(db, hasher, GetTokens());
        var request = new RegistroRequest { Nombre = "Ana", Correo = "ana@uta.edu.ec", Contrasena = "Demo123!" };

        await controller.Registro(request);
        var result = await controller.Registro(request);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Registro_ContrasenaCorta_DevuelveBadRequestConMensaje()
    {
        using var db = await GetDbConRolAsync();
        var controller = new AuthController(db, new PasswordHasher<Usuario>(), GetTokens());

        var result = await controller.Registro(new RegistroRequest
        {
            Nombre = "Ana",
            Correo = "ana@uta.edu.ec",
            Contrasena = "Ab1!"
        });

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("8", bad.Value!.ToString());
    }

    [Fact]
    public async Task CuentaRegistrada_PuedeIniciarSesion()
    {
        using var db = await GetDbConRolAsync();
        var hasher = new PasswordHasher<Usuario>();
        var tokens = GetTokens();

        var registro = new AuthController(db, hasher, tokens);
        await registro.Registro(new RegistroRequest
        {
            Nombre = "Ana",
            Correo = "ana@uta.edu.ec",
            Contrasena = "Demo123!"
        });

        var login = new AuthController(db, hasher, tokens);
        var result = await login.Login(new LoginRequest { Correo = "ana@uta.edu.ec", Contrasena = "Demo123!" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var respuesta = Assert.IsType<LoginResponse>(ok.Value);
        Assert.Equal("ana@uta.edu.ec", respuesta.Correo);
        Assert.False(string.IsNullOrWhiteSpace(respuesta.Token));
    }
}
