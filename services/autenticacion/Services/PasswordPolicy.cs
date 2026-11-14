namespace HomologaUTA.Autenticacion.Services;

/// <summary>
/// Requisitos mínimos de contraseña del registro (HU-12). Se validan en el backend y
/// se devuelve un mensaje que dice exactamente qué falta.
/// </summary>
public static class PasswordPolicy
{
    public const int LongitudMinima = 8;

    public static string? Validar(string? contrasena)
    {
        var valor = contrasena ?? string.Empty;

        if (valor.Length < LongitudMinima)
            return $"La contraseña debe tener al menos {LongitudMinima} caracteres.";
        if (!valor.Any(char.IsUpper))
            return "La contraseña debe incluir al menos una letra mayúscula.";
        if (!valor.Any(char.IsLower))
            return "La contraseña debe incluir al menos una letra minúscula.";
        if (!valor.Any(char.IsDigit))
            return "La contraseña debe incluir al menos un número.";

        return null;
    }
}
