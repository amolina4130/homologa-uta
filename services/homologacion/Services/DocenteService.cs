using System.Text.Json;
using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Models;
using HomologaUTA.Homologacion.Reportes;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Services;

/// <summary>
/// Flujo del docente asignado por el Coordinador: consulta sus solicitudes, evalúa las
/// asignaturas y genera su informe sugerido. La primera revisión queda registrada; si otro
/// docente calificó primero, el resto solo ve la trazabilidad.
/// </summary>
public class DocenteService
{
    /// <summary>Contenido mínimo para que una asignatura pueda considerarse homologable (RN-01).</summary>
    private const int UmbralContenidoHomologacion = 80;

    private readonly HomologacionDbContext _db;
    private readonly SolicitudesClient _solicitudes;
    private readonly CoordinadorService _coordinador;
    private readonly OllamaClient _ollama;
    private readonly ILogger<DocenteService> _logger;

    public DocenteService(
        HomologacionDbContext db,
        SolicitudesClient solicitudes,
        CoordinadorService coordinador,
        OllamaClient ollama,
        ILogger<DocenteService> logger)
    {
        _db = db;
        _solicitudes = solicitudes;
        _coordinador = coordinador;
        _ollama = ollama;
        _logger = logger;
    }

    /// <summary>
    /// Solicitudes delegadas al docente. Sin correo (llamada interna) se listan todas las
    /// delegaciones, igual que hacen los helpers de autorización cuando no hay identidad.
    /// </summary>
    public async Task<List<SolicitudDocenteDto>> ListarSolicitudesAsync(
        string? correo, CancellationToken ct = default)
    {
        var filtro =string.IsNullOrWhiteSpace(correo) ? string.Empty : correo.Trim().ToUpperInvariant();

        var query = _db.DelegacionesDocente.AsNoTracking().AsQueryable();
        if (filtro.Length > 0)
            query = query.Where(d => d.DocenteCorreo.ToUpper() == filtro);

        var propias = await query.OrderBy(d => d.FechaDelegacion).ToListAsync(ct);
        if (propias.Count == 0)
            return new List<SolicitudDocenteDto>();

        var ids = propias.Select(d => d.SolicitudId).Distinct().ToList();
        var todas = await _db.DelegacionesDocente
            .AsNoTracking()
            .Where(d => ids.Contains(d.SolicitudId))
            .ToListAsync(ct);

        var infoPorId = new Dictionary<int, SolicitudInfoDto>();
        try
        {
            var infos = await _solicitudes.ObtenerPorIdsAsync(ids, ct);
            infoPorId = infos.ToDictionary(i => i.Id);
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            _logger.LogWarning(ex, "No se pudo completar la lista de solicitudes del docente");
        }

        var analisisIds = await _db.AnalisisSolicitudes
            .AsNoTracking()
            .Where(a => ids.Contains(a.SolicitudId))
            .Select(a => new { a.Id, a.SolicitudId })
            .ToListAsync(ct);
        var idPorAnalisis = analisisIds.ToDictionary(a => a.Id, a => a.SolicitudId);

        var totales = await _db.AnalisisAsignaturas
            .AsNoTracking()
            .Where(a => idPorAnalisis.Keys.Contains(a.AnalisisSolicitudId))
            .GroupBy(a => a.AnalisisSolicitudId)
            .Select(g => new { AnalisisId = g.Key, Cantidad = g.Count() })
            .ToListAsync(ct);
        var totalPorSolicitud = totales
            .GroupBy(t => idPorAnalisis[t.AnalisisId])
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Cantidad));

        var lista = new List<SolicitudDocenteDto>();
        foreach (var delegacion in propias)
        {
            var revisadaPorOtro = todas.FirstOrDefault(d =>
                d.SolicitudId == delegacion.SolicitudId &&
                d.Id != delegacion.Id &&
                d.Estado == EstadoDelegacionDocente.REVISADO);

            var estado = delegacion.Estado == EstadoDelegacionDocente.REVISADO
                ? EstadoDelegacionDocente.REVISADO
                : revisadaPorOtro is not null ? "YA_REVISADO" : EstadoDelegacionDocente.PENDIENTE;

            // La revisión que se muestra es siempre la del docente que calificó primero,
            // aunque este docente tenga su delegación todavía pendiente.
            var revision = delegacion.Estado == EstadoDelegacionDocente.REVISADO
                ? delegacion
                : revisadaPorOtro;

            infoPorId.TryGetValue(delegacion.SolicitudId, out var info);
            totalPorSolicitud.TryGetValue(delegacion.SolicitudId, out var total);

            // El borrador propio tiene prioridad; si no existe se muestra el de otro docente.
            var sugerenciaDelegacion = !string.IsNullOrWhiteSpace(delegacion.SugerenciaJson)
                ? delegacion
                : todas.FirstOrDefault(d => d.SolicitudId == delegacion.SolicitudId &&
                                            d.Id != delegacion.Id &&
                                            !string.IsNullOrWhiteSpace(d.SugerenciaJson));
            var sugerencia = DeserializarSugerencia(sugerenciaDelegacion?.SugerenciaJson);

            lista.Add(new SolicitudDocenteDto
            {
                SolicitudId = delegacion.SolicitudId,
                NumeroProceso = info?.NumeroProceso ?? $"SOL-{delegacion.SolicitudId:D4}",
                Fecha = info?.FechaRegistro ?? delegacion.FechaDelegacion,
                Estudiante = info is null ? string.Empty : CoordinadorService.NombreEstudiante(info),
                EstudianteCedula = info is null ? string.Empty : CoordinadorService.CedulaEstudiante(info),
                ComiteCorreo = info?.EstudianteCorreo ?? string.Empty,
                CoordinadorCorreo = delegacion.CoordinadorCorreo,
                NombreDocente = delegacion.NombreDocente,
                DocenteCedula = delegacion.DocenteCedula,
                DocenteCorreoInforme = delegacion.DocenteCorreoInforme,
                Modulo = info?.Modulo ?? "INTERNO",
                UniversidadOrigen = info?.UniversidadOrigen,
                UniversidadDestino = info?.UniversidadDestino,
                CarreraOrigen = info?.CarreraOrigen ?? string.Empty,
                CarreraDestino = info?.CarreraDestino ?? string.Empty,
                EstadoDelegacion = estado,
                RevisadoPor = revisadaPorOtro?.DocenteCorreo
                              ?? (estado == EstadoDelegacionDocente.REVISADO ? delegacion.DocenteCorreo : null),
                PuedeEvaluar = estado == EstadoDelegacionDocente.PENDIENTE,
                TotalAsignaturas = total != 0 ? total : info?.TotalAsignaturas ?? 0,
                ResultadoDocente = CoordinadorService.CalcularResultadoDocente(revision?.EvaluacionesJson),
                InformeSugeridoTexto = revision?.InformeTexto ?? sugerencia?.InformeTexto,
                FechaRevision = revision?.FechaRevision,
                TieneSugerencia = sugerencia is not null,
                SugerenciaEnviada = revision is not null,
                FechaSugerencia = sugerencia?.FechaGeneracion ?? sugerenciaDelegacion?.FechaSugerencia,
                SugerenciaGeneradaPor = sugerencia?.GeneradaPor
                                        ?? sugerenciaDelegacion?.NombreDocente
                                        ?? sugerenciaDelegacion?.DocenteCorreo
            });
        }

        return lista.OrderBy(s => s.Fecha).ToList();
    }

    /// <summary>Detalle real de la solicitud más la delegación y el informe sugerido por plantilla.</summary>
    public async Task<DetalleDocenteDto> ObtenerDetalleAsync(
        int solicitudId, string? correo, CancellationToken ct = default)
    {
        var delegacion = await ObtenerDelegacionAsync(solicitudId, correo, ct)
                         ?? throw new InformeException(404, "Usted no tiene una delegación en esta solicitud.");

        var detalle = await _coordinador.ObtenerDetalleAsync(solicitudId, ct)
                      ?? throw new InformeException(404, "Solicitud no encontrada.");

        var revisadaPorOtro = await _db.DelegacionesDocente
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.SolicitudId == solicitudId &&
                                      d.Id != delegacion.Id &&
                                      d.Estado == EstadoDelegacionDocente.REVISADO, ct);

        var revision = delegacion.Estado == EstadoDelegacionDocente.REVISADO
            ? delegacion
            : revisadaPorOtro;
        var puedeEvaluar = delegacion.Estado == EstadoDelegacionDocente.PENDIENTE &&
                           revisadaPorOtro is null;

        // Preferimos la sugerencia del informe ya enviado; si todavía no se envió, mostramos
        // la de este docente y, en su defecto, la de otro docente asignado a la solicitud.
        var sugerenciaDelegacion = revision is not null &&
                                   !string.IsNullOrWhiteSpace(revision.SugerenciaJson)
            ? revision
            : !string.IsNullOrWhiteSpace(delegacion.SugerenciaJson)
                ? delegacion
                : await _db.DelegacionesDocente
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.SolicitudId == solicitudId &&
                                              d.Id != delegacion.Id &&
                                              !string.IsNullOrWhiteSpace(d.SugerenciaJson), ct);

        var sugerencia = DeserializarSugerencia(sugerenciaDelegacion?.SugerenciaJson);
        if (sugerencia is not null)
            sugerencia.Enviada = revision is not null;

        var texto = revision?.InformeTexto ?? sugerencia?.InformeTexto ?? string.Empty;

        var delegacionDto = CoordinadorService.ConstruirDelegacionDto(
            delegacion,
            revision?.DocenteCorreo
            ?? (delegacion.Estado == EstadoDelegacionDocente.REVISADO ? delegacion.DocenteCorreo : null));
        delegacionDto.InformeSugeridoTexto = texto;
        delegacionDto.ResultadoDocente = CoordinadorService.CalcularResultadoDocente(revision?.EvaluacionesJson);
        if (revisadaPorOtro is not null && delegacion.Estado != EstadoDelegacionDocente.REVISADO)
            delegacionDto.Estado = "YA_REVISADO";

        return new DetalleDocenteDto
        {
            Detalle = detalle,
            Delegacion = delegacionDto,
            InformeSugeridoTexto = texto,
            PuedeEvaluar = puedeEvaluar,
            Sugerencia = sugerencia,
            SugerenciaEsDeOtroDocente = sugerenciaDelegacion is not null &&
                                        sugerenciaDelegacion.Id != delegacion.Id,
            NombreDocente = revision?.NombreDocente ?? delegacion.NombreDocente ?? correo ?? string.Empty,
            DocenteCedula = revision?.DocenteCedula ?? delegacion.DocenteCedula ?? string.Empty,
            DocenteCorreoInforme = revision?.DocenteCorreoInforme ??
                                   delegacion.DocenteCorreoInforme ??
                                   delegacion.DocenteCorreo ?? correo ?? string.Empty
        };
    }

    /// <summary>
    /// PASO 1 del flujo del Docente: pide a la IA una sugerencia y la guarda como borrador.
    /// No envía nada al Coordinador: el Docente todavía debe agregar observaciones, decidir
    /// manualmente y enviar el informe sugerido con <see cref="EvaluarAsync"/>.
    /// </summary>
    public async Task<SugerenciaDocenteDto> GenerarSugerenciaAsync(
        int solicitudId,
        string? correo,
        GenerarSugerenciaRequest? request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(correo))
            throw new InformeException(400, "No se pudo identificar al docente.");

        var delegacion = await ObtenerDelegacionAsync(solicitudId, correo, ct)
                         ?? throw new InformeException(404, "Usted no tiene una delegación en esta solicitud.");
        await ValidarPuedeRevisarAsync(delegacion, solicitudId, correo, ct);
        AplicarDatosDocente(delegacion, request, correo);

        var analisis = await _db.AnalisisSolicitudes
            .AsNoTracking()
            .Include(a => a.Asignaturas)
                .ThenInclude(a => a.MateriasOrigen)
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct)
            ?? throw new InformeException(404, "La solicitud no tiene un análisis para evaluar.");

        var detalle = await _coordinador.ObtenerDetalleAsync(solicitudId, ct)
                      ?? throw new InformeException(404, "Solicitud no encontrada.");
        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct)
                   ?? throw new InformeException(404, "Solicitud no encontrada.");

        var anterior = DeserializarSugerencia(delegacion.SugerenciaJson);
        var evaluaciones = request?.Asignaturas is { Count: > 0 }
            ? request.Asignaturas
            : EvaluacionesPropuestas(detalle);
        AplicarReglasNoHomologables(analisis, evaluaciones);
        ValidarEvaluaciones(analisis, evaluaciones);

        var filas = CoordinadorService.ConstruirFilasInforme(detalle, evaluaciones);
        var numero = string.IsNullOrWhiteSpace(info.NumeroProceso)
            ? $"SOL-{solicitudId:D4}"
            : info.NumeroProceso;

        string? opinionIA = null;
        if (request?.ReutilizarOpinion == true && !string.IsNullOrWhiteSpace(anterior?.OpinionIA))
            opinionIA = anterior.OpinionIA;
        else
            opinionIA = await GenerarOpinionIAAsync(
                numero, info.CarreraOrigen, info.CarreraDestino, filas, ct);

        var observacion = request?.ObservacionGeneral?.Trim();
        if (string.IsNullOrWhiteSpace(observacion))
            observacion = anterior?.ObservacionGeneral;

        var textoPlantilla = PlantillaInformeDocente.GenerarTexto(new DatosInformeDocente
        {
            NumeroProceso = numero,
            NumeroMemorando = numero,
            NombreEstudiante = CoordinadorService.NombreEstudiante(info),
            CedulaEstudiante = CoordinadorService.CedulaEstudiante(info),
            CarreraOrigen = info.CarreraOrigen,
            CarreraDestino = info.CarreraDestino,
            Asignaturas = filas,
            ObservacionGeneral = observacion,
            Docente = delegacion.NombreDocente ?? correo,
            CedulaFirmante = delegacion.DocenteCedula ?? "-",
            CorreoFirmante = delegacion.DocenteCorreoInforme ?? correo,
            Fecha = DateTime.UtcNow,
            OpinionIA = opinionIA
        });
        var textoInforme = string.IsNullOrWhiteSpace(request?.InformeTexto)
            ? textoPlantilla
            : request!.InformeTexto!.Trim();

        var sugerencia = new SugerenciaDocenteDto
        {
            SolicitudId = solicitudId,
            DelegacionId = delegacion.Id,
            GeneradaPor = delegacion.NombreDocente ?? correo,
            NombreDocente = delegacion.NombreDocente ?? correo,
            DocenteCedula = delegacion.DocenteCedula ?? "-",
            DocenteCorreoInforme = delegacion.DocenteCorreoInforme ?? correo,
            FechaGeneracion = DateTime.UtcNow,
            Asignaturas = evaluaciones.Select(e =>
            {
                var a = detalle.Asignaturas.FirstOrDefault(x => x.Id == e.AsignaturaId);
                return new SugerenciaAsignaturaDto
                {
                    AsignaturaId = e.AsignaturaId,
                    AsignaturaOrigen = a?.Combinada == true && a.MateriasOrigen.Count > 0
                        ? string.Join(" + ", a.MateriasOrigen.Select(m => m.Nombre))
                        : a?.AsignaturaOrigen ?? string.Empty,
                    AsignaturaDestino = a?.AsignaturaDestino ?? string.Empty,
                    Homologable = e.Homologable,
                    Justificacion = e.Justificacion,
                    PorcentajeContenido = a?.Propuesta.PorcentajeContenido ?? 0
                };
            }).ToList(),
            ObservacionGeneral = observacion,
            OpinionIA = opinionIA,
            InformeTexto = textoInforme
        };

        delegacion.SugerenciaJson = JsonSerializer.Serialize(sugerencia);
        delegacion.InformeTexto = textoInforme;
        delegacion.FechaSugerencia = sugerencia.FechaGeneracion;
        await _db.SaveChangesAsync(ct);

        return sugerencia;
    }

    /// <summary>
    /// PASO 2: el Docente envía manualmente al Coordinador el informe sugerido ya revisado.
    /// Se reportan todas las asignaturas evaluadas, con SÍ o NO. El primero que envía queda
    /// REVISADO; el otro docente ve la trazabilidad y no puede modificarlo.
    /// </summary>
    public async Task<ResultadoEvaluacionDto> EvaluarAsync(
        int solicitudId,
        string? correo,
        EvaluarDocenteRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(correo))
            throw new InformeException(400, "No se pudo identificar al docente.");

        request ??= new EvaluarDocenteRequest();

        // El estado NO se cambia automáticamente al guardar observaciones ni al generar la
        // sugerencia. Solo se registra REVISADO cuando el Docente lo marca y envía.
        if (!string.Equals(request.EstadoRevision?.Trim(), EstadoDelegacionDocente.REVISADO,
                StringComparison.OrdinalIgnoreCase))
            throw new InformeException(400,
                "Marque el estado de su revisión como REVISADO antes de enviar el informe.");

        var delegacion = await ObtenerDelegacionAsync(solicitudId, correo, ct)
                         ?? throw new InformeException(404, "Usted no tiene una delegación en esta solicitud.");

        await ValidarPuedeRevisarAsync(delegacion, solicitudId, correo, ct);
        AplicarDatosDocente(delegacion, request, correo);

        var analisis = await _db.AnalisisSolicitudes
            .AsNoTracking()
            .Include(a => a.Asignaturas)
                .ThenInclude(a => a.MateriasOrigen)
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct)
            ?? throw new InformeException(404, "La solicitud no tiene un análisis para evaluar.");

        var evaluaciones = request?.Asignaturas ?? new List<EvaluacionAsignaturaDto>();
        AplicarReglasNoHomologables(analisis, evaluaciones);
        ValidarEvaluaciones(analisis, evaluaciones);

        var porId = analisis.Asignaturas.ToDictionary(a => a.Id);
        var filas = evaluaciones
            .OrderBy(e => e.AsignaturaId)
            .Select(e =>
            {
                var asignatura = porId[e.AsignaturaId];
                var justificacion = (e.Justificacion ?? string.Empty).Trim();
                return new FilaInformeAsignatura
                {
                    AsignaturaOrigen = asignatura.Combinada && asignatura.MateriasOrigen.Count > 0
                        ? string.Join(" + ", asignatura.MateriasOrigen.Select(m => m.Nombre))
                        : asignatura.AsignaturaOrigen,
                    AsignaturaDestino = asignatura.AsignaturaDestino,
                    Justificacion = justificacion,
                    Contenido = $"{asignatura.PorcentajeContenido}% · {justificacion}",
                    SeHomologa = e.Homologable ? "SÍ" : "NO"
                };
            })
            .ToList();

        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct)
                   ?? throw new InformeException(404, "Solicitud no encontrada.");

        var numero = string.IsNullOrWhiteSpace(info.NumeroProceso)
            ? $"SOL-{solicitudId:D4}"
            : info.NumeroProceso;
        // Si el Docente ya vio la opinión en el borrador, se conserva: el informe enviado debe
        // coincidir con lo que revisó. Si no hay borrador se genera por compatibilidad con el
        // endpoint directo.
        var borrador = DeserializarSugerencia(delegacion.SugerenciaJson);
        var opinionIA = borrador?.OpinionIA;
        if (string.IsNullOrWhiteSpace(opinionIA))
        {
            // Si este docente revisó el borrador generado por otro, el informe enviado debe
            // conservar la misma opinión que vio en pantalla.
            var borradorOtro = await _db.DelegacionesDocente
                .AsNoTracking()
                .Where(d => d.SolicitudId == solicitudId && d.Id != delegacion.Id &&
                            d.SugerenciaJson != null)
                .OrderByDescending(d => d.FechaSugerencia)
                .FirstOrDefaultAsync(ct);
            opinionIA = DeserializarSugerencia(borradorOtro?.SugerenciaJson)?.OpinionIA;
        }
        if (string.IsNullOrWhiteSpace(opinionIA))
            opinionIA = await GenerarOpinionIAAsync(
                numero, info.CarreraOrigen, info.CarreraDestino, filas, ct);

        var textoPlantilla = PlantillaInformeDocente.GenerarTexto(new DatosInformeDocente
        {
            NumeroProceso = numero,
            NumeroMemorando = numero,
            NombreEstudiante = CoordinadorService.NombreEstudiante(info),
            CedulaEstudiante = CoordinadorService.CedulaEstudiante(info),
            CarreraOrigen = info.CarreraOrigen,
            CarreraDestino = info.CarreraDestino,
            Asignaturas = filas,
            ObservacionGeneral = request?.ObservacionGeneral,
            Docente = delegacion.NombreDocente ?? correo,
            CedulaFirmante = delegacion.DocenteCedula ?? "-",
            CorreoFirmante = delegacion.DocenteCorreoInforme ?? correo,
            Fecha = DateTime.UtcNow,
            OpinionIA = opinionIA
        });
        var texto = string.IsNullOrWhiteSpace(request?.InformeTexto)
            ? textoPlantilla
            : request!.InformeTexto!.Trim();

        // Trazabilidad completa: se conserva quién generó la sugerencia y cuándo, y se guarda
        // la decisión final del Docente (puede diferir de lo que propuso la IA).
        if (borrador is not null)
        {
            borrador.Enviada = true;
            borrador.ObservacionGeneral = request?.ObservacionGeneral?.Trim();
            borrador.OpinionIA = opinionIA;
            borrador.NombreDocente = delegacion.NombreDocente ?? correo;
            borrador.DocenteCedula = delegacion.DocenteCedula ?? "-";
            borrador.DocenteCorreoInforme = delegacion.DocenteCorreoInforme ?? correo;
            borrador.InformeTexto = texto;
            borrador.Asignaturas = evaluaciones.Select(e =>
            {
                var a = porId[e.AsignaturaId];
                return new SugerenciaAsignaturaDto
                {
                    AsignaturaId = e.AsignaturaId,
                    AsignaturaOrigen = a.Combinada && a.MateriasOrigen.Count > 0
                        ? string.Join(" + ", a.MateriasOrigen.Select(m => m.Nombre))
                        : a.AsignaturaOrigen,
                    AsignaturaDestino = a.AsignaturaDestino,
                    Homologable = e.Homologable,
                    Justificacion = e.Justificacion,
                    PorcentajeContenido = a.PorcentajeContenido
                };
            }).ToList();
            delegacion.SugerenciaJson = JsonSerializer.Serialize(borrador);
        }

        delegacion.Estado = EstadoDelegacionDocente.REVISADO;
        delegacion.FechaRevision = DateTime.UtcNow;
        delegacion.NombreDocente ??= correo;
        delegacion.InformeTexto = texto;
        delegacion.EvaluacionesJson = JsonSerializer.Serialize(evaluaciones);
        await _db.SaveChangesAsync(ct);

        return new ResultadoEvaluacionDto
        {
            DelegacionId = delegacion.Id,
            SolicitudId = solicitudId,
            Estado = delegacion.Estado,
            InformeTexto = texto,
            FechaRevision = delegacion.FechaRevision.Value
        };
    }

    /// <summary>PDF del informe del docente asignado.</summary>
    public async Task<byte[]> GenerarPdfAsync(
        int solicitudId, string? correo, CancellationToken ct = default)
    {
        var delegacion = await ObtenerDelegacionAsync(solicitudId, correo, ct)
                         ?? throw new InformeException(404, "Usted no tiene una delegación en esta solicitud.");

        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct)
                   ?? throw new InformeException(404, "Solicitud no encontrada.");

        var analisis = await _db.AnalisisSolicitudes
            .AsNoTracking()
            .Include(a => a.Asignaturas)
                .ThenInclude(a => a.MateriasOrigen)
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct)
            ?? throw new InformeException(404, "La solicitud no tiene un análisis para revisar.");

        var evaluaciones = new List<EvaluacionAsignaturaDto>();
        if (!string.IsNullOrWhiteSpace(delegacion.EvaluacionesJson))
        {
            try
            {
                evaluaciones = JsonSerializer.Deserialize<List<EvaluacionAsignaturaDto>>(
                                   delegacion.EvaluacionesJson!) ?? new List<EvaluacionAsignaturaDto>();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Las evaluaciones de la delegación {DelegacionId} no son válidas", delegacion.Id);
            }
        }

        var borrador = DeserializarSugerencia(delegacion.SugerenciaJson);
        if (evaluaciones.Count == 0 && borrador is not null)
        {
            evaluaciones = borrador.Asignaturas.Select(a => new EvaluacionAsignaturaDto
            {
                AsignaturaId = a.AsignaturaId,
                Homologable = a.Homologable,
                Justificacion = a.Justificacion
            }).ToList();
        }

        if (evaluaciones.Count == 0)
        {
            evaluaciones = analisis.Asignaturas.Select(a =>
            {
                var motivo = ReglasHomologacion.MotivoSiNoHomologable(
                    ReglasHomologacion.NombreOrigen(
                        a.AsignaturaOrigen, a.Combinada, a.MateriasOrigen.Select(m => m.Nombre)),
                    a.AsignaturaDestino);
                return new EvaluacionAsignaturaDto
                {
                    AsignaturaId = a.Id,
                    Homologable = motivo is null &&
                                  (Normalizar(a.Decision) == DecisionAnalisis.HOMOLOGABLE ||
                                   Normalizar(a.DecisionCoordinador) == DecisionAnalisis.HOMOLOGABLE),
                    Justificacion = motivo ?? (string.IsNullOrWhiteSpace(a.ObservacionCoordinador)
                        ? a.Justificacion
                        : a.ObservacionCoordinador)
                };
            }).ToList();
        }

        var porId = analisis.Asignaturas.ToDictionary(a => a.Id);
        var filas = evaluaciones
            .Where(e => porId.ContainsKey(e.AsignaturaId))
            .OrderBy(e => e.AsignaturaId)
            .Select(e =>
            {
                var a = porId[e.AsignaturaId];
                return new FilaInformeAsignatura
                {
                    AsignaturaOrigen = a.Combinada && a.MateriasOrigen.Count > 0
                        ? string.Join(" + ", a.MateriasOrigen.Select(m => m.Nombre))
                        : a.AsignaturaOrigen,
                    AsignaturaDestino = a.AsignaturaDestino,
                    Justificacion = e.Justificacion,
                    Contenido = $"{a.PorcentajeContenido}% · {e.Justificacion}",
                    SeHomologa = e.Homologable ? "SÍ" : "NO"
                };
            })
            .ToList();

        var numero = string.IsNullOrWhiteSpace(info.NumeroProceso)
            ? $"SOL-{solicitudId:D4}"
            : info.NumeroProceso;

        return PlantillaInformeDocente.GenerarPdf(new DatosInformeDocente
        {
            NumeroProceso = numero,
            NumeroMemorando = numero,
            NombreEstudiante = CoordinadorService.NombreEstudiante(info),
            CedulaEstudiante = CoordinadorService.CedulaEstudiante(info),
            CarreraOrigen = info.CarreraOrigen,
            CarreraDestino = info.CarreraDestino,
            Asignaturas = filas,
            Docente = delegacion.NombreDocente ?? delegacion.DocenteCorreo,
            CedulaFirmante = delegacion.DocenteCedula ?? "-",
            CorreoFirmante = delegacion.DocenteCorreoInforme ?? delegacion.DocenteCorreo,
            TextoInforme = string.IsNullOrWhiteSpace(delegacion.InformeTexto)
                ? borrador?.InformeTexto
                : delegacion.InformeTexto,
            Fecha = delegacion.FechaRevision ?? delegacion.FechaSugerencia ?? DateTime.UtcNow,
            ObservacionGeneral = string.IsNullOrWhiteSpace(delegacion.InformeTexto)
                ? borrador?.ObservacionGeneral
                : null,
            OpinionIA = ExtraerOpinionIA(delegacion.InformeTexto) ?? borrador?.OpinionIA
        });
    }

    /// <summary>
    /// Ayuda del agente interno mientras se selecciona la carrera en "Nueva solicitud". No
    /// requiere solicitud registrada ni delegación: solo usa el catálogo real y la memoria ya
    /// cargada del agente. Funciona igual con fallback determinista si Ollama no responde.
    /// </summary>
    public async Task<RecomendacionIAInternaDto> ObtenerRecomendacionPorCarreraAsync(
        RecomendacionCarreraRequest request, CancellationToken ct = default)
    {
        if (request.CarreraOrigenId <= 0 || request.CarreraDestinoId <= 0)
            throw new InformeException(400, "Seleccione la carrera de origen y la de destino.");

        if (request.CarreraOrigenId == request.CarreraDestinoId)
            throw new InformeException(400, "La carrera de origen y destino no pueden ser iguales.");

        var respuesta = new RecomendacionIAInternaDto();
        var catalogoOrigen = await _solicitudes.ObtenerCatalogoAsync(request.CarreraOrigenId, ct);
        var catalogoDestino = await _solicitudes.ObtenerCatalogoAsync(request.CarreraDestinoId, ct);
        respuesta.HuellaMemoria = await _solicitudes.ObtenerHuellaMemoriaAsync(ct) ?? string.Empty;

        var ids = request.AsignaturasOrigenIds?
            .Where(i => i > 0)
            .ToHashSet() ?? new HashSet<int>();
        var origen = (ids.Count > 0
                ? catalogoOrigen.Where(c => ids.Contains(c.AsignaturaId))
                : catalogoOrigen)
            .OrderBy(c => c.Nivel)
            .ThenBy(c => c.Nombre)
            .ToList();

        if (origen.Count == 0)
            throw new InformeException(404, "La carrera de origen no tiene asignaturas para recomendar.");
        if (catalogoDestino.Count == 0)
            throw new InformeException(404, "La carrera de destino no tiene asignaturas en el catálogo.");

        var baseRecs = origen
            .Select(c => RecomendacionPorCarreraFallback(c, catalogoDestino))
            .ToList();

        // Al elegir carrera aún no hay materias marcadas: responder de inmediato con el catálogo
        // real cargado. El análisis con Ollama se reserva para cuando se seleccionan asignaturas;
        // así la ayuda aparece sin esperar a que el modelo procese mallas completas.
        if (ids.Count == 0)
        {
            respuesta.Recomendaciones = baseRecs;
            respuesta.Disponible = true;
            respuesta.Motivo =
                "Recomendación calculada con el catálogo y la memoria del agente interno. " +
                "Seleccione asignaturas para afinarla con IA.";
            return respuesta;
        }

        var origenTexto = string.Join(Environment.NewLine, origen.Select(c =>
            $"id={c.AsignaturaId}; materia={c.Nombre}; nivel={c.Nivel}; área={c.Area}; " +
            $"créditos={c.Creditos}; horas={c.Horas}; contenido={c.Contenido}"));
        var destinoTexto = string.Join(Environment.NewLine, catalogoDestino.Select(c =>
            $"id={c.AsignaturaId}; materia={c.Nombre}; nivel={c.Nivel}; área={c.Area}; " +
            $"créditos={c.Creditos}; horas={c.Horas}; contenido={c.Contenido}"));

        var prompt =
            "Eres el agente interno de homologación de la Universidad Técnica de Ambato. " +
            "Usa ÚNICAMENTE los datos de texto que se te dan (mallas, planes analíticos, " +
            "créditos, horas y áreas). No leas archivos ni URLs y no inventes datos. " +
            "Para cada id de materia de origen, recomienda la materia de destino más afín y " +
            "diga si se puede homologar. Responde solo un JSON válido con la forma " +
            "{\"recomendaciones\":[{\"asignaturaOrigenId\":1,\"asignaturaDestinoId\":23," +
            "\"homologable\":true,\"porcentajeContenido\":85,\"justificacion\":\"...\"}]}." +
            Environment.NewLine +
            "MATERIAS DE ORIGEN:" + Environment.NewLine + origenTexto + Environment.NewLine +
            "CATÁLOGO DESTINO:" + Environment.NewLine + destinoTexto;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(8));

        try
        {
            var json = await _ollama.GenerarRespuestaAsync(prompt, cts.Token, 512);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("recomendaciones", out var items) &&
                items.ValueKind == JsonValueKind.Array)
            {
                var porId = baseRecs.ToDictionary(r => r.AsignaturaId);
                var idsValidos = catalogoDestino.Select(c => c.AsignaturaId).ToHashSet();

                foreach (var item in items.EnumerateArray())
                {
                    if (!item.TryGetProperty("asignaturaOrigenId", out var idOrigen) ||
                        !idOrigen.TryGetInt32(out var origenId) ||
                        !porId.TryGetValue(origenId, out var recomendacion))
                        continue;

                    if (item.TryGetProperty("asignaturaDestinoId", out var idDestino) &&
                        idDestino.TryGetInt32(out var destinoId) &&
                        idsValidos.Contains(destinoId))
                    {
                        var elegida = catalogoDestino.First(c => c.AsignaturaId == destinoId);
                        recomendacion.AsignaturaDestinoId = elegida.AsignaturaId;
                        recomendacion.AsignaturaDestino = elegida.Nombre;
                        recomendacion.CreditosDestino = elegida.Creditos;
                        recomendacion.HorasDestino = elegida.Horas;
                    }

                    if (item.TryGetProperty("homologable", out var homologable) &&
                        homologable.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        recomendacion.Homologable = homologable.GetBoolean();

                    if (item.TryGetProperty("porcentajeContenido", out var porcentaje) &&
                        porcentaje.TryGetInt32(out var valor))
                        recomendacion.PorcentajeContenido = Math.Clamp(valor, 0, 100);

                    if (item.TryGetProperty("justificacion", out var justificacion) &&
                        justificacion.ValueKind == JsonValueKind.String)
                    {
                        var texto = justificacion.GetString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(texto))
                            recomendacion.Justificacion = texto.Length > 2000 ? texto[..2000] : texto;
                    }
                }

                // El modelo puede marcar "Sí" con un porcentaje bajo; la regla institucional
                // manda: sin 80% de contenido no se homologa, aunque el texto diga lo contrario.
                foreach (var recomendacion in baseRecs)
                {
                    var motivo = ReglasHomologacion.MotivoSiNoHomologable(recomendacion.AsignaturaOrigen);
                    if (motivo is not null)
                    {
                        recomendacion.PorcentajeContenido = 0;
                        recomendacion.Justificacion = motivo;
                    }
                    recomendacion.Homologable =
                        motivo is null &&
                        recomendacion.PorcentajeContenido >= UmbralContenidoHomologacion;
                }

                respuesta.Recomendaciones = baseRecs;
                respuesta.Disponible = true;
                return respuesta;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Ollama tardó demasiado al recomendar por carrera; se usa el catálogo cargado.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo obtener la recomendación por carrera; se usa el catálogo cargado.");
        }

        respuesta.Recomendaciones = baseRecs;
        respuesta.Disponible = true;
        respuesta.Motivo = "Recomendación calculada con el catálogo y la memoria del agente interno.";
        return respuesta;
    }

    /// <summary>Base determinista de la recomendación por carrera, también usada como fallback.</summary>
    private static RecomendacionMateriaDto RecomendacionPorCarreraFallback(
        AsignaturaCatalogoInternaDto origen, IReadOnlyList<AsignaturaCatalogoInternaDto> destino)
    {
        var mismoNombre = destino.FirstOrDefault(c => string.Equals(
            c.Nombre.Trim(), origen.Nombre.Trim(), StringComparison.OrdinalIgnoreCase));
        var mismoArea = destino.FirstOrDefault(c =>
            !string.IsNullOrWhiteSpace(origen.Area) &&
            string.Equals(c.Area.Trim(), origen.Area.Trim(), StringComparison.OrdinalIgnoreCase));
        var mismaCarga = destino.FirstOrDefault(c =>
            c.Creditos == origen.Creditos && c.Horas == origen.Horas);
        var elegida = mismoNombre ?? mismoArea ?? mismaCarga;

        var porcentaje = elegida is null ? 0 : mismoNombre is not null ? 95 : mismoArea is not null ? 75 : 60;
        var motivoExclusion = ReglasHomologacion.MotivoSiNoHomologable(origen.Nombre);
        var homologable = motivoExclusion is null &&
                          elegida is not null &&
                          porcentaje >= UmbralContenidoHomologacion;
        var justificacion = motivoExclusion ?? (elegida is null
            ? "No se identifica una equivalencia suficiente en la malla destino con los datos cargados."
            : mismoNombre is not null
                ? $"Coincidencia de nombre y contenido con {elegida.Nombre}."
                : mismoArea is not null
                    ? $"Coincidencia de área ({origen.Area}) con {elegida.Nombre}, pero el contenido estimado es {porcentaje}%: no alcanza el umbral del {UmbralContenidoHomologacion}%."
                    : $"Coincidencia de créditos y horas con {elegida.Nombre}, pero el contenido estimado es {porcentaje}%: no alcanza el umbral del {UmbralContenidoHomologacion}%.");

        return new RecomendacionMateriaDto
        {
            AsignaturaId = origen.AsignaturaId,
            AsignaturaOrigen = origen.Nombre,
            AsignaturaDestinoId = elegida?.AsignaturaId,
            AsignaturaDestino = elegida?.Nombre ?? "Sin equivalente en la malla destino",
            Homologable = homologable,
            PorcentajeContenido = motivoExclusion is null ? porcentaje : 0,
            Justificacion = justificacion,
            CreditosOrigen = origen.Creditos,
            CreditosDestino = elegida?.Creditos ?? 0,
            HorasOrigen = origen.Horas,
            HorasDestino = elegida?.Horas ?? 0
        };
    }

    /// <summary>
    /// Opción pequeña "IA me recomienda qué materias homologar". Solo aplica a solicitudes
    /// INTERNO: el agente trabaja con el catálogo, planes analíticos, créditos y horas ya
    /// cargados en la memoria, sin leer discos ni URLs.
    /// </summary>
    public async Task<RecomendacionIAInternaDto> ObtenerRecomendacionInternaAsync(
        int solicitudId, string? correo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(correo))
            throw new InformeException(400, "No se pudo identificar al docente.");

        _ = await ObtenerDelegacionAsync(solicitudId, correo, ct)
            ?? throw new InformeException(404, "Usted no tiene una delegación en esta solicitud.");

        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct)
                   ?? throw new InformeException(404, "Solicitud no encontrada.");

        var respuesta = new RecomendacionIAInternaDto { SolicitudId = solicitudId };
        if (Normalizar(info.Modulo) != "INTERNO")
        {
            respuesta.Motivo =
                "Esta opción solo está disponible en solicitudes internas: el agente interno " +
                "tiene cargadas las mallas, planes analíticos, créditos y horas de las 5 carreras.";
            return respuesta;
        }

        var analisis = await _db.AnalisisSolicitudes
            .AsNoTracking()
            .Include(a => a.Asignaturas)
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct)
            ?? throw new InformeException(404, "La solicitud no tiene un análisis para recomendar.");

        var catalogo = await _solicitudes.ObtenerCatalogoAsync(info.CarreraDestinoId, ct);
        respuesta.HuellaMemoria = await _solicitudes.ObtenerHuellaMemoriaAsync(ct) ?? string.Empty;

        var baseRecs = analisis.Asignaturas
            .OrderBy(a => a.Id)
            .Select(a => RecomendacionFallback(a, catalogo))
            .ToList();

        var origen = string.Join(Environment.NewLine, analisis.Asignaturas
            .OrderBy(a => a.Id)
            .Select(a => $"id={a.Id}; materia={a.AsignaturaOrigen}; área={a.AreaOrigen}; " +
                         $"porcentaje de contenido={a.PorcentajeContenido}%; " +
                         $"equivalencia previa={a.AsignaturaDestino}"));

        // Se envía todo el catálogo de la carrera destino como texto: es la memoria del agente.
        var destino = string.Join(Environment.NewLine, catalogo.Select(c =>
            $"id={c.AsignaturaId}; materia={c.Nombre}; nivel={c.Nivel}; área={c.Area}; " +
            $"créditos={c.Creditos}; horas={c.Horas}; contenido={c.Contenido}"));

        var prompt =
            "Eres el agente interno de homologación de la Universidad Técnica de Ambato. " +
            "Usa ÚNICAMENTE los datos de texto que se te dan (malla destino, planes analíticos, " +
            "créditos, horas y áreas de las 5 carreras). No leas archivos ni URLs y no inventes datos. " +
            "Para cada id de materia de origen, recomienda la materia de destino más afín y diga si " +
            "se puede homologar. Responde solo un JSON válido con la forma " +
            "{\"recomendaciones\":[{\"asignaturaOrigenId\":1,\"asignaturaDestinoId\":23," +
            "\"homologable\":true,\"porcentajeContenido\":85,\"justificacion\":\"...\"}]}." +
            Environment.NewLine +
            $"Carrera de origen: {info.CarreraOrigen}{Environment.NewLine}" +
            $"Carrera de destino: {info.CarreraDestino}{Environment.NewLine}{Environment.NewLine}" +
            $"MATERIAS DE ORIGEN:{Environment.NewLine}{origen}{Environment.NewLine}{Environment.NewLine}" +
            $"CATÁLOGO DESTINO:{Environment.NewLine}{destino}";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(8));

        try
        {
            var json = await _ollama.GenerarRespuestaAsync(prompt, cts.Token, 512);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("recomendaciones", out var items) &&
                items.ValueKind == JsonValueKind.Array)
            {
                var porId = baseRecs.ToDictionary(r => r.AsignaturaId);
                var idsValidos = analisis.Asignaturas.Select(a => a.Id).ToHashSet();

                foreach (var item in items.EnumerateArray())
                {
                    if (!item.TryGetProperty("asignaturaOrigenId", out var idOrigen) ||
                        !idOrigen.TryGetInt32(out var origenId) ||
                        !idsValidos.Contains(origenId) ||
                        !porId.TryGetValue(origenId, out var recomendacion))
                        continue;

                    if (item.TryGetProperty("asignaturaDestinoId", out var idDestino) &&
                        idDestino.TryGetInt32(out var destinoId))
                    {
                        var destinoElegido = catalogo.FirstOrDefault(c => c.AsignaturaId == destinoId);
                        if (destinoElegido is not null)
                        {
                            recomendacion.AsignaturaDestinoId = destinoElegido.AsignaturaId;
                            recomendacion.AsignaturaDestino = destinoElegido.Nombre;
                            recomendacion.CreditosDestino = destinoElegido.Creditos;
                            recomendacion.HorasDestino = destinoElegido.Horas;
                        }
                    }

                    if (item.TryGetProperty("homologable", out var homologable) &&
                        homologable.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        recomendacion.Homologable = homologable.GetBoolean();

                    if (item.TryGetProperty("porcentajeContenido", out var porcentaje) &&
                        porcentaje.TryGetInt32(out var valor))
                        recomendacion.PorcentajeContenido = Math.Clamp(valor, 0, 100);

                    if (item.TryGetProperty("justificacion", out var justificacion) &&
                        justificacion.ValueKind == JsonValueKind.String)
                    {
                        var texto = justificacion.GetString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(texto))
                            recomendacion.Justificacion = texto.Length > 2000 ? texto[..2000] : texto;
                    }
                }

                foreach (var recomendacion in baseRecs)
                {
                    var motivo = ReglasHomologacion.MotivoSiNoHomologable(recomendacion.AsignaturaOrigen);
                    if (motivo is not null)
                    {
                        recomendacion.PorcentajeContenido = 0;
                        recomendacion.Justificacion = motivo;
                    }
                    recomendacion.Homologable =
                        motivo is null &&
                        recomendacion.PorcentajeContenido >= UmbralContenidoHomologacion;
                }

                respuesta.Recomendaciones = baseRecs;
                respuesta.Disponible = true;
                return respuesta;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Ollama tardó demasiado en recomendar; se usa el catálogo cargado.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo obtener la recomendación de IA; se usa el catálogo cargado.");
        }

        respuesta.Recomendaciones = baseRecs;
        respuesta.Disponible = true;
        respuesta.Motivo = string.IsNullOrWhiteSpace(respuesta.Motivo)
            ? "Recomendación calculada con el catálogo y la memoria del agente interno."
            : respuesta.Motivo;
        return respuesta;
    }

    /// <summary>Recomendación determinista usada como base y cuando Ollama no responde.</summary>
    private static RecomendacionMateriaDto RecomendacionFallback(
        AnalisisAsignatura a, IReadOnlyList<AsignaturaCatalogoInternaDto> catalogo)
    {
        var destino = catalogo.FirstOrDefault(c => c.AsignaturaId == a.AsignaturaDestinoId)
                      ?? catalogo.FirstOrDefault(c => string.Equals(
                          c.Nombre.Trim(), a.AsignaturaDestino.Trim(), StringComparison.OrdinalIgnoreCase));

        var motivoExclusion = ReglasHomologacion.MotivoSiNoHomologable(
            a.AsignaturaOrigen, a.AsignaturaDestino);
        var homologable = motivoExclusion is null &&
                          (Normalizar(a.Decision) == DecisionAnalisis.HOMOLOGABLE ||
                           Normalizar(a.DecisionCoordinador) == DecisionAnalisis.HOMOLOGABLE);

        var porcentaje = a.PorcentajeContenido;
        if (porcentaje == 0 && destino is not null &&
            Normalizar(a.AreaOrigen) == Normalizar(destino.Area))
            porcentaje = 70;

        var justificacion = motivoExclusion ?? (string.IsNullOrWhiteSpace(a.Justificacion)
            ? homologable
                ? $"Coincidencia de contenidos y área con {destino?.Nombre ?? a.AsignaturaDestino}."
                : "No se identifica una equivalencia suficiente en la malla destino."
            : a.Justificacion);

        return new RecomendacionMateriaDto
        {
            AsignaturaId = a.Id,
            AsignaturaOrigen = a.Combinada && a.MateriasOrigen.Count > 0
                ? string.Join(" + ", a.MateriasOrigen.Select(m => m.Nombre))
                : a.AsignaturaOrigen,
            AsignaturaDestinoId = destino?.AsignaturaId,
            AsignaturaDestino = destino?.Nombre ?? a.AsignaturaDestino,
            Homologable = homologable,
            PorcentajeContenido = motivoExclusion is null ? porcentaje : 0,
            Justificacion = justificacion,
            HorasDestino = destino?.Horas ?? 0,
            CreditosDestino = destino?.Creditos ?? 0
        };
    }

    /// <summary>
    /// Pide a Ollama un párrafo corto de apoyo técnico. Si el modelo no responde,
    /// el informe se genera igual con la plantilla determinista.
    /// </summary>
    private async Task<string?> GenerarOpinionIAAsync(
        string numeroProceso,
        string carreraOrigen,
        string carreraDestino,
        IReadOnlyList<FilaInformeAsignatura> filas,
        CancellationToken ct)
    {
        var detalle = filas.Count == 0
            ? "No se identificaron asignaturas homologables."
            : string.Join(Environment.NewLine, filas.Select((f, i) =>
                $"{i + 1}. Origen: {f.AsignaturaOrigen}. Destino: {f.AsignaturaDestino}. " +
                $"Justificación: {f.Justificacion}. Contenido: {f.Contenido}"));

        var prompt =
            "Eres un analista de homologación académica de la Universidad Técnica de Ambato. " +
            "Con base únicamente en los datos siguientes, redacta UN párrafo de máximo 90 palabras " +
            "que refuerce técnicamente la sugerencia del docente. No inventes normas, datos ni " +
            "asignaturas. Usa obligatoriamente la expresión \"se sugiere\". " +
            "Responde únicamente un JSON válido con la forma {\"opinion\":\"...\"}." +
            Environment.NewLine +
            $"Proceso: {numeroProceso}{Environment.NewLine}" +
            $"Carrera de origen: {carreraOrigen}{Environment.NewLine}" +
            $"Carrera de destino: {carreraDestino}{Environment.NewLine}" +
            detalle;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(8));

        try
        {
            // 128 tokens bastan para el párrafo de 90 palabras y reducen el tiempo en CPU.
            var respuesta = await _ollama.GenerarRespuestaAsync(prompt, cts.Token, 128);
            using var doc = JsonDocument.Parse(respuesta);
            if (doc.RootElement.TryGetProperty("opinion", out var opinion) &&
                opinion.ValueKind == JsonValueKind.String)
            {
                var texto = opinion.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(texto))
                    return texto;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Ollama tardó demasiado; el informe docente se genera sin opinión IA.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo obtener la opinión de IA; se usa la plantilla sin ese párrafo.");
        }

        return null;
    }

    /// <summary>Recupera el párrafo de IA del texto guardado para que el PDF coincida con la vista.</summary>
    private static string? ExtraerOpinionIA(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;

        const string etiqueta = PlantillaInformeDocente.EtiquetaOpinionIA;
        var inicio = texto.IndexOf(etiqueta, StringComparison.Ordinal);
        if (inicio < 0) return null;

        inicio += etiqueta.Length;
        var fin = texto.IndexOf(Environment.NewLine + Environment.NewLine, inicio, StringComparison.Ordinal);
        var valor = fin < 0 ? texto[inicio..] : texto[inicio..fin];
        return valor.Trim();
    }

    private async Task<DelegacionDocente?> ObtenerDelegacionAsync(
        int solicitudId, string? correo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(correo))
            return null;

        var filtro = correo.Trim().ToUpperInvariant();
        return await _db.DelegacionesDocente
            .FirstOrDefaultAsync(d => d.SolicitudId == solicitudId &&
                                      d.DocenteCorreo.ToUpper() == filtro, ct);
    }

    /// <summary>
    /// Guarda el nombre, la cédula y el correo que el Docente corrige en su informe. Si no
    /// llegan datos nuevos se conservan los anteriores; el correo de acceso nunca se usa como
    /// firma cuando el Docente ya indicó uno propio.
    /// </summary>
    private static void AplicarDatosDocente(
        DelegacionDocente delegacion, DatosFirmaDocenteRequest? datos, string correo)
    {
        if (!string.IsNullOrWhiteSpace(datos?.NombreDocente))
            delegacion.NombreDocente = datos.NombreDocente.Trim();

        if (!string.IsNullOrWhiteSpace(datos?.DocenteCedula))
            delegacion.DocenteCedula = datos.DocenteCedula.Trim();
        else
            delegacion.DocenteCedula ??= "-";

        delegacion.DocenteCorreoInforme = string.IsNullOrWhiteSpace(datos?.DocenteCorreoInforme)
            ? delegacion.DocenteCorreoInforme ?? correo
            : datos.DocenteCorreoInforme.Trim();

        delegacion.NombreDocente ??= correo;
    }

    /// <summary>
    /// Bloquea cambios cuando ya hay informe sugerido enviado, ya sea por este docente o por
    /// otro de los asignados. El primero que envía deja constancia; el resto solo consulta.
    /// </summary>
    private async Task ValidarPuedeRevisarAsync(
        DelegacionDocente delegacion, int solicitudId, string? correo, CancellationToken ct)
    {
        var otra = await _db.DelegacionesDocente
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.SolicitudId == solicitudId &&
                                      d.Id != delegacion.Id &&
                                      d.Estado == EstadoDelegacionDocente.REVISADO, ct);
        if (otra is not null)
        {
            throw new InformeException(409,
                $"Esta solicitud ya fue revisada por {otra.NombreDocente ?? otra.DocenteCorreo}.");
        }

        if (delegacion.Estado == EstadoDelegacionDocente.REVISADO)
            throw new InformeException(409, "Usted ya registró su revisión de esta solicitud.");

        _ = correo;
    }

    /// <summary>Evaluación inicial con lo que propuso el agente o ajustó el Coordinador.</summary>
    private static List<EvaluacionAsignaturaDto> EvaluacionesPropuestas(DetalleSolicitudDto detalle) =>
        detalle.Asignaturas.Select(a =>
        {
            var decision = !string.IsNullOrWhiteSpace(a.DecisionCoordinador)
                ? a.DecisionCoordinador
                : a.Propuesta.Decision;
            var motivo = ReglasHomologacion.MotivoSiNoHomologable(
                ReglasHomologacion.NombreOrigen(
                    a.AsignaturaOrigen, a.Combinada, a.MateriasOrigen.Select(m => m.Nombre)),
                a.AsignaturaDestino);

            return new EvaluacionAsignaturaDto
            {
                AsignaturaId = a.Id,
                Homologable = motivo is null && Normalizar(decision) == DecisionAnalisis.HOMOLOGABLE,
                Justificacion = motivo ?? (!string.IsNullOrWhiteSpace(a.ObservacionCoordinador)
                    ? a.ObservacionCoordinador
                    : a.Propuesta.Justificacion)
            };
        }).ToList();

    private static SugerenciaDocenteDto? DeserializarSugerencia(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<SugerenciaDocenteDto>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Aplica la regla institucional de materias no homologables antes de validar: las materias excluidas nunca
    /// pueden enviarse como "Homologo", aunque vengan marcadas desde la pantalla o de un
    /// borrador antiguo. Así tampoco se exige justificación para una decisión forzada.
    /// </summary>
    private static void AplicarReglasNoHomologables(
        AnalisisSolicitud analisis, IEnumerable<EvaluacionAsignaturaDto> evaluaciones)
    {
        var porId = analisis.Asignaturas.ToDictionary(a => a.Id);
        foreach (var evaluacion in evaluaciones)
        {
            if (!porId.TryGetValue(evaluacion.AsignaturaId, out var asignatura))
                continue;

            var motivo = ReglasHomologacion.MotivoSiNoHomologable(
                ReglasHomologacion.NombreOrigen(
                    asignatura.AsignaturaOrigen, asignatura.Combinada,
                    asignatura.MateriasOrigen.Select(m => m.Nombre)),
                asignatura.AsignaturaDestino);

            if (motivo is null)
                continue;

            evaluacion.Homologable = false;
            evaluacion.Justificacion = motivo;
        }
    }

    private static void ValidarEvaluaciones(
        AnalisisSolicitud analisis, List<EvaluacionAsignaturaDto> evaluaciones)
    {
        var ids = analisis.Asignaturas.Select(a => a.Id).ToHashSet();
        var vistos = new HashSet<int>();

        foreach (var evaluacion in evaluaciones)
        {
            if (!ids.Contains(evaluacion.AsignaturaId))
                throw new InformeException(400,
                    $"La asignatura {evaluacion.AsignaturaId} no pertenece a esta solicitud.");

            if (!vistos.Add(evaluacion.AsignaturaId))
                throw new InformeException(400,
                    $"La asignatura {evaluacion.AsignaturaId} está repetida en la evaluación.");

            if (evaluacion.Homologable && string.IsNullOrWhiteSpace(evaluacion.Justificacion))
                throw new InformeException(400,
                    $"La justificación de la asignatura {evaluacion.AsignaturaId} es obligatoria.");

            if ((evaluacion.Justificacion?.Length ?? 0) > 2000)
                throw new InformeException(400,
                    $"La justificación de la asignatura {evaluacion.AsignaturaId} supera los 2000 caracteres.");
        }
    }

    private static string Normalizar(string? decision) =>
        (decision ?? string.Empty).Trim().ToUpperInvariant();
}
