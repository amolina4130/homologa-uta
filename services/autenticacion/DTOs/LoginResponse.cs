namespace HomologaUTA.Autenticacion.DTOs;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Cedula { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
}

/// <summary>Datos editables del perfil del usuario autenticado.</summary>
public class PerfilRequest
{
    public string? Nombre { get; set; }
    public string? Tipo { get; set; }
    public string? Titulo { get; set; }
    public string? Cedula { get; set; }
    public string? Telefono { get; set; }
}

/// <summary>Perfil guardado, sin datos sensibles.</summary>
public class PerfilResponse
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Cedula { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
}