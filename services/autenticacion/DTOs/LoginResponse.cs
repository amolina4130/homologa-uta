namespace HomologaUTA.Autenticacion.DTOs;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
}