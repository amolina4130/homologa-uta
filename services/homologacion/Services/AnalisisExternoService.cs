using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Connectors;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Eventos;
using HomologaUTA.Homologacion.Models;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Services;

/// <summary>
/// Envía la documentación al agente externo y guarda su decisión por asignatura con su justificación.
/// No impone el umbral del 80 %, pero aplica la regla institucional de materias no homologables.
/// Si el agente falla, no se guarda nada: se puede reintentar sin perder datos.
/// Desde HU-21/HU-22 solo se envían las asignaturas candidatas de la preselección y el agente puede
/// proponer una combinación de hasta dos materias de origen.
/// </summary>
public class AnalisisExternoService
{
    private const int MaxJustificacion = 2000;
    private const int MaxNombre = 200;
    private const int MaxDecision = 30;

    private readonly HomologacionDbContext _db;
    private readonly AgenteExternoConnector _conector;
    private readonly PreseleccionService _preseleccion;
    private readonly SolicitudesClient _solicitudes;
    private readonly DocumentosClient _documentos;
    private readonly IAnalisisEventPublisher _publisher;
    private readonly ILogger<AnalisisExternoService> _logger;

    public AnalisisExternoService(
        HomologacionDbContext db,
        AgenteExternoConnector conector,
        PreseleccionService preseleccion,
        SolicitudesClient solicitudes,
        DocumentosClient documentos,
        IAnalisisEventPublisher publisher,
        ILogger<AnalisisExternoService> logger)
    {
        _db = db;
        _conector = conector;
        _preseleccion = preseleccion;
        _solicitudes = solicitudes;
        _documentos = documentos;
        _publisher = publisher;
        _logger = logger;
    }

    /// <summary>
    /// Analiza la solicitud con el agente externo. Si no llega el cuerpo de la petición (o llega
    /// incompleto), se arma con los datos reales de la solicitud y el texto extraído de sus PDF.
    /// </summary>
    /// <exception cref="AnalisisSolicitudException">La solicitud no existe o no tiene documentos procesados.</exception>
    /// <exception cref="AgenteExternoTimeoutException">Venci�� el tiempo de espera; no se guardó nada.</exception>
    /// <exception cref="AgenteExternoException">El agente falló o respondió mal; no se guardó nada.</exception>
    public async Task<ResultadoAnalisisDto> AnalizarSolicitudAsync(
        int solicitudId, AgenteExternoRequest? request, CancellationToken ct = default)
    {
        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct)
                   ?? throw new AnalisisSolicitudException(404, "Solicitud no encontrada.");

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

        var entrada = await PrepararEntradaAsync(solicitudId, info, request, ct);

        // 1. Enviar al agente. Si falla o vence el tiempo, sale por excepción antes de tocar la base de datos.
        var respuesta = await _conector.EnviarDocumentacionAsync(entrada, ct);
        var filas = respuesta.Asignaturas
            .Select(a => ConstruirFila(a, entrada))
            .ToList();
        // Igual que el agente interno: sin combinaciones innecesarias y una sugerencia por materia.
        filas = AnalisisService.DescartarCombinacionesInnecesarias(filas);
        filas = AnalisisService.DepurarCandidatas(filas);
        // Las materias aprobadas que el agente no devolvió quedan para el docente, con lo que faltó.
        filas.AddRange(SinRespuesta(entrada, filas));

        // 2. Guardar: si la solicitud ya tenía un análisis, se reemplaza
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

