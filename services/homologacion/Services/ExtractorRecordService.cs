using HomologaUTA.Homologacion.Agents;
using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Services;

/// <summary>El récord no se pudo procesar.</summary>
public class RecordException : Exception
{
    public int StatusCode { get; }

    public RecordException(int statusCode, string mensaje) : base(mensaje)
    {
        StatusCode = statusCode;
    }
}

/// <summary>
/// Extrae las materias aprobadas del récord que el estudiante ya subió (HU-20). No crea una
/// subida nueva: reutiliza el documento procesado por el servicio de Documentos y le pide al
/// agente un JSON validado con nombre, nota, estado y periodo de cada materia.
/// </summary>
public class ExtractorRecordService
{
    private const int MinimoTextoUtil = 20;

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

    public async Task<ResultadoExtraccionRecordDto> ExtraerAsync(
        int solicitudId, CancellationToken ct = default)
    {
        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct)
                   ?? throw new RecordException(404, "Solicitud no encontrada.");

        List<DocumentoExtraidoDto> documentos;
        try
        {
            documentos = await _documentos.ObtenerPorSolicitudAsync(solicitudId, ct);
        }
        catch (DocumentosNoDisponibleException ex)
        {
            throw new RecordException(422, ex.Message);
        }

        var record = documentos.FirstOrDefault(d => EsRecord(d.TipoDocumento));
        if (record is null)
            throw new RecordException(404,
                "La solicitud no tiene un documento de récord/historial académico. Súbalo en Adjuntar documentos.");

        var texto = record.TextoExtraido?.Trim() ?? string.Empty;
        if (texto.Length < MinimoTextoUtil)
        {
            return new ResultadoExtraccionRecordDto
            {
                RequiereRevision = true,
                Motivo = "El documento no tiene texto legible (puede ser un PDF escaneado). " +
                         "Corrija las materias manualmente."
            };
        }

        ResultadoRecordAgente extraido;
        try
        {
            extraido = await _agente.ExtraerAsync(texto, ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Text.Json.JsonException or TaskCanceledException)
        {
            _logger.LogError(ex, "No se pudo extraer el récord de la solicitud {SolicitudId}", solicitudId);
            return new ResultadoExtraccionRecordDto
            {
                RequiereRevision = true,
                Motivo = "El agente no pudo leer el récord después de varios intentos. " +
                         "Corrija las materias manualmente."
            };
        }

        if (extraido.Materias.Count == 0)
        {
            return new ResultadoExtraccionRecordDto
            {
                RequiereRevision = true,
                Motivo = "No se encontraron materias en el récord. Corrija la lista manualmente."
            };
        }

        // La extracción reemplaza lo que se había guardado antes.
        var anteriores = await _db.MateriasAprobadas
            .Where(m => m.SolicitudId == solicitudId)
            .ToListAsync(ct);

        if (anteriores.Count > 0)
            _db.MateriasAprobadas.RemoveRange(anteriores);

        var fecha = DateTime.UtcNow;
        var nuevas = extraido.Materias.Select(m => new MateriaAprobada
        {
            SolicitudId = solicitudId,
            Nombre = m.Nombre.Trim(),
            Nota = m.Nota,
            EstadoSegunDocumento = m.EstadoSegunDocumento,
            Periodo = string.IsNullOrWhiteSpace(m.Periodo) ? null : m.Periodo.Trim(),
            FechaExtraccion = fecha
        }).ToList();

        _db.MateriasAprobadas.AddRange(nuevas);
        await _db.SaveChangesAsync(ct);

        try
        {
            await _solicitudes.RegistrarHistorialAsync(
                solicitudId, info.Estado, "sistema@homologauta", ct);
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            _logger.LogWarning(ex, "No se pudo dejar constancia de la extracción en el historial");
        }

        return new ResultadoExtraccionRecordDto
        {
            RequiereRevision = false,
            Motivo = null,
            Materias = nuevas.Select(MateriaAprobadaMapper.ToDto).ToList()
        };
    }

    private static bool EsRecord(string tipo) =>
        tipo.Contains("RECORD", StringComparison.OrdinalIgnoreCase)
        || tipo.Contains("HISTORIAL", StringComparison.OrdinalIgnoreCase)
        || tipo.Contains("RÉCORD", StringComparison.OrdinalIgnoreCase);
}

public static class MateriaAprobadaMapper
{
    public static MateriaAprobadaDto ToDto(MateriaAprobada m) => new()
    {
        Id = m.Id,
        Nombre = m.Nombre,
        Nota = m.Nota,
        EstadoSegunDocumento = m.EstadoSegunDocumento,
        Periodo = m.Periodo
    };
}
