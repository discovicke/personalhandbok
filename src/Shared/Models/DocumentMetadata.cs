namespace Shared.Models;

public sealed record DocumentMetadata(
    string Id,
    string FileName,
    string ContentType,
    DocumentType Type,
    long SizeBytes,
    DateTimeOffset UploadedAt);