        // 4. Mover la solicitud a PENDIENTE_REVISION para que aparezca al Coordinador (HU-09).
        var estadoActualizado = false;
        try
        {
            estadoActualizado = await _solicitudes.CambiarEstadoAsync(
                solicitudId, EstadoSolicitudHomologacion.PENDIENTE_REVISION, "sistema@homologauta", ct);
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            _logger.LogError(ex,
                "El análisis externo de {SolicitudId} se guardó, pero no se pudo actualizar su estado", solicitudId);
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

    /// <summary>
    /// Completa la petición del agente con los datos reales de la solicitud. Si hay materias
    /// aprobadas, primero hace la preselección por área y nivel (HU-21); si no, usa las
    /// asignaturas de la solicitud (comportamiento anterior).
    /// </summary>
    private async Task<AgenteExternoRequest> PrepararEntradaAsync(
        int solicitudId, SolicitudCompletaDto info, AgenteExternoRequest? request, CancellationToken ct)
    {
        var entrada = request ?? new AgenteExternoRequest();
        entrada.Documentos ??= new DocumentosAgenteExternoDto();
        entrada.SolicitudId = solicitudId;

        if (string.IsNullOrWhiteSpace(entrada.Estudiante))
            entrada.Estudiante = CoordinadorService.NombreEstudiante(info);
        if (string.IsNullOrWhiteSpace(entrada.CarreraOrigen))
            entrada.CarreraOrigen = info.CarreraOrigen;
        if (string.IsNullOrWhiteSpace(entrada.CarreraDestino))
            entrada.CarreraDestino = info.CarreraDestino;
        // Las universidades están disponibles en info (SolicitudCompletaDto) pero el contrato
        // AgenteExternoRequest actual no las incluye; se omiten para no romper el contrato.

        var usaPreseleccion = entrada.MateriasDestino.Count > 0 &&
                              entrada.MateriasDestino.Any(d => d.MateriasOrigenPermitidas.Count > 0);

        if (entrada.MateriasDestino.Count == 0)
        {
            EntradasPreseleccion? preseleccion = null;
            try
            {
                preseleccion = await _preseleccion.ConstruirEntradasAsync(solicitudId, ct);
            }
            catch (AnalisisSolicitudException ex) when (ex.StatusCode == 409)
            {
                _logger.LogWarning(ex, "El análisis externo de {SolicitudId} no pudo usar la preselección", solicitudId);
                throw;
            }

            // Solo son candidatas las materias APROBADAS según el récord académico.
            if (preseleccion is not { UsaMateriasAprobadas: true })
                throw new AnalisisSolicitudException(409,
                    "Primero suba el récord académico y confirme las materias aprobadas: " +
                    "la IA solo sugiere homologar materias que el estudiante aprobó.");

            if (preseleccion is { UsaMateriasAprobadas: true, Entradas.Count: > 0 })
            {
                entrada.MateriasDestino = preseleccion.Entradas.Select(ConstruirDestino).ToList();
                entrada.MateriasOrigen = preseleccion.Entradas
                    .SelectMany(e => e.MateriasOrigenPermitidas)
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                usaPreseleccion = true;
            }
            else
            {
                entrada.MateriasOrigen = info.Asignaturas
                    .Select(a => a.AsignaturaOrigen)
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                entrada.MateriasDestino = info.Asignaturas.Select(a => new MateriaDestinoAgenteExternoDto
                {
                    AsignaturaId = a.AsignaturaDestinoId ?? 0,
                    Nombre = a.AsignaturaDestino,
                    Area = a.AreaDestino,
                    Nivel = a.NivelDestino,
                    Contenido = a.ContenidoDestino,
                    MateriasOrigenPermitidas = new List<string> { a.AsignaturaOrigen }
                }).ToList();

                usaPreseleccion = false;
            }
        }
        else if (entrada.MateriasOrigen.Count == 0)
        {
            entrada.MateriasOrigen = entrada.MateriasDestino
                .SelectMany(d => d.MateriasOrigenPermitidas)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // El cuerpo ya trae la documentación: se respeta lo que envió el estudiante.
        if (entrada.Documentos.Silabos.Count > 0)
            return entrada;

        var documentos = await _documentos.ObtenerPorSolicitudAsync(solicitudId, ct);
        if (documentos.Count == 0)
        {
            throw new AnalisisSolicitudException(400,
                "La solicitud no tiene documentos procesados. Suba la malla, el historial y los sílabos antes de usar el agente externo.");
        }

        if (string.IsNullOrWhiteSpace(entrada.Documentos.MallaOrigen))
            entrada.Documentos.MallaOrigen = UnirTextos(documentos, "MALLA");
        if (string.IsNullOrWhiteSpace(entrada.Documentos.HistorialAcademico))
            entrada.Documentos.HistorialAcademico = UnirTextos(documentos, "HISTORIAL");

        // Los sílabos que se envían son los de la UNIVERSIDAD DE ORIGEN (documentos de la carpeta).
        // El contenido de destino ya va en MateriasDestino; antes se enviaba aquí también y el
        // agente terminaba comparando la asignatura de destino consigo misma.
        entrada.Documentos.Silabos = usaPreseleccion
            ? SilabosDeOrigen(entrada.MateriasOrigen, documentos)
            : ConstruirSilabos(info, documentos);

        return entrada;
    }

    private static MateriaDestinoAgenteExternoDto ConstruirDestino(AsignaturaAnalisisDto entrada) => new()
    {
        AsignaturaId = entrada.AsignaturaDestinoId ?? 0,
        Nombre = entrada.AsignaturaDestino,
        Area = entrada.AreaDestino,
        Nivel = entrada.NivelDestino,
        Contenido = entrada.ContenidoDestino,
        ResultadosAprendizaje = entrada.ResultadosDestino,
        Horas = entrada.HorasDestino,
        MateriasOrigenPermitidas = entrada.MateriasOrigenPermitidas
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList(),
        NotasMateriasOrigen = entrada.NotasMateriasOrigen
    };

    /// <summary>
    /// Asocia cada asignatura de la solicitud con el texto de un sílabo procesado. Si no hay
    /// sílabos, usa la descripción real de la asignatura para que el agente tenga contenido.
    /// </summary>
    private static List<SilaboAgenteExternoDto> ConstruirSilabos(
        SolicitudCompletaDto info, List<DocumentoExtraidoDto> documentos)
    {
        var silabos = documentos.Where(EsDocumentoDeAsignatura).ToList();

        var usados = new HashSet<int>();
        var lista = new List<SilaboAgenteExternoDto>();

        foreach (var asignatura in info.Asignaturas)
        {
            var documento = silabos.FirstOrDefault(d =>
                !usados.Contains(d.Id) && CoincideConAsignatura(d, asignatura.AsignaturaOrigen));

            if (documento is not null)
                usados.Add(documento.Id);

            var contenido = documento?.TextoExtraido;
            lista.Add(new SilaboAgenteExternoDto
            {
                Asignatura = asignatura.AsignaturaOrigen,
                Contenido = string.IsNullOrWhiteSpace(contenido)
                    ? asignatura.ContenidoOrigen
                    : contenido.Trim()
            });
        }

        return lista;
    }

    /// <summary>
    /// Sílabo de cada materia aprobada, buscado por nombre entre los documentos procesados de la
    /// carpeta. Si una materia no tiene sílabo no se le asigna otro: falta información.
    /// </summary>
    private static List<SilaboAgenteExternoDto> SilabosDeOrigen(
        List<string> materiasOrigen, List<DocumentoExtraidoDto> documentos)
    {
        var silabos = documentos.Where(EsDocumentoDeAsignatura).ToList();

        return materiasOrigen
            .Select(m => (Materia: m, Documento: silabos.FirstOrDefault(d => CoincideConAsignatura(d, m))))
            .Where(x => x.Documento is not null)
            .Select(x => new SilaboAgenteExternoDto { Asignatura = x.Materia, Contenido = x.Documento!.TextoExtraido!.Trim() })
            .ToList();
    }

    private const string CriterioExterno =
        "Criterios generales de equivalencia académica (contenidos, resultados de aprendizaje, horas y créditos). " +
        "No se aplican las reglas internas de homologación entre carreras de FISEI.";

    /// <summary>Materias aprobadas que el agente no evaluó: quedan para revisión con lo que faltó.</summary>
    private static IEnumerable<AnalisisAsignatura> SinRespuesta(AgenteExternoRequest entrada, List<AnalisisAsignatura> filas)
    {
        var evaluadas = filas.SelectMany(f => f.MateriasOrigen.Select(m => m.Nombre))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var materia in entrada.MateriasOrigen.Where(m => !evaluadas.Contains(m)))
        {
            var tieneSilabo = entrada.Documentos.Silabos.Any(s =>
                string.Equals(s.Asignatura, materia, StringComparison.OrdinalIgnoreCase));
            yield return new AnalisisAsignatura
            {
                AsignaturaOrigen = Recortar(materia, MaxNombre),
                AsignaturaDestino = "Sin equivalente probable",
                MateriasOrigen = new List<MateriaOrigenResultado> { new() { Nombre = Recortar(materia, MaxNombre) } },
                Decision = DecisionAnalisis.REQUIERE_REVISION,
                Justificacion = tieneSilabo
                    ? "El agente externo no encontró una asignatura equivalente en la malla de destino."
                    : "No se encontró el sílabo de esta materia en la carpeta de la universidad de origen; " +
                      "solicite el documento antes de decidir.",
                Diferencias = tieneSilabo ? null : "Información faltante: sílabo de la materia de origen.",
                ReglaAplicada = CriterioExterno,
                RequiereRevision = true
            };
        }
    }

    private static bool EsTipo(string tipo, string esperado) =>
        tipo.Contains(esperado, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// PDF de la universidad de origen que puede describir una asignatura: sílabo, plan analítico
    /// u otro documento suelto de la carpeta. No cuentan el récord, la malla, la carta ni el enlace.
    /// </summary>
    private static bool EsDocumentoDeAsignatura(DocumentoExtraidoDto d) =>
        !string.IsNullOrWhiteSpace(d.TextoExtraido) &&
        !EsTipo(d.TipoDocumento, "HISTORIAL") && !EsTipo(d.TipoDocumento, "RECORD") &&
        !EsTipo(d.TipoDocumento, "MALLA") && !EsTipo(d.TipoDocumento, "URL_EXTERNA") &&
        !EsTipo(d.TipoDocumento, "SOLICITUD");

    /// <summary>
    /// El documento es de la asignatura si su nombre aparece en el nombre del archivo o en el
    /// encabezado del texto (portada), sin distinguir tildes ni mayúsculas. No se busca en todo
    /// el texto: un sílabo nombra otras asignaturas como prerrequisitos.
    /// </summary>
    private static bool CoincideConAsignatura(DocumentoExtraidoDto documento, string asignatura)
    {
        var nombre = Normalizar(asignatura);
        if (nombre.Length < 3) return false;
        var archivo = Normalizar(Path.GetFileNameWithoutExtension(documento.NombreArchivo ?? string.Empty));
        if (Regex.IsMatch(archivo, $@"(^| ){Regex.Escape(nombre)}( |$)")) return true;
        var texto = documento.TextoExtraido ?? string.Empty;
        var portada = Normalizar(texto.Length > 600 ? texto[..600] : texto);
        return Regex.IsMatch(portada, $@"(^| ){Regex.Escape(nombre)}( |$)");
    }

    private static string Normalizar(string texto)
    {
        var d = texto.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(d.Length);
        foreach (var c in d)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return Regex.Replace(sb.ToString().ToLowerInvariant(), "[^a-z0-9]+", " ").Trim();
    }

    private static string UnirTextos(List<DocumentoExtraidoDto> documentos, string tipo) =>
        string.Join("\n\n", documentos
            .Where(d => EsTipo(d.TipoDocumento, tipo) && !string.IsNullOrWhiteSpace(d.TextoExtraido))
            .Select(d => d.TextoExtraido!.Trim()));

    /// <summary>
    /// La decisión y la justificación se guardan tal como las dio el agente (sin umbral ni reglas).
    /// Solo si faltan se marca la asignatura para revisión manual. Se guardan una o dos materias
    /// de origen (HU-22) con su nota cuando la preselección la conoce.
    /// </summary>
    private static AnalisisAsignatura ConstruirFila(AsignaturaAgenteExternoDto a, AgenteExternoRequest entrada)
    {
        var destino = BuscarDestino(entrada.MateriasDestino, a);
        var sinDecision = string.IsNullOrWhiteSpace(a.Decision);
        var sinJustificacion = string.IsNullOrWhiteSpace(a.Justificacion);

        var origenes = (a.MateriasOrigen ?? new List<string>())
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (origenes.Count == 0 && !string.IsNullOrWhiteSpace(a.NombreOrigen))
            origenes.Add(a.NombreOrigen.Trim());

        var excedeCombinacion = origenes.Count > 2;
        if (excedeCombinacion)
            origenes = origenes.Take(2).ToList();

        if (origenes.Count == 0)
            origenes.Add("Sin materia de origen");

        var notas = destino?.NotasMateriasOrigen ?? new Dictionary<string, decimal?>();

        var materias = origenes.Select(nombre => new MateriaOrigenResultado
        {
            Nombre = Recortar(nombre, MaxNombre),
            Nota = notas.TryGetValue(nombre, out var nota) ? nota : null
        }).ToList();

        string decision;
        string justificacion;
        bool requiereRevision;

        // Comparación con el método definido: sílabo de origen (carpeta) contra la asignatura
        // de destino de FISEI. Solo con los datos que existen; lo que falta se informa.
        var silaboOrigen = string.Join("\n", entrada.Documentos.Silabos
            .Where(s => origenes.Contains(s.Asignatura, StringComparer.OrdinalIgnoreCase))
            .Select(s => s.Contenido));
        // Las horas de origen salen del propio sílabo si las declara; la profundidad se mide con
        // los resultados de aprendizaje de la asignatura de FISEI.
        var horasOrigen = HomologaUTA.Homologacion.Agents.ComparadorSilabos.HorasDeSilabo(silaboOrigen);
        var comparacion = HomologaUTA.Homologacion.Agents.ComparadorSilabos.Comparar(
            string.IsNullOrWhiteSpace(silaboOrigen) ? null : silaboOrigen, null, horasOrigen,
            destino?.Contenido, destino?.ResultadosAprendizaje, destino?.Horas);
        var faltantes = new List<string>();
        if (string.IsNullOrWhiteSpace(silaboOrigen)) faltantes.Add("sílabo de la materia de origen");
        if (comparacion.Horas is null)
            faltantes.Add("horas de la asignatura de origen (verifíquelas en los documentos)");
        faltantes.Add("créditos de la universidad de origen (verifíquelos en los documentos)");

        // Desarrollo de Proyectos, Titulación y Prácticas son actividades finales de la carrera
        // receptora: no se reconocen por equivalencia de asignatura, sea interna o externa.
        var motivoExclusion = ReglasHomologacion.MotivoSiNoHomologable(
            string.Join(" + ", origenes), a.NombreDestino);
        if (motivoExclusion is not null)
        {
            decision = DecisionAnalisis.NO_HOMOLOGABLE;
            justificacion = motivoExclusion;
            requiereRevision = false;
        }
        else if (excedeCombinacion)
        {
            decision = DecisionAnalisis.REQUIERE_REVISION;
            justificacion = "El agente externo propuso más de dos materias de origen. Requiere revisión manual.";
            requiereRevision = true;
        }
        else if (sinDecision)
        {
            decision = DecisionAnalisis.REQUIERE_REVISION;
            justificacion = sinJustificacion
                ? "El agente externo no entregó decisión ni justificación."
                : Recortar(a.Justificacion.Trim(), MaxJustificacion);
            requiereRevision = true;
        }
        else
        {
            decision = Recortar(a.Decision.Trim(), MaxDecision);
            justificacion = sinJustificacion
                ? "El agente externo no entregó justificación."
                : Recortar(a.Justificacion.Trim(), MaxJustificacion);
            requiereRevision = sinJustificacion;
        }

        // Criterio general: además de los contenidos se miran los resultados de aprendizaje y las
        // horas. Si alguno queda bajo el 80 %, la decisión la toma el docente.
        if (decision == DecisionAnalisis.HOMOLOGABLE && !string.IsNullOrWhiteSpace(silaboOrigen) &&
            (comparacion.Profundidad < 80 || comparacion.Horas is < 80))
        {
            decision = DecisionAnalisis.REQUIERE_REVISION;
            requiereRevision = true;
            justificacion = Recortar(
                $"{justificacion} Cubre el {comparacion.Profundidad}% de los resultados de aprendizaje" +
                (comparacion.Horas is int h ? $" y el {h}% de la carga horaria" : string.Empty) +
                "; el docente debe verificar la equivalencia.", MaxJustificacion);
        }

        // Sin sílabo de origen no hay base para elegir destino: solo se mantiene si el nombre es
        // equivalente; si no, la materia queda "Sin equivalente probable" para que decida el docente.
        var destinoSinBase = string.IsNullOrWhiteSpace(silaboOrigen) &&
                             PreseleccionService.SimilitudNombre(string.Join(" ", origenes), a.NombreDestino) < 0.5;

        return new AnalisisAsignatura
        {
            AsignaturaOrigen = Recortar(string.Join(" + ", origenes), MaxNombre),
            AsignaturaDestino = destinoSinBase ? "Sin equivalente probable" : Recortar(a.NombreDestino.Trim(), MaxNombre),
            AsignaturaDestinoId = destinoSinBase ? null : a.AsignaturaDestinoId ?? (destino?.AsignaturaId > 0 ? destino.AsignaturaId : null),
            AreaDestino = string.IsNullOrWhiteSpace(destino?.Area) ? null : Recortar(destino!.Area!.Trim(), 80),
            Combinada = materias.Count == 2,
            MateriasOrigen = materias,
            PorcentajeContenido = a.PorcentajeSimilitud,
            PorcentajeProfundidad = string.IsNullOrWhiteSpace(silaboOrigen) ? 0 : comparacion.Profundidad,
            PorcentajeHoras = comparacion.Horas ?? 0,
            HorasOrigen = horasOrigen,
            HorasDestino = destino?.Horas,
            Decision = decision,
            Justificacion = Recortar(justificacion, MaxJustificacion),
            RequiereRevision = requiereRevision || string.IsNullOrWhiteSpace(silaboOrigen),
            NivelDestino = string.IsNullOrWhiteSpace(destino?.Nivel) ? null : Recortar(destino!.Nivel!.Trim(), 30),
            ContenidosCoincidentes = comparacion.Coincidencias.Count == 0 || string.IsNullOrWhiteSpace(silaboOrigen)
                ? null
                : Recortar(string.Join("; ", comparacion.Coincidencias), 2000),
            Diferencias = Recortar(string.Join("; ",
                (string.IsNullOrWhiteSpace(silaboOrigen) ? new List<string>() : comparacion.Diferencias)
                .Concat(faltantes.Select(f => $"Información faltante: {f}"))), 2000),
            ReglaAplicada = motivoExclusion ?? CriterioExterno,
            PropuestaPor = OrigenPropuesta.IA
        };
    }

    private static MateriaDestinoAgenteExternoDto? BuscarDestino(
        List<MateriaDestinoAgenteExternoDto>? destinos, AsignaturaAgenteExternoDto asignatura)
    {
        if (destinos is null || destinos.Count == 0)
            return null;

        if (asignatura.AsignaturaDestinoId is > 0)
        {
            var porId = destinos.FirstOrDefault(d => d.AsignaturaId == asignatura.AsignaturaDestinoId.Value);
            if (porId is not null)
                return porId;
        }

        return destinos.FirstOrDefault(d =>
            string.Equals(d.Nombre?.Trim(), asignatura.NombreDestino?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string Recortar(string texto, int max) =>
        texto.Length <= max ? texto : texto[..max];
}
