namespace Shared.Models;

public sealed record SearchChunk(
    string ChunkId,
    string DocumentId,
    string FileName,
    string Content,
    int ChunkIndex);
