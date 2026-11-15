using System.Net;
using System.Text;
using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Models;
using HomologaUTA.Homologacion.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomologaUTA.Homologacion.Tests;

/// <summary>Pruebas del registro de la resolución del Consejo Directivo (HU-17).</summary>
public class ResolucionServiceTests
{
    [Fact]
    public async Task Registrar_SinAnalisis_LanzaConflicto409()
    {
        var db = CrearDb();
        var servicio = CrearServicio(db, out _);

        var ex = await Assert.ThrowsAsync<ResolucionException>(
            () => servicio.RegistrarAsync(99, Request(1), "coordinador@uta.edu.ec"));

        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("informe técnico", ex.Message);
    }

    [Fact]
    public async Task Registrar_AnalisisAunPendiente_LanzaConflicto409()
    {
        var db = CrearDb();
        var (solicitudId, asignaturaId) = await SembrarAnalisisAsync(db, EstadoAnalisis.PENDIENTE_REVISION);
        var servicio = CrearServicio(db, out _);

        var ex = await Assert.ThrowsAsync<ResolucionException>(
            () => servicio.RegistrarAsync(solicitudId, Request(asignaturaId), "coordinador@uta.edu.ec"));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Registrar_Valido_GuardaResolucionYCambiaEstado()
    {
        var db = CrearDb();
        var (solicitudId, asignaturaId) = await SembrarAnalisisAsync(db, EstadoAnalisis.INFORME_EMITIDO);
        var servicio = CrearServicio(db, out var handler);

        var respuesta = await servicio.RegistrarAsync(
            solicitudId, Request(asignaturaId), "coordinador@uta.edu.ec");

        Assert.Equal(solicitudId, respuesta.SolicitudId);
        Assert.Equal("RES-2026-001", respuesta.NumeroResolucion);
        Assert.Single(respuesta.Decisiones);
        Assert.Equal("HOMOLOGABLE", respuesta.Decisiones[0].DecisionFinal);

        var guardada = await db.ResolucionesConsejo.Include(r => r.Decisiones).SingleAsync();
        Assert.Equal(solicitudId, guardada.SolicitudId);
        Assert.Equal("homologable".ToUpperInvariant(), guardada.Decisiones[0].DecisionFinal);

        Assert.Contains(handler.Peticiones,
            p => p == $"PUT http://solicitudes/internal/solicitudes/{solicitudId}/estado");
    }

    [Fact]
    public async Task Registrar_DosVeces_LanzaConflicto409()
    {
        var db = CrearDb();
        var (solicitudId, asignaturaId) = await SembrarAnalisisAsync(db, EstadoAnalisis.INFORME_EMITIDO);
        var servicio = CrearServicio(db, out _);

        await servicio.RegistrarAsync(solicitudId, Request(asignaturaId), "coordinador@uta.edu.ec");

        var ex = await Assert.ThrowsAsync<ResolucionException>(
            () => servicio.RegistrarAsync(solicitudId, Request(asignaturaId, "RES-2026-002"), "coordinador@uta.edu.ec"));

        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("ya fue registrada", ex.Message);
    }

    [Fact]
    public async Task Registrar_AsignaturaAjena_LanzaBadRequest400()
    {
        var db = CrearDb();
        var (solicitudId, _) = await SembrarAnalisisAsync(db, EstadoAnalisis.INFORME_EMITIDO);
        var servicio = CrearServicio(db, out _);

        var ex = await Assert.ThrowsAsync<ResolucionException>(
            () => servicio.RegistrarAsync(solicitudId, Request(9999), "coordinador@uta.edu.ec"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("no pertenece", ex.Message);
    }

    [Fact]
    public async Task Registrar_SinDecisiones_LanzaBadRequest400()
    {
        var db = CrearDb();
        var servicio = CrearServicio(db, out _);

        var request = new ResolucionRequest { NumeroResolucion = "RES-1", Decisiones = new() };

        var ex = await Assert.ThrowsAsync<ResolucionException>(
            () => servicio.RegistrarAsync(1, request, "coordinador@uta.edu.ec"));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Obtener_SinResolucion_DevuelveNull()
    {
        var db = CrearDb();
        var servicio = CrearServicio(db, out _);

        Assert.Null(await servicio.ObtenerAsync(123));
    }

    [Fact]
    public async Task Obtener_ConResolucion_DevuelveDecisiones()
    {
        var db = CrearDb();
        var (solicitudId, asignaturaId) = await SembrarAnalisisAsync(db, EstadoAnalisis.INFORME_EMITIDO);
        var servicio = CrearServicio(db, out _);
        await servicio.RegistrarAsync(solicitudId, Request(asignaturaId), "coordinador@uta.edu.ec");

        var resolucion = await servicio.ObtenerAsync(solicitudId);

        Assert.NotNull(resolucion);
        Assert.Equal("RES-2026-001", resolucion!.NumeroResolucion);
        Assert.Equal(asignaturaId, resolucion.Decisiones[0].AsignaturaId);
    }

    private static HomologacionDbContext CrearDb()
    {
        var options = new DbContextOptionsBuilder<HomologacionDbContext>()
            .UseInMemoryDatabase($"HomologacionTests_{Guid.NewGuid()}")
            .Options;
        return new HomologacionDbContext(options);
    }

    private static async Task<(int SolicitudId, int AsignaturaId)> SembrarAnalisisAsync(
        HomologacionDbContext db, string estado)
    {
        const int solicitudId = 10;
        var analisis = new AnalisisSolicitud
        {
            SolicitudId = solicitudId,
            Estado = estado,
            FechaAnalisis = DateTime.UtcNow
        };
        analisis.Asignaturas.Add(new AnalisisAsignatura
        {
            AsignaturaDestinoId = 5,
            AsignaturaOrigen = "Cálculo Diferencial",
            AsignaturaDestino = "Cálculo I",
            Decision = DecisionAnalisis.HOMOLOGABLE,
            Justificacion = "Contenido equivalente.",
            PorcentajeContenido = 90,
            PorcentajeProfundidad = 85,
            PorcentajeHoras = 100
        });

        db.AnalisisSolicitudes.Add(analisis);
        await db.SaveChangesAsync();

        return (solicitudId, analisis.Asignaturas[0].Id);
    }

    private static ResolucionService CrearServicio(HomologacionDbContext db, out HandlerOk handler)
    {
        handler = new HandlerOk();
        var http = new HttpClient(handler);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Solicitudes:Url"] = "http://solicitudes",
                ["Solicitudes:TimeoutSegundos"] = "5"
            })
            .Build();
        var cliente = new SolicitudesClient(http, config, NullLogger<SolicitudesClient>.Instance);
        return new ResolucionService(db, cliente, NullLogger<ResolucionService>.Instance);
    }

    private static ResolucionRequest Request(int asignaturaId, string numero = "RES-2026-001") => new()
    {
        NumeroResolucion = numero,
        Fecha = new DateTime(2026, 1, 15),
        Observacion = "Resolución aprobada por el Consejo.",
        Decisiones = new List<ResolucionDecisionRequest>
        {
            new() { AsignaturaId = asignaturaId, DecisionFinal = "homologable" }
        }
    };

    private sealed class HandlerOk : HttpMessageHandler
    {
        public List<string> Peticiones { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Peticiones.Add($"{request.Method} {request.RequestUri}");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
        }
    }
}
