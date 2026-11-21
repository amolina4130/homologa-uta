namespace HomologaUTA.Homologacion.DTOs;

public record MateriaAprobadaDto(
    int Id,
    string Nombre,
    decimal? Nota,
    string EstadoSegunDocumento,
    string? Periodo);