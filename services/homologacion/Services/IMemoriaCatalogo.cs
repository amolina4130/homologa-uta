using HomologaUTA.Homologacion.DTOs;

namespace HomologaUTA.Homologacion.Services;

// Stub temporal: se reemplaza por el cliente HTTP real del Catálogo cuando esté listo.
public class MemoriaCatalogoStub : IMemoriaCatalogo
{
    private static readonly List<CandidataDestinoDto> Datos = new()
    {
        new() { AsignaturaId = 1, Nombre = "Física I", Area = "Física", Nivel = "1", NivelNumerico = 1 },
        new() { AsignaturaId = 2, Nombre = "Física II", Area = "Física", Nivel = "2", NivelNumerico = 2 },
        new() { AsignaturaId = 3, Nombre = "Física III", Area = "Física", Nivel = "3", NivelNumerico = 3 },
        new() { AsignaturaId = 4, Nombre = "Fundamentos de Programación", Area = "Programación", Nivel = "1", NivelNumerico = 1 },
        new() { AsignaturaId = 5, Nombre = "Programación Orientada a Objetos", Area = "Programación", Nivel = "2", NivelNumerico = 2 },
        new() { AsignaturaId = 6, Nombre = "Estructuras de Datos", Area = "Programación", Nivel = "3", NivelNumerico = 3 },
        new() { AsignaturaId = 7, Nombre = "Cálculo Diferencial", Area = "Matemáticas", Nivel = "1", NivelNumerico = 1 },
        new() { AsignaturaId = 8, Nombre = "Cálculo Integral", Area = "Matemáticas", Nivel = "2", NivelNumerico = 2 },
        new() { AsignaturaId = 9, Nombre = "Bases de Datos", Area = "Bases de Datos", Nivel = "4", NivelNumerico = 4 },
    };

    public Task<bool> EstaCargadaAsync(CancellationToken ct = default) => Task.FromResult(true);

    public Task<IReadOnlyList<CandidataDestinoDto>> ObtenerAsignaturasAsync(
        int carreraId, int nivelMaximo, CancellationToken ct = default)
    {
        IReadOnlyList<CandidataDestinoDto> r = Datos.Where(a => a.NivelNumerico <= nivelMaximo).ToList();
        return Task.FromResult(r);
    }
}