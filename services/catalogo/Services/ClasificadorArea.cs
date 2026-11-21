using System.Globalization;
using System.Text;

namespace HomologaUTA.Catalogo.Services;

/// <summary>
/// Deriva el área de conocimiento de una asignatura a partir de su nombre (las mallas no la traen).
/// Las reglas se evalúan en orden: gana la primera que coincide. Si se cambian, hay que subir
/// <see cref="Version"/>: entra en la huella y fuerza a recalcular las áreas en la próxima carga.
/// </summary>
public static class ClasificadorArea
{
    public const int Version = 1;
    public const string AreaGeneral = "General";

    private static readonly (string Area, string[] Palabras)[] Reglas =
    {
        ("General", new[] { "(tic)", "(tac)" }),
        ("Gestión y Producción", new[] { "control de calidad" }),
        ("Automatización y Control", new[] { "control numerico", "oleohidraulica", "robots" }),
        ("Bases de Datos", new[] { "base de datos", "bases de datos", "inteligencia de negocios", "soporte de decisiones" }),
        ("Redes y Comunicaciones", new[] { "redes", "comunicacion", "antenas", "telefonia", "propagacion", "iot", "telematica" }),
        ("Ingeniería de Software", new[] { "software", "metodologias agiles", "pruebas" }),
        ("Matemáticas", new[] { "calculo", "algebra", "matematica", "estadistica", "probabilidad", "numerico", "ecuaciones diferenciales", "investigacion operativa", "investigacion de operaciones" }),
        ("Física", new[] { "fisica", "mecanica", "estatica", "electromagnetic", "termodinamica", "quimica" }),
        ("Electrónica y Electricidad", new[] { "electronic", "electric", "circuito", "instrumentacion", "microcontrol", "dispositivos", "medidas", "sistemas digitales", "senales", "embebidos" }),
        ("Automatización y Control", new[] { "automatizacion", "robotic", "control", "mecatronic", "mecanismo", "neumatic", "plc", "industria 4.0", "ciberfisic" }),
        ("Diseño y Manufactura", new[] { "dibujo", "cad/cam", "maquinas herramientas", "manufactura", "materiales" }),
        ("Programación", new[] { "programacion", "algoritmo", "estructura de datos", "aplicaciones", "computacion", "web", "sistemas operativos", "interaccion" }),
        ("Investigación y Titulación", new[] { "investigacion", "titulacion" }),
        ("Prácticas y Vinculación", new[] { "practica", "servicio comunitario", "realidad nacional" }),
        ("Gestión y Producción", new[] { "gestion", "administracion", "produccion", "calidad", "proyecto", "emprendimiento", "gerencia", "logistica", "contabilidad", "costos", "mantenimiento", "ergonomia", "higiene", "planta", "procesos", "legislacion", "economica", "metodos", "operaciones", "auditoria", "gobierno", "seguridad", "ambiental", "industrial", "industria" })
    };

    public static string Clasificar(string nombre)
    {
        var texto = Normalizar(nombre);
        foreach (var (area, palabras) in Reglas)
        {
            if (palabras.Any(p => texto.Contains(p)))
                return area;
        }
        return AreaGeneral;
    }

    // minúsculas y sin tildes, para que "Programación" y "programacion" coincidan
    private static string Normalizar(string texto)
    {
        var descompuesto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
