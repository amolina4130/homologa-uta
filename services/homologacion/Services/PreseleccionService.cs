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

    /// <summary>Una entrada por materia aprobada y candidata, más las combinaciones como alternativa.</summary>
    public List<AsignaturaAnalisisDto> EntradasIndividuales { get; set; } = new();
    public List<MateriaSinCandidataDto> SinCandidata { get; set; } = new();
}

/// <summary>
/// Reduce la carga que se envía al agente (HU-21): para cada materia aprobada del estudiante
/// busca en la memoria del agente las asignaturas de la carrera de destino del mismo área y
/// nivel. Una materia sin candidata queda en <c>sinCandidata</c> y no se envía al agente.
/// </summary>
public class PreseleccionService
{
    /// <summary>Valor que Solicitudes envía cuando no hay equivalencia en su catálogo.</summary>
    public const string SinEquivalenteEnMallaDestino = "Sin equivalente en la malla destino";

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
        // Solo cuentan las materias APROBADAS: el récord también puede traer reprobadas o retiradas.
        materias = materias.Where(m => ReglasHomologacion.EstaAprobada(m.EstadoSegunDocumento)).ToList();

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
        return Construir(info, materias, destino, await ObtenerMallaOrigenAsync(info, ct));
    }

    /// <summary>
    /// Agente interno: la carrera de origen también es de FISEI, así que su malla y sus sílabos
    /// están en el sistema. De ahí salen el contenido, el semestre, las horas y los créditos de
    /// cada materia aprobada (no se deducen del último semestre cursado). En externo, vacía.
    /// </summary>
    private async Task<List<MemoriaAsignaturaDto>> ObtenerMallaOrigenAsync(SolicitudCompletaDto info, CancellationToken ct)
    {
        if (EsExterno(info) || info.CarreraOrigenId <= 0) return new List<MemoriaAsignaturaDto>();
        try
        {
            return await _memoria.ObtenerAsignaturasAsync(info.CarreraOrigenId, null, ct);
        }
        catch (MemoriaNoDisponibleException ex)
        {
            _logger.LogWarning(ex, "No se pudo leer la malla de origen de la solicitud {SolicitudId}", info.Id);
            return new List<MemoriaAsignaturaDto>();
        }
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
        // Solo cuentan las materias APROBADAS: el récord también puede traer reprobadas o retiradas.
        materias = materias.Where(m => ReglasHomologacion.EstaAprobada(m.EstadoSegunDocumento)).ToList();

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

        var mallaOrigen = await ObtenerMallaOrigenAsync(info, ct);
        var preseleccion = Construir(info, materias, destino, mallaOrigen);
        var porMateria = materias
            .GroupBy(m => m.Nombre.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Se agrupa por asignatura de destino: si dos materias aprobadas son candidatas para el
        // mismo destino, el agente las recibe juntas y puede proponer la combinación (HU-22).
        var grupos = new Dictionary<int, GrupoPreseleccion>();

        foreach (var par in preseleccion.Pares)
        {
            var origen = info.Asignaturas.FirstOrDefault(a =>
                string.Equals(a.AsignaturaOrigen?.Trim(), par.MateriaOrigen.Trim(), StringComparison.OrdinalIgnoreCase));
            var enMalla = BuscarEnMalla(mallaOrigen, par.MateriaOrigen);
            porMateria.TryGetValue(par.MateriaOrigen.Trim(), out var aprobada);

            var silabo = enMalla is not null && !string.IsNullOrWhiteSpace(enMalla.ContenidosMinimos)
                ? enMalla.ContenidosMinimos
                : null;
            var contenidoOrigen = silabo
                ?? (string.IsNullOrWhiteSpace(origen?.ContenidoOrigen) ? null : origen!.ContenidoOrigen)
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
                    Nivel = !string.IsNullOrWhiteSpace(enMalla?.Nivel) ? enMalla!.Nivel : origen?.NivelOrigen,
                    Nota = par.Nota,
                    Horas = enMalla?.Horas is > 0 ? enMalla.Horas : origen?.HorasOrigen is > 0 ? origen.HorasOrigen : null,
                    Creditos = enMalla?.Creditos is > 0 ? enMalla.Creditos : origen?.CreditosOrigen is > 0 ? origen.CreditosOrigen : null,
                    Resultados = string.IsNullOrWhiteSpace(enMalla?.ResultadosAprendizaje) ? null : enMalla!.ResultadosAprendizaje,
                    DeSilabo = silabo is not null,
                    Periodo = aprobada?.Periodo,
                    EstadoSegunRecord = aprobada?.EstadoSegunDocumento
                });
            }
        }

        AsignaturaAnalisisDto Entrada(GrupoPreseleccion grupo, List<OrigenPreseleccion> origenes)
        {

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
                // En una combinación se muestran los niveles de las dos materias ("Primero y Segundo").
                NivelOrigen = string.Join(" y ", origenes.Select(o => o.Nivel).Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)) is { Length: > 0 and <= 30 } niveles
                    ? niveles
                    : origenes.Select(o => o.Nivel).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)),
                NivelDestino = grupo.Candidata.Nivel,
                NotaOrigen = origenes.FirstOrDefault()?.Nota,
                MateriasOrigenPermitidas = origenes.Select(o => o.Nombre).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                NotasMateriasOrigen = origenes
                    .GroupBy(o => o.Nombre.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().Nota, StringComparer.OrdinalIgnoreCase),
                // Con dos materias de origen, horas y créditos se suman (cubren juntas el destino).
                HorasOrigen = origenes.All(o => o.Horas is > 0) ? origenes.Sum(o => o.Horas!.Value) : null,
                CreditosOrigen = origenes.All(o => o.Creditos is > 0) ? origenes.Sum(o => o.Creditos!.Value) : null,
                HorasDestino = grupo.Candidata.Horas > 0 ? grupo.Candidata.Horas : null,
                CreditosDestino = grupo.Candidata.Creditos > 0 ? grupo.Candidata.Creditos : null,
                ResultadosOrigen = origenes.Any(o => o.Resultados is not null)
                    ? string.Join("\n", origenes.Select(o => o.Resultados).Where(r => r is not null))
                    : null,
                ResultadosDestino = string.IsNullOrWhiteSpace(grupo.Candidata.ResultadosAprendizaje)
                    ? null
                    : grupo.Candidata.ResultadosAprendizaje,
                ContenidoOrigenDeSilabo = origenes.All(o => o.DeSilabo),
                // En una combinación manda la materia aprobada hace más tiempo (regla de los 10 años).
                PeriodoAprobacion = origenes.Select(o => o.Periodo).Where(p => !string.IsNullOrWhiteSpace(p))
                    .OrderBy(p => System.Text.RegularExpressions.Regex.Matches(p!, @"\b(19|20)\d{2}\b").Select(m => int.Parse(m.Value)).DefaultIfEmpty(9999).Max())
                    .FirstOrDefault(),
                EstadoSegunRecord = string.Join(" ", origenes.Select(o => o.EstadoSegunRecord).Where(e => !string.IsNullOrWhiteSpace(e))),
                EsCombinacion = origenes.Count > 1
            };
        }

        List<OrigenPreseleccion> Unicos(GrupoPreseleccion g) => g.Origenes
            .GroupBy(o => o.Nombre, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();

        // Agrupadas por destino (las usa el agente externo, que decide la combinación).
        var entradas = grupos.Values.Select(g => Entrada(g, Unicos(g))).ToList();

        // Agente interno: cada materia aprobada se compara SOLA con cada candidata; la combinación
        // de dos materias se agrega solo como alternativa (HU-22) y se descarta si no cumple o si
        // una materia sola ya cubre el destino.
        var individuales = grupos.Values
            .SelectMany(g =>
            {
                var unicos = Unicos(g);
                var lista = unicos.Select(o => Entrada(g, new List<OrigenPreseleccion> { o })).ToList();
                // Todas las parejas posibles: el análisis conserva solo la mejor combinación que
                // cumpla y en la que cada materia aporte por sí misma (DescartarCombinacionesInnecesarias).
                for (var i = 0; i < unicos.Count; i++)
                    for (var j = i + 1; j < unicos.Count; j++)
                        lista.Add(Entrada(g, new List<OrigenPreseleccion> { unicos[i], unicos[j] }));
                return lista;
            })
            .ToList();

        return new EntradasPreseleccion
        {
            UsaMateriasAprobadas = true,
            Entradas = entradas,
            EntradasIndividuales = individuales,
            SinCandidata = preseleccion.SinCandidata
        };
    }

    /// <summary>
    /// Resuelve el centinela "Sin equivalente en la malla destino" que envía Solicitudes (HU-14).
    /// Reutiliza la preselección por área y nivel (HU-21) contra la memoria de la carrera destino:
    /// si encuentra una candidata real, actualiza la entrada; si no, la quita de las entradas y la
    /// devuelve para que el análisis la deje en "Sin equivalente probable" con REQUIERE_REVISION.
    /// </summary>
    public async Task<List<MateriaSinCandidataDto>> ResolverSinEquivalenteAsync(
        SolicitudCompletaDto info,
        List<AsignaturaAnalisisDto> entradas,
        CancellationToken ct = default)
    {
        var pendientes = entradas
            .Where(e => string.Equals(
                e.AsignaturaDestino?.Trim(),
                SinEquivalenteEnMallaDestino,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (pendientes.Count == 0)
            return new List<MateriaSinCandidataDto>();

        List<MemoriaAsignaturaDto> destino;
        try
        {
            destino = await _memoria.ObtenerAsignaturasAsync(info.CarreraDestinoId, null, ct);
        }
        catch (Exception ex) when (ex is MemoriaSinCargarException or MemoriaNoDisponibleException)
        {
            _logger.LogWarning(
                "No se pudo resolver {Cantidad} asignaturas sin equivalencia contra la memoria: {Motivo}",
                pendientes.Count, ex.Message);
            destino = new List<MemoriaAsignaturaDto>();
        }

        var sinCandidata = new List<MateriaSinCandidataDto>();
        foreach (var entrada in pendientes)
        {
            var nombreOrigen = (entrada.AsignaturaOrigen ?? string.Empty).Trim();
            var candidata = destino.FirstOrDefault(d =>
                                 string.Equals(d.Nombre.Trim(), nombreOrigen, StringComparison.OrdinalIgnoreCase))
                             ?? PrimeraCandidata(info, nombreOrigen, destino);

            if (candidata is null)
            {
                sinCandidata.Add(new MateriaSinCandidataDto
                {
                    MateriaOrigen = nombreOrigen,
                    Motivo = $"No hay una asignatura candidata en la carrera de destino para {nombreOrigen}."
                });
                entradas.Remove(entrada);
                continue;
            }

            entrada.AsignaturaDestino = candidata.Nombre;
            entrada.AsignaturaDestinoId = candidata.AsignaturaId;
            entrada.ContenidoDestino = candidata.ContenidosMinimos;
            entrada.AreaDestino = candidata.Area;
            entrada.NivelDestino = candidata.Nivel;
            entrada.HorasDestino = candidata.Horas > 0 ? candidata.Horas : null;
            entrada.CreditosDestino = candidata.Creditos > 0 ? candidata.Creditos : null;
            entrada.ResultadosDestino = string.IsNullOrWhiteSpace(candidata.ResultadosAprendizaje)
                ? null
                : candidata.ResultadosAprendizaje;
        }

        return sinCandidata;
    }

    private MemoriaAsignaturaDto? PrimeraCandidata(
        SolicitudCompletaDto info, string nombre, List<MemoriaAsignaturaDto> destino)
    {
        var materia = new Models.MateriaAprobada { Nombre = nombre };
        var preseleccion = Construir(info, new List<Models.MateriaAprobada> { materia }, destino, new List<MemoriaAsignaturaDto>());
        var idCandidata = preseleccion.Pares
            .FirstOrDefault(p => string.Equals(p.MateriaOrigen.Trim(), nombre, StringComparison.OrdinalIgnoreCase))
            ?.CandidatasDestino.FirstOrDefault()?.AsignaturaId;

        return idCandidata is null ? null : destino.FirstOrDefault(d => d.AsignaturaId == idCandidata.Value);
    }

    private PreseleccionDto Construir(
        SolicitudCompletaDto info,
        List<Models.MateriaAprobada> materias,
        List<MemoriaAsignaturaDto> destino,
        List<MemoriaAsignaturaDto> mallaOrigen)
    {
        var resultado = new PreseleccionDto();

        foreach (var materia in materias)
        {
            var enMalla = BuscarEnMalla(mallaOrigen, materia.Nombre);
            // El área sale de la malla (misma clasificación que el destino) cuando la materia está en ella.
            var area = !string.IsNullOrWhiteSpace(enMalla?.Area) ? enMalla!.Area : _config.DetectarArea(materia.Nombre);
            var nivelMalla = NivelUtil.Numerico(enMalla?.Nivel);
            var nivel = nivelMalla > 0 ? nivelMalla : NivelDeLaMateria(info, materia.Nombre);

            // Primero las asignaturas de nombre equivalente (aunque cambie el área o el nivel); luego
            // las de la misma área y nivel cercano. Los destinos con plan genérico (sin plan analítico
            // real) no sirven para comparar y solo entran si el nombre coincide. En el agente externo
            // no se conoce el nivel de origen, así que se ofrecen más candidatas.
            var externo = string.Equals(info.Modulo, "EXTERNO", StringComparison.OrdinalIgnoreCase);
            var maximo = externo ? Math.Max(_config.MaxCandidatas, 6) : _config.MaxCandidatas;
            double Parecido(MemoriaAsignaturaDto d) => SimilitudNombre(materia.Nombre, d.Nombre);

            var candidatas = destino
                .Where(d => Parecido(d) >= 0.5 ||
                            (MismaArea(area, d.Area) &&
                             !Agents.ComparadorSilabos.EsPlanGenerico(d.ContenidosMinimos) &&
                             (nivel is null || d.Nivel is null ||
                              NivelUtil.Numerico(d.Nivel) <= nivel.Value + _config.MargenNivel)))
                .OrderByDescending(Parecido)
                .ThenBy(d => Math.Abs(NivelUtil.Numerico(d.Nivel) - (nivel ?? NivelUtil.Numerico(d.Nivel))))
                .ThenBy(d => d.Nombre)
                .Take(maximo)
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
                    ContenidosMinimos = c.ContenidosMinimos,
                    ResultadosAprendizaje = c.ResultadosAprendizaje,
                    Horas = c.Horas,
                    Creditos = c.Creditos
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

    private static bool EsExterno(SolicitudInfoDto info) =>
        string.Equals(info.Modulo?.Trim(), "EXTERNO", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Busca una materia del récord en la malla de origen. El récord suele traer el nombre en
    /// mayúsculas y sin tildes ("CALCULO DIFERENCIAL"), así que se compara normalizado.
    /// </summary>
    internal static MemoriaAsignaturaDto? BuscarEnMalla(List<MemoriaAsignaturaDto> malla, string nombre)
    {
        if (malla.Count == 0 || string.IsNullOrWhiteSpace(nombre)) return null;
        var buscado = ClaveNombre(nombre);
        var exacta = malla.FirstOrDefault(m => ClaveNombre(m.Nombre) == buscado);
        if (exacta is not null) return exacta;

        // Variantes menores ("Base de Datos" / "Bases de Datos", "Física" / "Física Básica").
        var tokens = buscado.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        return malla
            .Select(m => (m, Parecido: Parecido(tokens, ClaveNombre(m.Nombre).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet())))
            .Where(x => x.Parecido >= 0.75)
            .OrderByDescending(x => x.Parecido)
            .Select(x => x.m)
            .FirstOrDefault();
    }

    /// <summary>Parecido entre dos nombres de asignatura (0 a 1), sin tildes ni conectores.</summary>
    internal static double SimilitudNombre(string? a, string? b) =>
        Parecido(ClaveNombre(a ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(),
                 ClaveNombre(b ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet());

    private static double Parecido(HashSet<string> a, HashSet<string> b) =>
        a.Count == 0 || b.Count == 0 ? 0 : (double)a.Intersect(b).Count() / Math.Max(a.Count, b.Count);

    private static readonly HashSet<string> Conectores = new(StringComparer.Ordinal)
        { "de", "del", "la", "las", "el", "los", "y", "e", "en", "a", "para" };

    private static string ClaveNombre(string texto)
    {
        var descompuesto = (texto ?? string.Empty).Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in descompuesto)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return string.Join(' ', sb.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !Conectores.Contains(p))
            .Select(p => p.EndsWith('s') && p.Length > 4 ? p[..^1] : p));
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
    public int? Horas { get; set; }
    public int? Creditos { get; set; }
    public string? Resultados { get; set; }
    public bool DeSilabo { get; set; }
    public string? Periodo { get; set; }
    public string? EstadoSegunRecord { get; set; }
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
