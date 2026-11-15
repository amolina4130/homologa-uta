using HomologaUTA.Homologacion.Connectors;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Eventos;
using HomologaUTA.Homologacion.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Services;

/// <summary>
/// Envía la documentación al agente externo y guarda su decisión por asignatura con su justificación,
/// sin imponerle reglas. Si el agente falla, no se guarda nada: se puede reintentar sin perder datos.
/// </summary>
public class AnalisisExternoService
{
    private const int MaxJustificacion = 2000;
    private const int MaxNombre = 200;
    private const int MaxDecision = 30;

    private readonly HomologacionDbContext _db;
    private readonly AgenteExternoConnector _conector;
    private readonly IAnalisisEventPublisher _publisher;
    private readonly ILogger<AnalisisExternoService> _logger;

    public AnalisisExternoService(
        HomologacionDbContext db,
        AgenteExternoConnector conector,
        IAnalisisEventPublisher publisher,
        ILogger<AnalisisExternoService> logger)
    {
        _db = db;
        _conector = conector;
        _publisher = publisher;
        _logger = logger;
    }

    /// <exception cref="AgenteExternoTimeoutException">Venció el tiempo de espera; no se guardó nada.</exception>
    /// <exception cref="AgenteExternoException">El agente falló o respondió mal; no se guardó nada.</exception>
    public async Task<ResultadoAnalisisDto> AnalizarSolicitudAsync(
        int solicitudId, AgenteExternoRequest request, CancellationToken ct = default)
    {
        request.SolicitudId = solicitudId;

        // 1. Enviar al agente. Si falla o vence el tiempo, sale por excepción antes de tocar la base de datos.
        var respuesta = await _conector.EnviarDocumentacionAsync(request, ct);
        var filas = respuesta.Asignaturas.Select(ConstruirFila).ToList();

        // 2. Guardar: si la solicitud ya tenía un análisis, se reemplaza
        var fecha = DateTime.UtcNow;
        var analisis = await _db.AnalisisSolicitudes
            .Include(x => x.Asignaturas)
            .FirstOrDefaultAsync(x => x.SolicitudId == solicitudId, ct);

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

        await _db.SaveChangesAsync(ct);

        // 3. Publicar el evento solo después de guardar
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
                Combinada = f.Combinada,
                MateriasOrigen = f.MateriasOrigen.OrderBy(m => m.Orden).Select(m => new MateriaOrigenDto
                {
                    Nombre = m.Nombre,
                    Nota = m.Nota
                }).ToList(),
                PorcentajeContenido = f.PorcentajeContenido,
                PorcentajeProfundidad = f.PorcentajeProfundidad,
                PorcentajeHoras = f.PorcentajeHoras,
                Decision = f.Decision,
                Justificacion = f.Justificacion,
                RequiereRevision = f.RequiereRevision
            }).ToList()
        };
    }

    /// <summary>
    /// La decisión y la justificación se guardan tal como las dio el agente (sin umbral ni reglas).
    /// Si el agente reporta una o dos materias de origen se guardan como combinación (HU-22);
    /// más de dos materias se marcan para revisión manual. Solo si faltan datos se marca para revisión.
    /// </summary>
    private static AnalisisAsignatura ConstruirFila(AsignaturaAgenteExternoDto a)
    {
        var sinDecision = string.IsNullOrWhiteSpace(a.Decision);
        var sinJustificacion = string.IsNullOrWhiteSpace(a.Justificacion);

        var materias = (a.MateriasOrigen ?? new List<MateriaOrigenExternoDto>())
            .Where(m => !string.IsNullOrWhiteSpace(m.Nombre))
            .ToList();
        if (materias.Count == 0)
            materias.Add(new MateriaOrigenExternoDto { Nombre = a.NombreOrigen });

        var demasiadas = materias.Count > 2;
        var usadas = materias.Take(2).ToList();
        var combinada = !demasiadas && usadas.Count == 2;

        var justificacion = demasiadas
            ? "El agente externo devolvió más de dos materias de origen; requiere revisión manual."
            : sinJustificacion
                ? "El agente externo no entregó justificación."
                : Recortar(a.Justificacion.Trim(), MaxJustificacion);

        return new AnalisisAsignatura
        {
            AsignaturaOrigen = Recortar(string.Join(" + ", usadas.Select(m => m.Nombre.Trim())), MaxNombre),
            AsignaturaDestino = Recortar(a.NombreDestino.Trim(), MaxNombre),
            Combinada = combinada,
            PorcentajeContenido = a.PorcentajeContenido ?? a.PorcentajeSimilitud,
            PorcentajeProfundidad = a.PorcentajeProfundidad ?? 0,
            PorcentajeHoras = a.PorcentajeCargaHoraria ?? 0,
            Decision = demasiadas || sinDecision
                ? DecisionAnalisis.REQUIERE_REVISION
                : Recortar(a.Decision.Trim(), MaxDecision),
            Justificacion = justificacion,
            RequiereRevision = demasiadas || sinDecision || sinJustificacion,
            MateriasOrigen = usadas.Select((m, i) => new MateriaOrigenAnalisis
            {
                Nombre = Recortar(m.Nombre.Trim(), MaxNombre),
                Nota = m.Nota,
                Orden = i + 1
            }).ToList()
        };
    }

    private static string Recortar(string texto, int max) =>
        texto.Length <= max ? texto : texto[..max];
}