namespace HomologaUTA.Homologacion.DTOs;

public record CandidataDestinoDto(int AsignaturaId, string Nombre, string Area, int Nivel);

public record ParPreseleccionDto(string MateriaOrigen, List<CandidataDestinoDto> CandidatasDestino);

public record SinCandidataDto(string MateriaOrigen, string Motivo);

public record ResultadoPreseleccionDto(List<ParPreseleccionDto> Pares, List<SinCandidataDto> SinCandidata);