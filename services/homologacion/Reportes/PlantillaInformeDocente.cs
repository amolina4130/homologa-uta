using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HomologaUTA.Homologacion.Reportes;

/// <summary>Una fila de la tabla de siete columnas del memorando de homologación.</summary>
public class FilaInformeAsignatura
{
    public string AsignaturaOrigen { get; set; } = string.Empty;
    public string AsignaturaDestino { get; set; } = string.Empty;
    public string Justificacion { get; set; } = string.Empty;

    /// <summary>Carrera de origen; si se omita se toma de los datos generales.</summary>
    public string CarreraOrigen { get; set; } = string.Empty;
    /// <summary>Carrera de destino; si se omita se toma de los datos generales.</summary>
    public string CarreraDestino { get; set; } = string.Empty;
    /// <summary>Contenido/porcentajes que sustentan la homologación.</summary>
    public string Contenido { get; set; } = string.Empty;
    /// <summary>Respuesta de la columna "¿Se homologa?".</summary>
    public string SeHomologa { get; set; } = string.Empty;

    /// <summary>Semestre de la materia de origen y de la de destino según las mallas.</summary>
    public string? NivelOrigen { get; set; }
    public string? NivelDestino { get; set; }

    /// <summary>Porcentajes del análisis comparativo (contenido, profundidad y carga horaria).</summary>
    public int? PorcentajeContenido { get; set; }
    public int? PorcentajeProfundidad { get; set; }
    public int? PorcentajeHoras { get; set; }

    /// <summary>Fundamento del análisis de la IA; se usa cuando el docente solo aceptó la sugerencia.</summary>
    public string? JustificacionIA { get; set; }
}

/// <summary>Datos comunes del encabezado de los informes del docente y del Coordinador.</summary>
public class DatosInformeBase
{
    public string NumeroProceso { get; set; } = string.Empty;
    public string NumeroMemorando { get; set; } = string.Empty;
    public string NombreEstudiante { get; set; } = string.Empty;
    public string CedulaEstudiante { get; set; } = string.Empty;
    public string CarreraOrigen { get; set; } = string.Empty;
    public string CarreraDestino { get; set; } = string.Empty;
    public string? ObservacionGeneral { get; set; }

    /// <summary>Solo para el agente externo (otra universidad).</summary>
    public string? UniversidadOrigen { get; set; }
    public string? UniversidadDestino { get; set; }

    /// <summary>C.I. de quien firma; se imprime en la línea de firma del memorando.</summary>
    public string CedulaFirmante { get; set; } = "-";

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public List<FilaInformeAsignatura> Asignaturas { get; set; } = new();
}

public class DatosInformeDocente : DatosInformeBase
{
    /// <summary>Correo o nombre del docente que firma el informe.</summary>
    public string Docente { get; set; } = string.Empty;

    /// <summary>Correo editable que se imprime junto a la firma.</summary>
    public string CorreoFirmante { get; set; } = string.Empty;

    /// <summary>Párrafo opcional propuesto por la IA y validado por el Docente.</summary>
    public string? OpinionIA { get; set; }

    /// <summary>
    /// Texto ya revisado por el Docente. Si viene, el PDF muestra ese texto en lugar de
    /// reconstruir la plantilla, para que coincida con lo que vio en pantalla.
    /// </summary>
    public string? TextoInforme { get; set; }
}

/// <summary>
/// Utilidades compartidas por las dos plantillas. El texto y el PDF se construyen con la
/// misma información para que no haya diferencias entre lo que se muestra y lo que se imprime.
/// </summary>
internal static class ContenidoInforme
{
    private const string Azul = "#1B4F9C";

    internal static string Valor(string? texto) => string.IsNullOrWhiteSpace(texto) ? "-" : texto.Trim();

