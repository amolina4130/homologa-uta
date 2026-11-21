using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;


using HomologaUTA.Homologacion.Models;
using Microsoft.EntityFrameworkCore;
namespace HomologaUTA.Homologacion.Controllers;

[ApiController]
[Route("homologacion")]
// El Gateway ya valida el token (ruta "homologacion", política "autenticado").
public class HomologacionController : ControllerBase
{
    private readonly ServicioValidacionSolicitud _servicio;
    private readonly IInformeService _informeService; // <-- NUEVO SERVICIO

    public HomologacionController(ServicioValidacionSolicitud servicio, IInformeService informeService)
    {
        _servicio = servicio;
        _informeService = informeService;
    }

    /// <summary>Ejecuta las reglas objetivas sobre la solicitud y devuelve el resultado por asignatura</summary>
    /// <remarks>
    /// Aplica RN-02, RN-04 y RN-01 a cada asignatura, y RN-05 (solo cambio de universidad) y RN-06 a toda la solicitud.
    /// Cada asignatura queda en uno de estos estados:
    ///
    /// * LISTA_PARA_AGENTE: cumple todas las reglas objetivas.
    /// * DERIVADA: más de diez años desde la aprobación (RN-01), pasa a validación de conocimientos.
    /// * EXCLUIDA: no aprobada (RN-02), tercera matrícula (RN-04) o requisitos de cambio de universidad no cumplidos (RN-05).
    /// * CON_FALTANTES: documentación incompleta (RN-06); el detalle indica qué falta.
    ///
    /// Cada resultado cita la regla aplicada.
    /// </remarks>
    /// <param name="id">Id de la solicitud</param>
    /// <param name="request">Datos académicos y documentación de la solicitud</param>
    /// <response code="200">Resultado de la validación por asignatura</response>
    /// <response code="400">Datos de entrada inválidos</response>
    [HttpPost("{id:int}/validar")]
    [ProducesResponseType(typeof(ResultadoValidacionSolicitudDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<ResultadoValidacionSolicitudDto> Validar(int id, [FromBody] ValidarSolicitudRequest request)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        var error = _servicio.ValidarEntrada(request);
        if (error is not null)
            return BadRequest(new { error });

        return Ok(_servicio.Validar(id, request));
    }

        /// <summary>Preselecciona las candidatas de la carrera destino por área y nivel</summary>
    /// <response code="200">Pares materia origen → candidatas, y materias sin candidata</response>
    /// <response code="409">Sin materias aprobadas o memoria no cargada</response>
    [HttpPost("{solicitudId:int}/preseleccion")]
    public async Task<IActionResult> Preseleccionar(
        int solicitudId,
        [FromServices] IPreseleccionService preseleccion,
        CancellationToken ct)
    {
        var solicitud = await _db.Solicitudes.FindAsync(new object[] { solicitudId }, ct); // AJUSTAR DbSet
        if (solicitud is null) return NotFound();

        var materias = await _db.MateriasAprobadas
            .Where(m => m.SolicitudId == solicitudId)
            .Select(m => new MateriaAprobadaDto(m.Id, m.Nombre, m.Nota, m.EstadoSegunDocumento, m.Periodo))
            .ToListAsync(ct);

        try
        {
            var resultado = await preseleccion.PreseleccionarAsync(
                materias,
                solicitud.CarreraDestinoId,   // AJUSTAR: carrera destino de la solicitud
                solicitud.NivelAprobado,      // AJUSTAR: nivel aprobado del estudiante
                ct);
            return Ok(resultado);
        }
        catch (PreseleccionException ex)
        {
            return Conflict(new { mensaje = ex.Message });
        }
    }
}