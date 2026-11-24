namespace HomologaUTA.Autenticacion.Models;

public class Usuario
{
    public int Id { get; set; }

    /// <summary>Nombre visible del usuario; el registro lo exige (HU-12).</summary>
    public string Nombre { get; set; } = string.Empty;

    public string Correo { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int RolId { get; set; }
    public Rol Rol { get; set; } = null!;

    // Perfil editable por el usuario. No cambia el correo de acceso ni el rol.
    public string Tipo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Cedula { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;

}