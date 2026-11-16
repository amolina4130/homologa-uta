using UglyToad.PdfPig;
using HomologaUTA.Catalogo.Models;
using HomologaUTA.Catalogo.Data;
using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Catalogo.Services;

public class CargaPlanesService
{
    private readonly CatalogoDbContext _db;

    public CargaPlanesService(CatalogoDbContext db)
    {
        _db = db;
    }

    public async Task<(int cargados, List<object> omitidos)> ProcesarPdfsAsync(List<IFormFile> archivos)
    {
        int cargados = 0;
        var omitidos = new List<object>();

        foreach (var archivo in archivos)
        {
            try
            {
                // Extraer texto con PdfPig
                using var stream = archivo.OpenReadStream();
                using var document = PdfDocument.Open(stream);
                var texto = string.Empty;
                foreach (var page in document.GetPages())
                {
                    texto += page.Text + "\n";
                }

                // Simulamos la extracción del ID de asignatura del nombre del archivo
                // Ejemplo: "plan_10.pdf" -> AsignaturaId = 10
                var nombreSinExtension = Path.GetFileNameWithoutExtension(archivo.FileName);
                var partes = nombreSinExtension.Split('_');
                if (partes.Length < 2 || !int.TryParse(partes[1], out int asignaturaId))
                {
                    omitidos.Add(new { archivo = archivo.FileName, motivo = "No se pudo identificar la asignatura en el nombre del archivo." });
                    continue;
                }

                // Verificar si ya existe un plan vigente
                var existe = await _db.PlanesAnaliticos.AnyAsync(p => p.AsignaturaId == asignaturaId && p.EsVigente);
                if (existe)
                {
                    omitidos.Add(new { archivo = archivo.FileName, motivo = "Ya existe un plan vigente para esta asignatura." });
                    continue;
                }

                // Guardar el nuevo plan
                var plan = new PlanAnalitico
                {
                    AsignaturaId = asignaturaId,
                    Contenido = texto,
                    ResultadosAprendizaje = "Extraído automáticamente del PDF.", // Podrías usar regex para extraer esta sección
                    FechaCarga = DateTime.UtcNow,
                    EsVigente = true
                };

                _db.PlanesAnaliticos.Add(plan);
                await _db.SaveChangesAsync();
                cargados++;
            }
            catch (Exception ex)
            {
                omitidos.Add(new { archivo = archivo.FileName, motivo = $"Error al procesar el PDF: {ex.Message}" });
            }
        }

        return (cargados, omitidos);
    }
}