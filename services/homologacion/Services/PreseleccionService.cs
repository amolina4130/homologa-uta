using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Services;

/// <summary>
/// Entradas que se envían al agente después de la preselección. Si el estudiante aún no tiene
/// materias aprobadas, <see cref="UsaMateriasAprobadas"/> queda en false y el análisis conserva
/// el flujo por las asignaturas de la solicitud.
/// </summary>
public class EntradasPreseleccion
{
    public bool UsaMateriasAprobadas { get; set; }
    public List<AsignaturaAnalisisDto> Entradas { get; set; } = new();
    public List<MateriaSinCandidataDto> SinCandidata { get; set; } = new();
}

/// <summary>
/// Reduce la carga que se envía al agente (HU-21): para cada materia aprobada del estudiante
/// busca en la memoria del agente las asignaturas de la carrera de destino del mismo área y
/// nivel. Una materia sin candidata queda en <c>sinCandidata</c> y no se envía al agente.
/// </summary>
public class PreseleccionService
{
    private readonly HomologacionDbContext _db;
    private readonly SolicitudesClient _solicitudes;
    private readonly IMemoriaClient _memoria;
    private readonly PreseleccionConfig _config;
    private readonly ILogger<PreseleccionService> _logger;

    public PreseleccionService(
        HomologacionDbContext db,
        SolicitudesClient solicitudes,
        IMemoriaClient memoria,
        PreseleccionConfig config,
        ILogger<PreseleccionService> logger)
    {
        _db = db;
        _solicitudes = solicitudes;
        _memoria = memoria;
        _config = config;
        _logger = logger;
    }

    /// <summary>Preselección visible por API (HU-21).</summary>
    /// <exception cref="AnalisisSolicitudException">404 sin solicitud, 409 sin materias o sin memoria.</exception>
    public async Task<PreseleccionDto> ObtenerPreseleccionAsync(int solicitudId, CancellationToken ct = default)
    {
        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct)
                   ?? throw new AnalisisSolicitudException(404, "Solicitud no encontrada.");

        var materias = await _db.MateriasAprobadas.AsNoTracking()
            .Where(m => m.SolicitudId == solicitudId)
            .OrderBy(m => m.Id)
            .ToListAsync(ct);

        if (materias.Count == 0)
            throw new AnalisisSolicitudException(409,
                "La solicitud no tiene materias aprobadas. Extraiga primero el récord académico.");

        MemoriaEstadoDto estado;
        try
        {
            estado = await _memoria.VerificarAsync(ct);
        }
        catch (MemoriaSinCargarException)
        {
            throw new AnalisisSolicitudException(409, "Memoria del agente sin cargar");
        }

        _logger.LogInformation(
            "Preselección de la solicitud {SolicitudId} con la huella de memoria {Huella}",
            solicitudId, estado.Huella);

