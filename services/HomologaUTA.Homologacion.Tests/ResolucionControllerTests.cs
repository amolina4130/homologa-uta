using System.Net;
using System.Security.Claims;
using System.Text;
using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Controllers;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomologaUTA.Homologacion.Tests;

/// <summary>Pruebas del controlador de resoluciones: acceso por rol y códigos HTTP (HU-17).</summary>
public class ResolucionControllerTests
{
    [Fact]
    public async Task Registrar_ConRolEstudiante_Devuelve403()
    {
        var controller = CrearController("ESTUDIANTE");

        var result = await controller.Registrar(1, Request(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task Registrar_ConRolConsejo_Devuelve403()
    {
        var controller = CrearController("CONSEJO");

        var result = await controller.Registrar(1, Request(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task Registrar_ConRolCoordinadorSinInforme_Devuelve409()
    {
        var controller = CrearController("COORDINADOR");

        var result = await controller.Registrar(1, Request(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    }

    [Fact]
    public async Task Registrar_SinClaimDeRol_NoBloqueaPor403()
    {
        var controller = CrearController(null);

        var result = await controller.Registrar(1, Request(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    }

    [Fact]
    public async Task Obtener_SinResolucion_Devuelve404()
    {
        var controller = CrearController("COORDINADOR");

        var result = await controller.Obtener(500, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    private static ResolucionController CrearController(string? rol)
    {
        var options = new DbContextOptionsBuilder<HomologacionDbContext>()
            .UseInMemoryDatabase($"ResolucionControllerTests_{Guid.NewGuid()}")
            .Options;
        var db = new HomologacionDbContext(options);

        var http = new HttpClient(new HandlerOk());
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Solicitudes:Url"] = "http://solicitudes"
            })
            .Build();
        var cliente = new SolicitudesClient(http, config, NullLogger<SolicitudesClient>.Instance);
        var servicio = new ResolucionService(db, cliente, NullLogger<ResolucionService>.Instance);

        var claims = rol is null
            ? Array.Empty<Claim>()
            : new[] { new Claim("rol", rol) };

        var controller = new ResolucionController(servicio);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        };
        return controller;
    }

    private static ResolucionRequest Request() => new()
    {
        NumeroResolucion = "RES-2026-001",
        Fecha = new DateTime(2026, 1, 15),
        Decisiones = new List<ResolucionDecisionRequest>
        {
            new() { AsignaturaId = 1, DecisionFinal = "HOMOLOGABLE" }
        }
    };

    private sealed class HandlerOk : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
    }
}
