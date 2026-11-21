using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HomologaUTA.Catalogo.Data;
using HomologaUTA.Catalogo.Models.Memoria;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Catalogo.Services;

/// <summary>Resultado de POST /memoria/carga.</summary>
/// <param name="Cargadas">Asignaturas creadas, actualizadas o eliminadas en esta carga.</param>
/// <param name="SinCambios">true si la huella no cambió y no se modificó nada.</param>
/// <param name="Huella">SHA-256 de los datos de origen.</param>
public record ResultadoCarga(int Cargadas, bool SinCambios, string Huella);

/// <summary>Resultado de GET /memoria/estado.</summary>
public record EstadoMemoria(string Huella, DateTime FechaCarga, int TotalCarreras, int TotalAsignaturas);

/// <summary>Asignatura de la memoria, tal como la consume el agente.</summary>
public record AsignaturaMemoriaDto(
    int AsignaturaId, string Nombre, string Nivel, string Area, int Creditos, int Horas,
    List<string> ContenidosMinimos);

/// <summary>Los datos de origen no se pueden cargar (por ejemplo, un nivel desconocido).</summary>
public class MemoriaCargaException : Exception
{
    public MemoriaCargaException(string mensaje) : base(mensaje) { }
}

internal record MallaCarrera(int Id, string Nombre);
internal record MallaAsignatura(int Id, int CarreraId, string Nombre, string Nivel, int Creditos, int Horas);

/// <summary>
/// Memoria del agente: carga única desde las mallas (HU-03) y los planes analíticos (HU-13),
/// con huella para no reescribir nada si los datos de origen no cambiaron.
/// </summary>
public class MemoriaService
{
    private static readonly SemaphoreSlim Cerrojo = new(1, 1);

    private static readonly JsonSerializerOptions OpcionesJson = new() { PropertyNameCaseInsensitive = true };

