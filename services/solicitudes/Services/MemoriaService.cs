using System.Security.Cryptography;
using System.Text;
using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.Models.Memoria;
using HomologaUTA.Solicitudes.Services;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Solicitudes.Services;

/// <summary>
/// Carga única de la memoria relacional del agente (HU-19). Calcula una huella de los datos
/// de origen; si no cambió no reescribe nada y, si cambió, actualiza solo lo que difiere.
/// </summary>
public class MemoriaService
{
    private readonly SolicitudesDbContext _db;

    public MemoriaService(SolicitudesDbContext db)
    {
        _db = db;
    }

    public async Task<ResultadoCargaMemoria> CargarAsync(CancellationToken ct = default)
    {
        var carreras = await _db.Carreras.AsNoTracking().ToListAsync(ct);
        var asignaturas = await _db.Asignaturas.AsNoTracking().ToListAsync(ct);
        var planes = await _db.PlanesAnaliticos.AsNoTracking().ToDictionaryAsync(p => p.AsignaturaId, ct);

        var huella = CalcularHuella(carreras, asignaturas, planes);

        var estado = await _db.MemoriaEstados.FirstOrDefaultAsync(ct);
        var yaCargada = await _db.MemoriaAsignaturas.ToListAsync(ct);

        if (estado is not null && estado.Huella == huella && yaCargada.Count > 0)
        {
            return new ResultadoCargaMemoria
            {
                Cargadas = 0,
                SinCambios = true,
                Huella = huella
            };
        }

        var porClave = yaCargada.ToDictionary(m => (m.CarreraId, m.AsignaturaId));
        var cargadas = 0;

        foreach (var asignatura in asignaturas)
        {
            var carrera = carreras.FirstOrDefault(c => c.Id == asignatura.CarreraId);
            if (carrera is null)
                continue;

            planes.TryGetValue(asignatura.Id, out var plan);
            var contenidos = string.IsNullOrWhiteSpace(plan?.Contenido)
                ? $"Contenidos mínimos de {asignatura.Nombre}."
                : plan!.Contenido;

            var area = string.IsNullOrWhiteSpace(asignatura.Area)
                ? AreaAsignatura.Detectar(asignatura.Nombre)
                : asignatura.Area;

            if (porClave.TryGetValue((asignatura.CarreraId, asignatura.Id), out var fila))
            {
                var cambio = fila.Nombre != asignatura.Nombre
                             || fila.Nivel != asignatura.Nivel
                             || fila.NivelNumerico != AreaAsignatura.NivelNumerico(asignatura.Nivel)
                             || fila.Area != area
                             || fila.Creditos != asignatura.Creditos
                             || fila.Horas != asignatura.Horas
                             || fila.ContenidosMinimos != contenidos
                             || fila.Carrera != carrera.Nombre;

                if (!cambio)
                    continue;

                fila.Carrera = carrera.Nombre;
                fila.Nombre = asignatura.Nombre;
                fila.Nivel = asignatura.Nivel;
                fila.NivelNumerico = AreaAsignatura.NivelNumerico(asignatura.Nivel);
                fila.Area = area;
                fila.Creditos = asignatura.Creditos;
                fila.Horas = asignatura.Horas;
                fila.ContenidosMinimos = contenidos;
                fila.FechaActualizacion = DateTime.UtcNow;
            }
            else
            {
                _db.MemoriaAsignaturas.Add(new MemoriaAsignatura
                {
                    CarreraId = asignatura.CarreraId,
                    Carrera = carrera.Nombre,
                    AsignaturaId = asignatura.Id,
                    Nombre = asignatura.Nombre,
                    Nivel = asignatura.Nivel,
                    NivelNumerico = AreaAsignatura.NivelNumerico(asignatura.Nivel),
                    Area = area,
                    Creditos = asignatura.Creditos,
                    Horas = asignatura.Horas,
                    ContenidosMinimos = contenidos,
                    FechaActualizacion = DateTime.UtcNow
                });
            }

            cargadas++;
        }

        if (estado is null)
        {
            estado = new MemoriaEstado();
            _db.MemoriaEstados.Add(estado);
        }

        estado.Huella = huella;
        estado.FechaCarga = DateTime.UtcNow;
        estado.TotalCarreras = carreras.Count;
        estado.TotalAsignaturas = asignaturas.Count;

        await _db.SaveChangesAsync(ct);

        return new ResultadoCargaMemoria
        {
            Cargadas = cargadas,
            SinCambios = false,
            Huella = huella
        };
    }

    private static string CalcularHuella(
        IReadOnlyList<Models.Carrera> carreras,
        IReadOnlyList<Models.Asignatura> asignaturas,
        IReadOnlyDictionary<int, Models.PlanAnalitico> planes)
    {
        var sb = new StringBuilder();
        foreach (var c in carreras.OrderBy(c => c.Id))
            sb.Append($"C{c.Id}|{c.Nombre}\n");
        foreach (var a in asignaturas.OrderBy(a => a.Id))
        {
            planes.TryGetValue(a.Id, out var plan);
            sb.Append($"A{a.Id}|{a.CarreraId}|{a.Nombre}|{a.Nivel}|{a.Creditos}|{a.Horas}|{a.Area}|{plan?.Contenido}\n");
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public class ResultadoCargaMemoria
{
    public int Cargadas { get; set; }
    public bool SinCambios { get; set; }
    public string Huella { get; set; } = string.Empty;
}
