using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
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
    // Simulación del extractor de PDF
    public class ExtractorPdfServiceFake : ExtractorPdfService
    {
        public override string ExtraerTexto(string ruta)
        {
            return "Texto simulado extraído del PDF";
        }
    }

    public class DocumentosControllerTests
    {
        // Ahora recibe el correo del estudiante para simular el token
        private DocumentosController CrearControlador(string correoEstudiante = "estudiante@uta.edu.ec")
        {
            var options = new DbContextOptionsBuilder<DocumentosDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            var dbContext = new DocumentosDbContext(options);

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    {"TamanioMaximoPdfMB", "10"}
                })
                .Build();

            var env = new EntornoFalso();
            var extractor = new ExtractorPdfServiceFake();

            var mockEventPublisher = new Mock<EventPublisherService>(MockBehavior.Loose, config);
            mockEventPublisher.Setup(x => x.PublicarDocumentoProcesadoAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                              .Returns(Task.CompletedTask);

            var controller = new DocumentosController(config, env, dbContext, extractor, mockEventPublisher.Object);

            // Simulamos el token del estudiante
            var claims = new List<Claim> { new Claim("correo", correoEstudiante) };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            return controller;
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

               // 1. PRUEBA: PDF válido (con archivo real)
        [Fact]
        public async Task SubirDocumento_PdfValido_RetornaOk()
        {
            var controller = CrearControlador();
            
            // Leemos el PDF real desde la carpeta de pruebas
            var rutaPdf = Path.Combine(Directory.GetCurrentDirectory(), "prueba.pdf");
            
            if (!File.Exists(rutaPdf))
            {
                throw new FileNotFoundException($"No se encontró el archivo PDF en: {rutaPdf}. Asegúrate de haber copiado 'prueba.pdf' a la carpeta de pruebas.");
            }

            var stream = new FileStream(rutaPdf, FileMode.Open, FileAccess.Read);
            
            var request = new DocumentoUploadRequest
            {
                Archivo = new FormFile(stream, 0, stream.Length, "Archivo", "prueba.pdf")
                {
                    Headers = new HeaderDictionary(),
                    ContentType = "application/pdf"
                },
                SolicitudId = 1,
                TipoDocumento = "MALLA"
            };

            var result = await controller.SubirDocumento(request);
            
            stream.Close();

            Assert.IsType<OkObjectResult>(result);
        }

        // 2. PRUEBA: Archivo que no es PDF
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

        // 3. PRUEBA: Archivo que excede el tamaño máximo
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

        // 4. PRUEBA: Consulta de documentos por un estudiante distinto
        [Fact]
        public async Task ObtenerDocumentosPorSolicitud_EstudianteDistinto_RetornaListaVacia()
        {
            // 1. Creamos un controlador para el estudiante 1 y subimos un documento
            var controllerEstudiante1 = CrearControlador("estudiante1@uta.edu.ec");
            var request = new DocumentoUploadRequest
            {
                Archivo = CrearArchivo("malla.pdf", "application/pdf", 1024),
                SolicitudId = 1,
                TipoDocumento = "MALLA"
            };
            await controllerEstudiante1.SubirDocumento(request);

            // 2. Creamos un controlador para el estudiante 2 (el intruso)
            var controllerEstudiante2 = CrearControlador("estudiante2@uta.edu.ec");

            // 3. El estudiante 2 intenta consultar la solicitud 1
            var result = await controllerEstudiante2.ObtenerDocumentosPorSolicitud(1);

            // 4. Verificamos que la lista esté vacía (porque los documentos son del estudiante 1)
            var okResult = Assert.IsType<OkObjectResult>(result);
            var lista = Assert.IsAssignableFrom<System.Collections.IEnumerable>(okResult.Value);
            Assert.Empty(lista);
        }
    }
}