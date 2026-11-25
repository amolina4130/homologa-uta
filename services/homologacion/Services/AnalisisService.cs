using HomologaUTA.Homologacion.Agents;
using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Eventos;
using HomologaUTA.Homologacion.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Services;

/// <summary>El análisis no se puede generar o consultar; trae el código HTTP que corresponde.</summary>
public class AnalisisSolicitudException : Exception
{
    public int StatusCode { get; }

    public AnalisisSolicitudException(int statusCode, string mensaje) : base(mensaje)
    {
        StatusCode = statusCode;
    }
}

/// <summary>
/// Analiza las asignaturas de una solicitud con el agente interno, guarda la decisión, los porcentajes
/// y la justificación por asignatura, deja la solicitud en PENDIENTE_REVISION y publica AnalisisCompletado.
/// Cuando el estudiante tiene materias aprobadas extraídas del récord, antes de llamar al agente se
/// hace la preselección por área y nivel (HU-21) y se permite la combinación de dos materias (HU-22).
/// </summary>
public class AnalisisService
{
    private const int MaxJustificacion = 2000;
    private const int MaxNombre = 200;

    private readonly HomologacionDbContext _db;
    private readonly AgenteInterno _agente;
    private readonly DecisionHomologacion _decision;
    private readonly PreseleccionService _preseleccion;
    private readonly IAnalisisEventPublisher _publisher;
    private readonly SolicitudesClient _solicitudes;
    private readonly ILogger<AnalisisService> _logger;

    public AnalisisService(
        HomologacionDbContext db,
        AgenteInterno agente,
        DecisionHomologacion decision,
        PreseleccionService preseleccion,
        IAnalisisEventPublisher publisher,
        SolicitudesClient solicitudes,
        ILogger<AnalisisService> logger)
    {
        _db = db;
        _agente = agente;
        _decision = decision;
        _preseleccion = preseleccion;
        _publisher = publisher;
        _solicitudes = solicitudes;
        _logger = logger;
    }

    /// <summary>
    /// Analiza la solicitud. Si no se envían asignaturas, las toma del servicio de Solicitudes
    /// (asignaturas reales seleccionadas por el estudiante) o de la preselección si ya existen
    /// materias aprobadas del récord.
    /// </summary>
    public async Task<ResultadoAnalisisDto> AnalizarSolicitudAsync(
        int solicitudId,
        IReadOnlyList<AsignaturaAnalisisDto>? asignaturas = null,
        CancellationToken ct = default)
    {
        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct);
        if (info is null)
            throw new AnalisisSolicitudException(404, "Solicitud no encontrada.");

        // Un análisis nuevo reemplaza las asignaturas y devuelve la solicitud a revisión:
        // no se permite cuando ya hay informe final o resolución, porque los borraría.
        var estadoActual = await _db.AnalisisSolicitudes
            .AsNoTracking()
            .Where(a => a.SolicitudId == solicitudId)
            .Select(a => a.Estado)
            .FirstOrDefaultAsync(ct);
        if (estadoActual is EstadoAnalisis.INFORME_EMITIDO or EstadoAnalisis.RESUELTA)
            throw new AnalisisSolicitudException(409,
                "La solicitud ya tiene informe final emitido; no se puede volver a analizar.");

        // La sugerencia de la IA se conserva tal como la recibió el docente: una vez delegada,
        // volver a analizar la reemplazaría y se perdería la trazabilidad.
        // El docente asignado genera (o repite) el análisis mientras revisa. Cuando ya envió su
        // informe, la sugerencia de la IA queda fija para conservar la trazabilidad.
        if (await _db.DelegacionesDocente.AnyAsync(d => d.SolicitudId == solicitudId &&
                                                        d.Estado == EstadoDelegacionDocente.REVISADO, ct))
            throw new AnalisisSolicitudException(409,
                "El docente ya envió su informe técnico; la sugerencia de la IA se conserva y no se vuelve a generar.");

        // Un análisis nuevo cambia las filas: el borrador del docente apuntaba a las anteriores.
        var borradores = await _db.DelegacionesDocente
            .Where(d => d.SolicitudId == solicitudId && d.SugerenciaJson != null)
            .ToListAsync(ct);
        foreach (var b in borradores) b.SugerenciaJson = null;

