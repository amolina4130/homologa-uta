namespace HomologaUTA.Solicitudes.Models;

public static class EstadoSolicitud
{
    public const string REGISTRADA = "REGISTRADA";
    public const string PENDIENTE_REVISION = "PENDIENTE_REVISION";

    /// <summary>El Coordinador ya emitió el informe técnico de la solicitud.</summary>
    public const string INFORME_EMITIDO = "INFORME_EMITIDO";

    public const string APROBADA = "APROBADA";
    public const string RECHAZADA = "RECHAZADA";
}