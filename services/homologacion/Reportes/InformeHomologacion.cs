using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.Models;
using HomologaUTA.Homologacion.Services;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Reportes;

/// <summary>
/// Reúne los datos de la solicitud (servicio de Solicitudes) y de su análisis (base de Homologación)
/// y los entrega a la plantilla para generar el PDF.
/// </summary>
public class InformeHomologacion : IInformeService
{
    private readonly HomologacionDbContext _db;
    private readonly SolicitudesClient _solicitudes;

    public InformeHomologacion(HomologacionDbContext db, SolicitudesClient solicitudes)
    {
        _db = db;
        _solicitudes = solicitudes;
    }

    public async Task<byte[]> GenerarPdf(int solicitudId, CancellationToken ct = default)
    {
        var datos = await ReunirDatosAsync(solicitudId, ct);
        return PlantillaInforme.Generar(datos);
    }

    public async Task<InformeDatos> ReunirDatosAsync(int solicitudId, CancellationToken ct = default)
    {
        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct);
        if (info is null)
            throw new AnalisisSolicitudException(404, "Solicitud no encontrada.");

        var analisis = await _db.AnalisisSolicitudes
            .AsNoTracking()
            .Include(a => a.Asignaturas)
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct);

        if (analisis is null)
            throw new AnalisisSolicitudException(404, "La solicitud todavía no tiene un análisis generado.");

        return new InformeDatos
        {
            SolicitudId = solicitudId,
            NumeroProceso = $"SOL-{solicitudId:D4}",
            EstudianteCorreo = info.EstudianteCorreo,
            FechaRegistro = info.FechaRegistro,
            CarreraOrigen = info.CarreraOrigen,
            CarreraDestino = info.CarreraDestino,
            Modulo = info.Modulo,
            UniversidadOrigen = info.UniversidadOrigen,
            UniversidadDestino = info.UniversidadDestino,
            FechaAnalisis = analisis.FechaAnalisis,
            CoordinadorCorreo = analisis.CoordinadorCorreo,
            FechaInforme = analisis.FechaInforme,
            Asignaturas = analisis.Asignaturas
                .OrderBy(a => a.Id)
                .Select(a => new InformeAsignatura
                {
                    AsignaturaOrigen = a.AsignaturaOrigen,
                    AsignaturaDestino = a.AsignaturaDestino,
                    PorcentajeContenido = a.PorcentajeContenido,
                    PorcentajeProfundidad = a.PorcentajeProfundidad,
                    PorcentajeHoras = a.PorcentajeHoras,
                    Decision = a.Decision,
                    Justificacion = a.Justificacion,
                    DecisionCoordinador = a.DecisionCoordinador,
                    ObservacionCoordinador = a.ObservacionCoordinador
                })
                .ToList(),
            Resolucion = await CargarResolucionAsync(analisis, ct)
        };
    }

    /// <summary>
    /// Resolución del Consejo Directivo de la solicitud. Todavía no existe la entidad ResolucionConsejo
    /// (TA-17.1): mientras tanto devuelve null y el informe omite esa sección sin error.
    /// Cuando exista, aquí se lee y se convierte a <see cref="InformeResolucion"/>.
    /// </summary>
    private Task<InformeResolucion?> CargarResolucionAsync(AnalisisSolicitud analisis, CancellationToken ct) =>
        Task.FromResult<InformeResolucion?>(null);
}
