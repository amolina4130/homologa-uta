using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using HomologaUTA.Documentos.Controllers;
using HomologaUTA.Documentos.Data;
using HomologaUTA.Documentos.Services;
using HomologaUTA.Documentos.DTOs;

namespace documentos.Tests
{
    // 1. SIMULACIÓN DEL EXTRACTOR DE PDF (Usando override, ya que es virtual)
    public class ExtractorPdfServiceFake : ExtractorPdfService
    {
        public override string ExtraerTexto(string ruta)
        {
            return "Texto simulado extraído del PDF";
        }
    }

    public class DocumentosControllerTests
    {
        private DocumentosController CrearControlador()
        {
            // 1. Base de datos en memoria
            var options = new DbContextOptionsBuilder<DocumentosDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            var dbContext = new DocumentosDbContext(options);

            // 2. Configuración
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    {"TamanioMaximoPdfMB", "10"}
                })
                .Build();

            // 3. Entorno falso
            var env = new EntornoFalso();

            // 4. Extractor falso (el que sí es virtual)
            var extractor = new ExtractorPdfServiceFake();

            // 5. ¡AQUÍ ESTÁ EL TRUCO! Usamos Moq para el EventPublisher
            // Así evitamos que el constructor real intente conectarse a RabbitMQ
            var mockEventPublisher = new Mock<EventPublisherService>(MockBehavior.Loose, config);
            mockEventPublisher.Setup(x => x.PublicarDocumentoProcesadoAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                              .Returns(Task.CompletedTask);

            return new DocumentosController(config, env, dbContext, extractor, mockEventPublisher.Object);
        }

        private class EntornoFalso : IWebHostEnvironment
        {
            public string ApplicationName { get; set; } = "Tests";
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string WebRootPath { get; set; } = string.Empty;
            public string EnvironmentName { get; set; } = "Tests";
            public string ContentRootPath { get; set; } = Path.GetTempPath();
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }

        private static IFormFile CrearArchivo(string nombre, string contentType, long bytes)
        {
            var stream = new MemoryStream(new byte[bytes]);
            return new FormFile(stream, 0, bytes, "Archivo", nombre)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }

        [Fact]
        public async Task SubirDocumento_PdfValido_RetornaOk()
        {
            var controller = CrearControlador();
            var request = new DocumentoUploadRequest
            {
                Archivo = CrearArchivo("malla.pdf", "application/pdf", 1024),
                SolicitudId = 1,
                TipoDocumento = "MALLA"
            };

            var result = await controller.SubirDocumento(request);
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task SubirDocumento_ArchivoNoPdf_RetornaBadRequest()
        {
            var controller = CrearControlador();
            var request = new DocumentoUploadRequest
            {
                Archivo = CrearArchivo("notas.txt", "text/plain", 1024),
                SolicitudId = 1,
                TipoDocumento = "MALLA"
            };

            var result = await controller.SubirDocumento(request);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task SubirDocumento_ArchivoExcedeTamano_RetornaBadRequest()
        {
            var controller = CrearControlador();
            var request = new DocumentoUploadRequest
            {
                Archivo = CrearArchivo("grande.pdf", "application/pdf", 15 * 1024 * 1024),
                SolicitudId = 1,
                TipoDocumento = "MALLA"
            };

            var result = await controller.SubirDocumento(request);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task ObtenerDocumentosPorSolicitud_SolicitudSinDocumentos_RetornaListaVacia()
        {
            var controller = CrearControlador();
            var result = await controller.ObtenerDocumentosPorSolicitud(99);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var lista = Assert.IsAssignableFrom<System.Collections.IEnumerable>(okResult.Value);
            Assert.Empty(lista);
        }
    }
}