        EntradasPreseleccion? preseleccion = null;
        List<AsignaturaAnalisisDto> entradas;

        if (asignaturas is { Count: > 0 })
        {
            entradas = asignaturas.ToList();
        }
        else
        {
            try
            {
                preseleccion = await _preseleccion.ConstruirEntradasAsync(solicitudId, ct);
            }
            catch (MemoriaNoDisponibleException ex)
            {
                throw new AnalisisSolicitudException(502, ex.Message);
            }

            // Solo son candidatas las materias APROBADAS según el récord académico. Sin récord
            // extraído no hay de dónde partir: no se analiza una selección hecha a mano.
            if (!preseleccion.UsaMateriasAprobadas)
                throw new AnalisisSolicitudException(409,
                    "Primero suba el récord académico y confirme las materias aprobadas: " +
                    "la IA solo sugiere homologar materias que el estudiante aprobó.");

            entradas = preseleccion.EntradasIndividuales.Count > 0
                ? preseleccion.EntradasIndividuales.ToList()
                : preseleccion.Entradas.ToList();

            // Si la mayoría de las materias del récord no existen en la malla de origen, la carrera
            // de origen registrada no es la del récord: compararía sin sílabos y sugeriría al azar.
            var materiasOrigen = entradas.Where(e => !e.EsCombinacion)
                .GroupBy(e => e.AsignaturaOrigen, StringComparer.OrdinalIgnoreCase).ToList();
            var enMalla = materiasOrigen.Count(g => g.Any(e => e.ContenidoOrigenDeSilabo));
            if (materiasOrigen.Count >= 4 && enMalla * 2 < materiasOrigen.Count)
                throw new AnalisisSolicitudException(409,
                    $"Solo {enMalla} de {materiasOrigen.Count} materias del récord están en la malla de " +
                    $"{info.CarreraOrigen}. Verifique que la carrera de origen de la solicitud sea la del récord académico.");
        }

        var sinCandidata = preseleccion?.SinCandidata?.ToList() ?? new List<MateriaSinCandidataDto>();

        // Solicitudes marca las asignaturas sin equivalencia en su catálogo con un centinela.
        // Antes de llamar al agente se intenta una candidata real en la memoria destino; las que
        // no la tengan quedan en revisión como "Sin equivalente probable".
        foreach (var pendiente in await _preseleccion.ResolverSinEquivalenteAsync(info, entradas, ct))
        {
            if (!sinCandidata.Any(s => string.Equals(
                    s.MateriaOrigen, pendiente.MateriaOrigen, StringComparison.OrdinalIgnoreCase)))
            {
                sinCandidata.Add(pendiente);
            }
        }

        if (entradas.Count == 0 && sinCandidata.Count == 0)
            throw new AnalisisSolicitudException(400, "La solicitud no tiene asignaturas para analizar.");

        // 1. Analizar cada asignatura candidata (agente + decisión calculada por código).
        // Las que no tienen candidata no se envían al agente: quedan en revisión con el motivo.
        var filas = new List<AnalisisAsignatura>();
        foreach (var a in entradas)
        {
            // Sin sílabo de origen no hay base para elegir destinos: solo se sugiere el de nombre
            // equivalente (para que el docente lo verifique); no se proponen destinos al azar.
            if (!a.ContenidoOrigenDeSilabo && !MismoNombre(a.AsignaturaOrigen, a.AsignaturaDestino))
                continue;
            var resultadoAgente = await AnalizarConAgenteAsync(a);
            filas.Add(ConstruirFila(a, resultadoAgente));
        }

        filas = DescartarCombinacionesInnecesarias(filas);
        filas = DepurarCandidatas(filas);

        foreach (var materia in sinCandidata)
        {
            filas.Add(new AnalisisAsignatura
            {
                AsignaturaOrigen = Recortar(materia.MateriaOrigen, MaxNombre),
                AsignaturaDestino = "Sin equivalente probable",
                ReglaAplicada = "Sin asignatura candidata del mismo campo y nivel en la malla de destino.",
                Decision = DecisionAnalisis.REQUIERE_REVISION,
                Justificacion = Recortar(
                    string.IsNullOrWhiteSpace(materia.Motivo)
                        ? "No hay una asignatura de destino candidata para esta materia."
                        : materia.Motivo,
                    MaxJustificacion),
                RequiereRevision = true
            });
        }

