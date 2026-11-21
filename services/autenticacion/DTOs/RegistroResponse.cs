namespace HomologaUTA.Autenticacion.DTOs;

/// <summary>Respuesta del registro de una cuenta. El rol siempre es Estudiante.</summary>
public class RegistroResponse
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Rol { get; set; } = "Estudiante";
}