using System.Text;

namespace Blazor.Services;

/// <summary>Rena hjälpfunktioner för chatt-streaming: ordbaserad buffring och AVBÖJER-trimning.</summary>
public static class ChatStreamHelper
{
    public static readonly string[] RefusalPrefixes = ["AVBÖJER:", "AVBOJER:"];

    /// <summary>True om texten (efter inledande whitespace) inleds med en refusal-markör.</summary>
    public static bool StartsWithRefusalPrefix(string text)
    {
        var trimmed = text.TrimStart();
        return RefusalPrefixes.Any(p => trimmed.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>True om texten fortfarande kan vara början på en refusal-markör. Vänta då med att skicka tills fler tecken kommit.</summary>
    public static bool CouldBeRefusalPrefix(string text)
    {
        var trimmed = text.TrimStart();
        return RefusalPrefixes.Any(p => p.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Tar bort refusal-markör och efterföljande whitespace. Returnerar texten oförändrad om ingen markör finns.</summary>
    public static string TrimRefusalPrefix(string text)
    {
        var trimmed = text.TrimStart();
        foreach (var prefix in RefusalPrefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var leadingWhitespace = text.Length - trimmed.Length;
                return text[(leadingWhitespace + prefix.Length)..].TrimStart();
            }
        }
        return text;
    }

    /// <summary>Flyttar kompletta ord (fram till sista whitespace) från <paramref name="pending"/> till utdata. Ofullständigt slutord stannar kvar.</summary>
    public static string ExtractCompleteWords(StringBuilder pending)
    {
        if (pending.Length == 0)
            return string.Empty;

        var content = pending.ToString();
        var lastWhitespace = -1;
        for (var i = content.Length - 1; i >= 0; i--)
        {
            if (char.IsWhiteSpace(content[i]))
            {
                lastWhitespace = i;
                break;
            }
        }

        if (lastWhitespace < 0)
            return string.Empty;

        var result = content[..(lastWhitespace + 1)];
        pending.Remove(0, lastWhitespace + 1);
        return result;
    }
}
