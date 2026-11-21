namespace HomologaUTA.Autenticacion.Models;

public class Usuario
{
    public int Id { get; set; }

    /// <summary>Nombre de la persona. Queda vacío en los usuarios semilla.</summary>
    public string Nombre { get; set; } = string.Empty;

    public string Correo { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int RolId { get; set; }
    public Rol Rol { get; set; } = null!;
}