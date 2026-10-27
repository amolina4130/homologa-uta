using Grpc.Core;
using HomologaUTA.Reglas.Data;
using HomologaUTA.Reglas.Grpc;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Reglas.Services;

public class ReglasGrpcService : ReglasService.ReglasServiceBase
{
    private readonly ReglasDbContext _db;

    public ReglasGrpcService(ReglasDbContext db)
    {
        _db = db;
    }

    public override async Task<ReglasActivasResponse> ObtenerReglasActivas(
        ReglasActivasRequest request, ServerCallContext context)
    {
        var reglasDb = await _db.Reglas
            .Where(r => r.Activa)
            .AsNoTracking()
            .ToListAsync(context.CancellationToken);

        var response = new ReglasActivasResponse();

        foreach (var regla in reglasDb)
        {
            response.Reglas.Add(new ReglaDto
            {
                Id = regla.Id,
                Codigo = regla.Codigo,
                Descripcion = regla.Descripcion,
                Fuente = regla.Fuente,
                Umbral = regla.Umbral?.ToString() ?? string.Empty,
                Activa = regla.Activa
            });
        }

        return response;
    }
}