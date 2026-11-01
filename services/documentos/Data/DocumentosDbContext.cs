using Microsoft.EntityFrameworkCore;
using HomologaUTA.Documentos.Models;

namespace HomologaUTA.Documentos.Data;

public class DocumentosDbContext : DbContext
{
    public DocumentosDbContext(DbContextOptions<DocumentosDbContext> options) : base(options) { }

    public DbSet<Documento> Documentos { get; set; }
}