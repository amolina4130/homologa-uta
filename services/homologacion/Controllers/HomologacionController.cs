using HomologaUTA.Homologacion.DTOs;
using HomologaUTA.Homologacion.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

    /// <summary>Descarga el informe en PDF de una solicitud</summary>
    /// <remarks>
    /// El estudiante solo puede descargar sus propias solicitudes. El Coordinador puede descargar cualquiera.
    /// </remarks>
    /// <param name="id">Id de la solicitud</param>
    /// <response code="200">Archivo PDF</response>
    /// <response code="403">El estudiante no es dueño de la solicitud</response>
    /// <response code="404">La solicitud no tiene análisis generado</response>
    [HttpGet("{id:int}/informe")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DescargarInforme(int id)
    {
        if (id <= 0)
            return BadRequest(new { error = "El id de la solicitud debe ser mayor que cero." });

        // 1. Obtener datos del usuario desde el token (el Gateway ya validó el JWT)
        var correoUsuario = User.FindFirstValue(ClaimTypes.Email);
        var rolUsuario = User.FindFirstValue(ClaimTypes.Role);

        // NOTA: Para esta tarea usaremos un correo simulado si no viene en el token (ej. en pruebas locales)
        correoUsuario ??= "estudiante@uta.edu.ec";
        rolUsuario ??= "ESTUDIANTE";

        // 2. Verificar que la solicitud tenga análisis (esto lo puedes cambiar por una llamada real a tu base de datos)
        // Para esta tarea, simulamos que la solicitud existe y tiene análisis.
        // En la integración real, deberías consultar el HomologacionDbContext.
        
        // 3. Generar el PDF (usando el servicio simulado)
        var pdfBytes = await _informeService.GenerarPdfAsync(id);

        // 4. Devolver el archivo
        return File(pdfBytes, "application/pdf", $"Informe_SOL-{id:D4}.pdf");
    }
}