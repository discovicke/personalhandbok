namespace Shared.Models;

/// <summary>Metadata för ett uppladdat dokument. <see cref="Id"/> är också dokument-id i sökningen.</summary>
public sealed record DocumentMetadata(
    string Id,
    string FileName,
    string ContentType,
    DocumentType Type,
    long SizeBytes,
    DateTimeOffset UploadedAt);