    /// <summary>
    /// Descarta el texto automático antiguo "Revisión con apoyo de IA": el Docente escribe su
    /// propia observación y ese rótulo no debe aparecer en ningún informe.
    /// </summary>
    internal static string? Observacion(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var limpio = texto.Trim();
        return limpio.Contains("Revisión con apoyo de IA", StringComparison.OrdinalIgnoreCase)
            ? null
            : limpio;
    }

    /// <summary>El informe final recomienda al Comité: nunca "se sugiere" ni "se resuelve" (no es la aprobación oficial).</summary>
    internal static string Afirmar(string? texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? "-"
            : Regex.Replace(texto.Trim(), @"se\s+(sugiere|resuelve)", "se recomienda", RegexOptions.IgnoreCase);

    internal static string Encabezado(DatosInformeBase datos) =>
        $"De mi consideración:{Environment.NewLine}" +
        $"En atención al memorando {Valor(datos.NumeroMemorando)}, " +
        $"el/la estudiante {Valor(datos.NombreEstudiante)} con cédula {Valor(datos.CedulaEstudiante)} " +
        $"solicita la homologación del cambio de la carrera {Valor(datos.CarreraOrigen)} " +
        $"a la carrera {Valor(datos.CarreraDestino)}. Revisada la documentación, ";

    /// <summary>Completa los campos opcionales de la fila con los datos generales del memorando.</summary>
    internal static FilaInformeAsignatura Normalizar(
        DatosInformeBase datos, FilaInformeAsignatura fila, bool afirmar = false)
    {
        Func<string?, string> limpio = afirmar
            ? texto => Afirmar(texto)
            : texto => Valor(texto);

        var contenido = string.IsNullOrWhiteSpace(fila.Contenido)
            ? limpio(fila.Justificacion)
            : limpio(fila.Contenido);

        return new FilaInformeAsignatura
        {
            CarreraOrigen = string.IsNullOrWhiteSpace(fila.CarreraOrigen)
                ? Valor(datos.CarreraOrigen)
                : limpio(fila.CarreraOrigen),
            CarreraDestino = string.IsNullOrWhiteSpace(fila.CarreraDestino)
                ? Valor(datos.CarreraDestino)
                : limpio(fila.CarreraDestino),
            AsignaturaOrigen = limpio(fila.AsignaturaOrigen),
            AsignaturaDestino = limpio(fila.AsignaturaDestino),
            Contenido = contenido,
            SeHomologa = string.IsNullOrWhiteSpace(fila.SeHomologa) ? "SÍ" : limpio(fila.SeHomologa),
            Justificacion = limpio(fila.Justificacion)
        };
    }

    internal static List<FilaInformeAsignatura> Normalizar(
        DatosInformeBase datos, IEnumerable<FilaInformeAsignatura> filas, bool afirmar = false) =>
        filas.Select(f => Normalizar(datos, f, afirmar)).ToList();

    internal static string FormarTabla(
        DatosInformeBase datos, IEnumerable<FilaInformeAsignatura> asignaturas, bool afirmar = false)
    {
        var filas = Normalizar(datos, asignaturas, afirmar);
        if (filas.Count == 0)
            return string.Empty;

        var lineas = new List<string>
        {
            "N.º | Carrera de origen | Carrera de destino | Materia a homologar (origen) | Contenido | Materia equivalente (destino) | ¿Se homologa?"
        };

        for (var i = 0; i < filas.Count; i++)
        {
            var f = filas[i];
            var contenido = f.Contenido == "-" && f.Justificacion != "-"
                ? f.Justificacion
                : f.Contenido;
            lineas.Add($"{i + 1} | {f.CarreraOrigen} | {f.CarreraDestino} | {f.AsignaturaOrigen} | " +
                       $"{contenido} | {f.AsignaturaDestino} | {f.SeHomologa}");
        }

        return string.Join(Environment.NewLine, lineas);
    }

    /// <summary>
    /// Quita del texto guardado la tabla en formato de pipes: el PDF la vuelve a dibujar como
    /// tabla real con el encabezado explícito, sin duplicarla.
    /// </summary>
    internal static string QuitarTabla(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        var resultado = new List<string>();
        var dentroDeTabla = false;
        foreach (var linea in texto.Replace("\r\n", "\n").Split('\n'))
        {
            if (linea.StartsWith("N.º |", StringComparison.Ordinal))
            {
                dentroDeTabla = true;
                continue;
            }

            if (dentroDeTabla && Regex.IsMatch(linea.TrimStart(), @"^\d+\s*\|"))
                continue;

            dentroDeTabla = false;
            resultado.Add(linea);
        }

        return string.Join(Environment.NewLine, resultado).Trim();
    }

    /// <summary>
    /// La firma se dibuja al final del PDF con los datos ya editados por el Docente; se quita
    /// del texto guardado para no repetirla.
    /// </summary>
    internal static string QuitarCola(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        var resultado = texto.Replace("\r\n", "\n");
        var cortes = new[] { "Atentamente," };
        var posicion = cortes
            .Select(c => resultado.IndexOf(c, StringComparison.OrdinalIgnoreCase))
            .Where(p => p >= 0)
            .DefaultIfEmpty(-1)
            .Min();

        return (posicion < 0 ? resultado : resultado[..posicion]).Trim();
    }

    private const string GrisFondo = "#F3F6FB";
    private const string Verde = "#1E7B45";
    private const string Rojo = "#B42318";

    /// <summary>Membrete compacto que se repite en cada página: logos, institución y título.</summary>
    internal static void MembretePdf(IContainer container, string titulo)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.ConstantItem(54).Height(40).Element(Logo("logo-uta.png"));
                row.RelativeItem().AlignMiddle().Column(col =>
                {
                    col.Item().AlignCenter().Text("UNIVERSIDAD TÉCNICA DE AMBATO").Bold().FontSize(11).FontColor(Azul);
                    col.Item().AlignCenter().Text("FACULTAD DE INGENIERÍA EN SISTEMAS, ELECTRÓNICA E INDUSTRIAL")
                        .FontSize(7.5f).FontColor(Azul);
                    col.Item().PaddingTop(2).AlignCenter().Text(titulo).Bold().FontSize(10);
                });
                row.ConstantItem(54).Height(40).Element(Logo("logo-fisei.png"));
            });
            column.Item().PaddingTop(4).LineHorizontal(1.2f).LineColor(Azul);
            column.Item().PaddingBottom(8);
        });
    }

    /// <summary>Bloque PARA / DE / ASUNTO / FECHA / N.º (solo en la primera página).</summary>
    internal static void BloqueMemorandoPdf(IContainer container, DatosInformeBase datos, string para, string de, string asunto)
    {
        container.Column(col =>
        {
            void Fila(string etiqueta, string valor) =>
                col.Item().PaddingBottom(1).Row(r =>
                {
                    r.ConstantItem(56).Text(etiqueta).Bold().FontSize(8.5f);
                    r.RelativeItem().Text(valor).FontSize(8.5f);
                });

            Fila("PARA:", para);
            Fila("DE:", de);
            Fila("ASUNTO:", asunto);
            Fila("FECHA:", $"Ambato, {datos.Fecha:dd/MM/yyyy}");
            Fila("N.º:", Valor(datos.NumeroMemorando));
        });
    }

    /// <summary>Recuadro con los datos del estudiante y del trámite.</summary>
    internal static void DatosSolicitudPdf(IContainer container, DatosInformeBase datos)
    {
        var externo = !string.IsNullOrWhiteSpace(datos.UniversidadOrigen);
        container.Background(GrisFondo).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(col =>
        {
            col.Item().Text("DATOS DE LA SOLICITUD").Bold().FontSize(7.5f).FontColor(Azul);
            col.Item().PaddingTop(3).Row(r =>
            {
                r.RelativeItem(3).Text(t =>
                {
                    t.Span("Estudiante: ").Bold().FontSize(8.5f);
                    t.Span(Valor(datos.NombreEstudiante)).FontSize(8.5f);
                });
                r.RelativeItem(2).Text(t =>
                {
                    t.Span("C.I.: ").Bold().FontSize(8.5f);
                    t.Span(Valor(datos.CedulaEstudiante)).FontSize(8.5f);
                });
                r.RelativeItem(2).Text(t =>
                {
                    t.Span("Proceso: ").Bold().FontSize(8.5f);
                    t.Span(Valor(datos.NumeroProceso)).FontSize(8.5f);
                });
            });
            if (externo)
            {
                col.Item().PaddingTop(2).Text(t =>
                {
                    t.Span("Universidad: ").Bold().FontSize(8.5f);
                    t.Span($"{Valor(datos.UniversidadOrigen)} → {Valor(datos.UniversidadDestino)}").FontSize(8.5f);
                });
            }
            col.Item().PaddingTop(2).Text(t =>
            {
                t.Span("Carrera: ").Bold().FontSize(8.5f);
                t.Span($"{Valor(datos.CarreraOrigen)} (origen) → {Valor(datos.CarreraDestino)} (destino)").FontSize(8.5f);
            });
        });
    }

    /// <summary>Cifras del resultado: evaluadas, homologadas y no homologadas.</summary>
    internal static void ResumenPdf(IContainer container, int evaluadas, int homologadas, bool soloHomologadas)
    {
        container.Row(row =>
        {
            void Cifra(string etiqueta, int valor, string color) =>
                row.RelativeItem().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(c =>
                {
                    c.Item().AlignCenter().Text(valor.ToString()).Bold().FontSize(14).FontColor(color);
                    c.Item().AlignCenter().Text(etiqueta).FontSize(7.5f).FontColor(Colors.Grey.Darken2);
                });

            if (soloHomologadas)
            {
                Cifra("Asignaturas que se homologan", homologadas, Verde);
            }
            else
            {
                Cifra("Asignaturas evaluadas", evaluadas, Azul);
                row.ConstantItem(6);
                Cifra("Se homologan", homologadas, Verde);
                row.ConstantItem(6);
                Cifra("No se homologan", evaluadas - homologadas, Rojo);
            }
        });
    }

    /// <summary>
    /// Justificación que se imprime: la del docente o coordinador; si solo aceptó la sugerencia de
    /// la IA, el fundamento del análisis (primera oración), que es lo que sustenta la decisión.
    /// </summary>
    internal static string JustificacionVisible(FilaInformeAsignatura f)
    {
        var propia = (f.Justificacion ?? string.Empty).Trim();
        var aceptaIA = propia.Length == 0 || propia.StartsWith("Acepta la sugerencia de la IA", StringComparison.OrdinalIgnoreCase);
        if (aceptaIA && f.PorcentajeContenido >= 80 && f.PorcentajeProfundidad >= 80 && f.PorcentajeHoras >= 80)
            return "Cumple el umbral del 80 % en contenidos, profundidad y carga horaria (análisis comparativo de sílabos).";
        if (aceptaIA && f.PorcentajeContenido is not null)
        {
            var faltan = new List<string>();
            if (f.PorcentajeContenido < 80) faltan.Add($"contenidos ({f.PorcentajeContenido}%)");
            if (f.PorcentajeProfundidad < 80) faltan.Add($"profundidad ({f.PorcentajeProfundidad}%)");
            if (f.PorcentajeHoras < 80) faltan.Add($"carga horaria ({f.PorcentajeHoras}%)");
            if (faltan.Count > 0)
                return $"No alcanza el 80 % en {string.Join(", ", faltan)}.";
        }
        if (aceptaIA && !string.IsNullOrWhiteSpace(f.JustificacionIA))
        {
            var ia = f.JustificacionIA.Trim();
            var fin = ia.IndexOf(". ", StringComparison.Ordinal);
            return fin > 0 ? ia[..(fin + 1)] : ia;
        }
        return Valor(propia);
    }

    private static string Porcentajes(FilaInformeAsignatura f) =>
        (f.PorcentajeContenido ?? 0) == 0 && (f.PorcentajeProfundidad ?? 0) == 0 && (f.PorcentajeHoras ?? 0) == 0
            ? "-"
            : $"C {Pct(f.PorcentajeContenido)}\nP {Pct(f.PorcentajeProfundidad)}\nH {Pct(f.PorcentajeHoras)}";

    private static string Pct(int? v) => v is null ? "-" : $"{v}%";

    /// <summary>
    /// Tabla de asignaturas: origen y destino con su semestre, coincidencia (contenido, profundidad,
    /// horas), decisión y justificación. En el informe final se omite la columna de decisión porque
    /// solo lista las que se homologan.
    /// </summary>
    internal static void TablaPdf(
        TableDescriptor table,
        DatosInformeBase datos,
        IEnumerable<FilaInformeAsignatura> asignaturas,
        bool afirmar = false,
        bool conDecision = true)
    {
        var filas = asignaturas.ToList();

        table.ColumnsDefinition(columns =>
        {
            columns.ConstantColumn(20);
            columns.RelativeColumn(22);
            columns.RelativeColumn(22);
            columns.ConstantColumn(44);
            if (conDecision) columns.ConstantColumn(40);
            columns.RelativeColumn(34);
        });

        table.Header(header =>
        {
            Cabecera(header, "N.º");
            Cabecera(header, "Asignatura aprobada (origen)");
            Cabecera(header, "Asignatura equivalente (destino)");
            Cabecera(header, "Coincid.");
            if (conDecision) Cabecera(header, "Decisión");
            Cabecera(header, "Justificación");
        });

        for (var i = 0; i < filas.Count; i++)
        {
            var f = filas[i];
            string fondo = i % 2 == 1 ? GrisFondo : "#FFFFFF";
            var si = (f.SeHomologa ?? "SÍ").StartsWith("S", StringComparison.OrdinalIgnoreCase);
            var justificacion = JustificacionVisible(f);
            if (afirmar) justificacion = Afirmar(justificacion);

            CeldaBase(table, fondo).AlignCenter().Text((i + 1).ToString()).FontSize(7.5f);
            CeldaBase(table, fondo).Column(c =>
            {
                c.Item().Text(Valor(f.AsignaturaOrigen)).SemiBold().FontSize(7.5f);
                if (!string.IsNullOrWhiteSpace(f.NivelOrigen))
                    c.Item().Text($"Nivel: {f.NivelOrigen}").FontSize(6.5f).FontColor(Colors.Grey.Darken1);
            });
            CeldaBase(table, fondo).Column(c =>
            {
                c.Item().Text(Valor(f.AsignaturaDestino)).SemiBold().FontSize(7.5f);
                if (!string.IsNullOrWhiteSpace(f.NivelDestino))
                    c.Item().Text($"Nivel: {f.NivelDestino}").FontSize(6.5f).FontColor(Colors.Grey.Darken1);
            });
            CeldaBase(table, fondo).Text(Porcentajes(f)).FontSize(6.5f).LineHeight(1.1f);
            if (conDecision)
                CeldaBase(table, fondo).AlignCenter().Text(si ? "SÍ" : "NO").Bold().FontSize(8).FontColor(si ? Verde : Rojo);
            CeldaBase(table, fondo).Text(justificacion).FontSize(7);
        }
    }

    private static IContainer CeldaBase(TableDescriptor table, string fondo) =>
        table.Cell().ShowEntire().Background(fondo).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingHorizontal(3);

    internal static void LeyendaPorcentajesPdf(IContainer container) =>
        container.Text("Coincid.: porcentaje de la asignatura de destino que cubre la de origen en C = contenidos, " +
                       "P = profundidad (resultados de aprendizaje) y H = carga horaria. Umbral de la normativa: 80 % " +
                       "(R-CAU-P-011-2026; Art. 20 R-CAU-P-122-2026).")
            .FontSize(6.5f).FontColor(Colors.Grey.Darken1);

    /// <summary>Firma con línea, nombre, cargo, C.I. y correo opcional; no se separa entre páginas.</summary>
    internal static void FirmaPdf(
        IContainer container, string nombre, string cargo, string cedula, string? correo = null) =>
        container.ShowEntire().Column(column =>
        {
            column.Item().Text("Atentamente,").FontSize(9);
            column.Item().PaddingTop(34).Row(row =>
                row.ConstantItem(220).PaddingBottom(2).LineHorizontal(0.8f).LineColor(Colors.Grey.Darken3));
            var esCorreo = nombre.Contains('@');
            column.Item().Text(esCorreo || nombre == "-" ? cargo : nombre).Bold().FontSize(9);
            if (!esCorreo && nombre != "-") column.Item().Text(cargo).FontSize(8.5f);
            if (!string.IsNullOrWhiteSpace(cedula) && cedula.Trim() != "-")
                column.Item().Text($"C.I.: {cedula.Trim()}").FontSize(8);
            var mail = !string.IsNullOrWhiteSpace(correo) ? correo : esCorreo ? nombre : null;
            if (!string.IsNullOrWhiteSpace(mail))
                column.Item().Text($"Correo: {mail.Trim()}").FontSize(8);
        });

    private static Action<IContainer> Logo(string nombreArchivo) => container =>
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "Assets", nombreArchivo);
        if (File.Exists(ruta))
            container.Image(File.ReadAllBytes(ruta)).FitArea();
        else
            container.Border(0.5f).BorderColor(Azul).AlignCenter().AlignMiddle()
                .Text(nombreArchivo.Contains("fisei") ? "FISEI" : "UTA")
                .Bold().FontSize(9).FontColor(Azul);
    };

    internal static void PiePdf(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text("HomologaUTA · Universidad Técnica de Ambato")
                .FontSize(8).FontColor(Colors.Grey.Medium);

            row.ConstantItem(120).AlignRight().Text(text =>
            {
                text.Span("Página ").FontSize(8);
                text.CurrentPageNumber().FontSize(8);
                text.Span(" de ").FontSize(8);
                text.TotalPages().FontSize(8);
            });
        });
    }

    internal static void Cabecera(TableCellDescriptor header, string texto)
    {
        header.Cell().Background(Azul).Padding(3)
            .Text(texto).FontColor(Colors.White).Bold().FontSize(7);
    }

    internal static void Celda(TableDescriptor table, string texto)
    {
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
            .Padding(3).Text(texto).FontSize(7);
    }
}

