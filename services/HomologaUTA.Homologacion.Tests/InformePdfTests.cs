using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Models;
using HomologaUTA.Homologacion.Reportes;
using HomologaUTA.Homologacion.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace HomologaUTA.Homologacion.Tests;

public class InformePdfTests
{
    // ---------- Plantilla (solo dibuja) ----------

    private static InformeData DatosBase(int asignaturas = 2) => new()
    {
        SolicitudId = 7,
        NumeroProceso = "SOL-0007",
        Estudiante = "Ana Pérez",
        FechaRegistro = new DateTime(2026, 11, 2),
        CarreraOrigen = "Software",
        CarreraDestino = "Tecnologías de la Información",
        Modulo = "INTERNO",
        FechaAnalisis = new DateTime(2026, 11, 3),
        Asignaturas = Enumerable.Range(1, asignaturas).Select(i => new InformeAsignatura
        {
            AsignaturaOrigen = $"Asignatura origen {i}",
            AsignaturaDestino = $"Asignatura destino {i}",
            PorcentajeContenido = 80,
            PorcentajeProfundidad = 70,
            PorcentajeHoras = 100,
            Decision = i % 3 == 0 ? "NO_HOMOLOGABLE" : i % 2 == 0 ? "REQUIERE_REVISION" : "HOMOLOGABLE",
            Justificacion = "Los contenidos coinciden en su mayoría; se cubren los temas principales de la asignatura."
        }).ToList()
    };

