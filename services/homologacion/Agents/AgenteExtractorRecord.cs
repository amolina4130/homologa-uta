using System.Text.Json;
using System.Text.Json.Serialization;
using HomologaUTA.Homologacion.Clients;
using Polly;
using Polly.Retry;

namespace HomologaUTA.Homologacion.Agents;

/// <summary>Materia aprobada tal como la devuelve el agente al leer el récord (HU-20).</summary>
public class MateriaExtraidaDto
{
    [JsonRequired]
    public string Nombre { get; set; } = string.Empty;

    public decimal? Nota { get; set; }

    public string? EstadoSegunDocumento { get; set; }
    public string? Periodo { get; set; }
}

public class ResultadoExtraccionRecord
{
    public List<MateriaExtraidaDto> Materias { get; set; } = new();
    public bool RequiereRevision { get; set; }
    public string? Motivo { get; set; }
}

/// <summary>
/// Agente que extrae del texto del récord académico las materias aprobadas, su nota,
/// estado y periodo. Valida el JSON y reintenta con Polly (HU-20).
/// </summary>
public class AgenteExtractorRecord
{
    private readonly OllamaClient _ollama;
    private readonly ResiliencePipeline _retry;
    private readonly ILogger<AgenteExtractorRecord> _logger;

    public AgenteExtractorRecord(OllamaClient ollama, ILogger<AgenteExtractorRecord> logger)
    {
        _ollama = ollama;
        _logger = logger;

        _retry = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                ShouldHandle = new PredicateBuilder()
                    .Handle<JsonException>()
                    .Handle<TaskCanceledException>()
                    .Handle<HttpRequestException>(),
                OnRetry = args =>
                {
                    _logger.LogWarning("Reintento {Attempt} al extraer el récord: {Error}",
                        args.AttemptNumber, args.Outcome.Exception?.Message);
                    return default;
                }
            })
            .Build();
    }

    public async Task<ResultadoExtraccionRecord> ExtraerAsync(string texto, CancellationToken ct = default)
    {
        var prompt = $@"Eres un asistente de la Universidad Técnica de Ambato.
Del siguiente texto de un récord académico extrae TODAS las materias con su nota, estado y periodo.
Devuelve ÚNICAMENTE un JSON válido con esta estructura:
{{""materias"":[{{""nombre"":""..."",""nota"":15.5,""estadoSegunDocumento"":""APROBADO"",""periodo"":""2023-1""}}]}}
Si no puedes leer el récord, devuelve {{""materias"":[]}}.
No inventes materias que no aparezcan en el texto.

Texto del récord:
{texto}";

        try
        {
            return await _retry.ExecuteAsync(async token =>
            {
                var respuesta = await _ollama.GenerarRespuestaAsync(prompt);
                var limpio = respuesta.Replace("```json", "").Replace("```", "").Trim();
                var resultado = Parsear(limpio);
                if (resultado is null)
                    throw new JsonException("La respuesta del agente no tiene el formato esperado.");
                return resultado;
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo extraer el récord académico tras los reintentos");
            return new ResultadoExtraccionRecord
            {
                RequiereRevision = true,
                Motivo = "No se pudo interpretar el récord académico automáticamente. Revise las materias manualmente."
            };
        }
    }

    private static ResultadoExtraccionRecord? Parsear(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        JsonElement lista;
        if (root.ValueKind == JsonValueKind.Array)
            lista = root;
        else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("materias", out var m))
            lista = m;
        else
            return null;

        if (lista.ValueKind != JsonValueKind.Array)
            return null;

        var resultado = new ResultadoExtraccionRecord();
        foreach (var item in lista.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;

            var nombre = LeerTexto(item, "nombre");
            if (string.IsNullOrWhiteSpace(nombre)) continue;

            resultado.Materias.Add(new MateriaExtraidaDto
            {
                Nombre = nombre.Trim(),
                Nota = LeerDecimal(item, "nota"),
                EstadoSegunDocumento = LeerTexto(item, "estadoSegunDocumento") ?? LeerTexto(item, "estado"),
                Periodo = LeerTexto(item, "periodo")
            });
        }

        if (resultado.Materias.Count == 0)
        {
            resultado.RequiereRevision = true;
            resultado.Motivo = "El récord no contiene materias legibles. Puede corregirlas manualmente.";
        }

        return resultado;
    }

    private static string? LeerTexto(JsonElement item, string propiedad)
    {
        if (!item.TryGetProperty(propiedad, out var valor)) return null;
        return valor.ValueKind switch
        {
            JsonValueKind.String => valor.GetString(),
            JsonValueKind.Number => valor.ToString(),
            _ => null
        };
    }

    private static decimal? LeerDecimal(JsonElement item, string propiedad)
    {
        if (!item.TryGetProperty(propiedad, out var valor)) return null;
        if (valor.ValueKind == JsonValueKind.Number && valor.TryGetDecimal(out var n)) return n;

        if (valor.ValueKind == JsonValueKind.String)
        {
            var texto = (valor.GetString() ?? string.Empty).Replace(',', '.');
            if (decimal.TryParse(texto, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var n2))
                return n2;
        }

        return null;
    }
}
