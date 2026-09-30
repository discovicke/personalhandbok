using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Shared.Config;
using Shared.Models;

namespace Blazor.Services;

/// <summary>Söktjänst med Azure AI Search i botten. Läser config från miljön.</summary>
public sealed class AzureSearchService : ISearchService
{
    private readonly SearchClient _searchClient;
    private readonly SearchIndexClient _indexClient;
    private readonly string _indexName;
    private readonly string? _semanticConfig;

    /// <summary>Kopplar upp med AZURE_SEARCH_ENDPOINT, AZURE_SEARCH_KEY och AZURE_SEARCH_INDEX.</summary>
    public AzureSearchService()
    {
        var endpoint = EnvLoader.GetRequired("AZURE_SEARCH_ENDPOINT");
        var key = EnvLoader.GetRequired("AZURE_SEARCH_KEY");
        _indexName = EnvLoader.GetRequired("AZURE_SEARCH_INDEX");
        _semanticConfig = Environment.GetEnvironmentVariable("AZURE_SEARCH_SEMANTIC_CONFIG");

        var credential = new AzureKeyCredential(key);
        _searchClient = new SearchClient(new Uri(endpoint), _indexName, credential);
        _indexClient = new SearchIndexClient(new Uri(endpoint), credential);
    }

    /// <summary>Skapar/uppdaterar sökindexet. Fast form varje gång: ChunkId, DocumentId, FileName, Content, ChunkIndex.</summary>
    public async Task EnsureIndexAsync(CancellationToken ct = default)
    {
        // Skydd: index med fältet "chunk_id" ägs av indexern (prod). Skriv aldrig över det.
        try
        {
            var existing = await _indexClient.GetIndexAsync(_indexName, cancellationToken: ct);
            if (existing.Value.Fields.Any(f => f.Name == "chunk_id"))
                throw new InvalidOperationException(
                    $"Indexet '{_indexName}' ägs av indexern och får inte skrivas över. " +
                    "EnsureIndex skapar bara test-index.");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Finns inte, skapa nedan.
        }

        var definition = new SearchIndex(_indexName)
        {
            Fields =
            {
                // Nyckel — unik per chunk, krävs av Azure Search.
                new SimpleField("ChunkId", SearchFieldDataType.String) { IsKey = true },
                // Filter för ersätta/ta bort per dokument.
                new SimpleField("DocumentId", SearchFieldDataType.String) { IsFilterable = true },
                // Sökbart filnamn + innehåll för personalfrågor.
                new SearchableField("FileName"),
                new SearchableField("Content"),
                // Ordning inom dokument — filtrerbar + sorterbar för återställd läsordning.
                new SimpleField("ChunkIndex", SearchFieldDataType.Int32) { IsFilterable = true, IsSortable = true },
            }
        };

        await _indexClient.CreateOrUpdateIndexAsync(definition, cancellationToken: ct);
    }

    /// <inheritdoc/>
    public async Task IndexAsync(IReadOnlyList<SearchChunk> chunks, CancellationToken ct = default)
    {
        var docs = chunks.Select(c => new SearchDocument
        {
            ["ChunkId"] = c.ChunkId,
            ["DocumentId"] = c.DocumentId,
            ["FileName"] = c.FileName,
            ["Content"] = c.Content,
            ["ChunkIndex"] = c.ChunkIndex,
        }).ToList();

        var batch = IndexDocumentsBatch.MergeOrUpload(docs);
        await _searchClient.IndexDocumentsAsync(batch, cancellationToken: ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SearchChunk>> SearchAsync(string query, int top = 5, CancellationToken ct = default)
    {
        // Fält i det riktiga indexet: chunk_id (nyckel, slutar med _pages_N), parent_id, chunk, title, text_vector.
        // Hybrid: textfråga + vektorfråga i samma anrop. Azure vektoriserar frågetexten själv.
        var options = new SearchOptions { Size = top };

        if (!string.IsNullOrWhiteSpace(_semanticConfig))
        {
            options.QueryType = SearchQueryType.Semantic;
            options.SemanticSearch = new SemanticSearchOptions { SemanticConfigurationName = _semanticConfig };
        }

        options.VectorSearch = new VectorSearchOptions
        {
            Queries =
            {
                new VectorizableTextQuery(query)
                {
                    KNearestNeighborsCount = top,
                    Fields = { "text_vector" }
                }
            }
        };

        var response = await _searchClient.SearchAsync<SearchDocument>(query, options, ct);

        var hits = new List<SearchChunk>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            var doc = result.Document;
            var chunkId = GetString(doc, "chunk_id");
            hits.Add(new SearchChunk(
                chunkId,
                GetString(doc, "parent_id"),
                GetString(doc, "title"),
                GetString(doc, "chunk"),
                ParsePageNumber(chunkId)));
        }

        return hits;
    }

    /// <inheritdoc/>
    public async Task<int> DeleteByFileNameAsync(string fileName, CancellationToken ct = default)
    {
        var keys = (await SearchAsync(fileName, top: 1000, ct))
            .Where(h => h.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
            .Select(h => h.ChunkId)
            .Where(id => id.Length > 0)
            .ToList();

        if (keys.Count == 0)
            return 0;

        await _searchClient.DeleteDocumentsAsync("chunk_id", keys, cancellationToken: ct);
        return keys.Count;
    }

    private static string GetString(SearchDocument doc, string field) =>
        doc.TryGetValue(field, out var value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;

    /// <summary>Läser sidnummer från chunk-id på formen ..._pages_N. Ger 0 om det saknas.</summary>
    private static int ParsePageNumber(string chunkId)
    {
        const string marker = "_pages_";
        var pos = chunkId.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return pos >= 0 && int.TryParse(chunkId[(pos + marker.Length)..], out var page) ? page : 0;
    }
}
