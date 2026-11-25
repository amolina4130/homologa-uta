namespace HomologaUTA.Autenticacion.DTOs;

/// <summary>Datos mínimos de un docente para elegirlo al delegar una solicitud.</summary>
public class DocenteResumen
{
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
}
