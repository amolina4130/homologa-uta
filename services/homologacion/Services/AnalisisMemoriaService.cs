using HomologaUTA.Homologacion.Agents;
using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Eventos;
using HomologaUTA.Homologacion.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Services;

/// <summary>El análisis con memoria no se puede ejecutar; trae el código HTTP que corresponde.</summary>
public class AnalisisMemoriaException : Exception
{
    public int StatusCode { get; }
    public AnalisisMemoriaException(int statusCode, string mensaje) : base(mensaje) => StatusCode = statusCode;
}

/// <summary>
/// Análisis completo usando la memoria del agente (HU-19), las equivalencias (HU-14),
/// la preselección por área y nivel (HU-21) y la combinación de dos materias de origen (HU-22).
/// </summary>
public class AnalisisMemoriaService
{
    private const int MaxJustificacion = 2000;
    private const int MaxNombre = 200;

    private readonly HomologacionDbContext _db;
    private readonly SolicitudesClient _solicitudes;
    private readonly ICatalogoClient _catalogo;
    private readonly AgenteInterno _agente;
    private readonly DecisionHomologacion _decision;
    private readonly IAnalisisEventPublisher _publisher;
    private readonly ILogger<AnalisisMemoriaService> _logger;

    public AnalisisMemoriaService(
        HomologacionDbContext db,
        SolicitudesClient solicitudes,
        ICatalogoClient catalogo,
        AgenteInterno agente,
        DecisionHomologacion decision,
        IAnalisisEventPublisher publisher,
        ILogger<AnalisisMemoriaService> logger)
    {
        _db = db;
        _solicitudes = solicitudes;
        _catalogo = catalogo;
        _agente = agente;
        _decision = decision;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<ResultadoDetalleDto> AnalizarAsync(
        int solicitudId, AnalisisMemoriaRequest request, string usuario, CancellationToken ct = default)
    {
        var solicitud = await _solicitudes.ObtenerDetalleAsync(solicitudId, ct)
                        ?? throw new AnalisisMemoriaException(404, "Solicitud no encontrada.");

        var estadoMemoria = await _catalogo.ObtenerEstadoMemoriaAsync(ct);
        if (estadoMemoria is null)
            throw new AnalisisMemoriaException(409, "La memoria del agente aún no se ha cargado.");

        var origenMemoria = await _catalogo.ObtenerAsignaturasMemoriaAsync(solicitud.CarreraOrigenId, null, ct);
        var destinoMemoria = await _catalogo.ObtenerAsignaturasMemoriaAsync(solicitud.CarreraDestinoId, null, ct);
        var equivalencias = await _catalogo.ObtenerEquivalenciasAsync(
            solicitud.CarreraOrigenId, solicitud.CarreraDestinoId, ct);

        var seleccionadas = solicitud.Asignaturas
            .Where(a => request.AsignaturasIds is null || request.AsignaturasIds.Count == 0
                        || request.AsignaturasIds.Contains(a.AsignaturaId))
            .ToList();

        if (seleccionadas.Count == 0 && request.Combinaciones.Count == 0)
            throw new AnalisisMemoriaException(400, "La solicitud no tiene asignaturas para analizar.");

        var filas = new List<AnalisisAsignatura>();
        var cubiertas = new HashSet<int>();

        // 1. Combinaciones explícitas (máximo dos materias de origen por materia de destino)
        foreach (var combinacion in request.Combinaciones)
        {
            var destino = destinoMemoria.FirstOrDefault(d => d.AsignaturaId == combinacion.AsignaturaDestinoId)
                          ?? throw new AnalisisMemoriaException(400,
                              $"La asignatura de destino {combinacion.AsignaturaDestinoId} no está en la memoria.");

            var origenes = combinacion.MateriasOrigenIds.Distinct().ToList();
            if (origenes.Count == 0 || origenes.Count > 2)
                throw new AnalisisMemoriaException(400, "Una combinación debe tener una o dos materias de origen.");

            var noPertenecen = origenes.Where(id => seleccionadas.All(s => s.AsignaturaId != id)).ToList();
            if (noPertenecen.Count > 0)
                throw new AnalisisMemoriaException(400,
                    "Todas las materias de origen deben pertenecer a la solicitud y estar preseleccionadas.");

            filas.Add(await ConstruirCombinadaAsync(origenes, destino, origenMemoria, seleccionadas, ct));
            foreach (var id in origenes) cubiertas.Add(id);
        }

        // 2. Materias individuales: equivalencia -> preselección -> agente
        foreach (var origen in seleccionadas)
        {
            if (cubiertas.Contains(origen.AsignaturaId)) continue;

            var equivalencia = equivalencias.FirstOrDefault(e => e.AsignaturaOrigenId == origen.AsignaturaId);
            if (equivalencia is not null)
            {
                filas.Add(new AnalisisAsignatura
                {
                    AsignaturaOrigen = Recortar(origen.Nombre, MaxNombre),
                    AsignaturaDestino = Recortar(equivalencia.AsignaturaDestino, MaxNombre),
                    AsignaturaDestinoId = equivalencia.AsignaturaDestinoId,
                    PorcentajeContenido = 100,
                    PorcentajeProfundidad = 100,
                    PorcentajeHoras = 100,
                    Decision = DecisionAnalisis.HOMOLOGABLE,
                    Justificacion = $"Equivalencia registrada entre {equivalencia.AsignaturaOrigen} y {equivalencia.AsignaturaDestino}.",
                    ReglaAplicada = "EQUIVALENCIA",
                    RequiereRevision = false,
                    MateriasOrigen = { NuevaMateria(origen) }
                });
                continue;
            }

            var area = AreaAsignatura.Detectar(origen.Nombre);
            var candidatas = destinoMemoria
                .Where(d => string.Equals(AreaAsignatura.Detectar(d.Nombre), area, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(d.Area, area, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Nivel)
                .ToList();

            if (candidatas.Count == 0)
            {
                filas.Add(new AnalisisAsignatura
                {
                    AsignaturaOrigen = Recortar(origen.Nombre, MaxNombre),
                    AsignaturaDestino = string.Empty,
                    Decision = DecisionAnalisis.SIN_EQUIVALENTE,
                    Justificacion = $"No se encontró una materia de destino del área {area} en la memoria del agente.",
                    RequiereRevision = true,
                    MateriasOrigen = { NuevaMateria(origen) }
                });
                continue;
            }

            var destino = candidatas[0];
            filas.Add(await ConstruirIndividualAsync(origen, destino, ct));
        }

        var fecha = DateTime.UtcNow;
        var analisis = await _db.AnalisisSolicitudes
            .Include(a => a.Asignaturas)
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct);

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
        analisis.MemoriaHuella = estadoMemoria.Huella;
        foreach (var fila in filas) analisis.Asignaturas.Add(fila);

        await _db.SaveChangesAsync(ct);

        try
        {
            await _solicitudes.CambiarEstadoAsync(solicitudId, "PENDIENTE_REVISION", usuario, ct);
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            _logger.LogError(ex, "Análisis guardado, pero no se pudo cambiar el estado de la solicitud {SolicitudId}", solicitudId);
        }

        try
        {
            await _publisher.PublicarAnalisisCompletadoAsync(new AnalisisCompletadoEvent
            {
                SolicitudId = solicitudId,
                EstadoFinal = EstadoAnalisis.PENDIENTE_REVISION,
                FechaAnalisis = fecha
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo publicar AnalisisCompletado de la solicitud {SolicitudId}", solicitudId);
        }

        return ADto(analisis);
    }

    public async Task<ResultadoDetalleDto?> ObtenerResultadoAsync(int solicitudId, CancellationToken ct = default)
    {
        var analisis = await _db.AnalisisSolicitudes
            .AsNoTracking()
            .Include(a => a.Asignaturas).ThenInclude(x => x.MateriasOrigen)
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct);
        return analisis is null ? null : ADto(analisis);
    }

    private async Task<AnalisisAsignatura> ConstruirIndividualAsync(
        SolicitudAsignaturaInfoDto origen, MemoriaAsignaturaDto destino, CancellationToken ct)
    {
        var fila = new AnalisisAsignatura
        {
            AsignaturaOrigen = Recortar(origen.Nombre, MaxNombre),
            AsignaturaDestino = Recortar(destino.Nombre, MaxNombre),
            AsignaturaDestinoId = destino.AsignaturaId,
            MateriasOrigen = { NuevaMateria(origen) }
        };

        var planOrigen = await ObtenerPlanSeguroAsync(origen.AsignaturaId, ct);
        var planDestino = await ObtenerPlanSeguroAsync(destino.AsignaturaId, ct);

        if (planOrigen is null || planDestino is null)
        {
            var faltante = planOrigen is null ? "origen" : "destino";
            fila.Decision = DecisionAnalisis.REQUIERE_REVISION;
            fila.Justificacion = $"Falta el plan analítico de la asignatura de {faltante}; requiere revisión manual.";
            fila.RequiereRevision = true;
            fila.ReglaAplicada = "PLAN_ANALITICO_FALTANTE";
            return fila;
        }

        var resultado = await _agente.AnalizarAsignaturaAsync(
            origen.Nombre, planOrigen.Contenido,
            destino.Nombre, planDestino.Contenido);

        if (resultado.RequiereRevision)
        {
            fila.Decision = DecisionAnalisis.REQUIERE_REVISION;
            fila.Justificacion = Recortar(resultado.Justificacion, MaxJustificacion);
            fila.RequiereRevision = true;
            return fila;
        }

        var decision = _decision.CalcularDecision(resultado.Contenido, resultado.Profundidad, resultado.Horas);
        fila.PorcentajeContenido = resultado.Contenido;
        fila.PorcentajeProfundidad = resultado.Profundidad;
        fila.PorcentajeHoras = resultado.Horas;
        fila.Decision = decision.Decision;
        fila.Justificacion = Recortar(
            string.IsNullOrWhiteSpace(resultado.Justificacion)
                ? decision.Justificacion
                : $"{decision.Justificacion} Análisis del agente: {resultado.Justificacion.Trim()}",
            MaxJustificacion);
        return fila;
    }

    private async Task<AnalisisAsignatura> ConstruirCombinadaAsync(
        List<int> origenesIds,
        MemoriaAsignaturaDto destino,
        List<MemoriaAsignaturaDto> origenMemoria,
        List<SolicitudAsignaturaInfoDto> seleccionadas,
        CancellationToken ct)
    {
        var origenes = origenesIds
            .Select(id => seleccionadas.First(s => s.AsignaturaId == id))
            .ToList();

        var fila = new AnalisisAsignatura
        {
            AsignaturaOrigen = Recortar(string.Join(" + ", origenes.Select(o => o.Nombre)), MaxNombre),
            AsignaturaDestino = Recortar(destino.Nombre, MaxNombre),
            AsignaturaDestinoId = destino.AsignaturaId,
            Combinada = origenes.Count == 2,
            MateriasOrigen = origenes.Select((o, i) =>
            {
                var m = NuevaMateria(o);
                m.Orden = i + 1;
                return m;
            }).ToList()
        };

        var contenidos = new List<string>();
        foreach (var origen in origenes)
        {
            var plan = await ObtenerPlanSeguroAsync(origen.AsignaturaId, ct);
            if (plan is null)
            {
                fila.Decision = DecisionAnalisis.REQUIERE_REVISION;
                fila.Justificacion = $"Falta el plan analítico de {origen.Nombre}; la combinación requiere revisión manual.";
                fila.RequiereRevision = true;
                fila.ReglaAplicada = "PLAN_ANALITICO_FALTANTE";
                return fila;
            }
            contenidos.Add(plan.Contenido);
        }

        var planDestino = await ObtenerPlanSeguroAsync(destino.AsignaturaId, ct);
        if (planDestino is null)
        {
            fila.Decision = DecisionAnalisis.REQUIERE_REVISION;
            fila.Justificacion = $"Falta el plan analítico de {destino.Nombre}; requiere revisión manual.";
            fila.RequiereRevision = true;
            fila.ReglaAplicada = "PLAN_ANALITICO_FALTANTE";
            return fila;
        }

        var resultado = await _agente.AnalizarAsignaturaAsync(
            fila.AsignaturaOrigen, string.Join("\n", contenidos),
            destino.Nombre, planDestino.Contenido);

        if (resultado.RequiereRevision)
        {
            fila.Decision = DecisionAnalisis.REQUIERE_REVISION;
            fila.Justificacion = Recortar(resultado.Justificacion, MaxJustificacion);
            fila.RequiereRevision = true;
            return fila;
        }

        var decision = _decision.CalcularDecision(resultado.Contenido, resultado.Profundidad, resultado.Horas);
        fila.PorcentajeContenido = resultado.Contenido;
        fila.PorcentajeProfundidad = resultado.Profundidad;
        fila.PorcentajeHoras = resultado.Horas;
        fila.Decision = decision.Decision;
        fila.Justificacion = Recortar(
            string.IsNullOrWhiteSpace(resultado.Justificacion)
                ? decision.Justificacion
                : $"{decision.Justificacion} Análisis del agente: {resultado.Justificacion.Trim()}",
            MaxJustificacion);
        return fila;
    }

    private async Task<PlanAnaliticoDto?> ObtenerPlanSeguroAsync(int asignaturaId, CancellationToken ct)
    {
        try
        {
            return await _catalogo.ObtenerPlanAnaliticoAsync(asignaturaId, ct);
        }
        catch (CatalogoNoDisponibleException ex)
        {
            _logger.LogWarning(ex, "No se pudo obtener el plan de la asignatura {AsignaturaId}", asignaturaId);
            return null;
        }
    }

    private static MateriaOrigenAnalisis NuevaMateria(SolicitudAsignaturaInfoDto origen) => new()
    {
        AsignaturaId = origen.AsignaturaId,
        Nombre = Recortar(origen.Nombre, MaxNombre),
        Orden = 1
    };

    private static ResultadoDetalleDto ADto(AnalisisSolicitud analisis) => new()
    {
        SolicitudId = analisis.SolicitudId,
        Estado = analisis.Estado,
        FechaAnalisis = analisis.FechaAnalisis,
        MemoriaHuella = analisis.MemoriaHuella,
        Asignaturas = analisis.Asignaturas.OrderBy(a => a.Id).Select(a => new ResultadoAnalisisAsignaturaDto
        {
            AsignaturaDestinoId = a.AsignaturaDestinoId,
            AsignaturaOrigen = a.AsignaturaOrigen,
            AsignaturaDestino = a.AsignaturaDestino,
            Combinada = a.Combinada,
            ReglaAplicada = a.ReglaAplicada,
            MateriasOrigen = a.MateriasOrigen.OrderBy(m => m.Orden).Select(m => new MateriaOrigenDto
            {
                AsignaturaId = m.AsignaturaId,
                Nombre = m.Nombre,
                Nota = m.Nota,
                EstadoSegunDocumento = m.EstadoSegunDocumento,
                Periodo = m.Periodo
            }).ToList(),
            PorcentajeContenido = a.PorcentajeContenido,
            PorcentajeProfundidad = a.PorcentajeProfundidad,
            PorcentajeHoras = a.PorcentajeHoras,
            Decision = a.Decision,
            Justificacion = a.Justificacion,
            RequiereRevision = a.RequiereRevision
        }).ToList()
    };

    private static string Recortar(string texto, int max) =>
        texto.Length <= max ? texto : texto[..max];
}
