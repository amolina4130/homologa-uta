namespace HomologaUTA.Homologacion.Models;

/// <summary>Estados de la delegación de una solicitud a un docente.</summary>
public static class EstadoDelegacionDocente
{
    public const string PENDIENTE = "PENDIENTE";
    public const string REVISADO = "REVISADO";
}

/// <summary>
/// Asignación de una solicitud a un docente para su revisión. El Coordinador puede delegar
/// hasta dos docentes por solicitud; el primero que califica deja constancia de su revisión.
/// </summary>
public class DelegacionDocente
{
    public int Id { get; set; }

    public int SolicitudId { get; set; }

    public string DocenteCorreo { get; set; } = string.Empty;

    /// <summary>Coordinador que realizó la delegación; se muestra al docente.</summary>
    public string CoordinadorCorreo { get; set; } = string.Empty;

    public DateTime FechaDelegacion { get; set; } = DateTime.UtcNow;

    public string Estado { get; set; } = EstadoDelegacionDocente.PENDIENTE;

    public DateTime? FechaRevision { get; set; }

    public string? InformeTexto { get; set; }

    /// <summary>JSON con las evaluaciones por asignatura enviadas por el docente.</summary>
    public string? EvaluacionesJson { get; set; }

    /// <summary>
    /// Borrador de la sugerencia generada por IA. Se guarda antes de enviar el informe para que
    /// el docente pueda revisarlo, agregar observaciones y decidir manualmente. Otros docentes
    /// asignados pueden verlo; no habilita el informe final hasta que se envíe (REVISADO).
    /// </summary>
    public string? SugerenciaJson { get; set; }

    /// <summary>Fecha en que se generó por última vez la sugerencia (aún no enviada).</summary>
    public DateTime? FechaSugerencia { get; set; }

    /// <summary>Nombre legible del docente; se inicializa con su correo cuando no se conoce.</summary>
    public string? NombreDocente { get; set; }

    /// <summary>Cédula del docente tal como debe imprimirse en su informe.</summary>
    public string? DocenteCedula { get; set; }

    /// <summary>Correo del docente para el informe; puede diferir del correo de acceso.</summary>
    public string? DocenteCorreoInforme { get; set; }

    /// <summary>Documentación adicional que el docente pidió al Coordinador (null si no pidió).</summary>
    public string? DocumentacionSolicitada { get; set; }

    public DateTime? FechaSolicitudDocumentacion { get; set; }
}
