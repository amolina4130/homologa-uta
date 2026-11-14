namespace HomologaUTA.Homologacion.Models;

/// <summary>
/// Materia de origen que cubre una asignatura de destino (HU-22). Normalmente hay una;
/// cuando la asignatura es combinada hay dos.
/// </summary>
public class MateriaOrigenResultado
{
    public int Id { get; set; }

    public int AnalisisAsignaturaId { get; set; }
    public AnalisisAsignatura AnalisisAsignatura { get; set; } = null!;

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Nota de la materia aprobada, si se conoce.</summary>
    public decimal? Nota { get; set; }
}
