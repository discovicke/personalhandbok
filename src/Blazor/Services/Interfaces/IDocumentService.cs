using Blazor.Models;

namespace Blazor.Services.Interfaces;

/// <summary>Laddar upp och listar dokument. Indexeringen sköter Azure själv.</summary>
public interface IDocumentService
{
    /// <summary>Laddar upp en fil, returnerar dess metadata och startar indexern.</summary>
    Task<DocumentMetadata> UploadAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>Listar alla uppladdade dokument.</summary>
    Task<IReadOnlyList<DocumentMetadata>> ListAsync(CancellationToken ct = default);

    /// <summary>Tar bort ett dokument helt och startar indexern. Försvinner från sök när indexern kört klart.</summary>
    Task DeleteAsync(string documentId, CancellationToken ct = default);
}
