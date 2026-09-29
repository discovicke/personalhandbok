namespace Shared.Models;

/// <summary>Filttyper som kan laddas upp.</summary>
public enum DocumentType
{
    Unknown,
    Pdf,
    Word,
    Markdown,
    Text
}

/// <summary>Hittar dokumenttyp från filnamn eller content type.</summary>
public static class DocumentTypeMapper
{
    /// <summary>Hittar typ från filändelse, faller tillbaka på content type.</summary>
    public static DocumentType FromFileName(string fileName, string? contentType = null)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => DocumentType.Pdf,
            ".docx" or ".doc" => DocumentType.Word,
            ".md" or ".markdown" => DocumentType.Markdown,
            ".txt" or ".text" => DocumentType.Text,
            _ => FromContentType(contentType)
        };
    }

    /// <summary>Hittar typ från en MIME-sträng.</summary>
    public static DocumentType FromContentType(string? contentType) => contentType switch
    {
        "application/pdf" => DocumentType.Pdf,
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document" or "application/msword" => DocumentType.Word,
        "text/markdown" => DocumentType.Markdown,
        var ct when ct is not null && ct.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase) => DocumentType.Text,
        _ => DocumentType.Unknown
    };
}
