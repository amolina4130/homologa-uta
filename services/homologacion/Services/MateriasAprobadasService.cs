using HomologaUTA.Homologacion.Clients;
using HomologaUTA.Homologacion.Data;
using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Homologacion.Services;

/// <summary>
/// Consulta y corrección de las materias aprobadas extraídas del récord (HU-20). El estudiante
/// solo corrige las suyas y solo mientras la solicitud siga REGISTRADA; cada corrección queda
/// en el historial de estados.
/// </summary>
public class MateriasAprobadasService
{
    private readonly HomologacionDbContext _db;
    private readonly SolicitudesClient _solicitudes;
    private readonly ILogger<MateriasAprobadasService> _logger;

    public MateriasAprobadasService(
        HomologacionDbContext db,
        SolicitudesClient solicitudes,
        ILogger<MateriasAprobadasService> logger)
    {
        _db = db;
        _solicitudes = solicitudes;
        _logger = logger;
    }

    public async Task<List<MateriaAprobadaDto>> ObtenerAsync(
        int solicitudId, CancellationToken ct = default)
    {
        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct)
                   ?? throw new RecordException(404, "Solicitud no encontrada.");

        return await _db.MateriasAprobadas.AsNoTracking()
            .Where(m => m.SolicitudId == solicitudId)
            .OrderBy(m => m.Id)
            .Select(m => new MateriaAprobadaDto
            {
                Id = m.Id,
                Nombre = m.Nombre,
                Nota = m.Nota,
                EstadoSegunDocumento = m.EstadoSegunDocumento,
                Periodo = m.Periodo
            })
            .ToListAsync(ct);
    }

    public async Task<List<MateriaAprobadaDto>> ActualizarAsync(
        int solicitudId,
        ActualizarMateriasAprobadasRequest request,
        string usuarioCorreo,
        bool esAdministrador,
        CancellationToken ct = default)
    {
        var info = await _solicitudes.ObtenerCompletaAsync(solicitudId, ct)
                   ?? throw new RecordException(404, "Solicitud no encontrada.");

        if (!esAdministrador &&
            !string.Equals(info.EstudianteCorreo, usuarioCorreo, StringComparison.OrdinalIgnoreCase))
        {
            throw new RecordException(403, "No puede corregir las materias aprobadas de otro estudiante.");
        }

        if (!string.Equals(info.Estado, "REGISTRADA", StringComparison.OrdinalIgnoreCase))
        {
            throw new RecordException(409,
                "La solicitud ya pasó a revisión y no se pueden corregir las materias aprobadas.");
        }

        var anteriores = await _db.MateriasAprobadas
            .Where(m => m.SolicitudId == solicitudId)
            .ToListAsync(ct);

        if (anteriores.Count > 0)
            _db.MateriasAprobadas.RemoveRange(anteriores);

        var fecha = DateTime.UtcNow;
        var nuevas = request?.Materias?
            .Where(m => !string.IsNullOrWhiteSpace(m.Nombre))
            .Select(m => new MateriaAprobada
            {
                SolicitudId = solicitudId,
                Nombre = m.Nombre.Trim(),
                Nota = m.Nota,
                EstadoSegunDocumento = string.IsNullOrWhiteSpace(m.EstadoSegunDocumento)
                    ? "aprobado"
                    : m.EstadoSegunDocumento.Trim(),
                Periodo = string.IsNullOrWhiteSpace(m.Periodo) ? null : m.Periodo.Trim(),
                FechaExtraccion = fecha
            })
            .ToList() ?? new List<MateriaAprobada>();

        _db.MateriasAprobadas.AddRange(nuevas);
        await _db.SaveChangesAsync(ct);

        try
        {
            await _solicitudes.RegistrarHistorialAsync(solicitudId, info.Estado, usuarioCorreo, ct);
        }
        catch (SolicitudesNoDisponibleException ex)
        {
            _logger.LogWarning(ex, "No se pudo registrar la corrección de materias en el historial");
        }

        return nuevas.Select(MateriaAprobadaMapper.ToDto).ToList();
    }
}
