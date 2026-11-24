using Microsoft.AspNetCore.Mvc;
using HomologaUTA.Documentos.DTOs;
using HomologaUTA.Documentos.Data;
using HomologaUTA.Documentos.Models;
using HomologaUTA.Documentos.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HomologaUTA.Documentos.Controllers;

[ApiController]
[Route("documentos")]
public class DocumentosController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly DocumentosDbContext _db;
    private readonly ExtractorPdfService _extractor;
    private readonly EventPublisherService _eventPublisher;
    private readonly CarpetaService? _carpetaService;

    public DocumentosController(
        IConfiguration config, 
        IWebHostEnvironment env, 
        DocumentosDbContext db, 
        ExtractorPdfService extractor,
        EventPublisherService eventPublisher,
        CarpetaService? carpetaService = null)
    {
        _config = config;
        _env = env;
        _db = db;
        _extractor = extractor;
        _eventPublisher = eventPublisher;
        _carpetaService = carpetaService;
    }

    [HttpPost]
    public async Task<IActionResult> SubirDocumento([FromForm] DocumentoUploadRequest request)
    {
        if (request.Archivo == null || request.Archivo.Length == 0)
            return BadRequest(new { error = "No se envió ningún archivo." });

        if (request.Archivo.ContentType != "application/pdf" && !request.Archivo.FileName.EndsWith(".pdf"))
            return BadRequest(new { error = "El archivo debe ser un PDF." });

        var tamanioMaximoMB = _config.GetValue<int>("TamanioMaximoPdfMB", 10);
        if (request.Archivo.Length > tamanioMaximoMB * 1024 * 1024)
            return BadRequest(new { error = $"El archivo no debe superar los {tamanioMaximoMB} MB." });

        var estudianteId = User.FindFirstValue("email")
                           ?? User.FindFirstValue(ClaimTypes.Email)
                           ?? User.FindFirstValue("sub")
                           ?? "anonimo";

        var carpetaPdfs = Path.Combine(_env.ContentRootPath, "pdfs");
        if (!Directory.Exists(carpetaPdfs))
            Directory.CreateDirectory(carpetaPdfs);

        var nombreArchivo = $"{request.SolicitudId}_{request.TipoDocumento}_{Guid.NewGuid()}.pdf";
        var rutaCompleta = Path.Combine(carpetaPdfs, nombreArchivo);

        using (var stream = new FileStream(rutaCompleta, FileMode.Create))
        {
            await request.Archivo.CopyToAsync(stream);
        }

        var textoExtraido = _extractor.ExtraerTexto(rutaCompleta);

        var documento = new Documento
        {
            SolicitudId = request.SolicitudId,
            TipoDocumento = request.TipoDocumento,
            NombreArchivo = nombreArchivo,
            TextoExtraido = textoExtraido,
            Estado = "PROCESADO",
            FechaSubida = DateTime.UtcNow,
            EstudianteId = estudianteId
        };

        _db.Documentos.Add(documento);
        await _db.SaveChangesAsync();

        await _eventPublisher.PublicarDocumentoProcesadoAsync(
            documento.Id, 
            documento.SolicitudId, 
            documento.TextoExtraido
        );

        return Ok(new
        {
            mensaje = "Archivo subido, procesado, guardado y evento publicado.",
            documentoId = documento.Id,
            nombreArchivo = nombreArchivo,
            textoExtraido = textoExtraido.Substring(0, Math.Min(50, textoExtraido.Length)) + "..."
        });
    }

    // Procesa todos los PDF de una carpeta en una sola petición (sin IFormFile).
    [HttpPost("carpeta")]
    public async Task<IActionResult> ProcesarCarpeta([FromBody] CarpetaRequest request, CancellationToken ct)
    {
        try
        {
            if (_carpetaService is null)
                return StatusCode(500, new { error = "El servicio de procesamiento de carpetas no está disponible." });

            var resultado = await _carpetaService.ProcesarAsync(request, ct);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Error inesperado al procesar la carpeta.", detalle = ex.Message });
        }
    }

    // Registra la URL de la carpeta compartida por el titular (solo homologación externa).
    [HttpPost("url-externa")]
    public async Task<IActionResult> RegistrarUrlExterna([FromBody] UrlExternaRequest request, CancellationToken ct)
    {
        try
        {
            if (_carpetaService is null)
                return StatusCode(500, new { error = "El servicio de documentos no está disponible." });

            var documento = await _carpetaService.RegistrarUrlExternaAsync(request, ct);
            return Ok(new
            {
                mensaje = "URL externa registrada para la solicitud.",
                documentos = new[] { documento },
                total = 1
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Error inesperado al registrar la URL externa.", detalle = ex.Message });
        }
    }

    [HttpGet("solicitud/{solicitudId}")]
    public async Task<IActionResult> ObtenerDocumentosPorSolicitud(int solicitudId)
    {
        var estudianteId = User.FindFirstValue("email")
                           ?? User.FindFirstValue(ClaimTypes.Email)
                           ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(estudianteId))
            return Unauthorized(new { error = "No autorizado." });

        var documentos = await _db.Documentos
            .Where(d => d.SolicitudId == solicitudId && d.EstudianteId == estudianteId)
            .ToListAsync();

        var carpetaPdfs = Path.Combine(_env.ContentRootPath, "pdfs");
        var resultado = documentos.Select(d =>
        {
            var rutaCompleta = Path.Combine(carpetaPdfs, d.NombreArchivo);
            long? tamanoBytes = System.IO.File.Exists(rutaCompleta)
                ? new FileInfo(rutaCompleta).Length
                : null;

            return new
            {
                d.Id,
                d.TipoDocumento,
                d.NombreArchivo,
                d.Estado,
                d.FechaSubida,
                TamanoBytes = tamanoBytes
            };
        }).ToList();

        return Ok(resultado);
    }

    // --- ENDPOINT DEFINITIVO Y A PRUEBA DE BALAS ---
    [HttpGet("descargar/{identificador}")]
    public async Task<IActionResult> DescargarPdf(string identificador)
    {
        Documento documento = null;

        // 1. Si el frontend envía un número (ID), buscamos por ID
        if (int.TryParse(identificador, out int id))
        {
            documento = await _db.Documentos.FindAsync(id);
        }
        // 2. Si el frontend envía el nombre del archivo, buscamos por NombreArchivo
        else
        {
            documento = await _db.Documentos.FirstOrDefaultAsync(d => d.NombreArchivo == identificador);
        }

        if (documento == null)
            return NotFound(new { error = "El documento no existe en la base de datos." });

        var carpetaPdfs = Path.Combine(_env.ContentRootPath, "pdfs");
        var rutaCompleta = Path.Combine(carpetaPdfs, documento.NombreArchivo);

        if (!System.IO.File.Exists(rutaCompleta))
            return NotFound(new { error = "El archivo físico no existe en el servidor. Intenta subirlo de nuevo." });

        // Devolvemos el archivo con un nombre genérico para que el navegador lo abra bien
        return PhysicalFile(rutaCompleta, "application/pdf", "Documento_PDF.pdf");
    }
}