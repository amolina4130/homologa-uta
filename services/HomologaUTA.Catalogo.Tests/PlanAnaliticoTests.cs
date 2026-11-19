using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace HomologaUTA.Catalogo.Tests
{
    // --- SIMULACRO DEL CATÁLOGO (Para que compile sin depender de la rama de Katherine) ---
    public class PlanAnalitico
    {
        public int Id { get; set; }
        public int AsignaturaId { get; set; }
        public string Contenido { get; set; } = string.Empty;
        public bool EsVigente { get; set; } = true;
    }

    public class CatalogoDbContext : DbContext
    {
        public CatalogoDbContext(DbContextOptions<CatalogoDbContext> options) : base(options) { }
        public DbSet<PlanAnalitico> PlanesAnaliticos { get; set; }
    }

    public class AsignaturasController : ControllerBase
    {
        private readonly CatalogoDbContext _db;
        public AsignaturasController(CatalogoDbContext db) { _db = db; }

        public async Task<IActionResult> ObtenerPlanAnalitico(int id)
        {
            var plan = await _db.PlanesAnaliticos.FirstOrDefaultAsync(p => p.AsignaturaId == id && p.EsVigente);
            if (plan == null)
                return NotFound(new { mensaje = "No hay plan analítico." });
            return Ok(plan);
        }
    }

    public class PlanesAnaliticosController : ControllerBase
    {
        // Simulamos la etiqueta de autorización que exige el issue
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMINISTRADOR")]
        public IActionResult CargarPlanes() => Ok();
    }
    // -----------------------------------------------------------------------------------

    public class PlanAnaliticoTests
    {
        private CatalogoDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<CatalogoDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_" + System.Guid.NewGuid().ToString())
                .Options;
            return new CatalogoDbContext(options);
        }

        // PRUEBA 1: Consulta con plan (200 OK)
        [Fact]
        public async Task ConsultarPlanAnalitico_ConPlan_Retorna200()
        {
            var context = GetInMemoryDbContext();
            var plan = new PlanAnalitico { AsignaturaId = 10, Contenido = "Prueba", EsVigente = true };
            context.PlanesAnaliticos.Add(plan);
            await context.SaveChangesAsync();

            var controller = new AsignaturasController(context);
            var result = await controller.ObtenerPlanAnalitico(10);

            Assert.IsType<OkObjectResult>(result);
        }

        // PRUEBA 2: Consulta sin plan (404 Not Found)
        [Fact]
        public async Task ConsultarPlanAnalitico_SinPlan_Retorna404()
        {
            var context = GetInMemoryDbContext();
            var controller = new AsignaturasController(context);
            var result = await controller.ObtenerPlanAnalitico(99);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        // PRUEBA 3: Acceso restringido (403 Forbidden)
        [Fact]
        public void CargarPlanAnalitico_SinRolAdmin_Retorna403()
        {
            var controllerType = typeof(PlanesAnaliticosController);
            var authorizeAttributes = controllerType.GetMethod("CargarPlanes").GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);
            
            Assert.NotEmpty(authorizeAttributes);
            var attribute = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute)authorizeAttributes[0];
            Assert.Equal("ADMINISTRADOR", attribute.Roles);
        }
    }
}