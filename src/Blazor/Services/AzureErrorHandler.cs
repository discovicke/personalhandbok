namespace Blazor.Services;

public class AzureErrorHandler
{
    /// <summary> En användarvänligare errorkod. Om du vill se hela exception, logga den separat. </summary>
    public static string UserFriendlyError(Exception exception) => exception switch
    {
        NotSupportedException => "Filtypen stöds inte. Bara PDF, Word, Markdown och text.",
        Azure.RequestFailedException e when e.Status == 404
            => "Hittade inte lagringen. Kolla BLOB_* i din .env.",
        Azure.RequestFailedException => "Söktjänsten svarar inte just nu. Försök igen om en stund.",
        InvalidOperationException e when e.Message.StartsWith("Missing required environment variable")
            => "En inställning saknas. Kopiera .env.example till .env och fyll i.",
        _ => "Något gick fel. Försök igen."
    };
}