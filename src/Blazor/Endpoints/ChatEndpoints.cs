using System.Text.Json;
using Blazor.Models;
using Blazor.Services.Interfaces;

namespace Blazor.Endpoints;

/// <summary>SSE-endpoint för chatt-streaming. Frontend kopplar upp sig här när chattupplevelsen är klar.</summary>
public static class ChatEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapChatStream(this WebApplication app)
    {
        app.MapPost("/api/chat/stream", async (ChatRequest request, IChatService chat, HttpContext context) =>
        {
            var ct = context.RequestAborted;

            if (request is null || string.IsNullOrWhiteSpace(request.Question))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = "Ange en fråga." }, JsonOptions, ct);
                return;
            }

            context.Response.Headers.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers["X-Accel-Buffering"] = "no";

            await foreach (var update in chat.AskStreamingAsync(request.Question, ct))
            {
                var (eventName, payload) = update.Status switch
                {
                    ChatStreamStatus.Searching => ("searching",
                        JsonSerializer.Serialize(new { message = "söker i dokument" }, JsonOptions)),
                    ChatStreamStatus.Thinking => ("thinking",
                        JsonSerializer.Serialize(new { message = "tänker" }, JsonOptions)),
                    ChatStreamStatus.Token => ("token",
                        JsonSerializer.Serialize(new { delta = update.Delta ?? string.Empty }, JsonOptions)),
                    _ => ("done",
                        JsonSerializer.Serialize(ToDonePayload(update.Final), JsonOptions)),
                };

                await context.Response.WriteAsync($"event: {eventName}\ndata: {payload}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);
            }
        });
    }

    private static object ToDonePayload(ChatResponse? final) => new
    {
        answer = final?.Answer ?? string.Empty,
        isRefused = final?.IsRefused ?? false,
        refusalReason = final?.RefusalReason,
        citations = final?.Citations ?? []
    };
}
