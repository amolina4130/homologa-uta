namespace HomologaUTA.Autenticacion.DTOs;

/// <summary>Datos del formulario de registro (HU-12). El rol no se elige: siempre es Estudiante.</summary>
public class RegistroRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Contrasena { get; set; } = string.Empty;
}

/// <summary>Cuenta creada; nunca incluye la contraseña ni su hash.</summary>
public class RegistroResponse
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Rol { get; set; } = "Estudiante";
}
