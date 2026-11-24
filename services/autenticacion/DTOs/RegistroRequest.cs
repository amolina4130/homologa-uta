namespace HomologaUTA.Autenticacion.DTOs;

/// <summary>Datos del formulario de registro (HU-12). Solo se pueden crear cuentas de Comité o Docente.</summary>
public class RegistroRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Contrasena { get; set; } = string.Empty;

    /// <summary>Rol solicitado: COMITE (por defecto) o DOCENTE.</summary>
    public string? Rol { get; set; }
}

/// <summary>Cuenta creada; nunca incluye la contraseña ni su hash.</summary>
public class RegistroResponse
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Rol { get; set; } = "COMITE";
}
