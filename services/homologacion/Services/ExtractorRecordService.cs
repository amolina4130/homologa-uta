using HomologaUTA.Homologacion.Agents;
using HomologaUTA.Homologacion.Clients;
using UglyToad.PdfPig;
using Microsoft.Extensions.Logging;

namespace HomologaUTA.Homologacion.Services;

public class ExtractorRecordService
{
    private readonly AgenteExtractorRecord _agente;
    private readonly DocumentosClient _documentosClient;
    private readonly ILogger<ExtractorRecordService> _logger;

    public ExtractorRecordService(AgenteExtractorRecord agente, DocumentosClient documentosClient, ILogger<ExtractorRecordService> logger)
    {
        _agente = agente;
        _documentosClient = documentosClient;
        _logger = logger;
    }

    public async Task<(List<MateriaRecordDto> materias, bool requiereRevision, string motivo)> ExtraerAsync(int solicitudId)
    {
        // 1. Obtener el documento ya subido
        var documentos = await _documentosClient.ObtenerPorSolicitudAsync(solicitudId);
        var record = documentos.FirstOrDefault(d => d.TipoDocumento.Contains("RECORD", StringComparison.OrdinalIgnoreCase));
        
        if (record == null)
            return (new List<MateriaRecordDto>(), true, "La solicitud no tiene un documento de récord académico.");

        if (string.IsNullOrWhiteSpace(record.TextoExtraido))
            return (new List<MateriaRecordDto>(), true, "No se pudo leer el documento (posible imagen escaneada).");

        // 2. Enviar el texto al agente
        return await _agente.ExtraerMateriasAsync(record.TextoExtraido);
    }
}