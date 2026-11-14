using System.Net.Http.Json;
using System.Text.Json;
using HomologaUTA.Homologacion.DTOs;
using Polly;
using Polly.Retry;

namespace HomologaUTA.Homologacion.Connectors;

/// <summary>El agente externo no respondió dentro del tiempo de espera.</summary>
public class AgenteExternoTimeoutException : Exception
{
    public AgenteExternoTimeoutException(int segundos)
        : base($"El agente externo no respondió en {segundos} segundos (tiempo de espera agotado).") { }
}

/// <summary>El agente externo no se pudo contactar o su respuesta no cumple el contrato.</summary>
public class AgenteExternoException : Exception
{
    public AgenteExternoException(string mensaje, Exception? inner = null) : base(mensaje, inner) { }
}

/// <summary>
/// La respuesta del agente externo incumple el contrato de combinación (más de dos materias
/// de origen o materias que no están aprobadas/preseleccionadas). Se reintenta con Polly.
/// </summary>
public class AgenteExternoValidacionException : AgenteExternoException
{
    public AgenteExternoResponse? Respuesta { get; }
    public IReadOnlyList<string> Errores { get; }

    public AgenteExternoValidacionException(
        string mensaje, AgenteExternoResponse? respuesta, IReadOnlyList<string> errores)
        : base(mensaje)
    {
        Respuesta = respuesta;
        Errores = errores;
    }
}

/// <summary>
/// Conector con el agente externo (HU-09). Envía la documentación y devuelve su respuesta sin
/// imponerle reglas ni umbrales. Desde HU-22 valida que proponga como máximo dos materias de
/// origen y que todas sean del estudiante; si no cumple, reintenta con Polly y, agotados los
/// intentos, deja esas asignaturas en REQUIERE_REVISION.
/// </summary>
public class AgenteExternoConnector
{
    private readonly HttpClient _http;
    private readonly string _url;
    private readonly int _timeoutSegundos;
    private readonly ResiliencePipeline _retryPipeline;
    private readonly ILogger<AgenteExternoConnector> _logger;