    private static void AssertEsPdf(byte[] bytes)
    {
        Assert.True(bytes.Length > 1000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void Plantilla_SinInformeNiResolucion_GeneraPdf()
    {
        AssertEsPdf(PlantillaInforme.Generar(DatosBase()));
    }

    [Fact]
    public void Plantilla_ConInformeDelCoordinador_GeneraPdf()
    {
        var d = DatosBase();
        d.CoordinadorCorreo = "coordinador@uta.edu.ec";
        d.FechaInforme = new DateTime(2026, 11, 5);
        d.Asignaturas[0].DecisionCoordinador = "HOMOLOGABLE";
        d.Asignaturas[0].ObservacionCoordinador = "Coincide el sílabo.";
        d.Asignaturas[1].DecisionCoordinador = "NO_HOMOLOGABLE";

        AssertEsPdf(PlantillaInforme.Generar(d));
    }

    [Fact]
    public void Plantilla_ConResolucionYModuloExterno_GeneraPdf()
    {
        var d = DatosBase();
        d.Modulo = "EXTERNO";
        d.UniversidadOrigen = "Universidad Central del Ecuador";
        d.UniversidadDestino = "Universidad Técnica de Ambato";
        d.FechaInforme = new DateTime(2026, 11, 5);
        d.Resolucion = new InformeResolucion
        {
            NumeroResolucion = "RES-2026-045",
            Fecha = new DateTime(2026, 11, 10),
            Observacion = "Aprobada en sesión ordinaria.",
            Decisiones = new() { new InformeDecisionFinal { Asignatura = "Asignatura origen 1", Decision = "HOMOLOGABLE" } }
        };

        AssertEsPdf(PlantillaInforme.Generar(d));
    }

    [Fact]
    public void Plantilla_ConMuchasAsignaturas_PaginaSinFallar()
    {
        var corto = PlantillaInforme.Generar(DatosBase(2));
        var largo = PlantillaInforme.Generar(DatosBase(80));

        AssertEsPdf(largo);
        Assert.True(largo.Length > corto.Length);
    }

    [Fact]
    public void Plantilla_SinAsignaturas_GeneraPdf()
    {
        AssertEsPdf(PlantillaInforme.Generar(DatosBase(0)));
    }

    // ---------- Servicio (reúne datos y genera) ----------

    private class HandlerFalso : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(_responder(request));
    }

    private static SolicitudesClient ClienteSolicitudes(params int[] idsExistentes)
    {
        var handler = new HandlerFalso(req =>
        {
            var id = int.Parse(req.RequestUri!.Segments[^2].TrimEnd('/'));
            if (!idsExistentes.Contains(id))
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            var dto = new SolicitudCompletaDto
            {
                Id = id,
                EstudianteNombre = "Ana Pérez",
                EstudianteCorreo = "comite@uta.edu.ec",
                FechaRegistro = new DateTime(2026, 11, 2),
                CarreraOrigen = "Software",
                CarreraDestino = "TI",
                Modulo = "INTERNO"
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(dto) };
        });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Solicitudes:Url"] = "http://solicitudes.test" })
            .Build();

        return new SolicitudesClient(new HttpClient(handler), config, NullLogger<SolicitudesClient>.Instance);
    }

    private static HomologacionDbContext BdConAnalisis(int solicitudId, bool conAnalisis = true)
    {
        var opciones = new DbContextOptionsBuilder<HomologacionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new HomologacionDbContext(opciones);

        if (conAnalisis)
        {
            var analisis = new AnalisisSolicitud
            {
                SolicitudId = solicitudId,
                FechaAnalisis = new DateTime(2026, 11, 3)
            };
            analisis.Asignaturas.Add(new AnalisisAsignatura
            {
                AsignaturaOrigen = "Programación I",
                AsignaturaDestino = "Fundamentos de Programación",
                PorcentajeContenido = 90,
                PorcentajeProfundidad = 85,
                PorcentajeHoras = 100,
                Decision = "HOMOLOGABLE",
                Justificacion = "Contenidos equivalentes."
            });
            analisis.Asignaturas.Add(new AnalisisAsignatura
            {
                AsignaturaOrigen = "Cálculo",
                AsignaturaDestino = "Matemática I",
                PorcentajeContenido = 40,
                PorcentajeProfundidad = 50,
                PorcentajeHoras = 60,
                Decision = "NO_HOMOLOGABLE",
                Justificacion = "Cobertura insuficiente."
            });

            db.AnalisisSolicitudes.Add(analisis);
            db.SaveChanges();
        }
        return db;
    }

    [Fact]
    public async Task GenerarPdf_ConSolicitudYAnalisis_DevuelvePdf()
    {
        using var db = BdConAnalisis(5);
        var servicio = new InformeService(db, ClienteSolicitudes(5), NullLogger<InformeService>.Instance);

        AssertEsPdf(await servicio.GenerarPdfAsync(5));
    }

    [Fact]
    public async Task ReunirDatos_ArmaNumeroDeProcesoYAsignaturas()
    {
        using var db = BdConAnalisis(5);
        var servicio = new InformeService(db, ClienteSolicitudes(5), NullLogger<InformeService>.Instance);

        var datos = await servicio.ReunirDatosAsync(5);

        Assert.Equal("SOL-0005", datos.NumeroProceso);
        Assert.Equal("Ana Pérez", datos.Estudiante);
        Assert.Equal(2, datos.Asignaturas.Count);
        Assert.Null(datos.Resolucion);   // sin resolución del Consejo: la sección se omite
    }

    [Fact]
    public async Task GenerarPdf_SolicitudInexistente_Lanza404()
    {
        using var db = BdConAnalisis(5);
        var servicio = new InformeService(db, ClienteSolicitudes(5), NullLogger<InformeService>.Instance);

        var ex = await Assert.ThrowsAsync<InformeException>(() => servicio.GenerarPdfAsync(99));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task GenerarPdf_SolicitudSinAnalisis_Lanza404()
    {
        using var db = BdConAnalisis(5, conAnalisis: false);
        var servicio = new InformeService(db, ClienteSolicitudes(5), NullLogger<InformeService>.Instance);

        var ex = await Assert.ThrowsAsync<InformeException>(() => servicio.GenerarPdfAsync(5));
        Assert.Equal(404, ex.StatusCode);
    }
}
