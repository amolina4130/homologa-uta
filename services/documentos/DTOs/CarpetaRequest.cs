namespace HomologaUTA.Documentos.DTOs;

public class CarpetaRequest
{
    public int SolicitudId { get; set; }
    public string Ruta { get; set; } = "";
    public string? Usuario { get; set; }
}

/// <summary>URL de la carpeta compartida por el titular (homologación externa).</summary>
public class UrlExternaRequest
{
    public int SolicitudId { get; set; }
    public string Url { get; set; } = "";
    public string? Usuario { get; set; }
}

public class ResultadoCarpetaDto
{
    public List<DocumentoCarpetaDto> Documentos { get; set; } = new();
    public int Total { get; set; }
}

public class DocumentoCarpetaDto
{
    public int Id { get; set; }
    public string NombreArchivo { get; set; } = "";
    public string TipoDocumento { get; set; } = "";
    public string TextoExtraido { get; set; } = "";
}