    private static readonly Dictionary<string, int> NivelesConocidos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Primero"] = 1, ["Segundo"] = 2, ["Tercero"] = 3, ["Cuarto"] = 4, ["Quinto"] = 5,
        ["Sexto"] = 6, ["Séptimo"] = 7, ["Octavo"] = 8, ["Noveno"] = 9, ["Décimo"] = 10
    };

    private readonly CatalogoDbContext _db;

    public MemoriaService(CatalogoDbContext db)
    {
        _db = db;
    }

    // ---------------------------------------------------------------
    // Carga
    // ---------------------------------------------------------------

    public async Task<ResultadoCarga> CargarAsync(CancellationToken ct = default)
    {
        // Una sola carga a la vez
        await Cerrojo.WaitAsync(ct);
        try
        {
            var origen = await LeerOrigenAsync(ct);
            var huella = CalcularHuella(origen);

            var ultima = await _db.MemoriaCargas.OrderByDescending(c => c.Id).FirstOrDefaultAsync(ct);
            if (ultima is not null && ultima.Huella == huella)
                return new ResultadoCarga(0, true, huella);

            var (cargadas, totalAsignaturas) = await AplicarAsync(origen, ct);

            _db.MemoriaCargas.Add(new CargaMemoria
            {
                Huella = huella,
                FechaCarga = DateTime.UtcNow,
                TotalCarreras = origen.Carreras.Count,
                TotalAsignaturas = totalAsignaturas
            });

            // Todo o nada: datos y huella se guardan en la misma transacción
            await _db.SaveChangesAsync(ct);
            return new ResultadoCarga(cargadas, false, huella);
        }
        finally
        {
            Cerrojo.Release();
        }
    }

    private sealed record Origen(List<MallaCarrera> Carreras, List<MallaAsignatura> Asignaturas, Dictionary<int, string> Planes);

    private async Task<Origen> LeerOrigenAsync(CancellationToken ct)
    {
        var carreras = LeerRecurso<List<MallaCarrera>>("Mallas.carreras.json");
        var asignaturas = LeerRecurso<List<MallaAsignatura>>("Mallas.asignaturas.json");

        var planes = await _db.PlanesAnaliticos.AsNoTracking()
            .Where(p => p.EsVigente)
            .Select(p => new { p.AsignaturaId, p.Contenido })
            .ToListAsync(ct);

        return new Origen(carreras, asignaturas, planes.ToDictionary(p => p.AsignaturaId, p => p.Contenido));
    }

    private static T LeerRecurso<T>(string nombre)
    {
        using var stream = typeof(MemoriaService).Assembly.GetManifestResourceStream(nombre)
            ?? throw new MemoriaCargaException($"No se encontró el recurso de mallas '{nombre}'.");

        // detecta el BOM UTF-8 que traen estos JSON
        using var lector = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return JsonSerializer.Deserialize<T>(lector.ReadToEnd(), OpcionesJson)
            ?? throw new MemoriaCargaException($"El recurso de mallas '{nombre}' está vacío.");
    }

    // La huella se calcula sobre el contenido ya normalizado (ordenado, sin espacios sobrantes ni
    // saltos de línea de Windows), así que reformatear los archivos no la cambia.
    private static string CalcularHuella(Origen o)
    {
        var sb = new StringBuilder();
        sb.Append("areas:v").Append(ClasificadorArea.Version).Append('\n');

        foreach (var c in o.Carreras.OrderBy(c => c.Id))
            sb.Append("C|").Append(c.Id).Append('|').Append(c.Nombre.Trim()).Append('\n');

        foreach (var a in o.Asignaturas.OrderBy(a => a.Id))
            sb.Append("A|").Append(a.Id).Append('|').Append(a.CarreraId).Append('|')
              .Append(a.Nombre.Trim()).Append('|').Append(a.Nivel.Trim()).Append('|')
              .Append(a.Creditos).Append('|').Append(a.Horas).Append('\n');

        foreach (var p in o.Planes.OrderBy(p => p.Key))
            sb.Append("P|").Append(p.Key).Append('|').Append(Sha256(p.Value.Replace("\r\n", "\n"))).Append('\n');

        return Sha256(sb.ToString());
    }

    private static string Sha256(string texto) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto))).ToLowerInvariant();

    private static int NumeroDeNivel(string nombre)
    {
        if (NivelesConocidos.TryGetValue(nombre, out var numero)) return numero;
        throw new MemoriaCargaException($"Nivel desconocido en las mallas: '{nombre}'.");
    }

    // Actualiza solo lo que difiere. Devuelve las asignaturas tocadas y el total de la memoria.
    private async Task<(int Cargadas, int TotalAsignaturas)> AplicarAsync(Origen origen, CancellationToken ct)
    {
        var carrerasDb = await _db.MemoriaCarreras.Include(c => c.Niveles).ToListAsync(ct);
        var asignaturasDb = await _db.MemoriaAsignaturas.Include(a => a.ContenidosMinimos).ToListAsync(ct);
        var cargadas = 0;

        // 1) Carreras
        foreach (var c in origen.Carreras)
        {
            var nombre = c.Nombre.Trim();
            var carrera = carrerasDb.FirstOrDefault(x => x.Id == c.Id);
            if (carrera is null)
            {
                carrera = new CarreraMemoria { Id = c.Id, Nombre = nombre };
                _db.MemoriaCarreras.Add(carrera);
                carrerasDb.Add(carrera);
            }
            else if (carrera.Nombre != nombre)
            {
                carrera.Nombre = nombre;
            }
        }

        // Asignaturas de carreras que existen en la malla
        var idsCarreras = origen.Carreras.Select(c => c.Id).ToHashSet();
        var asignaturasOrigen = origen.Asignaturas.Where(a => idsCarreras.Contains(a.CarreraId)).ToList();

        // 2) Niveles
        foreach (var grupo in asignaturasOrigen.GroupBy(a => a.CarreraId))
        {
            var carrera = carrerasDb.First(c => c.Id == grupo.Key);
            foreach (var nombreNivel in grupo.Select(a => a.Nivel.Trim()).Distinct())
            {
                var numero = NumeroDeNivel(nombreNivel);
                var nivel = carrera.Niveles.FirstOrDefault(n => n.Numero == numero);
                if (nivel is null)
                    carrera.Niveles.Add(new NivelMemoria { Numero = numero, Nombre = nombreNivel });
                else if (nivel.Nombre != nombreNivel)
                    nivel.Nombre = nombreNivel;
            }
        }

        // 3) Asignaturas y contenidos mínimos
        foreach (var a in asignaturasOrigen)
        {
            var carrera = carrerasDb.First(c => c.Id == a.CarreraId);
            var nivel = carrera.Niveles.First(n => n.Numero == NumeroDeNivel(a.Nivel.Trim()));
            var nombre = a.Nombre.Trim();
            var area = ClasificadorArea.Clasificar(nombre);
            var contenidos = origen.Planes.TryGetValue(a.Id, out var plan)
                ? ExtraerContenidosMinimos(plan)
                : new List<string>();

            var actual = asignaturasDb.FirstOrDefault(x => x.Id == a.Id);
            if (actual is null)
            {
                actual = new AsignaturaMemoria
                {
                    Id = a.Id, Carrera = carrera, Nivel = nivel, Nombre = nombre,
                    Area = area, Creditos = a.Creditos, Horas = a.Horas
                };
                for (var i = 0; i < contenidos.Count; i++)
                    actual.ContenidosMinimos.Add(new ContenidoMinimo { Orden = i + 1, Descripcion = contenidos[i] });

                _db.MemoriaAsignaturas.Add(actual);
                asignaturasDb.Add(actual);
                cargadas++;
                continue;
            }

            var cambio = false;
            if (actual.CarreraId != carrera.Id) { actual.Carrera = carrera; cambio = true; }
            if (nivel.Id == 0 || actual.NivelId != nivel.Id) { actual.Nivel = nivel; cambio = true; }
            if (actual.Nombre != nombre) { actual.Nombre = nombre; cambio = true; }
            if (actual.Area != area) { actual.Area = area; cambio = true; }
            if (actual.Creditos != a.Creditos) { actual.Creditos = a.Creditos; cambio = true; }
            if (actual.Horas != a.Horas) { actual.Horas = a.Horas; cambio = true; }
            if (SincronizarContenidos(actual, contenidos)) cambio = true;

            if (cambio) cargadas++;
        }

        // 4) Lo que ya no está en el origen
        var idsAsignaturas = asignaturasOrigen.Select(a => a.Id).ToHashSet();
        foreach (var sobrante in asignaturasDb.Where(a => !idsAsignaturas.Contains(a.Id)).ToList())
        {
            _db.MemoriaAsignaturas.Remove(sobrante);
            cargadas++;
        }

        var nivelesUsados = asignaturasOrigen
            .Select(a => (a.CarreraId, Numero: NumeroDeNivel(a.Nivel.Trim())))
            .ToHashSet();
        foreach (var carrera in carrerasDb.ToList())
        {
            if (!idsCarreras.Contains(carrera.Id))
            {
                _db.MemoriaCarreras.Remove(carrera);
                continue;
            }
            foreach (var nivel in carrera.Niveles.Where(n => n.Id != 0 && !nivelesUsados.Contains((carrera.Id, n.Numero))).ToList())
                _db.MemoriaNiveles.Remove(nivel);
        }

        return (cargadas, asignaturasOrigen.Count);
    }

    // Reutiliza las filas existentes por posición: solo toca las que cambian.
    private bool SincronizarContenidos(AsignaturaMemoria asignatura, List<string> nuevos)
    {
        var actuales = asignatura.ContenidosMinimos.OrderBy(c => c.Orden).ToList();
        var cambio = false;

        for (var i = 0; i < nuevos.Count; i++)
        {
            if (i < actuales.Count)
            {
                if (actuales[i].Descripcion != nuevos[i])
                {
                    actuales[i].Descripcion = nuevos[i];
                    cambio = true;
                }
            }
            else
            {
                asignatura.ContenidosMinimos.Add(new ContenidoMinimo { Orden = i + 1, Descripcion = nuevos[i] });
                cambio = true;
            }
        }

        for (var i = nuevos.Count; i < actuales.Count; i++)
        {
            _db.MemoriaContenidosMinimos.Remove(actuales[i]);
            cambio = true;
        }

        return cambio;
    }

    /// <summary>
    /// Convierte el texto del plan analítico en una lista corta de contenidos mínimos: corta por
    /// saltos de línea, viñetas, punto y coma y fin de oración. Es una extracción heurística del texto.
    /// </summary>
    internal static List<string> ExtraerContenidosMinimos(string? contenido)
    {
        if (string.IsNullOrWhiteSpace(contenido)) return new List<string>();

        return Regex.Split(contenido, @"[\r\n;•·▪]+|(?<=\.)\s+")
            .Select(t => t.Trim().TrimStart('-', '*', '–', '—', ' ').Trim())
            .Where(t => t.Length >= 4)
            .Select(t => t.Length > 400 ? t[..400] : t)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(40)
            .ToList();
    }

    // ---------------------------------------------------------------
    // Consulta
    // ---------------------------------------------------------------

    /// <summary>Estado de la memoria; null si todavía no se ha cargado.</summary>
    public async Task<EstadoMemoria?> ObtenerEstadoAsync(CancellationToken ct = default)
    {
        var carga = await _db.MemoriaCargas.AsNoTracking().OrderByDescending(c => c.Id).FirstOrDefaultAsync(ct);
        if (carga is null) return null;

        var carreras = await _db.MemoriaCarreras.CountAsync(ct);
        var asignaturas = await _db.MemoriaAsignaturas.CountAsync(ct);
        return new EstadoMemoria(carga.Huella, carga.FechaCarga, carreras, asignaturas);
    }

    /// <summary>Asignaturas de una carrera hasta un nivel máximo; null si la carrera no está en la memoria.</summary>
    public async Task<List<AsignaturaMemoriaDto>?> ObtenerAsignaturasAsync(
        int carreraId, int? nivelMaximo, CancellationToken ct = default)
    {
        if (!await _db.MemoriaCarreras.AnyAsync(c => c.Id == carreraId, ct))
            return null;

        var consulta = _db.MemoriaAsignaturas.AsNoTracking().Where(a => a.CarreraId == carreraId);
        if (nivelMaximo.HasValue)
            consulta = consulta.Where(a => a.Nivel.Numero <= nivelMaximo.Value);

        var filas = await consulta
            .OrderBy(a => a.Nivel.Numero).ThenBy(a => a.Nombre)
            .Select(a => new
            {
                a.Id, a.Nombre, Nivel = a.Nivel.Nombre, a.Area, a.Creditos, a.Horas,
                Contenidos = a.ContenidosMinimos.OrderBy(c => c.Orden).Select(c => c.Descripcion).ToList()
            })
            .ToListAsync(ct);

        return filas
            .Select(f => new AsignaturaMemoriaDto(f.Id, f.Nombre, f.Nivel, f.Area, f.Creditos, f.Horas, f.Contenidos))
            .ToList();
    }
}
