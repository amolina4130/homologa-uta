using HomologaUTA.Homologacion.Agents;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Eventos;
using HomologaUTA.Homologacion.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Services;

/// <summary>
/// Analiza las asignaturas de una solicitud con el agente interno, guarda la decisión, los porcentajes
/// y la justificación por asignatura, deja la solicitud en PENDIENTE_REVISION y publica AnalisisCompletado.
/// </summary>
public class AnalisisService
{
    private const int MaxJustificacion = 2000;
    private const int MaxNombre = 200;

    private readonly HomologacionDbContext _db;
    private readonly AgenteInterno _agente;
    private readonly DecisionHomologacion _decision;
    private readonly IAnalisisEventPublisher _publisher;
    private readonly ILogger<AnalisisService> _logger;

    public AnalisisService(
        HomologacionDbContext db,
        AgenteInterno agente,
        DecisionHomologacion decision,
        IAnalisisEventPublisher publisher,
        ILogger<AnalisisService> logger)
    {
        _db = db;
        _agente = agente;
        _decision = decision;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<ResultadoAnalisisDto> AnalizarSolicitudAsync(int solicitudId, IReadOnlyList<AsignaturaAnalisisDto> asignaturas)
    {
        // 1. Analizar cada asignatura (agente + decisión calculada por código)
        var filas = new List<AnalisisAsignatura>();
        foreach (var a in asignaturas)
        {
            var resultadoAgente = await _agente.AnalizarAsignaturaAsync(
                a.AsignaturaOrigen, a.ContenidoOrigen, a.AsignaturaDestino, a.ContenidoDestino);

            filas.Add(ConstruirFila(a, resultadoAgente));
        }

        // 2. Guardar: si la solicitud ya se había analizado, se reemplaza su resultado
        var fecha = DateTime.UtcNow;
        var analisis = await _db.AnalisisSolicitudes
            .Include(x => x.Asignaturas)
            .FirstOrDefaultAsync(x => x.SolicitudId == solicitudId);

        if (analisis is null)
        {
            analisis = new AnalisisSolicitud { SolicitudId = solicitudId };
            _db.AnalisisSolicitudes.Add(analisis);
        }
        else
        {
            _db.AnalisisAsignaturas.RemoveRange(analisis.Asignaturas.ToList());
            analisis.Asignaturas.Clear();
        }

        analisis.Estado = EstadoAnalisis.PENDIENTE_REVISION;
        analisis.FechaAnalisis = fecha;
        foreach (var fila in filas)
            analisis.Asignaturas.Add(fila);

        await _db.SaveChangesAsync();

        // 3. Publicar el evento solo después de guardar. Si RabbitMQ falla, el análisis ya quedó guardado.
        var publicado = false;
        try
        {
            await _publisher.PublicarAnalisisCompletadoAsync(new AnalisisCompletadoEvent
            {
                SolicitudId = solicitudId,
                EstadoFinal = EstadoAnalisis.PENDIENTE_REVISION,
                FechaAnalisis = fecha
            });
            publicado = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo publicar AnalisisCompletado de la solicitud {SolicitudId}", solicitudId);
        }

        return new ResultadoAnalisisDto
        {
            SolicitudId = solicitudId,
            Estado = EstadoAnalisis.PENDIENTE_REVISION,
            FechaAnalisis = fecha,
            EventoPublicado = publicado,
            Asignaturas = filas.Select(f => new ResultadoAnalisisAsignaturaDto
            {
                AsignaturaOrigen = f.AsignaturaOrigen,
                AsignaturaDestino = f.AsignaturaDestino,
                PorcentajeContenido = f.PorcentajeContenido,
                PorcentajeProfundidad = f.PorcentajeProfundidad,
                PorcentajeHoras = f.PorcentajeHoras,
                Decision = f.Decision,
                Justificacion = f.Justificacion,
                RequiereRevision = f.RequiereRevision
            }).ToList()
        };
    }

    private AnalisisAsignatura ConstruirFila(AsignaturaAnalisisDto entrada, ResultadoAgente agente)
    {
        string decision;
        string justificacion;

        if (agente.RequiereRevision)
        {
            // El agente no respondió bien tras los reintentos: lo decide una persona
            decision = DecisionAnalisis.REQUIERE_REVISION;
            justificacion = agente.Justificacion;
        }
        else
        {
            // La decisión la calcula el código (umbral del 80 %), no el agente
            var d = _decision.CalcularDecision(agente.Contenido, agente.Profundidad, agente.Horas);
            decision = d.Decision;
            justificacion = string.IsNullOrWhiteSpace(agente.Justificacion)
                ? d.Justificacion
                : $"{d.Justificacion} Análisis del agente: {agente.Justificacion.Trim()}";
        }

        return new AnalisisAsignatura
        {
            AsignaturaOrigen = Recortar(entrada.AsignaturaOrigen, MaxNombre),
            AsignaturaDestino = Recortar(entrada.AsignaturaDestino, MaxNombre),
            PorcentajeContenido = agente.Contenido,
            PorcentajeProfundidad = agente.Profundidad,
            PorcentajeHoras = agente.Horas,
            Decision = decision,
            Justificacion = Recortar(justificacion, MaxJustificacion),
            RequiereRevision = agente.RequiereRevision
        };
    }

    private static string Recortar(string texto, int max) =>
        texto.Length <= max ? texto : texto[..max];
}