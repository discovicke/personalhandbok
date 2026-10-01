namespace Blazor.Models;

/// <summary>Status i en chatt-ström mot <c>/api/chat/stream</c>.</summary>
public enum ChatStreamStatus
{
    Searching,
    Thinking,
    Token,
    Done
}

/// <summary>En händelse i chatt-strömmen. <see cref="Delta"/> sätts bara för <see cref="ChatStreamStatus.Token"/>, <see cref="Final"/> bara för <see cref="ChatStreamStatus.Done"/>.</summary>
public sealed record ChatStreamUpdate(ChatStreamStatus Status, string? Delta, ChatResponse? Final);
