namespace HomologaUTA.Homologacion.DTOs;

/// <summary>Estado de una asignatura después de aplicar las reglas objetivas.</summary>
public static class EstadoValidacion
{
    /// <summary>Cumple todas las reglas objetivas: pasa al agente.</summary>
    public const string LISTA_PARA_AGENTE = "LISTA_PARA_AGENTE";

    /// <summary>Se deriva a validación de conocimientos (RN-01).</summary>
    public const string DERIVADA = "DERIVADA";

    /// <summary>Se excluye de la homologación (RN-02, RN-04 o RN-05).</summary>
    public const string EXCLUIDA = "EXCLUIDA";

    /// <summary>Falta documentación o un requisito (RN-06).</summary>
    public const string CON_FALTANTES = "CON_FALTANTES";
}

/// <summary>Resultado de validar una solicitud: resumen, reglas de la solicitud y detalle por asignatura.</summary>
public class ResultadoValidacionSolicitudDto
{
    public int SolicitudId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public ResumenValidacionDto Resumen { get; set; } = new();

    /// <summary>Reglas que se evalúan sobre toda la solicitud (RN-05 y RN-06).</summary>
    public List<ValidacionGeneralDto> ValidacionesGenerales { get; set; } = new();

    public List<ResultadoAsignaturaDto> Asignaturas { get; set; } = new();
}

public class ResumenValidacionDto
{
    public int Total { get; set; }
    public int ListasParaAgente { get; set; }
    public int Derivadas { get; set; }
    public int Excluidas { get; set; }
    public int ConFaltantes { get; set; }
}

public class ValidacionGeneralDto
{
    /// <summary>Regla evaluada, por ejemplo "RN-06".</summary>
    public string Regla { get; set; } = string.Empty;
    public bool Cumple { get; set; }

    /// <summary>Explica qué requisito o documento falta cuando no se cumple.</summary>
    public string Detalle { get; set; } = string.Empty;
}

public class ResultadoAsignaturaDto
{
    public int? Id { get; set; }
    public string Asignatura { get; set; } = string.Empty;

    /// <summary>LISTA_PARA_AGENTE, DERIVADA, EXCLUIDA o CON_FALTANTES.</summary>
    public string Estado { get; set; } = string.Empty;

    /// <summary>Regla que determina el estado ("Ninguna" si queda lista para el agente).</summary>
    public string ReglaAplicada { get; set; } = string.Empty;

    public string Detalle { get; set; } = string.Empty;
}