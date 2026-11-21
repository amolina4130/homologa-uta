using HomologaUTA.Homologacion.DTOs;

namespace HomologaUTA.Homologacion.Services;

public interface IMemoriaCatalogo
{
    Task<bool> EstaCargadaAsync(CancellationToken ct = default);

    /// <summary>Asignaturas de la carrera hasta un nivel máximo (GET /memoria/carreras/{id}/asignaturas?nivelMaximo=n)</summary>
    Task<IReadOnlyList<CandidataDestinoDto>> ObtenerAsignaturasAsync(
        int carreraId, int nivelMaximo, CancellationToken ct = default);
}