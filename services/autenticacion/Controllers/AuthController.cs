using HomologaUTA.Autenticacion.Data;
using HomologaUTA.Autenticacion.DTOs;
using HomologaUTA.Autenticacion.Models;
using HomologaUTA.Autenticacion.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Autenticacion.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private const string RolEstudiante = "ESTUDIANTE";
    private const string RolEstudianteRespuesta = "Estudiante";

    private readonly AuthDbContext _db;
    private readonly IPasswordHasher<Usuario> _hasher;
    private readonly TokenService _tokens;

    public AuthController(AuthDbContext db, IPasswordHasher<Usuario> hasher, TokenService tokens)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
    }

    /// <summary>Valida las credenciales y devuelve un JWT</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var error = new { mensaje = "Credenciales incorrectas. Intente nuevamente" };

        var usuario = await _db.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Correo == request.Correo);

        if (usuario is null)
            return Unauthorized(error);

        var resultado = _hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, request.Contrasena);
        if (resultado == PasswordVerificationResult.Failed)
            return Unauthorized(error);

        return Ok(new LoginResponse
        {
            Token = _tokens.GenerarToken(usuario),
            Nombre = string.IsNullOrWhiteSpace(usuario.Nombre) ? usuario.Correo.Split('@')[0] : usuario.Nombre,
            Correo = usuario.Correo,
            Rol = usuario.Rol.Nombre
        });
    }

    /// <summary>Crea una cuenta nueva. El rol siempre es Estudiante</summary>
    /// <remarks>
    /// El correo debe ser único. La contraseña debe tener de 8 a 64 caracteres, con una mayúscula,
    /// una minúscula y un número; si no cumple, el mensaje indica qué falta. Se guarda con hash.
    /// </remarks>
    /// <response code="201">Cuenta creada</response>
    /// <response code="400">Datos inválidos; el mensaje indica el requisito que no se cumple</response>
    /// <response code="409">El correo ya está registrado</response>
    [HttpPost("registro")]
    [ProducesResponseType(typeof(RegistroResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Registrar(RegistroRequest request)
    {
        var error = ValidadorRegistro.Validar(request);
        if (error is not null)
            return BadRequest(new { mensaje = error });

        var correo = request.Correo.Trim().ToLowerInvariant();
        var conflicto = new { mensaje = "El correo ya está registrado." };

        if (await _db.Usuarios.AnyAsync(u => u.Correo == correo))
            return Conflict(conflicto);

        var rol = await _db.Roles.FirstOrDefaultAsync(r => r.Nombre == RolEstudiante);
        if (rol is null)
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { mensaje = "No se encontró el rol de Estudiante." });

        var usuario = new Usuario
        {
            Nombre = request.Nombre.Trim(),
            Correo = correo,
            RolId = rol.Id,
            Rol = rol
        };
        usuario.PasswordHash = _hasher.HashPassword(usuario, request.Contrasena);

        _db.Usuarios.Add(usuario);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Si otro registro con el mismo correo llegó al mismo tiempo, el índice único lo rechaza
            _db.Entry(usuario).State = EntityState.Detached;
            if (await _db.Usuarios.AnyAsync(u => u.Correo == correo))
                return Conflict(conflicto);

            throw;
        }

        return Created($"/auth/usuarios/{usuario.Id}", new RegistroResponse
        {
            Id = usuario.Id,
            Nombre = usuario.Nombre,
            Correo = usuario.Correo,
            // El contrato de la HU-12 usa el valor legible; en la base de datos el rol es ESTUDIANTE.
            Rol = RolEstudianteRespuesta
        });
    }
}