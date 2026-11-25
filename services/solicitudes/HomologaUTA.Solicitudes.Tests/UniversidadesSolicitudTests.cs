using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using HomologaUTA.Solicitudes.Controllers;
using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace HomologaUTA.Solicitudes.Tests;

public class UniversidadesSolicitudTests
{
    private static SolicitudesDbContext NuevoContexto()
    {
        var opciones = new DbContextOptionsBuilder<SolicitudesDbContext>()
            .UseInMemoryDatabase("TestUniversidades_" + Guid.NewGuid())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new SolicitudesDbContext(opciones);
    }

    private static SolicitudesController NuevoControlador(SolicitudesDbContext db)
    {
        var usuario = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Email, "estudiante@uta.edu.ec") }, "prueba"));
        return new SolicitudesController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = usuario }
            }
        };
    }

    private static SolicitudRequest Pedido(string modulo, string? origen, string? destino) => new()
    {
        CarreraOrigenId = 1,
        CarreraDestinoId = 2,
        AsignaturasIds = new List<int> { 1 },
        Modulo = modulo,
        UniversidadOrigen = origen,
        UniversidadDestino = destino
    };

    private static string? Mensaje(IActionResult resultado)
    {
        var mala = Assert.IsType<BadRequestObjectResult>(resultado);
        var tipo = mala.Value!.GetType();
        return (tipo.GetProperty("mensaje") ?? tipo.GetProperty("error"))?.GetValue(mala.Value) as string;
    }

    [Fact]
    public async Task Externo_SinUniversidades_DevuelveBadRequestConMensaje()
    {
        using var db = NuevoContexto();
        var resultado = await NuevoControlador(db).CrearSolicitud(Pedido("EXTERNO", null, null));

        Assert.Contains("universidad de origen", Mensaje(resultado));
        Assert.Empty(db.Solicitudes);
    }

    [Fact]
    public async Task Externo_SinUniversidadDestino_DevuelveBadRequest()
    {
        using var db = NuevoContexto();
        var resultado = await NuevoControlador(db).CrearSolicitud(Pedido("EXTERNO", "Universidad Central", "   "));

        Assert.NotNull(Mensaje(resultado));
        Assert.Empty(db.Solicitudes);
    }

    [Fact]
    public async Task Externo_ConUniversidades_LasGuardaSinEspacios()
    {
        using var db = NuevoContexto();
        var resultado = await NuevoControlador(db).CrearSolicitud(
            Pedido("EXTERNO", "  Universidad Central  ", "Universidad Tecnica  "));

        Assert.IsType<CreatedResult>(resultado);
        var guardada = await db.Solicitudes.FirstAsync();
        Assert.Equal("EXTERNO", guardada.Modulo);
        Assert.Equal("Universidad Central", guardada.UniversidadOrigen);
        Assert.Equal("Universidad Tecnica", guardada.UniversidadDestino);
    }

    [Fact]
    public async Task Interno_IgnoraLasUniversidadesEnviadas()
    {
        using var db = NuevoContexto();
        var resultado = await NuevoControlador(db).CrearSolicitud(
            Pedido("INTERNO", "Universidad Central", "Universidad Tecnica"));

        Assert.IsType<CreatedResult>(resultado);
        var guardada = await db.Solicitudes.FirstAsync();
        Assert.Null(guardada.UniversidadOrigen);
        Assert.Null(guardada.UniversidadDestino);
    }

    [Fact]
    public async Task Externo_UniversidadMuyLarga_DevuelveBadRequest()
    {
        using var db = NuevoContexto();
        var resultado = await NuevoControlador(db).CrearSolicitud(
            Pedido("EXTERNO", new string('A', 151), "Universidad Tecnica"));

        Assert.Contains("150", Mensaje(resultado));
        Assert.Empty(db.Solicitudes);
    }
}
