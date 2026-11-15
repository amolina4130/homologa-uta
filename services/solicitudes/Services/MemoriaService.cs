using System.Security.Cryptography;
using System.Text;
using HomologaUTA.Solicitudes.Data;
using HomologaUTA.Solicitudes.DTOs;
using HomologaUTA.Solicitudes.Models.Memoria;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Solicitudes.Services;

/// <summary>
/// Memoria del agente (HU-19). La carga calcula una huella de los datos de origen (mallas + planes),
/// no reescribe nada si la huella no cambió y actualiza solo lo que difiere.
/// </summary>
public class MemoriaService
{
    private readonly SolicitudesDbContext _db;

    public MemoriaService(SolicitudesDbContext db)
    {
        _db = db;
    }

    public async Task<MemoriaCargaResponse> CargarAsync(CancellationToken ct = default)
    {
        var carreras = await _db.Carreras.AsNoTracking().OrderBy(c => c.Id).ToListAsync(ct);
        var asignaturas = await _db.Asignaturas.AsNoTracking().OrderBy(a => a.Id).ToListAsync(ct);
        var planes = await _db.PlanesAnaliticos.AsNoTracking()
            .ToDictionaryAsync(p => p.AsignaturaId, ct);

        // 1. Materializar los datos de origen y calcular la huella
        var origen = asignaturas.Select(a =>
        {
            var carrera = carreras.FirstOrDefault(c => c.Id == a.CarreraId);
            var contenidos = planes.TryGetValue(a.Id, out var plan) && !string.IsNullOrWhiteSpace(plan.Contenido)
                ? plan.Contenido
                : $"{a.Nombre} - {a.Nivel}";
            return new
            {
                a.CarreraId,
                Carrera = carrera?.Nombre ?? string.Empty,
                a.Id,
                a.Nombre,
                Nivel = AreaAsignatura.NivelNumerico(a.Nivel),
                Area = AreaAsignatura.Detectar(a.Nombre),
                a.Creditos,
                a.Horas,
                Contenidos = contenidos
            };
        }).ToList();

        var huella = CalcularHuella(carreras.Select(c => $"{c.Id}|{c.Nombre}"), origen.Select(o =>
            $"{o.CarreraId}|{o.Id}|{o.Nombre}|{o.Nivel}|{o.Area}|{o.Creditos}|{o.Horas}|{o.Contenidos}"));

        var estado = await _db.MemoriaEstados.FirstOrDefaultAsync(ct);
        if (estado is not null && estado.Huella == huella && estado.TotalAsignaturas == origen.Count)
        {
            return new MemoriaCargaResponse { Cargadas = 0, SinCambios = true, Huella = huella };
        }

        // 2. Actualizar solo lo que difiere
        var existentes = await _db.MemoriaAsignaturas.ToListAsync(ct);
        var porClave = existentes.ToDictionary(x => (x.CarreraId, x.AsignaturaId));
        var cargadas = 0;

        foreach (var o in origen)
        {
            if (porClave.TryGetValue((o.CarreraId, o.Id), out var fila))
            {
                var cambio = fila.Nombre != o.Nombre || fila.Nivel != o.Nivel || fila.Area != o.Area
                    || fila.Creditos != o.Creditos || fila.Horas != o.Horas
                    || fila.ContenidosMinimos != o.Contenidos;
                if (!cambio) continue;

                fila.Nombre = o.Nombre;
                fila.Nivel = o.Nivel;
                fila.Area = o.Area;
                fila.Creditos = o.Creditos;
                fila.Horas = o.Horas;
                fila.ContenidosMinimos = o.Contenidos;
                cargadas++;
            }
            else
            {
                _db.MemoriaAsignaturas.Add(new MemoriaAsignatura
                {
                    CarreraId = o.CarreraId,
                    AsignaturaId = o.Id,
                    Nombre = o.Nombre,
                    Nivel = o.Nivel,
                    Area = o.Area,
                    Creditos = o.Creditos,
                    Horas = o.Horas,
                    ContenidosMinimos = o.Contenidos
                });
                cargadas++;
            }
        }

        // Quitar filas que ya no existen en el origen
        var clavesOrigen = origen.Select(o => (o.CarreraId, o.Id)).ToHashSet();
        var sobrantes = existentes.Where(x => !clavesOrigen.Contains((x.CarreraId, x.AsignaturaId))).ToList();
        _db.MemoriaAsignaturas.RemoveRange(sobrantes);

        if (estado is null)
        {
            estado = new MemoriaEstado();
            _db.MemoriaEstados.Add(estado);
        }
        estado.Huella = huella;
        estado.FechaCarga = DateTime.UtcNow;
        estado.TotalCarreras = carreras.Count;
        estado.TotalAsignaturas = origen.Count;

        await _db.SaveChangesAsync(ct);

        return new MemoriaCargaResponse { Cargadas = cargadas, SinCambios = false, Huella = huella };
    }

    public async Task<MemoriaEstadoDto?> ObtenerEstadoAsync(CancellationToken ct = default)
    {
        var estado = await _db.MemoriaEstados.AsNoTracking().FirstOrDefaultAsync(ct);
        if (estado is null) return null;

        return new MemoriaEstadoDto
        {
            Huella = estado.Huella,
            FechaCarga = estado.FechaCarga,
            TotalCarreras = estado.TotalCarreras,
            TotalAsignaturas = estado.TotalAsignaturas
        };
    }

    public async Task<List<MemoriaAsignaturaDto>> ObtenerAsignaturasAsync(
        int carreraId, int? nivelMaximo, CancellationToken ct = default)
    {
        var query = _db.MemoriaAsignaturas.AsNoTracking().Where(a => a.CarreraId == carreraId);
        if (nivelMaximo.HasValue)
            query = query.Where(a => a.Nivel <= nivelMaximo.Value);

        return await query
            .OrderBy(a => a.Nivel).ThenBy(a => a.Nombre)
            .Select(a => new MemoriaAsignaturaDto
            {
                AsignaturaId = a.AsignaturaId,
                Nombre = a.Nombre,
                Nivel = a.Nivel,
                Area = a.Area,
                Creditos = a.Creditos,
                Horas = a.Horas,
                ContenidosMinimos = a.ContenidosMinimos
            })
            .ToListAsync(ct);
    }

    private static string CalcularHuella(IEnumerable<string> carreras, IEnumerable<string> asignaturas)
    {
        var texto = string.Join("\n", carreras) + "\n--\n" + string.Join("\n", asignaturas);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(texto));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
