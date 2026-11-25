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
    public void InformeFinal_RecomiendaYVaDirigidoAlComite()
    {
        var texto = PlantillaInformeFinal.GenerarTexto(
            "SOL-0003", "UTA-FISEI-2026-045", "Estudiante Prueba", "1234567890",
            "Tecnologías de la Información", "Software", Filas(),
            firmante: "coordinador@uta.edu.ec");

        Assert.Equal("Comité de Homologación", PlantillaInformeFinal.Destinatario);
        Assert.Contains("De mi consideración:", texto);
        Assert.Contains("se recomienda la homologación", texto);
        Assert.DoesNotContain("se sugiere", texto);
        Assert.DoesNotContain("se resuelve", texto);
        Assert.DoesNotContain("Quipus", texto);
        Assert.Contains("Coordinador de Homologación", texto);
        Assert.Contains("C.I.:", texto);
    }

    [Fact]
    public void Pdfs_SeGeneranConTablaYDatosNuevos()
    {
        var filas = new List<FilaInformeAsignatura>
        {
            new() { AsignaturaOrigen = "Física", AsignaturaDestino = "Física", SeHomologa = "SÍ", NivelOrigen = "Primero", NivelDestino = "Primero",
                    PorcentajeContenido = 100, PorcentajeProfundidad = 100, PorcentajeHoras = 100,
                    Justificacion = "Acepta la sugerencia de la IA.", JustificacionIA = "El sílabo cubre el 100% de los temas. Cumple." },
            new() { AsignaturaOrigen = "Álgebra", AsignaturaDestino = "Álgebra Lineal", SeHomologa = "NO", PorcentajeContenido = 60, Justificacion = "No cubre espacios vectoriales." },
        };

        var docente = PlantillaInformeDocente.GenerarPdf(new DatosInformeDocente
        {
            NumeroProceso = "SOL-0001", NumeroMemorando = "SOL-0001", NombreEstudiante = "Estudiante", CedulaEstudiante = "1234567890",
            CarreraOrigen = "TI", CarreraDestino = "Software", Asignaturas = filas, Docente = "Docente", CedulaFirmante = "1800000000"
        });
        var final = PlantillaInformeFinal.GenerarPdfDesdeTexto("Para: Comité\nDe mi consideración: texto editado.\nN.º | A | B\n1 | x | y\n\nAtentamente,\nX", new DatosInformeFinal
        {
            NumeroProceso = "SOL-0001", NumeroMemorando = "M-1", NombreEstudiante = "Estudiante", CedulaEstudiante = "1234567890",
            CarreraOrigen = "TI", CarreraDestino = "Software", Asignaturas = filas.Take(1).ToList(), Firmante = "coordinador@uta.edu.ec",
            UniversidadOrigen = "Universidad X", UniversidadDestino = "UTA"
        });
        Assert.True(docente.Length > 1000);
        Assert.True(final.Length > 1000);
    }
}
