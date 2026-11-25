using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Connectors;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Models;
using HomologaUTA.Homologacion.Reportes;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace HomologaUTA.Homologacion.Services;

/// <summary>El informe no se puede emitir; trae el código HTTP que corresponde.</summary>
public class InformeException : Exception
{
    public int StatusCode { get; }

    public InformeException(int statusCode, string mensaje) : base(mensaje)
    {
        StatusCode = statusCode;
    }
}

/// <summary>Lista las solicitudes pendientes, muestra su detalle con la propuesta del agente y guarda el informe técnico.</summary>
public class CoordinadorService
{
    /// <summary>Estado inicial hasta que el récord académico entregue los datos reales.</summary>
    internal const string TitularSinIdentificar = "Estudiante sin identificar";

    private readonly HomologacionDbContext _db;
    private readonly SolicitudesClient _solicitudes;
    private readonly AnalisisService _analisis;
    private readonly AnalisisExternoService _analisisExterno;
    private readonly ILogger<CoordinadorService> _logger;

    public CoordinadorService(
        HomologacionDbContext db,
        SolicitudesClient solicitudes,
        AnalisisService analisis,
        AnalisisExternoService analisisExterno,
        ILogger<CoordinadorService> logger)
    {
        _db = db;
        _solicitudes = solicitudes;
        _analisis = analisis;
        _analisisExterno = analisisExterno;
        _logger = logger;
    }

    /// <summary>Solicitudes pendientes de revisión, las más antiguas primero.</summary>
    public async Task<List<PendienteDto>> ListarPendientesAsync(
        bool incluirEmitidos = false, CancellationToken ct = default)
    {
        // Fuente 1: análisis que esperan al Coordinador. El trámite termina cuando el
        // Coordinador emite el informe final y lo envía al Comité: esos ya no están
        // pendientes y solo se incluyen si se piden (para consultarlos o corregirlos).
        var analisis = await _db.AnalisisSolicitudes
            .AsNoTracking()
            .Where(a => a.Estado == EstadoAnalisis.PENDIENTE_REVISION
                        || (incluirEmitidos && a.Estado == EstadoAnalisis.INFORME_EMITIDO))
            .Select(a => new
            {
                a.SolicitudId,
                a.FechaAnalisis,
                a.Estado,
                TotalAsignaturas = a.Asignaturas.Count
            })
            .ToListAsync(ct);

        // Fuente 2: solicitudes marcadas como pendientes. Así también aparecen las que
        // quedaron sin análisis por un fallo previo (al abrirlas se genera).
        var pendientes = await _solicitudes.ObtenerPendientesAsync(ct);

        var ids = analisis.Select(a => a.SolicitudId)
            .Union(pendientes.Select(p => p.Id))
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new List<PendienteDto>();

        var infos = await _solicitudes.ObtenerPorIdsAsync(ids, ct);
        var infoPorId = infos.ToDictionary(i => i.Id);
        var analisisPorId = analisis.ToDictionary(a => a.SolicitudId);

        var lista = new List<PendienteDto>();
        foreach (var id in ids)
        {
            if (!infoPorId.TryGetValue(id, out var info))
            {
                _logger.LogWarning("La solicitud {SolicitudId} está pendiente pero no existe en Solicitudes", id);
                continue;
            }

            analisisPorId.TryGetValue(id, out var a);

            lista.Add(new PendienteDto
            {
                SolicitudId = id,
                NumeroProceso = info.NumeroProceso,
                Fecha = info.FechaRegistro,
                Estudiante = NombreEstudiante(info),
                EstudianteCedula = CedulaEstudiante(info),
                ComiteCorreo = info.EstudianteCorreo,
                Modulo = info.Modulo,
                UniversidadOrigen = info.UniversidadOrigen,
                UniversidadDestino = info.UniversidadDestino,
                Tipo = info.Tipo,
                CarreraOrigen = info.CarreraOrigen,
                CarreraDestino = info.CarreraDestino,
                FechaAnalisis = a?.FechaAnalisis ?? info.FechaRegistro,
                TotalAsignaturas = a?.TotalAsignaturas ?? info.TotalAsignaturas,
                Estado = a?.Estado ?? info.Estado
            });
        }

        return lista.OrderBy(p => p.Fecha).ToList();
    }

    /// <summary>
    /// Detalle de una solicitud con la propuesta real del agente por asignatura. Si la
    /// solicitud existe pero aún no tiene análisis, se genera en ese momento con sus
    /// asignaturas reales; nunca se devuelven datos inventados.
    /// </summary>
    public async Task<DetalleSolicitudDto?> ObtenerDetalleAsync(int solicitudId, CancellationToken ct = default)
    {
        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct);
        if (info is null)
            return null;

        var analisis = await CargarAnalisisAsync(solicitudId, ct);

        if (analisis is null)
        {
            // Una solicitud con informe emitido pero sin análisis es un dato inconsistente.
            if (string.Equals(info.Estado, EstadoSolicitudHomologacion.INFORME_EMITIDO, StringComparison.OrdinalIgnoreCase))
                return null;

            try
            {
                if (string.Equals(info.Modulo, "EXTERNO", StringComparison.OrdinalIgnoreCase))
                {
                    await _analisisExterno.AnalizarSolicitudAsync(solicitudId, null, ct);
                }
                else
                {
                    // Sin cuerpo: el servicio decide si usa las materias aprobadas (preselección
                    // HU-21) o las asignaturas de la solicitud.
                    await _analisis.AnalizarSolicitudAsync(solicitudId, null, ct);
                }
            }
            catch (Exception ex) when (ex is AnalisisSolicitudException
                                       or AgenteExternoTimeoutException
                                       or AgenteExternoException
                                       or DocumentosNoDisponibleException
                                       or SolicitudesNoDisponibleException)
            {
                _logger.LogWarning(ex, "No se pudo generar el análisis de la solicitud {SolicitudId}", solicitudId);
                return null;
            }

            analisis = await CargarAnalisisAsync(solicitudId, ct);
        }

