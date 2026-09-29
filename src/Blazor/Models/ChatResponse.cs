namespace Shared.Models;

/// <summary>Källhänvisning för ett svar. ChunkId möjliggör hopp-till-källa senare.</summary>
public sealed record Citation(
    string DocumentId,
    string FileName,
    string Quote,
    string? ChunkId = null);

/// <summary>Svar från assistenten, eller ett nej-svar när den inte kan svara.</summary>
public sealed record ChatResponse(
    string Answer,
    bool IsRefused,
    string? RefusalReason,
    List<Citation> Citations)
{
    /// <summary>Skapar ett nej-svar med angiven anledning.</summary>
    public static ChatResponse Refused(string reason) =>
        new(string.Empty, true, reason, []);
}