        // 2. Guardar: si la solicitud ya se había analizado, se reemplaza su resultado
        var fecha = DateTime.UtcNow;
        var analisis = await _db.AnalisisSolicitudes
            .Include(x => x.Asignaturas)
                .ThenInclude(a => a.MateriasOrigen)
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
        analisis.CoordinadorCorreo = null;
        analisis.FechaInforme = null;
        foreach (var fila in filas)
            analisis.Asignaturas.Add(fila);

        await _db.SaveChangesAsync(ct);

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

        // 4. Mover la solicitud a PENDIENTE_REVISION para que aparezca al Coordinador.
        var estadoActualizado = false;
        try
        {
            estadoActualizado = await _solicitudes.CambiarEstadoAsync(
                solicitudId, EstadoSolicitudHomologacion.PENDIENTE_REVISION, "sistema@homologauta", ct);
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            _logger.LogError(ex, "El análisis de {SolicitudId} se guardó, pero no se pudo actualizar su estado", solicitudId);
        }

        return new ResultadoAnalisisDto
        {
            SolicitudId = solicitudId,
            Estado = EstadoAnalisis.PENDIENTE_REVISION,
            FechaAnalisis = fecha,
            EventoPublicado = publicado,
            EstadoSolicitudActualizado = estadoActualizado,
            Asignaturas = filas.Select(f => new ResultadoAnalisisAsignaturaDto
            {
                AsignaturaOrigen = f.AsignaturaOrigen,
                AsignaturaDestino = f.AsignaturaDestino,
                AsignaturaDestinoId = f.AsignaturaDestinoId,
                Combinada = f.Combinada,
                MateriasOrigen = f.MateriasOrigen
                    .Select(m => new MateriaOrigenDto { Nombre = m.Nombre, Nota = m.Nota })
                    .ToList(),
                PorcentajeContenido = f.PorcentajeContenido,
                PorcentajeProfundidad = f.PorcentajeProfundidad,
                PorcentajeHoras = f.PorcentajeHoras,
                Decision = f.Decision,
                Justificacion = f.Justificacion,
                RequiereRevision = f.RequiereRevision
            }).ToList()
        };
    }

    /// <summary>Resultado que ve el estudiante con la propuesta del agente y, si existe, el informe del Coordinador.</summary>
    public async Task<ResultadoSolicitudDto> ObtenerResultadoSolicitudAsync(
        int solicitudId, CancellationToken ct = default)
    {
        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct);
        if (info is null)
            throw new AnalisisSolicitudException(404, "Solicitud no encontrada.");

        var analisis = await _db.AnalisisSolicitudes
            .AsNoTracking()
            .Include(a => a.Asignaturas)
                .ThenInclude(a => a.MateriasOrigen)
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct);

        if (analisis is null)
            throw new AnalisisSolicitudException(404, "La solicitud todavía no tiene un análisis generado.");

        return new ResultadoSolicitudDto
        {
            SolicitudId = solicitudId,
            Numero = string.IsNullOrWhiteSpace(info.NumeroProceso)
                ? $"SOL-{solicitudId:D4}"
                : info.NumeroProceso,
            Tipo = info.Tipo,
            Modulo = string.Equals(info.Modulo, "EXTERNO", StringComparison.OrdinalIgnoreCase)
                ? "Agente externo"
                : "Agente interno",
            Estado = analisis.Estado,
            Estudiante = CoordinadorService.NombreEstudiante(info),
            EstudianteCedula = CoordinadorService.CedulaEstudiante(info),
            ComiteCorreo = info.EstudianteCorreo,
            FechaRegistro = info.FechaRegistro,
            CarreraOrigen = info.CarreraOrigen,
            CarreraDestino = info.CarreraDestino,
            ModuloSolicitud = info.Modulo,
            UniversidadOrigen = info.UniversidadOrigen,
            UniversidadDestino = info.UniversidadDestino,
            FechaAnalisis = analisis.FechaAnalisis,
            FechaInforme = analisis.FechaInforme,
            CoordinadorCorreo = analisis.CoordinadorCorreo,
            Asignaturas = analisis.Asignaturas
                .OrderBy(a => a.Id)
                .Select(a => new AsignaturaResultadoSolicitudDto
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
                    PorcentajeContenido = a.PorcentajeContenido,
                    PorcentajeProfundidad = a.PorcentajeProfundidad,
                    PorcentajeHoras = a.PorcentajeHoras,
                    Decision = a.Decision,
                    Justificacion = a.Justificacion,
                    RequiereRevision = a.RequiereRevision,
                    DecisionCoordinador = a.DecisionCoordinador,
                    ObservacionCoordinador = a.ObservacionCoordinador,
                    NivelOrigen = a.NivelOrigen,
                    NivelDestino = a.NivelDestino,
                    HorasOrigen = a.HorasOrigen,
                    HorasDestino = a.HorasDestino,
                    CreditosOrigen = a.CreditosOrigen,
                    CreditosDestino = a.CreditosDestino,
                    ContenidosCoincidentes = a.ContenidosCoincidentes,
                    Diferencias = a.Diferencias,
                    ReglaAplicada = a.ReglaAplicada,
                    PropuestaPor = a.PropuestaPor
                })
                .ToList()
        };
    }

    /// <summary>Convierte las asignaturas reales de la solicitud en la entrada del agente.</summary>
    public static List<AsignaturaAnalisisDto> ConstruirAsignaturas(SolicitudCompletaDto info) =>
        info.Asignaturas.Select(a => new AsignaturaAnalisisDto
        {
            AsignaturaOrigen = a.AsignaturaOrigen,
            ContenidoOrigen = a.ContenidoOrigen,
            AsignaturaDestino = a.AsignaturaDestino,
            ContenidoDestino = a.ContenidoDestino,
            AsignaturaDestinoId = a.AsignaturaDestinoId,
            AreaOrigen = a.AreaOrigen,
            AreaDestino = a.AreaDestino,
            NivelOrigen = a.NivelOrigen,
            NivelDestino = a.NivelDestino,
            HorasOrigen = a.HorasOrigen > 0 ? a.HorasOrigen : null,
            HorasDestino = a.HorasDestino > 0 ? a.HorasDestino : null,
            CreditosOrigen = a.CreditosOrigen > 0 ? a.CreditosOrigen : null,
            CreditosDestino = a.CreditosDestino > 0 ? a.CreditosDestino : null,
            EquivalenciaAplicada = a.EquivalenciaAplicada,
            MateriasOrigenPermitidas = new List<string> { a.AsignaturaOrigen }
        }).ToList();

    /// <summary>
    /// Aplica una equivalencia registrada antes de llamar al agente: si la regla existe, la
    /// asignatura queda homologable y se cita la regla. En otro caso, el agente compara contenidos.
    /// </summary>
    private async Task<ResultadoAgente> AnalizarConAgenteAsync(AsignaturaAnalisisDto entrada)
    {
        // Regla institucional de materias no homologables: estas materias no se homologan aunque exista una
        // equivalencia registrada o el contenido parezca idéntico. Se evalúan primero para
        // que ninguna otra regla pueda marcarlas como homologables.
        var motivoExclusion = ReglasHomologacion.MotivoSiNoHomologable(
                                  entrada.AsignaturaOrigen, entrada.AsignaturaDestino)
                              ?? ReglasHomologacion.MotivoPorRecord(entrada.PeriodoAprobacion, entrada.EstadoSegunRecord);
        if (motivoExclusion is not null)
        {
            return new ResultadoAgente
            {
                Contenido = 0,
                Profundidad = 0,
                Horas = 0,
                Justificacion = motivoExclusion,
                RequiereRevision = false,
                ReglaExclusion = motivoExclusion,
                MateriasOrigen = new List<string> { entrada.AsignaturaOrigen }
            };
        }

        if (!string.IsNullOrWhiteSpace(entrada.EquivalenciaAplicada))
        {
            return new ResultadoAgente
            {
                Contenido = 100,
                Profundidad = 100,
                Horas = 100,
                Justificacion = entrada.EquivalenciaAplicada.Trim(),
                RequiereRevision = false,
                MateriasOrigen = new List<string> { entrada.AsignaturaOrigen }
            };
        }

        // El análisis compara los sílabos registrados en el sistema (malla y plan analítico de
        // origen y de destino) con el método de cálculo definido en ComparadorSilabos. Termina
        // en milisegundos y no depende de Ollama; la redacción con IA se pide en la sugerencia
        // del Docente.
        var resultado = AgenteInterno.CompararSilabos(
            entrada.AsignaturaOrigen,
            entrada.ContenidoOrigenDeSilabo ? entrada.ContenidoOrigen : null,
            entrada.ResultadosOrigen,
            entrada.HorasOrigen,
            entrada.AsignaturaDestino,
            entrada.ContenidoDestino,
            entrada.ResultadosDestino,
            entrada.HorasDestino);

        if (!entrada.ContenidoOrigenDeSilabo)
        {
            // Sin sílabo de origen no hay con qué comparar: no se inventa el contenido.
            resultado.RequiereRevision = true;
            resultado.Justificacion =
                $"No hay sílabo registrado para {entrada.AsignaturaOrigen} en la malla de origen; " +
                "el docente debe revisar la equivalencia con la documentación del estudiante.";
        }
        return resultado;
    }

    private AnalisisAsignatura ConstruirFila(AsignaturaAnalisisDto entrada, ResultadoAgente agente)
    {
        string decision;
        string justificacion;
        string regla;

        if (agente.ReglaExclusion is not null)
        {
            decision = DecisionAnalisis.NO_HOMOLOGABLE;
            justificacion = agente.ReglaExclusion;
            regla = agente.ReglaExclusion;
        }
        else if (!string.IsNullOrWhiteSpace(entrada.EquivalenciaAplicada))
        {
            decision = DecisionAnalisis.HOMOLOGABLE;
            justificacion = agente.Justificacion;
            regla = $"Equivalencia registrada: {entrada.EquivalenciaAplicada.Trim()}";
        }
        else if (agente.RequiereRevision)
        {
            // Faltan datos para aplicar el método de cálculo: lo decide una persona
            decision = DecisionAnalisis.REQUIERE_REVISION;
            justificacion = agente.Justificacion;
            regla = ReglaUmbral;
        }
        else
        {
            // La decisión la calcula el código (umbral del 80 %), no el agente
            var d = _decision.CalcularDecision(agente.Contenido, agente.Profundidad, agente.Horas);
            decision = d.Decision;
            justificacion = string.IsNullOrWhiteSpace(agente.Justificacion)
                ? d.Justificacion
                : $"{agente.Justificacion.Trim()} {d.Justificacion}";
            regla = ReglaUmbral;
            if (agente.SinDatoHoras && decision == DecisionAnalisis.NO_HOMOLOGABLE &&
                agente.Contenido >= 80 && agente.Profundidad >= 80)
            {
                decision = DecisionAnalisis.REQUIERE_REVISION;
                justificacion += " Falta el dato de horas: el docente debe verificar la carga horaria.";
            }
            else if (decision == DecisionAnalisis.NO_HOMOLOGABLE &&
                     agente.Contenido >= UmbralCercano && agente.Profundidad >= UmbralCercano &&
                     agente.Horas >= UmbralCercano)
            {
                // Cerca del umbral en todos los criterios: la comparación por temas puede no reconocer
                // temas redactados distinto y las horas pueden compensarse con trabajo autónomo.
                // No se descarta: el docente decide con las diferencias a la vista.
                decision = DecisionAnalisis.REQUIERE_REVISION;
                justificacion += $" Está cerca del umbral (al menos {UmbralCercano} % en contenido, profundidad y horas): " +
                                 "el docente debe verificar si las diferencias se cubren en el sílabo de origen.";
            }
        }

        if (!ReglasHomologacion.EsMateriaNoHomologable(
                entrada.AsignaturaOrigen, entrada.AsignaturaDestino) &&
            !string.IsNullOrWhiteSpace(entrada.EquivalenciaAplicada) &&
            !justificacion.Contains(entrada.EquivalenciaAplicada, StringComparison.OrdinalIgnoreCase))
        {
            justificacion = $"{entrada.EquivalenciaAplicada.Trim()} {justificacion}";
        }

        // El agente a veces une varias materias en un solo texto ("A + B"): se separan.
        var nombres = (agente.MateriasOrigen ?? new List<string>())
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .SelectMany(m => m.Split(new[] { '+', ',', ';' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Una propuesta inválida del agente (materias que el estudiante no aprobó o más de dos)
        // no aborta el análisis de toda la solicitud: esa asignatura queda en revisión manual.
        string? problema = null;

        if (entrada.MateriasOrigenPermitidas.Count > 0)
        {
            var ajenas = nombres
                .Where(n => !entrada.MateriasOrigenPermitidas.Contains(n, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (ajenas.Count > 0)
            {
                problema = $"El agente propuso materias que el estudiante no tiene aprobadas ({string.Join(", ", ajenas)}).";
                nombres = nombres.Except(ajenas, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }

        if (nombres.Count > 2)
        {
            problema ??= "El agente propuso más de dos materias de origen para una asignatura.";
            nombres = nombres.Take(2).ToList();
        }

        if (nombres.Count == 0)
            nombres.Add(entrada.AsignaturaOrigen.Trim());

        var requiereRevision = agente.RequiereRevision;
        if (problema is not null)
        {
            decision = DecisionAnalisis.REQUIERE_REVISION;
            justificacion = $"{problema} Revise la equivalencia manualmente. {justificacion}";
            requiereRevision = true;
        }

        var materias = nombres.Select((nombre, indice) => new MateriaOrigenResultado
        {
            Nombre = Recortar(nombre, MaxNombre),
            Nota = entrada.NotasMateriasOrigen.TryGetValue(nombre, out var nota)
                ? nota
                : indice == 0 ? entrada.NotaOrigen : null
        }).ToList();

        return new AnalisisAsignatura
        {
            AsignaturaOrigen = Recortar(string.Join(" + ", nombres), MaxNombre),
            AsignaturaDestino = Recortar(entrada.AsignaturaDestino, MaxNombre),
            AsignaturaDestinoId = entrada.AsignaturaDestinoId,
            AreaOrigen = RecortarOpcional(entrada.AreaOrigen, 80),
            AreaDestino = RecortarOpcional(entrada.AreaDestino, 80),
            Combinada = nombres.Count == 2,
            MateriasOrigen = materias,
            PorcentajeContenido = agente.Contenido,
            PorcentajeProfundidad = agente.Profundidad,
            PorcentajeHoras = agente.Horas,
            Decision = decision,
            Justificacion = Recortar(justificacion, MaxJustificacion),
            RequiereRevision = requiereRevision,
            NivelOrigen = RecortarOpcional(entrada.NivelOrigen, 30),
            NivelDestino = RecortarOpcional(entrada.NivelDestino, 30),
            HorasOrigen = entrada.HorasOrigen,
            HorasDestino = entrada.HorasDestino,
            CreditosOrigen = entrada.CreditosOrigen,
            CreditosDestino = entrada.CreditosDestino,
            ContenidosCoincidentes = Lista(agente.Coincidencias),
            Diferencias = Lista(agente.Diferencias.Concat(
                agente.DatosFaltantes.Select(f => $"Dato no disponible: {f}"))),
            ReglaAplicada = Recortar(regla, 500),
            PropuestaPor = OrigenPropuesta.IA
        };
    }

    /// <summary>
    /// Cada materia aprobada se compara con hasta tres candidatas de destino. Al docente se le
    /// presentan las que cumplen la regla y, si ninguna cumple, solo la de mayor coincidencia
    /// (con sus diferencias), para no llenar la revisión de comparaciones descartadas.
    /// </summary>
    public static List<AnalisisAsignatura> DepurarCandidatas(List<AnalisisAsignatura> filas)
    {
        var lista = filas
            .GroupBy(f => f.AsignaturaOrigen, StringComparer.OrdinalIgnoreCase)
            .Select(g => g
                // Una materia aprobada se sugiere para un solo destino: el que mejor la recibe.
                .OrderBy(f => f.Decision switch
                {
                    DecisionAnalisis.HOMOLOGABLE => 0,
                    DecisionAnalisis.REQUIERE_REVISION => 1,
                    _ => 2
                })
                .ThenByDescending(f => f.PorcentajeContenido + f.PorcentajeProfundidad + f.PorcentajeHoras)
                .ThenByDescending(f => PreseleccionService.SimilitudNombre(f.AsignaturaOrigen, f.AsignaturaDestino))
                .First())
            .ToList();

        // Una materia que ya cuenta en una combinación homologable no se repite sola.
        var enCombinacion = lista
            .Where(f => f.Combinada && f.Decision == DecisionAnalisis.HOMOLOGABLE)
            .SelectMany(f => f.MateriasOrigen.Select(m => m.Nombre))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return lista.Where(f => f.Combinada || !enCombinacion.Contains(f.AsignaturaOrigen)).ToList();
    }

    /// <summary>
    /// La combinación de dos materias es una alternativa: solo se conserva si es homologable y
    /// ninguna de las dos materias cubre sola ese destino. Tampoco se usa una materia que ya se
    /// homologa sola a otra asignatura: una materia aprobada no cuenta para dos destinos.
    /// </summary>
    public static List<AnalisisAsignatura> DescartarCombinacionesInnecesarias(List<AnalisisAsignatura> filas)
    {
        var individuales = filas
            .Where(f => !f.Combinada && f.Decision == DecisionAnalisis.HOMOLOGABLE)
            .ToList();
        var destinosCubiertos = individuales
            .Select(f => f.AsignaturaDestino)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var origenesUsados = individuales
            .Select(f => f.AsignaturaOrigen)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var conservadas = filas
            .Where(f => !f.Combinada ||
                        (f.Decision == DecisionAnalisis.HOMOLOGABLE &&
                         !destinosCubiertos.Contains(f.AsignaturaDestino) &&
                         !f.MateriasOrigen.Any(m => origenesUsados.Contains(m.Nombre))))
            .ToList();

        // Si la combinación cubre el destino, la fila individual de una de sus materias hacia ese
        // mismo destino sobra: la materia ya cuenta dentro de la combinación.
        var combinaciones = conservadas.Where(f => f.Combinada).ToList();
        return conservadas
            .Where(f => f.Combinada || !combinaciones.Any(c =>
                string.Equals(c.AsignaturaDestino, f.AsignaturaDestino, StringComparison.OrdinalIgnoreCase) &&
                c.MateriasOrigen.Any(m => string.Equals(m.Nombre, f.AsignaturaOrigen, StringComparison.OrdinalIgnoreCase))))
            .ToList();
    }

    /// <summary>true si dos asignaturas tienen el mismo nombre (sin tildes ni palabras vacías).</summary>
    public static bool MismoNombre(string? a, string? b)
    {
        var ta = ComparadorSilabos.Tokens(a).ToHashSet(StringComparer.Ordinal);
        var tb = ComparadorSilabos.Tokens(b).ToHashSet(StringComparer.Ordinal);
        if (ta.Count == 0 || tb.Count == 0) return false;
        var comunes = ta.Count(tb.Contains);
        return (double)comunes / ta.Union(tb).Count() >= 0.75;
    }

    /// <summary>Contenido desde el cual una comparación que no llega al 80 % se envía a revisión.</summary>
    internal const int UmbralCercano = 65;

    internal const string ReglaUmbral =
        "Análisis comparativo de contenidos (R-CAU-P-011-2026; Art. 20 R-CAU-P-122-2026): " +
        "contenido, profundidad (resultados de aprendizaje) y carga horaria de al menos el 80 % de la asignatura receptora.";

    private static string? Lista(IEnumerable<string> temas)
    {
        var texto = string.Join("; ", temas.Where(t => !string.IsNullOrWhiteSpace(t)));
        return string.IsNullOrWhiteSpace(texto) ? null : Recortar(texto, 2000);
    }

    private static string Recortar(string texto, int max) =>
        texto.Length <= max ? texto : texto[..max];

    private static string? RecortarOpcional(string? texto, int max) =>
        string.IsNullOrWhiteSpace(texto)
            ? null
            : texto.Trim().Length <= max ? texto.Trim() : texto.Trim()[..max];
}

/// <summary>Estados del servicio de Solicitudes que usa Homologación.</summary>
internal static class EstadoSolicitudHomologacion
{
    public const string PENDIENTE_REVISION = "PENDIENTE_REVISION";
    public const string INFORME_EMITIDO = "INFORME_EMITIDO";
}
