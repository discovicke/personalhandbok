using Azure;
using Azure.Search.Documents.Indexes;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Shared.Config;
using Shared.Models;

namespace Blazor.Services;

/// <summary>Dokumenttjänst med Blob Storage i botten. Azure knäcker och chunkar filerna själv.</summary>
public sealed class DocumentService : IDocumentService
{
    private readonly BlobContainerClient _container;
    private readonly SearchIndexerClient _indexerClient;
    private readonly string _indexerName;

    /// <summary>Kopplar upp med BLOB_STORAGE_CONNECTION_STRING, BLOB_CONTAINER_NAME och BLOB_INDEXER_NAME.</summary>
    public DocumentService()
    {
        var connectionString = EnvLoader.GetRequired("BLOB_STORAGE_CONNECTION_STRING");
        var containerName = EnvLoader.GetRequired("BLOB_CONTAINER_NAME");
        _indexerName = EnvLoader.GetRequired("BLOB_INDEXER_NAME");

        _container = new BlobServiceClient(connectionString).GetBlobContainerClient(containerName);

        var searchEndpoint = EnvLoader.GetRequired("AZURE_SEARCH_ENDPOINT");
        var searchKey = EnvLoader.GetRequired("AZURE_SEARCH_KEY");
        _indexerClient = new SearchIndexerClient(new Uri(searchEndpoint), new AzureKeyCredential(searchKey));
    }

    /// <inheritdoc/>
    public async Task<DocumentMetadata> UploadAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var type = DocumentTypeMapper.FromFileName(fileName, contentType);
        if (type == DocumentType.Unknown)
            throw new NotSupportedException($"Filtypen stöds inte: {fileName}");

        var blobName = Path.GetFileName(fileName);
        var blob = _container.GetBlobClient(blobName);

        await blob.UploadAsync(content, overwrite: true, cancellationToken: ct);
        var properties = (await blob.GetPropertiesAsync(cancellationToken: ct)).Value;

        await RunIndexerAsync(ct);

        return new DocumentMetadata(
            blobName,
            blobName,
            properties.ContentType,
            type,
            properties.ContentLength,
            properties.LastModified);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DocumentMetadata>> ListAsync(CancellationToken ct = default)
    {
        var result = new List<DocumentMetadata>();
        await foreach (var item in _container.GetBlobsAsync(cancellationToken: ct))
        {
            result.Add(new DocumentMetadata(
                item.Name,
                item.Name,
                item.Properties.ContentType ?? "application/octet-stream",
                DocumentTypeMapper.FromFileName(item.Name, item.Properties.ContentType),
                item.Properties.ContentLength ?? 0,
                item.Properties.LastModified ?? DateTimeOffset.MinValue));
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string documentId, CancellationToken ct = default)
    {
        await _container.GetBlobClient(documentId).DeleteIfExistsAsync(cancellationToken: ct);
        await RunIndexerAsync(ct);
    }

    private async Task RunIndexerAsync(CancellationToken ct)
    {
        try
        {
            await _indexerClient.RunIndexerAsync(_indexerName, cancellationToken: ct);
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 429)
        {
            // 409: indexern kör redan. 429: nyss startad (min 180 s mellan körningar). Båda OK.
        }
    }
}
