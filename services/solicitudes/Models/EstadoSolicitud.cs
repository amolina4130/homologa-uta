namespace HomologaUTA.Solicitudes.Models;

public static class EstadoSolicitud
{
    public const string REGISTRADA = "REGISTRADA";
    public const string PENDIENTE_REVISION = "PENDIENTE_REVISION";
    public const string APROBADA = "APROBADA";
    public const string RECHAZADA = "RECHAZADA";

    /// <summary>El Consejo Directivo ya registró su resolución (HU-17).</summary>
    public const string RESUELTA = "RESUELTA";

    /// <summary>Estados en los que el estudiante todavía puede editar su solicitud.</summary>
    public static bool EsEditable(string estado) => estado == REGISTRADA;
}
