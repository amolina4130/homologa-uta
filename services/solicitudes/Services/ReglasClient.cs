using HomologaUTA.Reglas.Grpc;
using Grpc.Net.Client;

namespace HomologaUTA.Solicitudes.Services;

public class ReglasClient
{
    private readonly string _urlServicioReglas;

    public ReglasClient(IConfiguration configuration)
    {
        _urlServicioReglas = configuration["ReglasService:Url"] ?? "http://localhost:5001";
    }

    public async Task<List<ReglaDto>> ObtenerReglasActivasAsync()
    {
        using var channel = GrpcChannel.ForAddress(_urlServicioReglas);
        var client = new ReglasService.ReglasServiceClient(channel);
        var response = await client.ObtenerReglasActivasAsync(new ReglasActivasRequest());
        return response.Reglas.ToList();
    }
}
