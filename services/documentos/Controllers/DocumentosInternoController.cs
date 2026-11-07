using HomologaUTA.Documentos.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Documentos.Controllers;

/// <summary>
/// Consultas entre servicios. No se expone por el Gateway: solo se alcanza desde la red
/// interna de Docker para que el análisis externo reciba el texto ya extraído de los PDF.
/// </summary>
[ApiController]
[Route("internal/documentos")]
public class DocumentosInternoController : ControllerBase
{
    private readonly DocumentosDbContext _db;

    public DocumentosInternoController(DocumentosDbContext db)
    {
        _db = db;
    }

    [HttpGet("solicitud/{solicitudId:int}")]
    public async Task<IActionResult> ObtenerPorSolicitud(int solicitudId)
    {
        var documentos = await _db.Documentos
            .AsNoTracking()
            .Where(d => d.SolicitudId == solicitudId)
            .OrderBy(d => d.Id)
            .Select(d => new
            {
                d.Id,
                d.TipoDocumento,
                d.NombreArchivo,
                d.Estado,
                d.TextoExtraido
            })
            .ToListAsync();

        return Ok(documentos);
    }
}
