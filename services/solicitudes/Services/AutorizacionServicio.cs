using System.Security.Claims;

namespace HomologaUTA.Solicitudes.Services;

/// <summary>
/// Comprobación de rol en el propio microservicio. El Gateway ya aplica la política, pero así
/// el servicio también responde 403 si se llama directamente con un token de otro rol.
/// Sin token (llamadas internas o de desarrollo) se permite.
/// </summary>
public static class AutorizacionServicio
{
    public static bool EsAdministrador(ClaimsPrincipal? user)
    {
        var identidades = user?.Identities?.Where(i => i.IsAuthenticated).ToList();
        if (identidades is null || identidades.Count == 0)
            return true;

        var u = user!;
        return u.FindFirstValue("rol")?.Equals("ADMINISTRADOR", StringComparison.OrdinalIgnoreCase) == true
               || u.IsInRole("ADMINISTRADOR");    }
}