        if (analisis is null)
            return null;

        // La resolución del Consejo (si ya existe) se muestra en el mismo detalle y en el
        // formulario de resolución (HU-17/HU-22).
        var resolucion = await _db.ResolucionesConsejo
            .AsNoTracking()
            .Include(r => r.Decisiones)
            .FirstOrDefaultAsync(r => r.SolicitudId == solicitudId, ct);

        var decisionesFinales = resolucion?.Decisiones
            .ToDictionary(d => d.AsignaturaId) ?? new Dictionary<int, DecisionResolucion>();

        return new DetalleSolicitudDto
        {
            SolicitudId = solicitudId,
            NumeroProceso = info.NumeroProceso,
            Estado = analisis.Estado,
            Fecha = info.FechaRegistro,
            Estudiante = NombreEstudiante(info),
            EstudianteCedula = CedulaEstudiante(info),
            ComiteCorreo = info.EstudianteCorreo,
            Tipo = info.Tipo,
            CarreraOrigen = info.CarreraOrigen,
            CarreraDestino = info.CarreraDestino,
            Modulo = info.Modulo,
            UniversidadOrigen = info.UniversidadOrigen,
            UniversidadDestino = info.UniversidadDestino,
            FechaAnalisis = analisis.FechaAnalisis,
            CoordinadorCorreo = analisis.CoordinadorCorreo,
            FechaInforme = analisis.FechaInforme,
            Resolucion = resolucion is null
                ? null
                : ResolucionService.ConstruirDto(resolucion, analisis.Asignaturas, analisis.Estado),
            Asignaturas = analisis.Asignaturas
                .OrderBy(a => a.Id)
                .Select(a =>
                {
                    decisionesFinales.TryGetValue(a.Id, out var final);
                    return new DetalleAsignaturaDto
                    {
                        Id = a.Id,
                        AsignaturaOrigen = a.AsignaturaOrigen,
                        AsignaturaDestino = a.AsignaturaDestino,
                        AsignaturaDestinoId = a.AsignaturaDestinoId,
                        Combinada = a.Combinada,
                        MateriasOrigen = a.MateriasOrigen
                            .Select(m => new MateriaOrigenDto { Nombre = m.Nombre, Nota = m.Nota })
                            .ToList(),
                        AreaOrigen = a.AreaOrigen,
                        AreaDestino = a.AreaDestino,
                        Propuesta = new PropuestaAgenteDto
                        {
                            Decision = a.Decision,
                            Justificacion = a.Justificacion,
                            PorcentajeContenido = a.PorcentajeContenido,
                            PorcentajeProfundidad = a.PorcentajeProfundidad,
                            PorcentajeHoras = a.PorcentajeHoras,
                            RequiereRevision = a.RequiereRevision
                        },
                        DecisionCoordinador = a.DecisionCoordinador,
                        ObservacionCoordinador = a.ObservacionCoordinador,
                        DecisionFinal = final?.DecisionFinal
                    };
                })
                .ToList()
        };
    }

    /// <exception cref="InformeException">404 sin análisis/solicitud, 409 si ya se emitió, 400 si el informe no es válido.</exception>
    public async Task<ResultadoInformeDto> EmitirInformeAsync(
        int solicitudId, EmitirInformeRequest request, string coordinadorCorreo, CancellationToken ct = default)
    {
        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct);
        if (info is null)
            throw new InformeException(404, "Solicitud no encontrada.");

        var analisis = await _db.AnalisisSolicitudes
            .Include(a => a.Asignaturas)
                .ThenInclude(a => a.MateriasOrigen)
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct);

        if (analisis is null)
            throw new InformeException(404, "La solicitud no tiene un análisis para revisar.");

        if (analisis.Estado == EstadoAnalisis.RESUELTA)
            throw new InformeException(409,
                "La solicitud ya fue resuelta por el Consejo Directivo y no admite cambios en el informe.");

        if (analisis.Estado == EstadoAnalisis.INFORME_EMITIDO)
            throw new InformeException(409, "El informe técnico de esta solicitud ya fue emitido.");

        ValidarInforme(analisis, request);

        foreach (var item in request.Asignaturas)
        {
            var asignatura = analisis.Asignaturas.First(a => a.Id == item.AsignaturaId);
            asignatura.DecisionCoordinador = NormalizarDecision(item.Decision);
            asignatura.ObservacionCoordinador = item.Observacion.Trim();
        }

        analisis.Estado = EstadoAnalisis.INFORME_EMITIDO;
        analisis.CoordinadorCorreo = coordinadorCorreo;
        analisis.FechaInforme = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // El informe ya quedó guardado. Si Solicitudes no responde, se registra el problema
        // pero no se pierde el trabajo del Coordinador.
        var estadoActualizado = false;
        try
        {
            estadoActualizado = await _solicitudes.CambiarEstadoAsync(
                solicitudId, EstadoSolicitudHomologacion.INFORME_EMITIDO, coordinadorCorreo, ct);
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            _logger.LogError(ex, "El informe de {SolicitudId} se guardó, pero no se pudo actualizar Solicitudes", solicitudId);
        }

        return new ResultadoInformeDto
        {
            SolicitudId = solicitudId,
            Estado = analisis.Estado,
            CoordinadorCorreo = coordinadorCorreo,
            FechaInforme = analisis.FechaInforme.Value,
            EstadoSolicitudActualizado = estadoActualizado,
            Asignaturas = analisis.Asignaturas
                .OrderBy(a => a.Id)
                .Select(a => new InformeAsignaturaResultadoDto
                {
                    AsignaturaId = a.Id,
                    AsignaturaOrigen = a.AsignaturaOrigen,
                    Decision = a.DecisionCoordinador!,
                    Observacion = a.ObservacionCoordinador!
                })
                .ToList()
        };
    }

    // ------------------------------------------------------------------
    // Delegación a docentes e informe final (nuevo flujo sin rol estudiante)
    // ------------------------------------------------------------------

    /// <summary>Delegar la solicitud a uno o dos docentes; reemplaza cualquier delegación anterior.</summary>
    /// <exception cref="InformeException">400 si la lista no tiene entre 1 y 2 correos válidos.</exception>
    public async Task<List<DelegacionDto>> DelegarAsync(
        int solicitudId, IEnumerable<string> correos, string usuario, CancellationToken ct = default)
    {
        var destinatarios = (correos ?? Enumerable.Empty<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (destinatarios.Count is < 1 or > 2)
            throw new InformeException(400, "Debe delegar la solicitud a uno o dos docentes.");

        var anteriores = await _db.DelegacionesDocente
            .Where(d => d.SolicitudId == solicitudId)
            .ToListAsync(ct);

        // Se guarda la baja antes de insertar para no chocar con el índice único
        // (SolicitudId, DocenteCorreo) cuando se vuelve a delegar en el mismo docente.
        if (anteriores.Count > 0)
        {
            _db.DelegacionesDocente.RemoveRange(anteriores);
            await _db.SaveChangesAsync(ct);
        }

        var fecha = DateTime.UtcNow;
        var nuevas = destinatarios.Select(correo => new DelegacionDocente
        {
            SolicitudId = solicitudId,
            DocenteCorreo = correo,
            CoordinadorCorreo = usuario ?? string.Empty,
            NombreDocente = correo,
            Estado = EstadoDelegacionDocente.PENDIENTE,
            FechaDelegacion = fecha
        }).ToList();

        _db.DelegacionesDocente.AddRange(nuevas);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "El coordinador {Coordinador} delegó la solicitud {SolicitudId} en: {Docentes}",
            usuario, solicitudId, string.Join(", ", destinatarios));

        return nuevas.Select(d => ConstruirDelegacionDto(d)).ToList();
    }

    /// <summary>Delegaciones vigentes de una solicitud, con la trazabilidad de quien revisó.</summary>
    public async Task<List<DelegacionDto>> ObtenerDelegacionesAsync(
        int solicitudId, CancellationToken ct = default)
    {
        var delegaciones = await _db.DelegacionesDocente
            .AsNoTracking()
            .Where(d => d.SolicitudId == solicitudId)
            .OrderBy(d => d.Id)
            .ToListAsync(ct);

        return delegaciones
            .Select(d => ConstruirDelegacionDto(
                d, d.Estado == EstadoDelegacionDocente.REVISADO ? d.DocenteCorreo : null))
            .ToList();
    }

    /// <summary>
    /// Detalle real de la solicitud, delegaciones y texto del informe final armado con los
    /// resultados de los docentes (o, si no los hay, con las decisiones del Coordinador/agente).
    /// </summary>
    public async Task<ConsolidadoDto> ObtenerConsolidadoAsync(
        int solicitudId, CancellationToken ct = default)
    {
        var detalle = await ObtenerDetalleAsync(solicitudId, ct)
                      ?? throw new InformeException(404, "Solicitud no encontrada.");

        var analisis = await _db.AnalisisSolicitudes
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct);

        var delegaciones = await ObtenerDelegacionesAsync(solicitudId, ct);
        var evaluaciones = await ObtenerEvaluacionesConsolidadasAsync(solicitudId, detalle, ct);

        // El informe final solo existe cuando el Coordinador lo emitió. Mientras tanto se
        // muestra el informe sugerido del Docente, que es el que habilita el informe final.
        var revisada = delegaciones.FirstOrDefault(d =>
            d.Estado == EstadoDelegacionDocente.REVISADO &&
            !string.IsNullOrWhiteSpace(d.InformeTexto));
        var informeSugerido = revisada?.InformeTexto ?? string.Empty;
        var informeFinalGuardado = analisis?.Estado == EstadoAnalisis.INFORME_EMITIDO
            ? analisis.InformeFinalTexto ?? string.Empty
            : string.Empty;
        var filasInformeSugerido = revisada is null
            ? new List<FilaInformeSugeridoDto>()
            : await ObtenerFilasInformeSugeridoAsync(revisada.Id, detalle, ct);

        return new ConsolidadoDto
        {
            Detalle = detalle,
            Delegaciones = delegaciones,
            InformeFinalTexto = string.IsNullOrWhiteSpace(informeFinalGuardado) ? string.Empty : informeFinalGuardado,
            InformeSugeridoTexto = informeSugerido,
            RevisadoPor = revisada?.RevisadoPor ?? revisada?.DocenteCorreo,
            ResultadoDocente = revisada?.ResultadoDocente,
            EstadoRevisionDocente = revisada is null ? "PENDIENTE" : EstadoDelegacionDocente.REVISADO,
            PuedeGenerarInformeFinal = revisada is not null,
            EvaluacionesConsolidadas = evaluaciones,
            InformeSugeridoAsignaturas = filasInformeSugerido
        };
    }

    /// <summary>
    /// Reconstruye la tabla legible del informe sugerido usando las decisiones que el Docente
    /// envió y los nombres reales del análisis. El texto plano sigue disponible para el PDF.
    /// </summary>
    private async Task<List<FilaInformeSugeridoDto>> ObtenerFilasInformeSugeridoAsync(
        int delegacionId, DetalleSolicitudDto detalle, CancellationToken ct)
    {
        var delegacion = await _db.DelegacionesDocente
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == delegacionId, ct);
        if (delegacion is null)
            return new List<FilaInformeSugeridoDto>();

        List<EvaluacionAsignaturaDto>? evaluaciones = null;
        if (!string.IsNullOrWhiteSpace(delegacion.EvaluacionesJson))
        {
            try
            {
                evaluaciones = JsonSerializer.Deserialize<List<EvaluacionAsignaturaDto>>(
                    delegacion.EvaluacionesJson!);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex,
                    "Las evaluaciones de la delegación {DelegacionId} no son válidas", delegacionId);
            }
        }

        if (evaluaciones is null || evaluaciones.Count == 0)
        {
            // Compatibilidad con informes guardados antes de EvaluacionesJson.
            try
            {
                var sugerencia = JsonSerializer.Deserialize<SugerenciaDocenteDto>(
                    delegacion.SugerenciaJson ?? string.Empty);
                if (sugerencia?.Asignaturas.Count > 0)
                    evaluaciones = sugerencia.Asignaturas.Select(a => new EvaluacionAsignaturaDto
                    {
                        AsignaturaId = a.AsignaturaId,
                        Homologable = a.Homologable,
                        Justificacion = a.Justificacion
                    }).ToList();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex,
                    "La sugerencia de la delegación {DelegacionId} no es válida", delegacionId);
            }
        }

        if (evaluaciones is null || evaluaciones.Count == 0)
            return new List<FilaInformeSugeridoDto>();

        var porId = evaluaciones
            .GroupBy(e => e.AsignaturaId)
            .ToDictionary(g => g.Key, g => g.First());

        return detalle.Asignaturas
            .Where(a => porId.ContainsKey(a.Id))
            .OrderBy(a => a.Id)
            .Select(a =>
            {
                var evaluacion = porId[a.Id];
                var nombreOrigen = ReglasHomologacion.NombreOrigen(
                    a.AsignaturaOrigen, a.Combinada, a.MateriasOrigen.Select(m => m.Nombre));
                var motivo = ReglasHomologacion.MotivoSiNoHomologable(nombreOrigen, a.AsignaturaDestino);
                var justificacion = string.IsNullOrWhiteSpace(evaluacion.Justificacion)
                    ? motivo ?? a.Propuesta.Justificacion
                    : evaluacion.Justificacion.Trim();

                return new FilaInformeSugeridoDto
                {
                    AsignaturaId = a.Id,
                    AsignaturaOrigen = nombreOrigen,
                    AsignaturaDestino = a.AsignaturaDestino,
                    CarreraOrigen = detalle.CarreraOrigen,
                    CarreraDestino = detalle.CarreraDestino,
                    Contenido = $"{a.Propuesta.PorcentajeContenido}%",
                    SeHomologa = motivo is null && evaluacion.Homologable ? "SÍ" : "NO",
                    Justificacion = motivo ?? justificacion
                };
            })
            .ToList();
    }

    /// <summary>
    /// Guarda el informe final del Coordinador (se puede volver a emitir para regenerarlo) y
    /// conserva los datos del encabezado para que el PDF sea reproducible.
    /// </summary>
    public async Task<ResultadoInformeDto> EmitirInformeFinalAsync(
        int solicitudId,
        InformeFinalRequest request,
        string coordinadorCorreo,
        CancellationToken ct = default)
    {
        if (request is null)
            throw new InformeException(400, "Debe enviar el informe final.");

        var analisis = await _db.AnalisisSolicitudes
            .Include(a => a.Asignaturas)
                .ThenInclude(a => a.MateriasOrigen)
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct)
            ?? throw new InformeException(404, "La solicitud no tiene un análisis para revisar.");

        if (analisis.Estado == EstadoAnalisis.RESUELTA)
            throw new InformeException(409,
                "La solicitud ya fue resuelta por el Consejo Directivo y no admite cambios en el informe.");

        // El informe final consolida el informe sugerido del Docente: no se puede emitir
        // antes de que uno de los docentes delegados haya revisado la solicitud.
        var revisionDocente = await _db.DelegacionesDocente
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.SolicitudId == solicitudId &&
                                      d.Estado == EstadoDelegacionDocente.REVISADO &&
                                      !string.IsNullOrWhiteSpace(d.InformeTexto), ct);
        if (revisionDocente is null)
            throw new InformeException(409,
                "El informe final solo se puede generar después de que el Docente haya revisado " +
                "y emitido su informe sugerido.");

        var asignaturas = request.Asignaturas ?? new List<InformeAsignaturaDto>();
        ValidarInforme(analisis, new EmitirInformeRequest
        {
            Asignaturas = asignaturas
        });

        foreach (var item in asignaturas)
        {
            var asignatura = analisis.Asignaturas.First(a => a.Id == item.AsignaturaId);
            asignatura.DecisionCoordinador = NormalizarDecision(item.Decision);
            asignatura.ObservacionCoordinador = item.Observacion.Trim();
        }

        var detalle = await ObtenerDetalleAsync(solicitudId, ct)
                      ?? throw new InformeException(404, "Solicitud no encontrada.");

        var nombre = string.IsNullOrWhiteSpace(request.NombreEstudiante)
            ? detalle.Estudiante
            : request.NombreEstudiante.Trim();
        var cedula = string.IsNullOrWhiteSpace(request.CedulaEstudiante)
            ? (string.IsNullOrWhiteSpace(detalle.EstudianteCedula) ? "-" : detalle.EstudianteCedula.Trim())
            : request.CedulaEstudiante.Trim();

        if (string.Equals(nombre, TitularSinIdentificar, StringComparison.OrdinalIgnoreCase) ||
            cedula == "-")
        {
            throw new InformeException(400,
                "Antes de generar el informe final, extraiga el récord académico o corrija " +
                "el nombre y la cédula del titular en el panel del Coordinador.");
        }

        // La corrección del Coordinador se guarda en Solicitudes para futuros informes.
        try
        {
            await _solicitudes.ActualizarTitularAsync(
                solicitudId, nombre, cedula, coordinadorCorreo, ct);
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            _logger.LogWarning(
                ex,
                "El informe final de {SolicitudId} se generó pero no se pudo guardar el titular corregido",
                solicitudId);
        }

        var memorando = string.IsNullOrWhiteSpace(request.NumeroMemorando)
            ? detalle.NumeroProceso
            : request.NumeroMemorando.Trim();
        var firmante = string.IsNullOrWhiteSpace(request.NombreFirmante)
            ? coordinadorCorreo
            : request.NombreFirmante.Trim();

        var evaluaciones = asignaturas
            .Select(a => new EvaluacionAsignaturaDto
            {
                AsignaturaId = a.AsignaturaId,
                Homologable = NormalizarDecision(a.Decision) == DecisionAnalisis.HOMOLOGABLE,
                Justificacion = a.Observacion.Trim()
            })
            .ToList();

        var textoPersonalizado = string.IsNullOrWhiteSpace(request.InformeFinalTexto)
            ? null
            : request.InformeFinalTexto.Trim();

        var filas = ConstruirFilasInforme(detalle, evaluaciones);
        var texto = textoPersonalizado ?? PlantillaInformeFinal.GenerarTexto(new DatosInformeFinal
        {
            NumeroProceso = detalle.NumeroProceso,
            NumeroMemorando = memorando,
            NombreEstudiante = nombre,
            CedulaEstudiante = cedula,
            CarreraOrigen = detalle.CarreraOrigen,
            CarreraDestino = detalle.CarreraDestino,
            Asignaturas = filas,
            Firmante = firmante,
            CoordinadorCorreo = coordinadorCorreo,
            Fecha = DateTime.UtcNow
        });

        analisis.Estado = EstadoAnalisis.INFORME_EMITIDO;
        analisis.CoordinadorCorreo = coordinadorCorreo;
        analisis.FechaInforme = DateTime.UtcNow;
        analisis.InformeFinalTexto = texto;
        analisis.NombreEstudiante = nombre;
        analisis.CedulaEstudiante = cedula;
        analisis.NumeroMemorando = memorando;
        analisis.NombreFirmante = firmante;
        await _db.SaveChangesAsync(ct);

        var estadoActualizado = false;
        try
        {
            estadoActualizado = await _solicitudes.CambiarEstadoAsync(
                solicitudId, EstadoSolicitudHomologacion.INFORME_EMITIDO, coordinadorCorreo, ct);
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            _logger.LogError(ex, "El informe final de {SolicitudId} se guardó, pero no se pudo actualizar Solicitudes", solicitudId);
        }

        return ConstruirResultadoInforme(
            analisis, coordinadorCorreo, estadoActualizado, texto, nombre, cedula, memorando, firmante);
    }

    /// <summary>
    /// Genera una vista previa del texto del informe final sin emitirlo ni persistir cambios.
    /// El Coordinador puede editar este texto antes de emitir el informe final.
    /// </summary>
    public async Task<string> GenerarVistaPreviaInformeFinalAsync(
        int solicitudId,
        InformeFinalRequest request,
        string coordinadorCorreo,
        CancellationToken ct = default)
    {
        if (request is null)
            throw new InformeException(400, "Debe enviar los datos del informe final.");

        var analisis = await _db.AnalisisSolicitudes
            .AsNoTracking()
            .Include(a => a.Asignaturas)
                .ThenInclude(a => a.MateriasOrigen)
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct)
            ?? throw new InformeException(404, "La solicitud no tiene un anǭlisis para revisar.");

        var revisionDocente = await _db.DelegacionesDocente
            .AsNoTracking()
            .AnyAsync(d => d.SolicitudId == solicitudId &&
                           d.Estado == EstadoDelegacionDocente.REVISADO &&
                           !string.IsNullOrWhiteSpace(d.InformeTexto), ct);
        if (!revisionDocente)
            throw new InformeException(409,
                "El informe final solo se puede generar despuǸs de que el Docente haya revisado " +
                "y emitido su informe sugerido.");

        var asignaturas = request.Asignaturas ?? new List<InformeAsignaturaDto>();
        ValidarInforme(analisis, new EmitirInformeRequest { Asignaturas = asignaturas });

        var detalle = await ObtenerDetalleAsync(solicitudId, ct)
                      ?? throw new InformeException(404, "Solicitud no encontrada.");

        var nombre = string.IsNullOrWhiteSpace(request.NombreEstudiante)
            ? detalle.Estudiante
            : request.NombreEstudiante.Trim();
        var cedula = string.IsNullOrWhiteSpace(request.CedulaEstudiante)
            ? (string.IsNullOrWhiteSpace(detalle.EstudianteCedula) ? "-" : detalle.EstudianteCedula.Trim())
            : request.CedulaEstudiante.Trim();
        var memorando = string.IsNullOrWhiteSpace(request.NumeroMemorando)
            ? detalle.NumeroProceso
            : request.NumeroMemorando.Trim();
        var firmante = string.IsNullOrWhiteSpace(request.NombreFirmante)
            ? coordinadorCorreo
            : request.NombreFirmante.Trim();

        var evaluaciones = asignaturas
            .Select(a => new EvaluacionAsignaturaDto
            {
                AsignaturaId = a.AsignaturaId,
                Homologable = NormalizarDecision(a.Decision) == DecisionAnalisis.HOMOLOGABLE,
                Justificacion = a.Observacion.Trim()
            })
            .ToList();

        var filas = ConstruirFilasInforme(detalle, evaluaciones);
        return PlantillaInformeFinal.GenerarTexto(new DatosInformeFinal
        {
            NumeroProceso = detalle.NumeroProceso,
            NumeroMemorando = memorando,
            NombreEstudiante = nombre,
            CedulaEstudiante = cedula,
            CarreraOrigen = detalle.CarreraOrigen,
            CarreraDestino = detalle.CarreraDestino,
            Asignaturas = filas,
            Firmante = firmante,
            CoordinadorCorreo = coordinadorCorreo,
            Fecha = DateTime.UtcNow
        });
    }

    /// <summary>PDF del informe final con los datos guardados al emitirlo.</summary>
    public async Task<byte[]> GenerarInformeFinalPdfAsync(
        int solicitudId, CancellationToken ct = default)
    {
        var analisis = await _db.AnalisisSolicitudes
            .AsNoTracking()
            .Include(a => a.Asignaturas)
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct)
            ?? throw new InformeException(404, "La solicitud no tiene un análisis para revisar.");

        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct)
                   ?? throw new InformeException(404, "Solicitud no encontrada.");

        var detalle = await ObtenerDetalleAsync(solicitudId, ct)
                      ?? throw new InformeException(404, "Solicitud no encontrada.");

        // El PDF del informe final refleja lo que aprobó el Coordinador, no el borrador
        // de los docentes. Si todavía no se emitió, se usa el consolidado disponible.
        var evaluaciones = analisis.Asignaturas.Any(a => !string.IsNullOrWhiteSpace(a.DecisionCoordinador))
            ? analisis.Asignaturas.Select(a => new EvaluacionAsignaturaDto
            {
                AsignaturaId = a.Id,
                Homologable = NormalizarDecision(a.DecisionCoordinador) == DecisionAnalisis.HOMOLOGABLE,
                Justificacion = a.ObservacionCoordinador ?? string.Empty
            }).ToList()
            : await ObtenerEvaluacionesConsolidadasAsync(solicitudId, detalle, ct);
        var filas = ConstruirFilasInforme(detalle, evaluaciones);

        var datos = new DatosInformeFinal
        {
            NumeroProceso = string.IsNullOrWhiteSpace(info.NumeroProceso)
                ? $"SOL-{solicitudId:D4}"
                : info.NumeroProceso,
            NumeroMemorando = analisis.NumeroMemorando ?? detalle.NumeroProceso,
            NombreEstudiante = analisis.NombreEstudiante ?? detalle.Estudiante,
            CedulaEstudiante = string.IsNullOrWhiteSpace(analisis.CedulaEstudiante)
                ? (string.IsNullOrWhiteSpace(detalle.EstudianteCedula) ? "-" : detalle.EstudianteCedula)
                : analisis.CedulaEstudiante,
            CarreraOrigen = detalle.CarreraOrigen,
            CarreraDestino = detalle.CarreraDestino,
            Asignaturas = filas,
            Firmante = analisis.NombreFirmante ?? analisis.CoordinadorCorreo ?? string.Empty,
            CoordinadorCorreo = analisis.CoordinadorCorreo,
            Fecha = analisis.FechaInforme ?? DateTime.UtcNow
        };

        if (!string.IsNullOrWhiteSpace(analisis.InformeFinalTexto))
            return PlantillaInformeFinal.GenerarPdfDesdeTexto(analisis.InformeFinalTexto, datos);

        return PlantillaInformeFinal.GenerarPdf(datos);
    }

    /// <summary>Variante del PDF que usa los datos enviados en el momento de la emisión.</summary>
    public async Task<byte[]> GenerarInformeFinalPdfAsync(
        int solicitudId,
        InformeFinalRequest request,
        string? coordinadorCorreo = null,
        CancellationToken ct = default)
    {
        var consolidado = await ObtenerConsolidadoAsync(solicitudId, ct);
        var evaluaciones = request.Asignaturas
            .Select(a => new EvaluacionAsignaturaDto
            {
                AsignaturaId = a.AsignaturaId,
                Homologable = NormalizarDecision(a.Decision) == DecisionAnalisis.HOMOLOGABLE,
                Justificacion = a.Observacion.Trim()
            })
            .ToList();

        return PlantillaInformeFinal.GenerarPdf(new DatosInformeFinal
        {
            NumeroProceso = consolidado.Detalle.NumeroProceso,
            NumeroMemorando = request.NumeroMemorando ?? consolidado.Detalle.NumeroProceso,
            NombreEstudiante = request.NombreEstudiante ?? consolidado.Detalle.Estudiante,
            CedulaEstudiante = string.IsNullOrWhiteSpace(request.CedulaEstudiante)
                ? (string.IsNullOrWhiteSpace(consolidado.Detalle.EstudianteCedula)
                    ? "-"
                    : consolidado.Detalle.EstudianteCedula)
                : request.CedulaEstudiante,
            CarreraOrigen = consolidado.Detalle.CarreraOrigen,
            CarreraDestino = consolidado.Detalle.CarreraDestino,
            Asignaturas = ConstruirFilasInforme(consolidado.Detalle, evaluaciones),
            Firmante = request.NombreFirmante ?? coordinadorCorreo ?? string.Empty,
            CoordinadorCorreo = coordinadorCorreo,
            Fecha = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Evaluaciones de los docentes; si ninguno registró evaluaciones se usan las decisiones
    /// vigentes del Coordinador o, en su defecto, las del agente.
    /// </summary>
    private async Task<List<EvaluacionAsignaturaDto>> ObtenerEvaluacionesConsolidadasAsync(
        int solicitudId, DetalleSolicitudDto detalle, CancellationToken ct)
    {
        var idsValidos = detalle.Asignaturas.Select(a => a.Id).ToHashSet();
        var nombresPorId = detalle.Asignaturas.ToDictionary(a => a.Id);
        var revisadas = await _db.DelegacionesDocente
            .AsNoTracking()
            .Where(d => d.SolicitudId == solicitudId &&
                        d.Estado == EstadoDelegacionDocente.REVISADO &&
                        d.EvaluacionesJson != null)
            .OrderBy(d => d.FechaRevision)
            .ThenBy(d => d.Id)
            .ToListAsync(ct);

        var resultado = new List<EvaluacionAsignaturaDto>();
        var vistos = new HashSet<int>();

        foreach (var delegacion in revisadas)
        {
            List<EvaluacionAsignaturaDto>? evaluaciones;
            try
            {
                evaluaciones = JsonSerializer.Deserialize<List<EvaluacionAsignaturaDto>>(delegacion.EvaluacionesJson!);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Las evaluaciones de la delegación {DelegacionId} no son válidas", delegacion.Id);
                continue;
            }

            foreach (var evaluacion in evaluaciones ?? new List<EvaluacionAsignaturaDto>())
            {
                if (!idsValidos.Contains(evaluacion.AsignaturaId) || !vistos.Add(evaluacion.AsignaturaId))
                    continue;

                var asignatura = nombresPorId[evaluacion.AsignaturaId];
                var motivo = ReglasHomologacion.MotivoSiNoHomologable(
                    ReglasHomologacion.NombreOrigen(
                        asignatura.AsignaturaOrigen, asignatura.Combinada,
                        asignatura.MateriasOrigen.Select(m => m.Nombre)),
                    asignatura.AsignaturaDestino);
                if (motivo is not null)
                {
                    evaluacion.Homologable = false;
                    evaluacion.Justificacion = motivo;
                }

                resultado.Add(evaluacion);
            }
        }

        if (resultado.Count > 0)
            return resultado;

        return detalle.Asignaturas
            .OrderBy(a => a.Id)
            .Select(a =>
            {
                var decision = NormalizarDecision(string.IsNullOrWhiteSpace(a.DecisionCoordinador)
                    ? a.Propuesta.Decision
                    : a.DecisionCoordinador);
                var motivo = ReglasHomologacion.MotivoSiNoHomologable(
                    ReglasHomologacion.NombreOrigen(
                        a.AsignaturaOrigen, a.Combinada, a.MateriasOrigen.Select(m => m.Nombre)),
                    a.AsignaturaDestino);

                return new EvaluacionAsignaturaDto
                {
                    AsignaturaId = a.Id,
                    Homologable = motivo is null && decision == DecisionAnalisis.HOMOLOGABLE,
                    Justificacion = motivo ?? (string.IsNullOrWhiteSpace(a.ObservacionCoordinador)
                        ? a.Propuesta.Justificacion
                        : a.ObservacionCoordinador)
                };
            })
            .ToList();
    }

    /// <summary>Filas (solo homologables) a partir del detalle real y las evaluaciones dadas.</summary>
    public static List<FilaInformeAsignatura> ConstruirFilasInforme(
        DetalleSolicitudDto detalle, IEnumerable<EvaluacionAsignaturaDto> evaluaciones)
    {
        var porId = evaluaciones
            .Where(e => e.Homologable)
            .GroupBy(e => e.AsignaturaId)
            .ToDictionary(g => g.Key, g => g.First());

        return detalle.Asignaturas
            .Where(a => porId.ContainsKey(a.Id) &&
                        !ReglasHomologacion.EsMateriaNoHomologable(
                            ReglasHomologacion.NombreOrigen(
                                a.AsignaturaOrigen, a.Combinada,
                                a.MateriasOrigen.Select(m => m.Nombre)),
                            a.AsignaturaDestino))
            .OrderBy(a => a.Id)
            .Select(a =>
            {
                var origen = a.Combinada && a.MateriasOrigen.Count > 0
                    ? string.Join(" + ", a.MateriasOrigen.Select(m => m.Nombre))
                    : a.AsignaturaOrigen;

                var justificacion = porId[a.Id].Justificacion;
                return new FilaInformeAsignatura
                {
                    AsignaturaOrigen = origen,
                    AsignaturaDestino = a.AsignaturaDestino,
                    Justificacion = justificacion,
                    Contenido = $"{a.Propuesta.PorcentajeContenido}% · {justificacion}",
                    SeHomologa = "SÍ"
                };
            })
            .ToList();
    }

    public static DelegacionDto ConstruirDelegacionDto(DelegacionDocente delegacion, string? revisadoPor = null) => new()
    {
        Id = delegacion.Id,
        SolicitudId = delegacion.SolicitudId,
        DocenteCorreo = delegacion.DocenteCorreo,
        NombreDocente = delegacion.NombreDocente,
        DocenteCedula = delegacion.DocenteCedula,
        DocenteCorreoInforme = delegacion.DocenteCorreoInforme,
        CoordinadorCorreo = delegacion.CoordinadorCorreo,
        Estado = delegacion.Estado,
        FechaDelegacion = delegacion.FechaDelegacion,
        FechaRevision = delegacion.FechaRevision,
        InformeTexto = delegacion.InformeTexto,
        InformeSugeridoTexto = delegacion.InformeTexto,
        RevisadoPor = revisadoPor,
        ResultadoDocente = CalcularResultadoDocente(delegacion.EvaluacionesJson)
    };

    /// <summary>
    /// Un docente aprueba la homologación cuando marcó al menos una asignatura como homologable;
    /// si marcó todas como no homologables, el resultado es NO_APROBADO.
    /// </summary>
    internal static string? CalcularResultadoDocente(string? evaluacionesJson)
    {
        if (string.IsNullOrWhiteSpace(evaluacionesJson))
            return null;

        try
        {
            var evaluaciones = JsonSerializer.Deserialize<List<EvaluacionAsignaturaDto>>(evaluacionesJson);
            if (evaluaciones is null || evaluaciones.Count == 0)
                return null;

            return evaluaciones.Any(e => e.Homologable) ? "APROBADO" : "NO_APROBADO";
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ResultadoInformeDto ConstruirResultadoInforme(
        AnalisisSolicitud analisis,
        string coordinadorCorreo,
        bool estadoActualizado,
        string texto,
        string nombre,
        string cedula,
        string memorando,
        string firmante) => new()
    {
        SolicitudId = analisis.SolicitudId,
        Estado = analisis.Estado,
        CoordinadorCorreo = coordinadorCorreo,
        FechaInforme = analisis.FechaInforme ?? DateTime.UtcNow,
        EstadoSolicitudActualizado = estadoActualizado,
        Asignaturas = analisis.Asignaturas
            .OrderBy(a => a.Id)
            .Select(a => new InformeAsignaturaResultadoDto
            {
                AsignaturaId = a.Id,
                AsignaturaOrigen = a.AsignaturaOrigen,
                Decision = a.DecisionCoordinador ?? string.Empty,
                Observacion = a.ObservacionCoordinador ?? string.Empty
            })
            .ToList(),
        InformeFinalTexto = texto,
        NombreEstudiante = nombre,
        CedulaEstudiante = cedula,
        NumeroMemorando = memorando,
        NombreFirmante = firmante
    };

    private async Task<AnalisisSolicitud?> CargarAnalisisAsync(int solicitudId, CancellationToken ct) =>
        await _db.AnalisisSolicitudes
            .Include(a => a.Asignaturas)
                .ThenInclude(a => a.MateriasOrigen)
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct);

    /// <summary>Valida que el informe traiga exactamente las asignaturas del análisis, sin repetidas ni ajenas.</summary>
    private static void ValidarInforme(AnalisisSolicitud analisis, EmitirInformeRequest request)
    {
        if (request?.Asignaturas is null || request.Asignaturas.Count == 0)
            throw new InformeException(400, "Debe enviar la decisión de al menos una asignatura.");

        var idsAnalisis = analisis.Asignaturas.Select(a => a.Id).ToHashSet();
        var vistos = new HashSet<int>();

        foreach (var item in request.Asignaturas)
        {
            if (!idsAnalisis.Contains(item.AsignaturaId))
                throw new InformeException(400, $"La asignatura {item.AsignaturaId} no pertenece a esta solicitud.");

            if (!vistos.Add(item.AsignaturaId))
                throw new InformeException(400, $"La asignatura {item.AsignaturaId} está repetida en el informe.");

            // El Coordinador tampoco puede aprobar estas materias por regla institucional.
            var asignatura = analisis.Asignaturas.First(a => a.Id == item.AsignaturaId);
            var motivoExclusion = ReglasHomologacion.MotivoSiNoHomologable(
                ReglasHomologacion.NombreOrigen(
                    asignatura.AsignaturaOrigen, asignatura.Combinada,
                    asignatura.MateriasOrigen.Select(m => m.Nombre)),
                asignatura.AsignaturaDestino);
            if (motivoExclusion is not null)
            {
                item.Decision = DecisionAnalisis.NO_HOMOLOGABLE;
                if (string.IsNullOrWhiteSpace(item.Observacion))
                    item.Observacion = motivoExclusion;
            }

            var decision = NormalizarDecision(item.Decision);
            if (decision != DecisionAnalisis.HOMOLOGABLE && decision != DecisionAnalisis.NO_HOMOLOGABLE)
                throw new InformeException(400, $"Decisión inválida para la asignatura {item.AsignaturaId}. Use HOMOLOGABLE o NO_HOMOLOGABLE.");

            if (string.IsNullOrWhiteSpace(item.Observacion))
                throw new InformeException(400, $"La observación de la asignatura {item.AsignaturaId} es obligatoria.");

            if (item.Observacion.Trim().Length > 1000)
                throw new InformeException(400, $"La observación de la asignatura {item.AsignaturaId} supera los 1000 caracteres.");
        }

        if (vistos.Count != idsAnalisis.Count)
            throw new InformeException(400, "Debe enviar la decisión de todas las asignaturas del análisis.");
    }

    private static string NormalizarDecision(string? decision) =>
        (decision ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>
    /// El informe muestra el nombre real del titular tomado del récord; el correo es solo
    /// de la cuenta del Comité que registró el trámite y nunca se usa como nombre.
    /// </summary>
    internal static string NombreEstudiante(SolicitudInfoDto info) =>
        string.IsNullOrWhiteSpace(info.EstudianteNombre)
            ? TitularSinIdentificar
            : info.EstudianteNombre.Trim();

    internal static string CedulaEstudiante(SolicitudInfoDto info) =>
        string.IsNullOrWhiteSpace(info.EstudianteCedula)
            ? "-"
            : info.EstudianteCedula.Trim();
}
