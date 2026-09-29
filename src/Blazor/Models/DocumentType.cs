namespace Shared.Models;

public enum DocumentType
{
    Unknown,
    Pdf,
    Word,
    Markdown,
    Text
}

public static class DocumentTypeMapper
{
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

    public static DocumentType FromContentType(string? contentType) => contentType switch
    {
        "application/pdf" => DocumentType.Pdf,
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document" or "application/msword" => DocumentType.Word,
        "text/markdown" => DocumentType.Markdown,
        var ct when ct is not null && ct.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase) => DocumentType.Text,
        _ => DocumentType.Unknown
    };
}
