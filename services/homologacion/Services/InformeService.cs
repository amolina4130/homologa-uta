using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Models;
using HomologaUTA.Homologacion.Reportes;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Services;

/// <summary>Genera el informe PDF de homologación (HU-18).</summary>
public interface IInformeService
{
    /// <summary>Devuelve los bytes del PDF de la solicitud.</summary>
    /// <exception cref="InformeException">404 si la solicitud o su análisis no existen.</exception>
    Task<byte[]> GenerarPdfAsync(int solicitudId, CancellationToken ct = default);
}

/// <summary>
/// Arma los datos del informe con la solicitud real, el análisis del agente, la decisión del
/// Coordinador y, si ya existe, la resolución del Consejo Directivo. La generación del PDF la
/// hace <see cref="PlantillaInforme"/> para que el controlador solo devuelva los bytes.
/// </summary>
public class InformeService : IInformeService
{
    private readonly HomologacionDbContext _db;
    private readonly SolicitudesClient _solicitudes;
    private readonly ILogger<InformeService> _logger;

    public InformeService(
        HomologacionDbContext db,
        SolicitudesClient solicitudes,
        ILogger<InformeService> logger)
    {
        _db = db;
        _solicitudes = solicitudes;
        _logger = logger;
    }

    public async Task<byte[]> GenerarPdfAsync(int solicitudId, CancellationToken ct = default)
    {
        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct)
                   ?? throw new InformeException(404, "Solicitud no encontrada.");

        var analisis = await _db.AnalisisSolicitudes
            .AsNoTracking()
            .Include(a => a.Asignaturas)
                .ThenInclude(a => a.MateriasOrigen)
            .Include(a => a.Resolucion!)
                .ThenInclude(r => r.Decisiones)
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct)
            ?? throw new InformeException(404,
                "La solicitud todavía no tiene un análisis generado; no se puede emitir el informe.");

        var datos = ConstruirDatos(info, analisis);

        try
        {
            return PlantillaInforme.Generar(datos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo generar el PDF del informe de la solicitud {SolicitudId}", solicitudId);
            throw new InformeException(500, "No se pudo generar el informe PDF. Intente nuevamente.");
        }
    }

    private static InformeData ConstruirDatos(SolicitudCompletaDto info, AnalisisSolicitud analisis)
    {
        var finales = analisis.Resolucion?.Decisiones
            .ToDictionary(d => d.AsignaturaId) ?? new Dictionary<int, DecisionResolucion>();

        var asignaturas = analisis.Asignaturas
            .OrderBy(a => a.Id)
            .Select(a =>
            {
                finales.TryGetValue(a.Id, out var decision);

                return new InformeAsignatura
                {
                    AsignaturaOrigen = a.AsignaturaOrigen,
                    AsignaturaDestino = a.AsignaturaDestino,
                    Combinada = a.Combinada,
                    MateriasOrigen = a.MateriasOrigen.Select(m => m.Nombre).ToList(),
                    PorcentajeContenido = a.PorcentajeContenido,
                    PorcentajeProfundidad = a.PorcentajeProfundidad,
                    PorcentajeHoras = a.PorcentajeHoras,
                    Decision = a.Decision,
                    Justificacion = a.Justificacion,
                    DecisionCoordinador = a.DecisionCoordinador,
                    ObservacionCoordinador = a.ObservacionCoordinador,
                    DecisionFinal = decision?.DecisionFinal
                };
            })
            .ToList();

        InformeResolucion? resolucion = null;
        if (analisis.Resolucion is not null)
        {
            resolucion = new InformeResolucion
            {
                NumeroResolucion = analisis.Resolucion.NumeroResolucion,
                Fecha = analisis.Resolucion.Fecha,
                Observacion = analisis.Resolucion.Observacion,
                RegistradoPor = analisis.Resolucion.RegistradoPor,
                Decisiones = analisis.Resolucion.Decisiones
                    .OrderBy(d => d.Id)
                    .Select(d =>
                    {
                        var asignatura = analisis.Asignaturas.FirstOrDefault(a => a.Id == d.AsignaturaId);
                        return new InformeDecisionFinal
                        {
                            Asignatura = DescripcionAsignatura(asignatura),
                            Decision = d.DecisionFinal
                        };
                    })
                    .ToList()
            };
        }

        return new InformeData
        {
            SolicitudId = info.Id,
            NumeroProceso = string.IsNullOrWhiteSpace(info.NumeroProceso)
                ? $"SOL-{info.Id:D4}"
                : info.NumeroProceso,
            Estudiante = CoordinadorService.NombreEstudiante(info),
            Tipo = info.Tipo,
            Modulo = info.Modulo,
            CarreraOrigen = info.CarreraOrigen,
            CarreraDestino = info.CarreraDestino,
            UniversidadOrigen = info.UniversidadOrigen,
            UniversidadDestino = info.UniversidadDestino,
            FechaRegistro = info.FechaRegistro,
            FechaAnalisis = analisis.FechaAnalisis,
            FechaInforme = analisis.FechaInforme,
            CoordinadorCorreo = analisis.CoordinadorCorreo,
            Estado = analisis.Estado,
            Asignaturas = asignaturas,
            Resolucion = resolucion
        };
    }

    private static string DescripcionAsignatura(AnalisisAsignatura? asignatura)
    {
        if (asignatura is null)
            return "-";

        if (asignatura.Combinada && asignatura.MateriasOrigen.Count > 0)
            return $"{string.Join(" + ", asignatura.MateriasOrigen.Select(m => m.Nombre))} → {asignatura.AsignaturaDestino}";

        return asignatura.AsignaturaDestino;
    }
}
