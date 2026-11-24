using HomologaUTA.Homologacion.Reportes;
using Xunit;

namespace HomologaUTA.Homologacion.Tests;

public class PlantillasInformeTests
{
    private static List<FilaInformeAsignatura> Filas() => new()
    {
        new FilaInformeAsignatura
        {
            AsignaturaOrigen = "Cálculo Integral",
            AsignaturaDestino = "Cálculo II",
            Justificacion = "se sugiere homologar por coincidencia de contenidos",
            Contenido = "85% · coincidencia de contenidos",
            SeHomologa = "SÍ"
        }
    };

    [Fact]
    public void InformeDocente_UsaMembreteYTablaDeSieteColumnas()
    {
        var texto = PlantillaInformeDocente.GenerarTexto(
            "SOL-0003", "UTA-FISEI-2026-045", "Estudiante Prueba", "1234567890",
            "Tecnologías de la Información", "Software", Filas(),
            docente: "docente@uta.edu.ec");

        Assert.Contains("De mi consideración:", texto);
        Assert.Contains("se sugiere la siguiente decisión por asignatura", texto);
        Assert.Contains(
            "N.º | Carrera de origen | Carrera de destino | Materia a homologar (origen) | Contenido | Materia equivalente (destino) | ¿Se homologa?",
            texto);
        Assert.Contains("C.I.:", texto);
    }

    [Fact]
    public void InformeDocente_IncluyeOpinionIAOpcional()
    {
        var texto = PlantillaInformeDocente.GenerarTexto(
            "SOL-0003", "UTA-FISEI-2026-045", "Estudiante Prueba", "1234567890",
            "Tecnologías de la Información", "Software", Filas(),
            docente: "docente@uta.edu.ec",
            opinionIA: "se sugiere reforzar la revisión de contenidos comunes");

        Assert.Contains("Opinión técnica asistida por IA:", texto);
        Assert.Contains("se sugiere reforzar la revisión de contenidos comunes", texto);
    }

    [Fact]
    public void InformeDocente_MuestraFirmaEditableYEncabezadoDeTabla()
    {
        var texto = PlantillaInformeDocente.GenerarTexto(new DatosInformeDocente
        {
            NumeroProceso = "SOL-0003",
            NumeroMemorando = "UTA-FISEI-2026-045",
            NombreEstudiante = "Estudiante Prueba",
            CedulaEstudiante = "1234567890",
            CarreraOrigen = "Tecnologías de la Información",
            CarreraDestino = "Software",
            Asignaturas = Filas(),
            Docente = "Ing. María Pérez",
            CedulaFirmante = "1800000001",
            CorreoFirmante = "docente@uta.edu.ec"
        });

        Assert.Contains(
            "Tabla de asignaturas evaluadas (materias que se pueden homologar y materias que no):",
            texto);
        Assert.Contains("Ing. María Pérez", texto);
        Assert.Contains("C.I.: 1800000001", texto);
        Assert.Contains("Correo: docente@uta.edu.ec", texto);
        Assert.DoesNotContain("Quipus", texto);
    }

    [Fact]
    public void InformeDocente_MuestraTambienLasMateriasQueNoSeHomologan()
    {
        var filas = new List<FilaInformeAsignatura>
        {
            new()
            {
                AsignaturaOrigen = "Cálculo Integral",
                AsignaturaDestino = "Cálculo II",
                Justificacion = "coincidencia de contenidos",
                Contenido = "85% · coincidencia de contenidos",
                SeHomologa = "SÍ"
            },
            new()
            {
                AsignaturaOrigen = "Manejo y Configuración del Software",
                AsignaturaDestino = "Auditoría de TI",
                Justificacion = "60%: no alcanza el umbral del 80%",
                Contenido = "60% · no alcanza el umbral del 80%",
                SeHomologa = "NO"
            }
        };

        var texto = PlantillaInformeDocente.GenerarTexto(
            "SOL-0003", "UTA-FISEI-2026-045", "Estudiante Prueba", "1234567890",
            "Tecnologías de la Información", "Software", filas,
            docente: "docente@uta.edu.ec");

        Assert.Contains("Cálculo II | SÍ", texto);
        Assert.Contains("Auditoría de TI | NO", texto);
        Assert.DoesNotContain("Quipus", texto);
    }

    [Fact]
    public void Informes_IgnoranElTextoAutomaticoDeApoyoIA()
    {
        var texto = PlantillaInformeDocente.GenerarTexto(
            "SOL-0003", "UTA-FISEI-2026-045", "Estudiante Prueba", "1234567890",
            "Tecnologías de la Información", "Software", Filas(),
            observacionGeneral: "Revisión con apoyo de IA",
            docente: "docente@uta.edu.ec");

        Assert.DoesNotContain("Revisión con apoyo de IA", texto);
        Assert.DoesNotContain("Observación general", texto);
    }

    [Fact]
    public void InformeFinal_AfirmaYVaDirigidoAlConsejoDirectivo()
    {
        var texto = PlantillaInformeFinal.GenerarTexto(
            "SOL-0003", "UTA-FISEI-2026-045", "Estudiante Prueba", "1234567890",
            "Tecnologías de la Información", "Software", Filas(),
            firmante: "coordinador@uta.edu.ec");

        Assert.Contains("Consejo Directivo", texto);
        Assert.Contains("De mi consideración:", texto);
        Assert.Contains("se resuelve homologar", texto);
        Assert.DoesNotContain("se sugiere", texto);
        Assert.DoesNotContain("Quipus", texto);
        Assert.Contains("Coordinador de Homologación", texto);
        Assert.Contains("C.I.:", texto);
    }
}
