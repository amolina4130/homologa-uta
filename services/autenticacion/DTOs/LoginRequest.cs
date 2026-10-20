using System.ComponentModel.DataAnnotations;

namespace HomologaUTA.Autenticacion.DTOs;

public class LoginRequest
{
    [Required]
    public string Correo { get; set; } = string.Empty;

    [Required]
    public string Contrasena { get; set; } = string.Empty;
}