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

    /// <summary>Kopplar upp med AZURE_SEARCH_ENDPOINT, AZURE_SEARCH_KEY och AZURE_SEARCH_INDEX.</summary>
    public AzureSearchService()
    {
        var endpoint = EnvLoader.GetRequired("AZURE_SEARCH_ENDPOINT");
        var key = EnvLoader.GetRequired("AZURE_SEARCH_KEY");
        _indexName = EnvLoader.GetRequired("AZURE_SEARCH_INDEX");

        var credential = new AzureKeyCredential(key);
        _searchClient = new SearchClient(new Uri(endpoint), _indexName, credential);
        _indexClient = new SearchIndexClient(new Uri(endpoint), credential);
    }

    /// <inheritdoc/>
    public async Task EnsureIndexAsync(CancellationToken ct = default)
    {
        var definition = new SearchIndex(_indexName)
        {
            Fields =
            {
                new SimpleField("ChunkId", SearchFieldDataType.String) { IsKey = true },
                new SearchableField("Content"),
                new SearchableField("FileName"),
                new SimpleField("DocumentId", SearchFieldDataType.String) { IsFilterable = true },
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
        var options = new SearchOptions { Size = top };
        var response = await _searchClient.SearchAsync<SearchDocument>(query, options, ct);

        var hits = new List<SearchChunk>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            var doc = result.Document;
            hits.Add(new SearchChunk(
                GetString(doc, "ChunkId"),
                GetString(doc, "DocumentId"),
                GetString(doc, "FileName"),
                GetString(doc, "Content"),
                GetInt(doc, "ChunkIndex")));
        }

        return hits;
    }

    private static string GetString(SearchDocument doc, string field) =>
        doc.TryGetValue(field, out var value) 
            ? value?.ToString() ?? string.Empty 
            : string.Empty;

    private static int GetInt(SearchDocument doc, string field) =>
        doc.TryGetValue(field, out var value) switch
        {
            true when value is int i => i,
            true when value is long l => (int)l,
            _ => 0
        };
}
