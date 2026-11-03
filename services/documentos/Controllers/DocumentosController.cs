using Microsoft.AspNetCore.Mvc;
using HomologaUTA.Documentos.DTOs;
using HomologaUTA.Documentos.Data;
using HomologaUTA.Documentos.Models;
using HomologaUTA.Documentos.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims; // <-- NUEVO para leer el token

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

    public DocumentosController(
        IConfiguration config, 
        IWebHostEnvironment env, 
        DocumentosDbContext db, 
        ExtractorPdfService extractor,
        EventPublisherService eventPublisher)
    {
        _config = config;
        _env = env;
        _db = db;
        _extractor = extractor;
        _eventPublisher = eventPublisher;
    }

    [HttpPost]
    public async Task<IActionResult> SubirDocumento([FromForm] DocumentoUploadRequest request)
    {
        // 1. Validaciones
        if (request.Archivo == null || request.Archivo.Length == 0)
            return BadRequest(new { error = "No se envió ningún archivo." });

        if (request.Archivo.ContentType != "application/pdf" && !request.Archivo.FileName.EndsWith(".pdf"))
            return BadRequest(new { error = "El archivo debe ser un PDF." });

        var tamanioMaximoMB = _config.GetValue<int>("TamanioMaximoPdfMB", 10);
        if (request.Archivo.Length > tamanioMaximoMB * 1024 * 1024)
            return BadRequest(new { error = $"El archivo no debe superar los {tamanioMaximoMB} MB." });

        // Extraer el correo del estudiante desde el token
        var estudianteId = User.Claims.FirstOrDefault(c => c.Type == "correo")?.Value 
                           ?? User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value 
                           ?? "anonimo";

        // 2. Guardar archivo en disco
        var carpetaPdfs = Path.Combine(_env.ContentRootPath, "pdfs");
        if (!Directory.Exists(carpetaPdfs))
            Directory.CreateDirectory(carpetaPdfs);

        var nombreArchivo = $"{request.SolicitudId}_{request.TipoDocumento}_{Guid.NewGuid()}.pdf";
        var rutaCompleta = Path.Combine(carpetaPdfs, nombreArchivo);

        using (var stream = new FileStream(rutaCompleta, FileMode.Create))
        {
            await request.Archivo.CopyToAsync(stream);
        }

        // 3. Extraer texto (PdfPig)
        var textoExtraido = _extractor.ExtraerTexto(rutaCompleta);

        // 4. Guardar en la base de datos
        var documento = new Documento
        {
            SolicitudId = request.SolicitudId,
            TipoDocumento = request.TipoDocumento,
            NombreArchivo = nombreArchivo,
            TextoExtraido = textoExtraido,
            Estado = "PROCESADO",
            FechaSubida = DateTime.UtcNow,
            EstudianteId = estudianteId // <-- NUEVO
        };

        _db.Documentos.Add(documento);
        await _db.SaveChangesAsync();

        // 5. PUBLICAR EVENTO EN RABBITMQ
        await _eventPublisher.PublicarDocumentoProcesadoAsync(
            documento.Id, 
            documento.SolicitudId, 
            documento.TextoExtraido
        );

        return Ok(new
        {
            mensaje = "Archivo subido, procesado, guardado y evento publicado.",
            documentoId = documento.Id,
            textoExtraido = textoExtraido.Substring(0, Math.Min(50, textoExtraido.Length)) + "..."
        });
    }

    [HttpGet("solicitud/{solicitudId}")]
    public async Task<IActionResult> ObtenerDocumentosPorSolicitud(int solicitudId)
    {
        // Extraer el correo del estudiante del token
        var estudianteId = User.Claims.FirstOrDefault(c => c.Type == "correo")?.Value 
                           ?? User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

        if (string.IsNullOrEmpty(estudianteId))
            return Unauthorized(new { error = "No autorizado." });

        // Buscar los documentos y verificar que pertenezcan al estudiante
        var documentos = await _db.Documentos
            .Where(d => d.SolicitudId == solicitudId && d.EstudianteId == estudianteId)
            .Select(d => new { d.Id, d.TipoDocumento, d.NombreArchivo, d.Estado, d.FechaSubida })
            .ToListAsync();

        return Ok(documentos);
    }
}