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
            Correo = usuario.Correo,
            Rol = usuario.Rol.Nombre
        });
    }
}