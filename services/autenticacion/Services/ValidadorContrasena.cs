namespace HomologaUTA.Autenticacion.Services;

/// <summary>
/// Reglas mínimas de contraseña para el registro (HU-12): longitud, mayúscula, minúscula y dígito.
/// Devuelve un mensaje que indica exactamente qué requisito falta.
/// </summary>
public static class ValidadorContrasena
{
    public const int LongitudMinima = 8;

    public static string? Validar(string? contrasena)
    {
        if (string.IsNullOrWhiteSpace(contrasena))
            return "La contraseña es obligatoria.";

        if (contrasena.Length < LongitudMinima)
            return $"La contraseña debe tener al menos {LongitudMinima} caracteres.";

        if (!contrasena.Any(char.IsUpper))
            return "La contraseña debe incluir al menos una letra mayúscula.";

        if (!contrasena.Any(char.IsLower))
            return "La contraseña debe incluir al menos una letra minúscula.";

        if (!contrasena.Any(char.IsDigit))
            return "La contraseña debe incluir al menos un número.";

        return null;
    }
}