/// <summary>Plantilla del informe del docente asignado: usa "se sugiere" y firma como docente.</summary>
public static class PlantillaInformeDocente
{
    static PlantillaInformeDocente()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public const string SinHomologables =
        "No se identifica ninguna asignatura homologable; en consecuencia, " +
        "el estudiante debe iniciar su plan de estudios desde cero.";

    /// <summary>Encabezado visible de la tabla con la decisión SÍ/NO de cada materia.</summary>
    public const string TituloTablaHomologables =
        "Tabla de asignaturas evaluadas (materias que se pueden homologar y materias que no):";

    /// <summary>Rótulo del párrafo opcional generado con IA; también se usa para extraerlo del texto guardado.</summary>
    public const string EtiquetaOpinionIA = "Opinión técnica asistida por IA:";

    public static string GenerarTexto(DatosInformeDocente datos) => GenerarTextoDeDatos(datos);

    public static string GenerarTexto(
        string numeroProceso,
        string numeroMemorando,
        string nombreEstudiante,
        string cedulaEstudiante,
        string carreraOrigen,
        string carreraDestino,
        IEnumerable<FilaInformeAsignatura> asignaturas,
        string? observacionGeneral = null,
        string? docente = null,
        DateTime? fecha = null,
        string? opinionIA = null)
    {
        return GenerarTextoDeDatos(new DatosInformeDocente
        {
            NumeroProceso = numeroProceso,
            NumeroMemorando = numeroMemorando,
            NombreEstudiante = nombreEstudiante,
            CedulaEstudiante = cedulaEstudiante,
            CarreraOrigen = carreraOrigen,
            CarreraDestino = carreraDestino,
            Asignaturas = asignaturas.ToList(),
            ObservacionGeneral = observacionGeneral,
            Docente = docente ?? string.Empty,
            Fecha = fecha ?? DateTime.UtcNow,
            OpinionIA = opinionIA
        });
    }

