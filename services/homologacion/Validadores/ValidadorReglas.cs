namespace HomologaUTA.Homologacion.Validadores;

// Clase para simular los datos de la asignatura que viene de la base de datos
public class AsignaturaInput
{
    public string Nombre { get; set; } = string.Empty;
    public bool Aprobada { get; set; }
    public int AniosDesdeAprobacion { get; set; }
    public int NumeroMatricula { get; set; }
}

// Clase para devolver el resultado
public class ResultadoValidacion
{
    public string Asignatura { get; set; } = string.Empty;
    public bool EsValida { get; set; }
    public string ReglaAplicada { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
}

public class ValidadorReglas
{
    public ResultadoValidacion ValidarAsignatura(AsignaturaInput asignatura)
    {
        var resultado = new ResultadoValidacion { Asignatura = asignatura.Nombre, EsValida = true };

        // RN-02: Únicamente se homologan asignaturas aprobadas.
        if (!asignatura.Aprobada)
        {
            resultado.EsValida = false;
            resultado.ReglaAplicada = "RN-02";
            resultado.Detalle = "La asignatura no está aprobada. Se excluye según la regla RN-02.";
            return resultado;
        }

        // RN-04: Se excluyen las asignaturas que llegaron a tercera matrícula.
        if (asignatura.NumeroMatricula >= 3)
        {
            resultado.EsValida = false;
            resultado.ReglaAplicada = "RN-04";
            resultado.Detalle = "La asignatura tiene tercera matrícula. Se excluye según la regla RN-04.";
            return resultado;
        }

        // RN-01: Solo procede si no han pasado más de 10 años
        if (asignatura.AniosDesdeAprobacion > 10)
        {
            resultado.EsValida = false;
            resultado.ReglaAplicada = "RN-01";
            resultado.Detalle = "Aprobada hace más de 10 años. Se deriva a validación de conocimientos según RN-01.";
            return resultado;
        }

        // Si pasa todas las reglas, está lista para el agente
        resultado.ReglaAplicada = "Ninguna";
        resultado.Detalle = "Cumple todas las reglas objetivas. Lista para el agente de IA.";
        return resultado;
    }
}