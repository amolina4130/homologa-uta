using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Reportes;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Services;

/// <summary>Genera el informe de homologación en PDF (HU-18).</summary>
public interface IInformeService
{
    Task<byte[]> GenerarPdfAsync(int solicitudId, string? correo, bool esCoordinadorOAdministrador, CancellationToken ct = default);
}

public class InformeService : IInformeService
{
    private readonly HomologacionDbContext _db;
    private readonly SolicitudesClient _solicitudes;
    private readonly ILogger<InformeService> _logger;

    public InformeService(HomologacionDbContext db, SolicitudesClient solicitudes, ILogger<InformeService> logger)
    {
        _db = db;
        _solicitudes = solicitudes;
        _logger = logger;
    }

    public async Task<byte[]> GenerarPdfAsync(
        int solicitudId, string? correo, bool esCoordinadorOAdministrador, CancellationToken ct = default)
    {
        var solicitud = await _solicitudes.ObtenerDetalleAsync(solicitudId, ct)
                        ?? throw new InformeException(404, "Solicitud no encontrada.");

        if (!esCoordinadorOAdministrador &&
            !string.Equals(correo, solicitud.EstudianteCorreo, StringComparison.OrdinalIgnoreCase))
            throw new InformeException(403, "No puede descargar el informe de una solicitud de otro estudiante.");

        var analisis = await _db.AnalisisSolicitudes
            .AsNoTracking()
            .Include(a => a.Asignaturas).ThenInclude(x => x.MateriasOrigen)
            .FirstOrDefaultAsync(a => a.SolicitudId == solicitudId, ct);

        if (analisis is null)
            throw new InformeException(404, "La solicitud todavía no tiene un informe técnico.");

        var resolucion = await _db.ResolucionesConsejo
            .AsNoTracking()
            .Include(r => r.Decisiones)
            .FirstOrDefaultAsync(r => r.SolicitudId == solicitudId, ct);

        var lineas = new List<string>
        {
            "UNIVERSIDAD TÉCNICA DE AMBATO",
            "Sistema de Homologación de Asignaturas",
            "",
            $"Número de proceso: {solicitud.NumeroProceso}",
            $"Estudiante: {solicitud.EstudianteCorreo}",
            $"Módulo: {solicitud.Modulo}",
            $"Carrera de origen: {solicitud.CarreraOrigen}",
            $"Carrera de destino: {solicitud.CarreraDestino}",
        };

        if (!string.IsNullOrWhiteSpace(solicitud.UniversidadOrigen))
            lineas.Add($"Universidad de origen: {solicitud.UniversidadOrigen}");
        if (!string.IsNullOrWhiteSpace(solicitud.UniversidadDestino))
            lineas.Add($"Universidad de destino: {solicitud.UniversidadDestino}");

        lineas.Add($"Fecha de la solicitud: {solicitud.FechaRegistro:dd/MM/yyyy}");
        lineas.Add($"Fecha del análisis: {analisis.FechaAnalisis:dd/MM/yyyy}");
        lineas.Add("");

        if (analisis.Asignaturas.Count == 0)
        {
            lineas.Add("La solicitud no tiene asignaturas analizadas.");
        }

        var numero = 1;
        foreach (var asignatura in analisis.Asignaturas.OrderBy(a => a.Id))
        {
            lineas.Add($"Asignatura {numero}: {asignatura.AsignaturaOrigen}  ->  {asignatura.AsignaturaDestino}");
            if (asignatura.MateriasOrigen.Count > 0)
            {
                var origen = string.Join(" + ", asignatura.MateriasOrigen
                    .OrderBy(m => m.Orden)
                    .Select(m => $"{m.Nombre}{(m.Nota.HasValue ? $" (nota {m.Nota:0.##})" : "")}"));
                lineas.Add($"Materias de origen: {origen}{(asignatura.Combinada ? " [combinada]" : "")}");
            }

            lineas.Add($"Propuesta del agente: {asignatura.Decision}");
            lineas.Add($"Contenido: {asignatura.PorcentajeContenido}%  Profundidad: {asignatura.PorcentajeProfundidad}%  Carga horaria: {asignatura.PorcentajeHoras}%");
            lineas.Add($"Justificación: {asignatura.Justificacion}");
            lineas.Add($"Decisión del Coordinador: {asignatura.DecisionCoordinador ?? "Pendiente"}");
            if (!string.IsNullOrWhiteSpace(asignatura.ObservacionCoordinador))
                lineas.Add($"Observación: {asignatura.ObservacionCoordinador}");
            lineas.Add("");
            numero++;
        }

        if (resolucion is not null)
        {
            lineas.Add("RESOLUCIÓN DEL CONSEJO DIRECTIVO");
            lineas.Add($"Número: {resolucion.NumeroResolucion}   Fecha: {resolucion.Fecha:dd/MM/yyyy}");
            if (!string.IsNullOrWhiteSpace(resolucion.Observacion))
                lineas.Add($"Observación: {resolucion.Observacion}");
            foreach (var decision in resolucion.Decisiones.OrderBy(d => d.Id))
                lineas.Add($"Asignatura {decision.AsignaturaId}: {decision.DecisionFinal}");
            lineas.Add("");
        }

        lineas.Add($"Informe emitido por: {analisis.CoordinadorCorreo ?? "Coordinador de Homologación"}");
        if (analisis.FechaInforme.HasValue)
            lineas.Add($"Fecha del informe: {analisis.FechaInforme:dd/MM/yyyy}");

        _logger.LogInformation("Informe PDF generado para la solicitud {SolicitudId}", solicitudId);
        return PdfSimpleWriter.Crear(
            "INFORME DE HOMOLOGACIÓN",
            $"Informe_{solicitud.NumeroProceso}",
            lineas);
    }
}
