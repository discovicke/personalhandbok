public class AzureErrorHandler
{
    public static string UserFriendlyError(Exception exception) => exception switch
    {
        Azure.RequestFailedException => "Söktjänsten är nere, försök igen.",
        InvalidOperationException x when x.Message.StartsWith("Missing required environment variable") => 
        "En eller flera inställningar saknas. Säkerställ att .env är korrekt.",
        _ => "Något gick fel. Försök igen.",
    };
}