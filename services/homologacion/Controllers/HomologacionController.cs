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
    using HomologaUTA.Homologacion.Models;
using Microsoft.EntityFrameworkCore;
    /// <summary>Resultados por asignatura de destino, con sus materias de origen (informe técnico y detalle)</summary>
    /// <response code="200">Lista de resultados</response>
    [HttpGet("{solicitudId:int}/resultados")]
    public async Task<IActionResult> ObtenerResultados(int solicitudId, CancellationToken ct)
    {
        var resultados = await _db.ResultadosAsignatura                   // AJUSTAR DbSet
            .Include(r => r.MateriasOrigen)
            .Where(r => r.SolicitudId == solicitudId)                     // AJUSTAR propiedad
            .ToListAsync(ct);

        return Ok(resultados.Select(ResultadoAsignaturaMapper.ADto));
    }

    /// <summary>Define las materias de origen de un resultado (una o dos)</summary>
    /// <response code="200">Resultado actualizado</response>
    /// <response code="400">Más de dos materias de origen o ninguna</response>
    /// <response code="404">Resultado no encontrado</response>
    [HttpPut("{solicitudId:int}/resultados/{resultadoId:int}/materias-origen")]
    public async Task<IActionResult> AsignarMateriasOrigen(
        int solicitudId, int resultadoId,
        [FromBody] List<MateriaOrigenDto> materias, CancellationToken ct)
    {
        var resultado = await _db.ResultadosAsignatura
            .Include(r => r.MateriasOrigen)
            .FirstOrDefaultAsync(r => r.Id == resultadoId && r.SolicitudId == solicitudId, ct); // AJUSTAR

        if (resultado is null) return NotFound();

        try
        {
            resultado.AsignarMateriasOrigen(
                materias.Select(m => new MateriaOrigenResultado { Nombre = m.Nombre, Nota = m.Nota }));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }

        await _db.SaveChangesAsync(ct);
        return Ok(ResultadoAsignaturaMapper.ADto(resultado));
    }
}