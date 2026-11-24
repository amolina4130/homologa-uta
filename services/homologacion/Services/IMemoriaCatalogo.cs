using HomologaUTA.Homologacion.DTOs;

namespace HomologaUTA.Homologacion.Services;

// Stub temporal: se reemplaza por el cliente HTTP real del Catálogo cuando esté listo.
public class MemoriaCatalogoStub : IMemoriaCatalogo
{
    private static readonly List<CandidataDestinoDto> Datos = new()
    {
        new(1, "Física I", "Física", 1),
        new(2, "Física II", "Física", 2),
        new(3, "Física III", "Física", 3),
        new(4, "Fundamentos de Programación", "Programación", 1),
        new(5, "Programación Orientada a Objetos", "Programación", 2),
        new(6, "Estructuras de Datos", "Programación", 3),
        new(7, "Cálculo Diferencial", "Matemáticas", 1),
        new(8, "Cálculo Integral", "Matemáticas", 2),
        new(9, "Bases de Datos", "Bases de Datos", 4),
    };

    public Task<bool> EstaCargadaAsync(CancellationToken ct = default) => Task.FromResult(true);

    public Task<IReadOnlyList<CandidataDestinoDto>> ObtenerAsignaturasAsync(
        int carreraId, int nivelMaximo, CancellationToken ct = default)
    {
        IReadOnlyList<CandidataDestinoDto> r = Datos.Where(a => a.Nivel <= nivelMaximo).ToList();
        return Task.FromResult(r);
    }
}