    private static string GenerarTextoDeDatos(DatosInformeDocente datos)
    {
        var asignaturas = datos.Asignaturas.Where(a =>
            !string.IsNullOrWhiteSpace(a.AsignaturaDestino) ||
            !string.IsNullOrWhiteSpace(a.AsignaturaOrigen)).ToList();
        var hayHomologables = asignaturas.Any(a =>
            a.SeHomologa?.StartsWith("S", StringComparison.OrdinalIgnoreCase) == true);
        var observacion = ContenidoInforme.Observacion(datos.ObservacionGeneral);

        var introduccion = asignaturas.Count == 0
            ? SinHomologables
            : hayHomologables
                ? "se sugiere la siguiente decisión por asignatura:"
                : "se sugiere no homologar las asignaturas evaluadas:";

        var partes = new List<string>
        {
            ContenidoInforme.Encabezado(datos) + introduccion
        };

        var tabla = ContenidoInforme.FormarTabla(datos, asignaturas);
        if (tabla.Length > 0)
        {
            partes.Add(TituloTablaHomologables);
            partes.Add(tabla);
        }

        if (observacion is not null)
            partes.Add($"Observación general: {observacion}");

        if (!string.IsNullOrWhiteSpace(datos.OpinionIA))
            partes.Add($"{EtiquetaOpinionIA} {datos.OpinionIA.Trim()}");

        var firma = $"Atentamente,{Environment.NewLine}{ContenidoInforme.Valor(datos.Docente)}{Environment.NewLine}" +
                    $"Docente asignado{Environment.NewLine}C.I.: {ContenidoInforme.Valor(datos.CedulaFirmante)}";
        if (!string.IsNullOrWhiteSpace(datos.CorreoFirmante))
            firma += $"{Environment.NewLine}Correo: {datos.CorreoFirmante.Trim()}";
        partes.Add(firma);

        return string.Join(Environment.NewLine + Environment.NewLine, partes);
    }

