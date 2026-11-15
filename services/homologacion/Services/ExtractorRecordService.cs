using HomologaUTA.Homologacion.Agents;
using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Services;

/// <summary>La extracción no se puede ejecutar; trae el código HTTP que corresponde.</summary>
public class ExtraccionException : Exception
{
    public int StatusCode { get; }
    public ExtraccionException(int statusCode, string mensaje) : base(mensaje) => StatusCode = statusCode;
}

/// <summary>
/// Lee el récord académico ya subido (servicio de Documentos), extrae las materias con el agente
/// y las guarda/reemplaza para la solicitud (HU-20).
/// </summary>
public class ExtractorRecordService
{
    private const int MinimoCaracteres = 20;

    private readonly HomologacionDbContext _db;
    private readonly SolicitudesClient _solicitudes;
    private readonly DocumentosClient _documentos;
    private readonly AgenteExtractorRecord _agente;
    private readonly ILogger<ExtractorRecordService> _logger;

    public ExtractorRecordService(
        HomologacionDbContext db,
        SolicitudesClient solicitudes,
        DocumentosClient documentos,
        AgenteExtractorRecord agente,
        ILogger<ExtractorRecordService> logger)
    {
        _db = db;
        _solicitudes = solicitudes;
        _documentos = documentos;
        _agente = agente;
        _logger = logger;
    }

    public async Task<ExtraerRecordResponse> ExtraerAsync(int solicitudId, CancellationToken ct = default)
    {
        var solicitud = await _solicitudes.ObtenerDetalleAsync(solicitudId, ct)
                        ?? throw new ExtraccionException(404, "Solicitud no encontrada.");

        var documentos = await _documentos.ObtenerTextosAsync(solicitudId, "HISTORIAL", ct);
        var texto = string.Join("\n", documentos
            .Select(d => d.TextoExtraido)
            .Where(t => !string.IsNullOrWhiteSpace(t)));

        if (string.IsNullOrWhiteSpace(texto) || texto.Trim().Length < MinimoCaracteres)
            throw new ExtraccionException(422,
                "El récord académico no tiene texto legible. Vuelva a subir un PDF con texto (no una imagen escaneada).");

        var resultado = await _agente.ExtraerAsync(texto, ct);

        if (!resultado.RequiereRevision)
        {
            var anteriores = await _db.MateriasAprobadas
                .Where(m => m.SolicitudId == solicitudId)
                .ToListAsync(ct);
            _db.MateriasAprobadas.RemoveRange(anteriores);

            foreach (var materia in resultado.Materias)
            {
                _db.MateriasAprobadas.Add(new MateriaAprobada
                {
                    SolicitudId = solicitudId,
                    Nombre = Recortar(materia.Nombre, 200),
                    Nota = materia.Nota,
                    EstadoSegunDocumento = Recortar(materia.EstadoSegunDocumento, 50),
                    Periodo = Recortar(materia.Periodo, 50),
                    FechaExtraccion = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Récord de la solicitud {SolicitudId}: {Total} materias extraídas",
                solicitudId, resultado.Materias.Count);
        }

        return new ExtraerRecordResponse
        {
            Materias = resultado.Materias.Select(ADto).ToList(),
            RequiereRevision = resultado.RequiereRevision,
            Motivo = resultado.Motivo
        };
    }

    internal static MateriaAprobadaDto ADto(MateriaAprobada m) => new()
    {
        Id = m.Id,
        Nombre = m.Nombre,
        Nota = m.Nota,
        EstadoSegunDocumento = m.EstadoSegunDocumento,
        Periodo = m.Periodo,
        AsignaturaId = m.AsignaturaId
    };

    private static MateriaAprobadaDto ADto(MateriaExtraidaDto m) => new()
    {
        Nombre = m.Nombre,
        Nota = m.Nota,
        EstadoSegunDocumento = m.EstadoSegunDocumento,
        Periodo = m.Periodo
    };

    private static string? Recortar(string? texto, int max) =>
        texto is null || texto.Length <= max ? texto : texto[..max];
}
