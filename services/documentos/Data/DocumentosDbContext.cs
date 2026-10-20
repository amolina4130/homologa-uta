using Microsoft.EntityFrameworkCore;

namespace HomologaUTA.Documentos.Data;

public class DocumentosDbContext : DbContext
{
    public DocumentosDbContext(DbContextOptions<DocumentosDbContext> options) : base(options) { }
}