    public static byte[] GenerarPdf(DatosInformeDocente datos) => GenerarPdfDeDatos(datos);

    public static byte[] GenerarPdf(
        string numeroProceso,
        string numeroMemorando,
        string nombreEstudiante,
        string cedulaEstudiante,
        string carreraOrigen,
        string carreraDestino,
        IEnumerable<FilaInformeAsignatura> asignaturas,
        string? observacionGeneral = null,
        string? docente = null,
        DateTime? fecha = null,
        string? opinionIA = null)
    {
        return GenerarPdfDeDatos(new DatosInformeDocente
        {
            NumeroProceso = numeroProceso,
            NumeroMemorando = numeroMemorando,
            NombreEstudiante = nombreEstudiante,
            CedulaEstudiante = cedulaEstudiante,
            CarreraOrigen = carreraOrigen,
            CarreraDestino = carreraDestino,
            Asignaturas = asignaturas.ToList(),
            ObservacionGeneral = observacionGeneral,
            Docente = docente ?? string.Empty,
            Fecha = fecha ?? DateTime.UtcNow,
            OpinionIA = opinionIA
        });
    }

    /// <summary>Destinatario del informe del docente: lo recibe la Coordinación.</summary>
    public const string Destinatario = "Coordinación de Homologación";

    private static byte[] GenerarPdfDeDatos(DatosInformeDocente datos)
    {
        var asignaturas = datos.Asignaturas.Where(a =>
            !string.IsNullOrWhiteSpace(a.AsignaturaDestino) ||
            !string.IsNullOrWhiteSpace(a.AsignaturaOrigen)).ToList();
        var homologadas = asignaturas.Count(a =>
            a.SeHomologa?.StartsWith("S", StringComparison.OrdinalIgnoreCase) == true);
        // Primero las que se homologan, luego las que no.
        var ordenadas = asignaturas
            .OrderByDescending(a => a.SeHomologa?.StartsWith("S", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
        var observacion = ContenidoInforme.Observacion(datos.ObservacionGeneral);
        var introduccion = asignaturas.Count == 0
            ? SinHomologables
            : homologadas > 0
                ? "se sugiere la siguiente decisión por asignatura:"
                : "se sugiere no homologar las asignaturas evaluadas:";

        // Texto revisado por el docente (sin la tabla ni la firma, que se dibujan aparte).
        var textoNarrativo = ContenidoInforme.QuitarCola(ContenidoInforme.QuitarTabla(datos.TextoInforme))
            .Replace(TituloTablaHomologables, string.Empty).Trim();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.6f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken4).LineHeight(1.25f));

                page.Header().Element(h => ContenidoInforme.MembretePdf(h, "INFORME TÉCNICO DEL DOCENTE"));

                page.Content().Element(content => content.Column(column =>
                {
                    column.Item().Element(c => ContenidoInforme.BloqueMemorandoPdf(c, datos,
                        Destinatario,
                        $"{ContenidoInforme.Valor(datos.Docente)}, docente asignado",
                        "Informe técnico de homologación de asignaturas"));
                    column.Item().PaddingTop(8).Element(c => ContenidoInforme.DatosSolicitudPdf(c, datos));

                    column.Item().PaddingTop(10).Element(c =>
                    {
                        if (!string.IsNullOrWhiteSpace(textoNarrativo))
                        {
                            c.Text(textoNarrativo).FontSize(9).Justify();
                            return;
                        }
                        c.Text(text =>
                        {
                            text.Span(ContenidoInforme.Encabezado(datos));
                            text.Span(introduccion).Bold();
                        });
                    });

                    if (asignaturas.Count > 0)
                    {
                        column.Item().PaddingTop(10).Element(c =>
                            ContenidoInforme.ResumenPdf(c, asignaturas.Count, homologadas, soloHomologadas: false));
                        column.Item().PaddingTop(10).Text("Asignaturas evaluadas").Bold().FontSize(9.5f).FontColor("#1B4F9C");
                        column.Item().PaddingTop(4).Table(table =>
                            ContenidoInforme.TablaPdf(table, datos, ordenadas));
                        column.Item().PaddingTop(3).Element(ContenidoInforme.LeyendaPorcentajesPdf);
                    }

                    if (string.IsNullOrWhiteSpace(textoNarrativo))
                    {
                        if (observacion is not null)
                            column.Item().PaddingTop(10).Text(t =>
                            {
                                t.Span("Observación general: ").Bold();
                                t.Span(observacion);
                            });

                        if (!string.IsNullOrWhiteSpace(datos.OpinionIA))
                            column.Item().PaddingTop(8).Text(t =>
                            {
                                t.Span(EtiquetaOpinionIA + " ").Bold();
                                t.Span(datos.OpinionIA.Trim());
                            });
                    }

                    column.Item().PaddingTop(18).Element(firma =>
                        ContenidoInforme.FirmaPdf(
                            firma,
                            ContenidoInforme.Valor(datos.Docente),
                            "Docente asignado",
                            datos.CedulaFirmante,
                            datos.CorreoFirmante));
                }));

                page.Footer().Element(ContenidoInforme.PiePdf);
            });
        }).GeneratePdf();
    }
}