        var destino = await _memoria.ObtenerAsignaturasAsync(info.CarreraDestinoId, null, ct);
        return Construir(info, materias, destino);
    }

    /// <summary>true si el estudiante ya tiene materias aprobadas extraídas del récord (HU-20).</summary>
    public async Task<bool> TieneMateriasAprobadasAsync(int solicitudId, CancellationToken ct = default) =>
        await _db.MateriasAprobadas.AsNoTracking().AnyAsync(m => m.SolicitudId == solicitudId, ct);

    /// <summary>
    /// Convierte la preselección en entradas para el agente. Solo se envían las candidatas.
    /// La lista de materias aprobadas viaja como materias permitidas para la combinación (HU-22).
    /// </summary>
    public async Task<EntradasPreseleccion> ConstruirEntradasAsync(
        int solicitudId, CancellationToken ct = default)
    {
        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct)
                   ?? throw new AnalisisSolicitudException(404, "Solicitud no encontrada.");

        var materias = await _db.MateriasAprobadas.AsNoTracking()
            .Where(m => m.SolicitudId == solicitudId)
            .OrderBy(m => m.Id)
            .ToListAsync(ct);

        if (materias.Count == 0)
            return new EntradasPreseleccion { UsaMateriasAprobadas = false };

        MemoriaEstadoDto estado;
        try
        {
            estado = await _memoria.VerificarAsync(ct);
        }
        catch (MemoriaSinCargarException)
        {
            throw new AnalisisSolicitudException(409, "Memoria del agente sin cargar");
        }

        _logger.LogInformation(
            "El análisis de {SolicitudId} usa la memoria con huella {Huella}", solicitudId, estado.Huella);

        var destino = await _memoria.ObtenerAsignaturasAsync(info.CarreraDestinoId, null, ct);
        var preseleccion = Construir(info, materias, destino);

        // Se agrupa por asignatura de destino: si dos materias aprobadas son candidatas para el
        // mismo destino, el agente las recibe juntas y puede proponer la combinación (HU-22).
        var grupos = new Dictionary<int, GrupoPreseleccion>();

        foreach (var par in preseleccion.Pares)
        {
            var origen = info.Asignaturas.FirstOrDefault(a =>
                string.Equals(a.AsignaturaOrigen?.Trim(), par.MateriaOrigen.Trim(), StringComparison.OrdinalIgnoreCase));

            var contenidoOrigen = origen?.ContenidoOrigen
                ?? $"Materia aprobada {par.MateriaOrigen}" +
                   (par.Nota.HasValue ? $" con nota {par.Nota:0.##}" : string.Empty) +
                   (string.IsNullOrWhiteSpace(par.Area) ? "." : $". Área: {par.Area}.");

            foreach (var candidata in par.CandidatasDestino)
            {
                if (!grupos.TryGetValue(candidata.AsignaturaId, out var grupo))
                {
                    grupo = new GrupoPreseleccion
                    {
                        Candidata = candidata,
                        Origenes = new List<OrigenPreseleccion>()
                    };
                    grupos[candidata.AsignaturaId] = grupo;
                }

                grupo.Origenes.Add(new OrigenPreseleccion
                {
                    Nombre = par.MateriaOrigen,
                    Contenido = contenidoOrigen,
                    Area = par.Area,
                    Nivel = origen?.NivelOrigen,
                    Nota = par.Nota
                });
            }
        }

        var entradas = grupos.Values.Select(grupo =>
        {
            var origenes = grupo.Origenes
                .GroupBy(o => o.Nombre, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            var areas = origenes
                .Select(o => o.Area)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new AsignaturaAnalisisDto
            {
                AsignaturaOrigen = origenes.Count == 1
                    ? origenes[0].Nombre
                    : string.Join(" + ", origenes.Select(o => o.Nombre)),
                ContenidoOrigen = string.Join("\n", origenes.Select(o => $"{o.Nombre}: {o.Contenido}")),
                AsignaturaDestino = grupo.Candidata.Nombre,
                ContenidoDestino = grupo.Candidata.ContenidosMinimos,
                AsignaturaDestinoId = grupo.Candidata.AsignaturaId,
                AreaOrigen = areas.Count == 1 ? areas[0] : null,
                AreaDestino = grupo.Candidata.Area,
                NivelOrigen = origenes.Select(o => o.Nivel).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)),
                NivelDestino = grupo.Candidata.Nivel,
                NotaOrigen = origenes.FirstOrDefault()?.Nota,
                MateriasOrigenPermitidas = origenes.Select(o => o.Nombre).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                NotasMateriasOrigen = origenes
                    .GroupBy(o => o.Nombre.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().Nota, StringComparer.OrdinalIgnoreCase)
            };
        }).ToList();

        return new EntradasPreseleccion
        {
            UsaMateriasAprobadas = true,
            Entradas = entradas,
            SinCandidata = preseleccion.SinCandidata
        };
    }

    private PreseleccionDto Construir(
        SolicitudCompletaDto info,
        List<Models.MateriaAprobada> materias,
        List<MemoriaAsignaturaDto> destino)
    {
        var resultado = new PreseleccionDto();

        foreach (var materia in materias)
        {
            var area = _config.DetectarArea(materia.Nombre);
            var nivel = NivelDeLaMateria(info, materia.Nombre);

            var candidatas = destino
                .Where(d => MismaArea(area, d.Area))
                .Where(d => nivel is null
                            || d.Nivel is null
                            || NivelUtil.Numerico(d.Nivel) <= nivel.Value + _config.MargenNivel)
                .OrderBy(d => Math.Abs(NivelUtil.Numerico(d.Nivel) - (nivel ?? NivelUtil.Numerico(d.Nivel))))
                .ThenBy(d => d.Nombre)
                .Take(_config.MaxCandidatas)
                .ToList();

            if (candidatas.Count == 0)
            {
                resultado.SinCandidata.Add(new MateriaSinCandidataDto
                {
                    MateriaOrigen = materia.Nombre,
                    Motivo = $"No hay asignaturas del área {area} hasta el nivel " +
                             $"{(nivel.HasValue ? nivel.Value + _config.MargenNivel : "máximo")} en la carrera de destino."
                });
                continue;
            }

            resultado.Pares.Add(new ParPreseleccionDto
            {
                MateriaOrigen = materia.Nombre,
                Nota = materia.Nota,
                Area = area,
                Nivel = nivel,
                CandidatasDestino = candidatas.Select(c => new CandidataDestinoDto
                {
                    AsignaturaId = c.AsignaturaId,
                    Nombre = c.Nombre,
                    Area = c.Area,
                    Nivel = c.Nivel,
                    NivelNumerico = NivelUtil.Numerico(c.Nivel),
                    ContenidosMinimos = c.ContenidosMinimos
                }).ToList()
            });
        }

        return resultado;
    }

    /// <summary>Busca el nivel de la materia aprobada en las asignaturas de la solicitud.</summary>
    private static int? NivelDeLaMateria(SolicitudCompletaDto info, string nombre)
    {
        var origen = info.Asignaturas.FirstOrDefault(a =>
            string.Equals(a.AsignaturaOrigen?.Trim(), nombre.Trim(), StringComparison.OrdinalIgnoreCase));

        if (origen is null)
            return null;

        var nivel = NivelUtil.Numerico(origen.NivelOrigen);
        return nivel > 0 ? nivel : null;
    }

    private static bool MismaArea(string areaOrigen, string areaDestino) =>
        !string.IsNullOrWhiteSpace(areaOrigen)
        && !string.IsNullOrWhiteSpace(areaDestino)
        && string.Equals(areaOrigen.Trim(), areaDestino.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <summary>Un destino con todas las materias aprobadas que podrían cubrirlo (HU-22).</summary>
internal class GrupoPreseleccion
{
    public CandidataDestinoDto Candidata { get; set; } = null!;
    public List<OrigenPreseleccion> Origenes { get; set; } = new();
}

/// <summary>Materia aprobada que es candidata para un destino.</summary>
internal class OrigenPreseleccion
{
    public string Nombre { get; set; } = string.Empty;
    public string? Contenido { get; set; }
    public string? Area { get; set; }
    public string? Nivel { get; set; }
    public decimal? Nota { get; set; }
}

/// <summary>Configuración de la preselección: tabla de áreas, margen de nivel y máximo de candidatas.</summary>
public class PreseleccionConfig
{
    public int MaxCandidatas { get; set; } = 3;
    public int MargenNivel { get; set; } = 2;
    public List<AreaConfig> Areas { get; set; } = new();

    public string DetectarArea(string nombre)
    {
        var texto = (nombre ?? string.Empty).ToLowerInvariant();

        foreach (var area in Areas)
        {
            if (area.PalabrasClave is not null &&
                area.PalabrasClave.Any(p => !string.IsNullOrWhiteSpace(p) &&
                                            texto.Contains(p.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)))
            {
                return area.Area;
            }
        }

        return NivelUtil.DetectarAreaPorDefecto(nombre ?? string.Empty);
    }
}

public class AreaConfig
{
    public string Area { get; set; } = string.Empty;
    public List<string> PalabrasClave { get; set; } = new();
}

/// <summary>Utilidades de nivel y clasificación por área con los valores por defecto del sistema.</summary>
public static class NivelUtil
{
    public static int Numerico(string? nivel)
    {
        var texto = (nivel ?? string.Empty).ToLowerInvariant();

        var mapa = new Dictionary<string, int>
        {
            ["primero"] = 1,
            ["segundo"] = 2,
            ["tercero"] = 3,
            ["cuarto"] = 4,
            ["quinto"] = 5,
            ["sexto"] = 6,
            ["septimo"] = 7,
            ["séptimo"] = 7,
            ["octavo"] = 8,
            ["noveno"] = 9,
            ["decimo"] = 10,
            ["décimo"] = 10
        };

        foreach (var par in mapa)
        {
            if (texto.Contains(par.Key, StringComparison.OrdinalIgnoreCase))
                return par.Value;
        }

        return int.TryParse(texto, out var n) ? n : 0;
    }

    public static string DetectarAreaPorDefecto(string nombre)
    {
        var texto = (nombre ?? string.Empty).ToLowerInvariant();

        var reglas = new (string Area, string[] Palabras)[]
        {
            ("Matemáticas", new[] { "algebra", "álgebra", "calculo", "cálculo", "matemat", "estad", "numeric", "numéric" }),
            ("Física", new[] { "fisica", "física", "mecanica", "mecánica", "electric", "eléctric", "optic", "óptic", "termodinam" }),
            ("Programación", new[] { "programaci", "algoritmo", "software", "datos", "base de datos", "computaci", "aplicaciones", "web", "redes", "sistemas operativos" }),
            ("Química", new[] { "quimica", "química" }),
            ("Biología", new[] { "biolog", "biológic" }),
            ("Electrónica", new[] { "electronic", "electrónic", "circuit", "control", "automatiz", "robot", "telecomunicacion", "telecomunicación", "señal", "senal" }),
            ("Industrial", new[] { "industrial", "produccion", "producción", "calidad", "gestion de proyectos", "gestión de proyectos", "economic", "económic", "emprendimiento" }),
            ("Humanidades", new[] { "realidad nacional", "metodolog", "investigaci", "legislaci", "comunicaci", "lenguaje", "ingles", "inglés" })
        };

        foreach (var (area, palabras) in reglas)
        {
            if (palabras.Any(p => texto.Contains(p, StringComparison.OrdinalIgnoreCase)))
                return area;
        }

        return "General";
    }
}
