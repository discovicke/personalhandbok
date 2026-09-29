namespace Shared.Models;

public sealed record Citation(
    string DocumentId,
    string FileName,
    string Quote,
    string? ChunkId = null);

public sealed record ChatResponse(
    string Answer,
    bool IsRefused,
    string? RefusalReason,
    List<Citation> Citations)
{
    public static ChatResponse Refused(string reason) =>
        new(string.Empty, true, reason, []);
}
