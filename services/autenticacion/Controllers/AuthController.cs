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
            Nombre = string.IsNullOrWhiteSpace(usuario.Nombre)
                ? usuario.Correo.Split('@')[0]
                : usuario.Nombre,
            Correo = usuario.Correo,
            Rol = usuario.Rol.Nombre
        });
    }

    /// <summary>Registra una cuenta nueva con rol Estudiante</summary>
    /// <remarks>
    /// El rol no se elige desde el registro: siempre queda como Estudiante.
    /// La contraseña se guarda con hash y debe tener al menos 8 caracteres,
    /// una mayúscula, una minúscula y un número.
    /// </remarks>
    /// <response code="201">Cuenta creada</response>
    /// <response code="400">Datos inválidos o contraseña que no cumple la política</response>
    /// <response code="409">El correo ya está registrado</response>
    [HttpPost("registro")]
    [ProducesResponseType(typeof(RegistroResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Registro(RegistroRequest request)
    {
        var correo = (request.Correo ?? string.Empty).Trim().ToLowerInvariant();
        var nombre = (request.Nombre ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(nombre))
            return BadRequest(new { mensaje = "El nombre es obligatorio." });

        if (string.IsNullOrWhiteSpace(correo) || !correo.Contains('@') || correo.StartsWith('@') || correo.EndsWith('@'))
            return BadRequest(new { mensaje = "El correo no tiene un formato válido." });

        var errorContrasena = ValidadorContrasena.Validar(request.Contrasena);
        if (errorContrasena is not null)
            return BadRequest(new { mensaje = errorContrasena });

        if (await _db.Usuarios.AnyAsync(u => u.Correo == correo))
            return Conflict(new { mensaje = "Ya existe una cuenta registrada con este correo." });

        var rolEstudiante = await _db.Roles.FirstOrDefaultAsync(r => r.Nombre == "ESTUDIANTE");
        if (rolEstudiante is null)
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { mensaje = "No se encontró el rol Estudiante configurado." });

        var usuario = new Usuario
        {
            Nombre = nombre,
            Correo = correo,
            RolId = rolEstudiante.Id
        };
        usuario.PasswordHash = _hasher.HashPassword(usuario, request.Contrasena);

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, new RegistroResponse
        {
            Id = usuario.Id,
            Nombre = usuario.Nombre,
            Correo = usuario.Correo,
            Rol = "Estudiante"
        });
    }
}