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

    /// <summary>
    /// Registra una cuenta nueva. El rol siempre es Estudiante, el correo debe ser único y la
    /// contraseña se guarda con hash (HU-12).
    /// </summary>
    [HttpPost("registro")]
    [ProducesResponseType(typeof(RegistroResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Registro(RegistroRequest request)
    {
        var nombre = (request?.Nombre ?? string.Empty).Trim();
        var correo = (request?.Correo ?? string.Empty).Trim().ToLowerInvariant();
        var contrasena = request?.Contrasena ?? string.Empty;

        if (string.IsNullOrWhiteSpace(nombre))
            return BadRequest(new { mensaje = "El nombre es obligatorio." });

        if (nombre.Length > 200)
            return BadRequest(new { mensaje = "El nombre no puede superar los 200 caracteres." });

        if (string.IsNullOrWhiteSpace(correo) || !correo.Contains('@') || correo.StartsWith('@') || correo.EndsWith('@'))
            return BadRequest(new { mensaje = "Ingrese un correo electrónico válido." });

        if (correo.Length > 150)
            return BadRequest(new { mensaje = "El correo no puede superar los 150 caracteres." });

        var errorContrasena = PasswordPolicy.Validar(contrasena);
        if (errorContrasena is not null)
            return BadRequest(new { mensaje = errorContrasena });

        if (await _db.Usuarios.AnyAsync(u => u.Correo == correo))
            return Conflict(new { mensaje = "El correo ya está registrado. Inicie sesión o use otro correo." });

        var rol = await _db.Roles.FirstOrDefaultAsync(r =>
            r.Nombre == "ESTUDIANTE" || r.Nombre == "Estudiante");

        if (rol is null)
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { mensaje = "El rol Estudiante no está configurado en el sistema." });

        var usuario = new Usuario
        {
            Nombre = nombre,
            Correo = correo,
            RolId = rol.Id
        };
        usuario.PasswordHash = _hasher.HashPassword(usuario, contrasena);

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        return Created($"/auth/usuarios/{usuario.Id}", new RegistroResponse
        {
            Id = usuario.Id,
            Nombre = usuario.Nombre,
            Correo = usuario.Correo,
            Rol = "Estudiante"
        });
    }
}