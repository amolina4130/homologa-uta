using HomologaUTA.Homologacion.Models;

namespace HomologaUTA.Homologacion.DTOs;

public record MateriaOrigenDto(string Nombre, decimal? Nota);

public record ResultadoAsignaturaDto(
    int AsignaturaDestinoId,
    bool Combinada,
    List<MateriaOrigenDto> MateriasOrigen,
    decimal PorcentajeContenido,
    decimal PorcentajeProfundidad,
    decimal PorcentajeCargaHoraria,
    string Decision,
    string Justificacion);

public static class ResultadoAsignaturaMapper
{
    // AJUSTAR: los nombres de las propiedades a los de su ResultadoAsignatura
    public static ResultadoAsignaturaDto ADto(ResultadoAsignatura r) => new(
        r.AsignaturaDestinoId,
        r.Combinada,
        r.MateriasOrigen.Select(m => new MateriaOrigenDto(m.Nombre, m.Nota)).ToList(),
        r.PorcentajeContenido,
        r.PorcentajeProfundidad,
        r.PorcentajeCargaHoraria,
        r.Decision,
        r.Justificacion);
public DbSet<MateriaOrigenResultado> MateriasOrigenResultado => Set<MateriaOrigenResultado>();
}
