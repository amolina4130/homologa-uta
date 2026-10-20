using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HomologaUTA.Autenticacion.Models;
using Microsoft.IdentityModel.Tokens;

namespace HomologaUTA.Autenticacion.Services;

public class TokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerarToken(Usuario usuario)
    {
        var clave = _config["JWT_SECRET"] ?? _config["Jwt:Key"]
            ?? throw new InvalidOperationException("Falta la clave JWT");

        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clave)),
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, usuario.Correo),
            new Claim("rol", usuario.Rol.Nombre)
        };

        var horas = int.Parse(_config["Jwt:ExpirationHours"] ?? "8");

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "HomologaUTA",
            audience: _config["Jwt:Audience"] ?? "HomologaUTA",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(horas),
            signingCredentials: credenciales);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}