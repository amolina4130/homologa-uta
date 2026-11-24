using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HomologaUTA.Documentos.Controllers;
using HomologaUTA.Documentos.Data;
using HomologaUTA.Documentos.DTOs;
using HomologaUTA.Documentos.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace documentos.Tests;

public class CarpetaServiceTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly string _raiz;

    public CarpetaServiceTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), "carpeta-tests-" + Guid.NewGuid().ToString("N"));
        _raiz = Path.Combine(_contentRoot, "documentos");
        Directory.CreateDirectory(_raiz);
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
            Directory.Delete(_contentRoot, recursive: true);
    }

    private IConfiguration CrearConfig(
        int maximo = 50,
        string? prefijoWindows = null,
        string? raizContenedor = null)
    {
        var valores = new Dictionary<string, string?>
        {
            ["Documentos:Raiz"] = _raiz,
            ["Documentos:MaxArchivosCarpeta"] = maximo.ToString()
        };

        if (!string.IsNullOrWhiteSpace(prefijoWindows) && !string.IsNullOrWhiteSpace(raizContenedor))
        {
            valores["Documentos:Raices:0:Windows"] = prefijoWindows;
            valores["Documentos:Raices:0:Contenedor"] = raizContenedor;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
    }

    private static DocumentosDbContext CrearDb() => new(new DbContextOptionsBuilder<DocumentosDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private static Mock<EventPublisherService> CrearPublisher(IConfiguration config)
    {
        var mock = new Mock<EventPublisherService>(MockBehavior.Loose, config);
        mock.Setup(p => p.PublicarDocumentoProcesadoAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    private CarpetaService CrearServicio(IConfiguration config, Mock<EventPublisherService> publisher, DocumentosDbContext db) =>
        new(db, new ExtractorTextoLargoFalso(), publisher.Object, config,
            NullLogger<CarpetaService>.Instance, new EntornoFalso(_contentRoot));

    private void CrearPdf(string carpeta, string nombre)
    {
        Directory.CreateDirectory(carpeta);
        File.WriteAllBytes(Path.Combine(carpeta, nombre), new byte[] { 0x25, 0x50, 0x44, 0x46 });
    }

    [Fact]
    public async Task ProcesarAsync_RutaConUnidadWindows_ClasificaGuardaYPublicaTodo()
    {
        var destino = Path.Combine(_raiz, "CarpetaUTA");
        CrearPdf(destino, "Malla Curricular 2026.pdf");
        CrearPdf(destino, "Récord Académico.pdf");
        CrearPdf(destino, "SÍLABO de Matemáticas.PDF");
        CrearPdf(destino, "plan analitico.pdf");
        CrearPdf(destino, "foto.png"); // debe ignorarse

        var config = CrearConfig();
        var publisher = CrearPublisher(config);
        var db = CrearDb();
        var servicio = CrearServicio(config, publisher, db);

        var resultado = await servicio.ProcesarAsync(
            new CarpetaRequest { SolicitudId = 42, Ruta = @"C:\Users\Usuario\CarpetaUTA" },
            CancellationToken.None);

        Assert.Equal(4, resultado.Total);
        Assert.Equal(4, resultado.Documentos.Count);
        Assert.All(resultado.Documentos, d =>
        {
            Assert.Equal(120, d.TextoExtraido.Length);
            Assert.Contains("_CARPETA_", d.NombreArchivo);
            Assert.EndsWith(".pdf", d.NombreArchivo);
            Assert.True(d.Id > 0);
        });
        Assert.Equal(
            new[] { "HISTORIAL", "MALLA", "PLAN_ANALITICO", "SILABO" },
            resultado.Documentos.Select(d => d.TipoDocumento).OrderBy(t => t).ToArray());

        var guardados = await db.Documentos.ToListAsync();
        Assert.Equal(4, guardados.Count);
        Assert.All(guardados, d =>
        {
            Assert.Equal(42, d.SolicitudId);
            Assert.Equal("comite@uta.edu.ec", d.EstudianteId);
            Assert.Equal("PROCESADO", d.Estado);
            Assert.Equal(200, d.TextoExtraido.Length);
        });

        publisher.Verify(
            p => p.PublicarDocumentoProcesadoAsync(It.IsAny<int>(), 42, It.IsAny<string>()),
            Times.Exactly(4));
        Assert.Equal(4, Directory.GetFiles(Path.Combine(_contentRoot, "pdfs"), "*_CARPETA_*.pdf").Length);
    }

    [Fact]
    public async Task ProcesarAsync_ConUsuario_ConservaElEstudianteIndicado()
    {
        var destino = Path.Combine(_raiz, "CarpetaUTA");
        CrearPdf(destino, "historial notas.pdf");

        var config = CrearConfig();
        var publisher = CrearPublisher(config);
        var db = CrearDb();
        var resultado = await CrearServicio(config, publisher, db).ProcesarAsync(
            new CarpetaRequest { SolicitudId = 7, Ruta = "C:/Users/Usuario/CarpetaUTA", Usuario = "estudiante@uta.edu.ec" },
            CancellationToken.None);

        Assert.Equal(1, resultado.Total);
        Assert.Equal("HISTORIAL", resultado.Documentos[0].TipoDocumento);
        Assert.Equal("estudiante@uta.edu.ec", (await db.Documentos.SingleAsync()).EstudianteId);
    }

    [Fact]
    public async Task ProcesarAsync_RutaDeDescargasMapeada_LeeCarpetaDelVolumenSoloLectura()
    {
        var descargas = Path.Combine(_contentRoot, "descargas");
        var silabos = Path.Combine(descargas, "CARPETAESTUDIANTE", "Sílabo");
        CrearPdf(silabos, "SÍLABO de Software.pdf");

        var config = CrearConfig(
            prefijoWindows: @"C:\Users\Usuario\Downloads",
            raizContenedor: descargas);
        var publisher = CrearPublisher(config);
        var db = CrearDb();

        var resultado = await CrearServicio(config, publisher, db).ProcesarAsync(
            new CarpetaRequest
            {
                SolicitudId = 9,
                Ruta = @"C:\Users\Usuario\Downloads\CARPETAESTUDIANTE\Sílabo"
            },
            CancellationToken.None);

        Assert.Equal(1, resultado.Total);
        Assert.Equal("SILABO", resultado.Documentos[0].TipoDocumento);
        Assert.Equal(9, (await db.Documentos.SingleAsync()).SolicitudId);
    }

    [Fact]
    public async Task ProcesarAsync_ExcedeMaximo_ProcesaSoloElMaximo()
    {
        var destino = Path.Combine(_raiz, "CarpetaUTA");
        for (var i = 0; i < 4; i++)
            CrearPdf(destino, $"silabo-{i}.pdf");

        var config = CrearConfig(maximo: 2);
        var publisher = CrearPublisher(config);
        var db = CrearDb();
        var resultado = await CrearServicio(config, publisher, db).ProcesarAsync(
            new CarpetaRequest { SolicitudId = 1, Ruta = "CarpetaUTA" }, CancellationToken.None);

        Assert.Equal(2, resultado.Total);
        Assert.Equal(2, await db.Documentos.CountAsync());
    }

    [Theory]
    [InlineData("malla.pdf", "MALLA")]
    [InlineData("MALLA CURRICULAR.PDF", "MALLA")]
    [InlineData("record academico.pdf", "HISTORIAL")]
    [InlineData("Récord Académico.pdf", "HISTORIAL")]
    [InlineData("historial.pdf", "HISTORIAL")]
    [InlineData("notas finales.pdf", "HISTORIAL")]
    [InlineData("silabo.pdf", "SILABO")]
    [InlineData("SÍLABO de Física.pdf", "SILABO")]
    [InlineData("plan analitico.pdf", "PLAN_ANALITICO")]
    [InlineData("PLAN ANALÍTICO.pdf", "PLAN_ANALITICO")]
    [InlineData("certificado.pdf", "OTRO")]
    public void Clasificar_PorNombre_DevuelveElTipoEsperado(string nombre, string esperado) =>
        Assert.Equal(esperado, CarpetaService.Clasificar(nombre));

    [Fact]
    public async Task ProcesarAsync_RutaVacia_LanzaInvalidOperationException()
    {
        var config = CrearConfig();
        var servicio = CrearServicio(config, CrearPublisher(config), CrearDb());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.ProcesarAsync(new CarpetaRequest { SolicitudId = 1, Ruta = "  " }, CancellationToken.None));
    }

    [Fact]
    public async Task ProcesarAsync_RutaConTraversal_LanzaInvalidOperationException()
    {
        var config = CrearConfig();
        var servicio = CrearServicio(config, CrearPublisher(config), CrearDb());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.ProcesarAsync(new CarpetaRequest { SolicitudId = 1, Ruta = "C:/Users/../ secretos" }, CancellationToken.None));
    }

    [Fact]
    public async Task ProcesarAsync_CarpetaInexistente_LanzaInvalidOperationException()
    {
        var config = CrearConfig();
        var servicio = CrearServicio(config, CrearPublisher(config), CrearDb());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.ProcesarAsync(new CarpetaRequest { SolicitudId = 1, Ruta = "NoExiste" }, CancellationToken.None));

        Assert.Contains("No se encontró la carpeta", ex.Message);
    }

    [Fact]
    public async Task ProcesarAsync_SinPdf_LanzaInvalidOperationException()
    {
        Directory.CreateDirectory(Path.Combine(_raiz, "Vacia"));
        File.WriteAllText(Path.Combine(_raiz, "Vacia", "notas.txt"), "sin pdf");

        var config = CrearConfig();
        var servicio = CrearServicio(config, CrearPublisher(config), CrearDb());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.ProcesarAsync(new CarpetaRequest { SolicitudId = 1, Ruta = "Vacia" }, CancellationToken.None));
    }

    [Fact]
    public async Task ProcesarCarpeta_RutaInvalida_RetornaBadRequest()
    {
        var config = CrearConfig();
        var db = CrearDb();
        var servicio = CrearServicio(config, CrearPublisher(config), db);
        var controller = new DocumentosController(
            config, new EntornoFalso(_contentRoot), db,
            new ExtractorTextoLargoFalso(), CrearPublisher(config).Object, servicio);

        var resultado = await controller.ProcesarCarpeta(
            new CarpetaRequest { SolicitudId = 1, Ruta = "../../escape" }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);
    }

    public class ExtractorTextoLargoFalso : ExtractorPdfService
    {
        public override string ExtraerTexto(string rutaPdf) => new('X', 200);
    }

    private class EntornoFalso : IWebHostEnvironment
    {
        public EntornoFalso(string contentRoot) => ContentRootPath = contentRoot;

        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Tests";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
