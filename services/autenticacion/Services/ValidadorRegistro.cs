using System.Net.Mail;
using HomologaUTA.Autenticacion.DTOs;

namespace HomologaUTA.Autenticacion.Services;

/// <summary>Valida los datos de un registro. Devuelve el mensaje del primer problema o null si todo está bien.</summary>
public static class ValidadorRegistro
{
    public const int NombreMaximo = 100;
    public const int CorreoMaximo = 150;
    public const int ContrasenaMinima = 8;
    public const int ContrasenaMaxima = 64;

    public static string? Validar(RegistroRequest request)
    {
        var nombre = request.Nombre?.Trim();
        if (string.IsNullOrEmpty(nombre))
            return "El nombre es obligatorio.";
        if (nombre.Length > NombreMaximo)
            return $"El nombre no puede pasar de {NombreMaximo} caracteres.";

        var correo = request.Correo?.Trim();
        if (string.IsNullOrEmpty(correo))
            return "El correo es obligatorio.";
        if (correo.Length > CorreoMaximo || !EsCorreoValido(correo))
            return "El correo no tiene un formato válido.";

        return ValidarContrasena(request.Contrasena);
    }

    /// <summary>Indica exactamente qué requisitos le faltan a la contraseña.</summary>
    public static string? ValidarContrasena(string? contrasena)
    {
        var valor = contrasena ?? string.Empty;

        if (valor.Length > ContrasenaMaxima)
            return $"La contraseña no puede pasar de {ContrasenaMaxima} caracteres.";

        var faltan = new List<string>();
        if (valor.Length < ContrasenaMinima) faltan.Add($"al menos {ContrasenaMinima} caracteres");
        if (!valor.Any(char.IsUpper)) faltan.Add("una letra mayúscula");
        if (!valor.Any(char.IsLower)) faltan.Add("una letra minúscula");
        if (!valor.Any(char.IsDigit)) faltan.Add("un número");

        return faltan.Count == 0
            ? null
            : $"La contraseña no cumple los requisitos. Falta: {string.Join(", ", faltan)}.";
    }

    private static bool EsCorreoValido(string correo)
    {
        if (!MailAddress.TryCreate(correo, out var direccion) || direccion.Address != correo)
            return false;

        var dominio = correo[(correo.LastIndexOf('@') + 1)..];
        return dominio.Contains('.');
    }
}