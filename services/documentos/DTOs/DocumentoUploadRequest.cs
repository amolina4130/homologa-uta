using Microsoft.AspNetCore.Http;

namespace HomologaUTA.Documentos.DTOs;

public class DocumentoUploadRequest
{
    public IFormFile Archivo { get; set; } = null!;
    public int SolicitudId { get; set; }
    public string TipoDocumento { get; set; } = string.Empty;
}