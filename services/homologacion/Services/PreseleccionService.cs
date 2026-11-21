using System.Globalization;
using System.Text;
using HomologaUTA.Homologacion.DTOs;
using Microsoft.Extensions.Options;

namespace HomologaUTA.Homologacion.Services;

public class PreseleccionException : Exception
{
    public PreseleccionException(string mensaje) : base(mensaje) { }
}

public interface IPreseleccionService
{
    Task<ResultadoPreseleccionDto> PreseleccionarAsync(
        IReadOnlyList<MateriaAprobadaDto> materias,
        int carreraDestinoId,
        int nivelAprobado,
        CancellationToken ct = default);
}

public class PreseleccionService : IPreseleccionService
{
    private readonly IMemoriaCatalogo _memoria;
    private readonly PreseleccionOptions _opciones;

    public PreseleccionService(IMemoriaCatalogo memoria, IOptionsSnapshot<PreseleccionOptions> opciones)
    {
        _memoria = memoria;
        _opciones = opciones.Value;
    }

    public async Task<ResultadoPreseleccionDto> PreseleccionarAsync(
        IReadOnlyList<MateriaAprobadaDto> materias,
        int carreraDestinoId,
        int nivelAprobado,
        CancellationToken ct = default)
    {
        if (materias.Count == 0)
            throw new PreseleccionException("La solicitud no tiene materias aprobadas.");

        if (!await _memoria.EstaCargadaAsync(ct))
            throw new PreseleccionException("La memoria del catálogo no está cargada.");

        var nivelMaximo = nivelAprobado + _opciones.MargenNivel;
        var asignaturas = await _memoria.ObtenerAsignaturasAsync(carreraDestinoId, nivelMaximo, ct);

        var pares = new List<ParPreseleccionDto>();
        var sinCandidata = new List<SinCandidataDto>();

        foreach (var materia in materias)
        {
            var area = DetectarArea(materia.Nombre);
            if (area is null)
            {
                sinCandidata.Add(new(materia.Nombre, "No se pudo determinar el área de la materia."));
                continue;
            }

            var candidatas = asignaturas
                .Where(a => Normalizar(a.Area) == Normalizar(area))
                .OrderBy(a => Math.Abs(a.Nivel - nivelAprobado))
                .ThenBy(a => a.Nombre)
                .Take(_opciones.MaxCandidatas)
                .ToList();

            if (candidatas.Count == 0)
                sinCandidata.Add(new(materia.Nombre,
                    $"No hay asignaturas del área {area} hasta el nivel {nivelMaximo} en la carrera destino."));
            else
                pares.Add(new(materia.Nombre, candidatas));
        }

        return new ResultadoPreseleccionDto(pares, sinCandidata);
    }

    private string? DetectarArea(string nombreMateria)
    {
        var nombre = Normalizar(nombreMateria);
        string? mejor = null;
        var mejorPuntaje = 0;

        foreach (var (area, palabras) in _opciones.Areas)
        {
            var puntaje = palabras.Count(p => nombre.Contains(Normalizar(p)));
            if (puntaje > mejorPuntaje)
            {
                mejorPuntaje = puntaje;
                mejor = area;
            }
        }
        return mejor;
    }

    private static string Normalizar(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in descompuesto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().ToLowerInvariant().Trim();
    }
}