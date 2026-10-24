using Microsoft.AspNetCore.Mvc;
using HomologaUTA.Documentos.DTOs;

namespace HomologaUTA.Documentos.Controllers;

[ApiController]
[Route("documentos")]
public class DocumentosController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public DocumentosController(IConfiguration config, IWebHostEnvironment env)
    {
        _config = config;
        _env = env;
    }

    [HttpPost]
    public async Task<IActionResult> SubirDocumento([FromForm] DocumentoUploadRequest request)
    {
        // 1. Validar que se envió un archivo
        if (request.Archivo == null || request.Archivo.Length == 0)
            return BadRequest(new { error = "No se envió ningún archivo." });

        // 2. Validar que sea un PDF
        if (request.Archivo.ContentType != "application/pdf" && !request.Archivo.FileName.EndsWith(".pdf"))
            return BadRequest(new { error = "El archivo debe ser un PDF." });

        // 3. Validar el tamaño máximo
        var tamanioMaximoMB = _config.GetValue<int>("TamanioMaximoPdfMB", 10);
        var tamanioMaximoBytes = tamanioMaximoMB * 1024 * 1024;
        
        if (request.Archivo.Length > tamanioMaximoBytes)
            return BadRequest(new { error = $"El archivo no debe superar los {tamanioMaximoMB} MB." });

        // 4. Guardar el archivo
        var carpetaPdfs = Path.Combine(_env.ContentRootPath, "pdfs");
        if (!Directory.Exists(carpetaPdfs))
            Directory.CreateDirectory(carpetaPdfs);

        var nombreArchivo = $"{request.SolicitudId}_{request.TipoDocumento}_{Guid.NewGuid()}.pdf";
        var rutaCompleta = Path.Combine(carpetaPdfs, nombreArchivo);

        using (var stream = new FileStream(rutaCompleta, FileMode.Create))
        {
            await request.Archivo.CopyToAsync(stream);
        }

        return Ok(new
        {
            mensaje = "Archivo subido correctamente.",
            solicitudId = request.SolicitudId,
            tipo = request.TipoDocumento,
            nombreArchivo = nombreArchivo
        });
    }
}