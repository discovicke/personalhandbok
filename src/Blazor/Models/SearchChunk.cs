namespace Blazor.Models;

/// <summary>Textbit lagrad i sökindexet.</summary>
public sealed record SearchChunk(
    string ChunkId,
    string DocumentId,
    string FileName,
    string Content,
    int ChunkIndex);
