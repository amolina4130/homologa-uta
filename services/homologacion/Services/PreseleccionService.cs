using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Services;

/// <summary>La preselección no se puede ejecutar; trae el código HTTP que corresponde.</summary>
public class PreseleccionException : Exception
{
    public int StatusCode { get; }
    public PreseleccionException(int statusCode, string mensaje) : base(mensaje) => StatusCode = statusCode;
}

/// <summary>
/// Preselección automática de materias candidatas por área y nivel (HU-21).
/// Solo las candidatas se envían después al agente.
/// </summary>
public class PreseleccionService
{
    private const int MaxCandidatas = 5;

    private readonly HomologacionDbContext _db;
    private readonly SolicitudesClient _solicitudes;
    private readonly ICatalogoClient _catalogo;
    private readonly ILogger<PreseleccionService> _logger;

    public PreseleccionService(
        HomologacionDbContext db,
        SolicitudesClient solicitudes,
        ICatalogoClient catalogo,
        ILogger<PreseleccionService> logger)
    {
        _db = db;
        _solicitudes = solicitudes;
        _catalogo = catalogo;
        _logger = logger;
    }

    public async Task<PreseleccionResponse> PreseleccionarAsync(int solicitudId, CancellationToken ct = default)
    {
        var solicitud = await _solicitudes.ObtenerDetalleAsync(solicitudId, ct)
                        ?? throw new PreseleccionException(404, "Solicitud no encontrada.");

        var materias = await _db.MateriasAprobadas
            .AsNoTracking()
            .Where(m => m.SolicitudId == solicitudId)
            .OrderBy(m => m.Id)
            .ToListAsync(ct);

        if (materias.Count == 0)
            throw new PreseleccionException(409, "La solicitud no tiene materias aprobadas extraídas del récord.");

        var estado = await _catalogo.ObtenerEstadoMemoriaAsync(ct);
        if (estado is null)
            throw new PreseleccionException(409, "La memoria del agente aún no se ha cargado.");

        var destino = await _catalogo.ObtenerAsignaturasMemoriaAsync(solicitud.CarreraDestinoId, null, ct);

        var respuesta = new PreseleccionResponse();
        foreach (var materia in materias)
        {
            var area = AreaAsignatura.Detectar(materia.Nombre);
            var candidatas = destino
                .Where(d => string.Equals(AreaAsignatura.Detectar(d.Nombre), area, StringComparison.OrdinalIgnoreCase)
                            || area == "General" || string.Equals(d.Area, area, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Nivel)
                .Take(MaxCandidatas)
                .Select(d => new CandidataDestinoDto
                {
                    AsignaturaId = d.AsignaturaId,
                    Nombre = d.Nombre,
                    Area = string.IsNullOrWhiteSpace(d.Area) ? AreaAsignatura.Detectar(d.Nombre) : d.Area,
                    Nivel = d.Nivel
                })
                .ToList();

            if (candidatas.Count == 0)
            {
                respuesta.SinCandidata.Add(materia.Nombre);
                continue;
            }

            respuesta.Pares.Add(new PreseleccionParDto
            {
                MateriaOrigen = materia.Nombre,
                AreaOrigen = area,
                CandidatasDestino = candidatas
            });
        }

        _logger.LogInformation("Preselección de la solicitud {SolicitudId}: {Pares} con candidatas, {Sin} sin candidata",
            solicitudId, respuesta.Pares.Count, respuesta.SinCandidata.Count);

        return respuesta;
    }
}
