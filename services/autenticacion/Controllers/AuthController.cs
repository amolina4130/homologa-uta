using HomologaUTA.Autenticacion.Data;
using HomologaUTA.Autenticacion.DTOs;
using HomologaUTA.Autenticacion.Models;
using HomologaUTA.Autenticacion.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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
            Rol = usuario.Rol.Nombre,
            Tipo = usuario.Tipo,
            Titulo = usuario.Titulo,
            Cedula = usuario.Cedula,
            Telefono = usuario.Telefono
        });
    }

    /// <summary>Guarda el perfil editable del usuario en sesión. No cambia correo, rol ni contraseña.</summary>
    [Authorize]
    [HttpPut("perfil")]
    [ProducesResponseType(typeof(PerfilResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ActualizarPerfil([FromBody] PerfilRequest? request)
    {
        var correo = User.FindFirstValue("email")
                     ?? User.FindFirstValue(ClaimTypes.Email)
                     ?? User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(correo))
            return Unauthorized(new { mensaje = "No se pudo identificar al usuario." });

        var usuario = await _db.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Correo == correo);

        if (usuario is null)
            return NotFound(new { mensaje = "Usuario no encontrado." });

        var nombre = request?.Nombre?.Trim();
        if (!string.IsNullOrWhiteSpace(nombre))
        {
            if (nombre.Length > 200)
                return BadRequest(new { mensaje = "El nombre no puede superar los 200 caracteres." });
            usuario.Nombre = nombre;
        }

        var campos = new (string? Valor, int Maximo, Action<Usuario, string> Asignar)[]
        {
            (request?.Tipo, 80, (u, v) => u.Tipo = v),
            (request?.Titulo, 80, (u, v) => u.Titulo = v),
            (request?.Cedula, 50, (u, v) => u.Cedula = v),
            (request?.Telefono, 50, (u, v) => u.Telefono = v)
        };

        foreach (var (valor, maximo, asignar) in campos)
        {
            var texto = valor?.Trim() ?? string.Empty;
            if (texto.Length > maximo)
                return BadRequest(new { mensaje = $"El campo no puede superar los {maximo} caracteres." });
            asignar(usuario, texto);
        }

        await _db.SaveChangesAsync();

        return Ok(new PerfilResponse
        {
            Id = usuario.Id,
            Nombre = usuario.Nombre,
            Correo = usuario.Correo,
            Rol = usuario.Rol.Nombre,
            Tipo = usuario.Tipo,
            Titulo = usuario.Titulo,
            Cedula = usuario.Cedula,
            Telefono = usuario.Telefono
        });
    }

    /// <summary>
    /// Docentes registrados, para que el Coordinador elija a quién delegar una solicitud
    /// en lugar de escribir el correo a mano. El Gateway restringe esta ruta al rol COORDINADOR.
    /// </summary>
    [HttpGet("docentes")]
    [ProducesResponseType(typeof(List<DocenteResumen>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Docentes()
    {
        var docentes = await _db.Usuarios
            .AsNoTracking()
            .Where(u => u.Rol.Nombre == "DOCENTE")
            .OrderBy(u => u.Nombre == "" ? u.Correo : u.Nombre)
            .Select(u => new DocenteResumen
            {
                Nombre = u.Nombre,
                Correo = u.Correo,
                Titulo = u.Titulo
            })
            .ToListAsync();

        return Ok(docentes);
    }

    /// <summary>
    /// Registra una cuenta nueva de Comité o Docente. El correo debe ser único y la
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

        var rolSolicitado = (request?.Rol ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(rolSolicitado))
            rolSolicitado = "COMITE";

        if (rolSolicitado != "COMITE" && rolSolicitado != "DOCENTE")
            return BadRequest(new { mensaje = "El rol debe ser COMITE o DOCENTE." });

        var rol = await _db.Roles.FirstOrDefaultAsync(r =>
            r.Nombre.ToUpper() == rolSolicitado);

        if (rol is null)
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { mensaje = $"El rol {rolSolicitado} no está configurado en el sistema." });

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
            Rol = rolSolicitado
        });
    }
}