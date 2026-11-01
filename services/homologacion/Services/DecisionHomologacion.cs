namespace HomologaUTA.Homologacion.Services;

public class ResultadoDecision
{
    public bool EsHomologable { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string Justificacion { get; set; } = string.Empty;
}

public class DecisionHomologacion
{
    // Por defecto usamos el 80% según la normativa, pero se puede cambiar
    private const int UmbralPorDefecto = 80;

    public ResultadoDecision CalcularDecision(int porcentajeContenido, int porcentajeProfundidad, int porcentajeHoras, int? umbralDesdeCatalogo = null)
    {
        // 1. Leemos el umbral (del catálogo o el valor por defecto)
        int umbral = umbralDesdeCatalogo ?? UmbralPorDefecto;
        
        var resultado = new ResultadoDecision();

        // 2. Evaluamos si cumple con el umbral en las tres dimensiones
        bool cumpleContenido = porcentajeContenido >= umbral;
        bool cumpleProfundidad = porcentajeProfundidad >= umbral;
        bool cumpleHoras = porcentajeHoras >= umbral;

        // 3. Decisión final calculada por C# (no por el agente)
        if (cumpleContenido && cumpleProfundidad && cumpleHoras)
        {
            resultado.EsHomologable = true;
            resultado.Decision = "HOMOLOGABLE";
            resultado.Justificacion = $"Cumple con el umbral del {umbral}% en contenido ({porcentajeContenido}%), profundidad ({porcentajeProfundidad}%) y carga horaria ({porcentajeHoras}%).";
        }
        else
        {
            resultado.EsHomologable = false;
            resultado.Decision = "NO_HOMOLOGABLE";
            
            var fallos = new List<string>();
            if (!cumpleContenido) fallos.Add($"contenido ({porcentajeContenido}%)");
            if (!cumpleProfundidad) fallos.Add($"profundidad ({porcentajeProfundidad}%)");
            if (!cumpleHoras) fallos.Add($"horas ({porcentajeHoras}%)");
            
            resultado.Justificacion = $"No alcanza el umbral del {umbral}% en: {string.Join(", ", fallos)}.";
        }

        return resultado;
    }
}