    public AgenteExternoConnector(HttpClient http, IConfiguration config, ILogger<AgenteExternoConnector> logger)
    {
        _http = http;
        _logger = logger;
        _url = config["AgenteExterno:Url"]
               ?? throw new InvalidOperationException("Falta la configuración AgenteExterno:Url.");
        _timeoutSegundos = config.GetValue<int?>("AgenteExterno:TimeoutSegundos") ?? 30;

        var reintentos = Math.Max(0, config.GetValue<int?>("AgenteExterno:Reintentos") ?? 2);

        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = reintentos,
                Delay = TimeSpan.FromMilliseconds(300),
                ShouldHandle = new PredicateBuilder()
                    .Handle<AgenteExternoValidacionException>(),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Reintento {Intento} con el agente externo por respuesta inválida: {Error}",
                        args.AttemptNumber, args.Outcome.Exception?.Message);
                    return default;
                }
            })
            .Build();

        // El tiempo de espera lo controla el token de cada llamada, no el HttpClient
        _http.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async Task<AgenteExternoResponse> EnviarDocumentacionAsync(
        AgenteExternoRequest request, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSegundos));

        AgenteExternoResponse? ultima = null;

        try
        {
            return await _retryPipeline.ExecuteAsync(async token =>
            {
                var resultado = await EnviarUnaVezAsync(request, token);
                ultima = resultado;

                var errores = ValidarRespuesta(request, resultado);
                if (errores.Count > 0)
                {
                    throw new AgenteExternoValidacionException(
                        $"El agente externo devolvió una combinación inválida: {string.Join("; ", errores)}",
                        resultado, errores);
                }

                return resultado;
            }, cts.Token);
        }
        catch (AgenteExternoValidacionException ex)
        {
            _logger.LogError(ex,
                "El agente externo no entregó una respuesta válida tras los reintentos (solicitud {SolicitudId})",
                request.SolicitudId);

            // No se pierde todo el análisis: las asignaturas inválidas quedan para revisión manual.
            return Degradar(request, ultima ?? ex.Respuesta);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Tiempo de espera agotado con el agente externo (solicitud {SolicitudId})", request.SolicitudId);
            throw new AgenteExternoTimeoutException(_timeoutSegundos);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "No se pudo contactar al agente externo (solicitud {SolicitudId})", request.SolicitudId);
            throw new AgenteExternoException("No se pudo contactar al agente externo.", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Respuesta del agente externo con formato inválido (solicitud {SolicitudId})", request.SolicitudId);
            throw new AgenteExternoException("La respuesta del agente externo no tiene el formato del contrato.", ex);
        }
    }

    private async Task<AgenteExternoResponse> EnviarUnaVezAsync(
        AgenteExternoRequest request, CancellationToken ct)
    {
        using var respuesta = await _http.PostAsJsonAsync(_url, request, ct);

        if (!respuesta.IsSuccessStatusCode)
            throw new AgenteExternoException(
                $"El agente externo respondió con error {(int)respuesta.StatusCode}.");

        var resultado = await respuesta.Content.ReadFromJsonAsync<AgenteExternoResponse>(ct);

        if (resultado is null || !resultado.AnalisisCompletado || resultado.Asignaturas.Count == 0)
            throw new AgenteExternoException(
                "El agente externo no completó el análisis o no devolvió asignaturas.");

        return resultado;
    }

    /// <summary>
    /// Contrato de combinación (HU-22): una o dos materias de origen por destino, todas dentro
    /// de las aprobadas y preseleccionadas.
    /// </summary>
    private static List<string> ValidarRespuesta(AgenteExternoRequest request, AgenteExternoResponse respuesta)
    {
        var errores = new List<string>();

        if (respuesta.Asignaturas is null || respuesta.Asignaturas.Count == 0)
        {
            errores.Add("La respuesta no trae asignaturas.");
            return errores;
        }

        var globales = new HashSet<string>(
            (request.MateriasOrigen ?? new List<string>())
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m.Trim()),
            StringComparer.OrdinalIgnoreCase);

        foreach (var asignatura in respuesta.Asignaturas)
        {
            var destino = BuscarDestino(request.MateriasDestino, asignatura);

            var permitidas = destino?.MateriasOrigenPermitidas is { Count: > 0 } lista
                ? new HashSet<string>(
                    lista.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m.Trim()),
                    StringComparer.OrdinalIgnoreCase)
                : globales;

            if (string.IsNullOrWhiteSpace(asignatura.NombreDestino))
                errores.Add("Una asignatura no indica el nombre de destino.");

            if (string.IsNullOrWhiteSpace(asignatura.Decision))
                errores.Add($"La asignatura {asignatura.NombreDestino} no trae decisión.");

            if (string.IsNullOrWhiteSpace(asignatura.Justificacion))
                errores.Add($"La asignatura {asignatura.NombreDestino} no trae justificación.");

            var origenes = NormalizarOrigenes(asignatura);

            if (origenes.Count == 0)
                errores.Add($"La asignatura {asignatura.NombreDestino} no indica materias de origen.");
            else if (origenes.Count > 2)
                errores.Add($"La asignatura {asignatura.NombreDestino} propone más de dos materias de origen.");
            else if (permitidas.Count > 0)
            {
                var ajenas = origenes.Where(o => !permitidas.Contains(o)).ToList();
                if (ajenas.Count > 0)
                {
                    errores.Add(
                        $"La asignatura {asignatura.NombreDestino} propone materias no aprobadas ni preseleccionadas: " +
                        string.Join(", ", ajenas) + ".");
                }
            }
        }

        return errores;
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

    private static List<string> NormalizarOrigenes(AsignaturaAgenteExternoDto asignatura)
    {
        var origenes = (asignatura.MateriasOrigen ?? new List<string>())
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (origenes.Count == 0 && !string.IsNullOrWhiteSpace(asignatura.NombreOrigen))
            origenes.Add(asignatura.NombreOrigen.Trim());

        return origenes;
    }

    /// <summary>
    /// Respuesta final cuando el agente no cumple el contrato: conserva las filas válidas y
    /// deja las demás en REQUIERE_REVISION para que las revise una persona.
    /// </summary>
    private static AgenteExternoResponse Degradar(
        AgenteExternoRequest request, AgenteExternoResponse? ultima)
    {
        var respuesta = ultima ?? new AgenteExternoResponse
        {
            SolicitudId = request.SolicitudId,
            AnalisisCompletado = true
        };

        var destinos = request.MateriasDestino ?? new List<MateriaDestinoAgenteExternoDto>();
        if (destinos.Count == 0)
            return respuesta;

        var porNombre = (respuesta.Asignaturas ?? new List<AsignaturaAgenteExternoDto>())
            .Where(a => !string.IsNullOrWhiteSpace(a.NombreDestino))
            .GroupBy(a => a.NombreDestino.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var filas = new List<AsignaturaAgenteExternoDto>();

        foreach (var destino in destinos)
        {
            if (porNombre.TryGetValue(destino.Nombre.Trim(), out var fila) && FilaValida(destino, fila))
            {
                filas.Add(fila);
                continue;
            }

            var origen = destino.MateriasOrigenPermitidas
                .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))?.Trim();

            filas.Add(new AsignaturaAgenteExternoDto
            {
                AsignaturaDestinoId = destino.AsignaturaId,
                NombreDestino = destino.Nombre,
                NombreOrigen = origen ?? string.Empty,
                MateriasOrigen = origen is null ? new List<string>() : new List<string> { origen },
                Decision = "REQUIERE_REVISION",
                Justificacion = "El agente externo no entregó una combinación válida tras varios intentos. " +
                                "Requiere revisión manual.",
                PorcentajeSimilitud = 0
            });
        }

        return new AgenteExternoResponse
        {
            SolicitudId = request.SolicitudId,
            AnalisisCompletado = true,
            Asignaturas = filas,
            ObservacionesGenerales = "Una o más asignaturas requieren revisión manual."
        };
    }

    private static bool FilaValida(MateriaDestinoAgenteExternoDto destino, AsignaturaAgenteExternoDto fila)
    {
        if (string.IsNullOrWhiteSpace(fila.Justificacion))
            return false;

        var origenes = NormalizarOrigenes(fila);
        if (origenes.Count is 0 or > 2)
            return false;

        var permitidas = new HashSet<string>(
            destino.MateriasOrigenPermitidas
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m.Trim()),
            StringComparer.OrdinalIgnoreCase);

        return permitidas.Count == 0 || origenes.All(permitidas.Contains);
    }
}
