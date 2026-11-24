using System.Security.Claims;

namespace HomologaUTA.Homologacion.Services;

/// <summary>
/// Comprobación de rol y de propiedad en el propio microservicio (HU-17/HU-18/HU-20).
/// El Gateway ya aplica la política, pero así el servicio también responde 403 si se llama
/// directamente con un token de otro rol. Sin token (llamadas internas o de desarrollo) se
/// permite, igual que en el servicio de Solicitudes.
/// </summary>
public static class AutorizacionHomologacion
{
    public static bool HayIdentidad(ClaimsPrincipal? user) =>
        user?.Identities?.Any(i => i.IsAuthenticated) == true;

    /// <summary>Correo del token; con MapInboundClaims=false llega como claim "email".</summary>
    public static string? Correo(ClaimsPrincipal? user) =>
        user?.FindFirstValue(ClaimTypes.Email)
        ?? user?.FindFirstValue("email")
        ?? user?.FindFirstValue("correo");

    public static string? Rol(ClaimsPrincipal? user) =>
        user?.FindFirstValue("rol") ?? user?.FindFirstValue(ClaimTypes.Role);

    /// <summary>
    /// true si el usuario tiene alguno de los roles. Sin identidad autenticada devuelve true:
    /// las demás capas (Gateway) aplican la política real y las llamadas internas no traen token.
    /// </summary>
    public static bool EsRol(ClaimsPrincipal? user, params string[] roles)
    {
        if (!HayIdentidad(user))
            return true;

        var actual = Rol(user);
        return actual is not null &&
               roles.Any(r => string.Equals(r, actual, StringComparison.OrdinalIgnoreCase));
    }

    public static bool EsCoordinadorOAdministrador(ClaimsPrincipal? user) =>
        EsRol(user, "COORDINADOR", "ADMINISTRADOR");

    public static bool EsComiteOAdministrador(ClaimsPrincipal? user) =>
        EsRol(user, "COMITE", "ADMINISTRADOR");

    public static bool EsDocenteOAdministrador(ClaimsPrincipal? user) =>
        EsRol(user, "DOCENTE", "ADMINISTRADOR");

    /// <summary>
    /// La resolución la registra el Coordinador. El Consejo Directivo es solo destinatario
    /// del informe final y no usuario del sistema.
    /// </summary>
    public static bool PuedeRegistrarResolucion(ClaimsPrincipal? user) =>
        EsRol(user, "COORDINADOR", "ADMINISTRADOR");

    /// <summary>Quienes pueden revisar expedientes: Comité, Coordinador y Docentes.</summary>
    public static bool EsGestor(ClaimsPrincipal? user) =>
        EsRol(user, "COMITE", "COORDINADOR", "DOCENTE", "ADMINISTRADOR");

    /// <summary>
    /// El Comité sube los documentos; los roles que revisan los expedientes pueden acceder
    /// a cualquiera. Sin delegación, el docente solo ve las solicitudes asignadas.
    /// </summary>
    public static bool PuedeAccederSolicitud(ClaimsPrincipal? user, string? correoDueno)
    {
        if (!HayIdentidad(user))
            return true;

        if (EsGestor(user))
            return true;

        var correo = Correo(user);
        return correo is not null &&
               string.Equals(correo, correoDueno, StringComparison.OrdinalIgnoreCase);
    }
}
