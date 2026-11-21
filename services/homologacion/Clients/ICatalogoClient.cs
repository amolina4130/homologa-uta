using System.Threading.Tasks;

namespace HomologaUTA.Homologacion.Clients
{
    public class PlanAnaliticoDto
    {
        public int AsignaturaId { get; set; }
        public string Contenido { get; set; } = string.Empty;
        public string ResultadosAprendizaje { get; set; } = string.Empty;
        public string FechaCarga { get; set; } = string.Empty; // Lo dejamos como string para el JSON
    }

    public interface ICatalogoClient
    {
        Task<PlanAnaliticoDto?> ObtenerPlanAnaliticoAsync(int asignaturaId);
    }
}