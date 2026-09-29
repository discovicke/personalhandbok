using Shared.Models;

namespace Blazor.Services;

/// <summary>Sparar textbitar och söker bland dem.</summary>
public interface ISearchService
{
    /// <summary>Skapar indexet om det saknas, uppdaterar om schemat ändrats.</summary>
    Task EnsureIndexAsync(CancellationToken ct = default);
    /// <summary>Laddar upp eller uppdaterar bitar. Samma ChunkId skriver över.</summary>
    Task IndexAsync(IReadOnlyList<SearchChunk> chunks, CancellationToken ct = default);
    /// <summary>Fulltextsökning, mest relevanta träffarna först.</summary>
    Task<IReadOnlyList<SearchChunk>> SearchAsync(string query, int top = 5, CancellationToken ct = default);
}
