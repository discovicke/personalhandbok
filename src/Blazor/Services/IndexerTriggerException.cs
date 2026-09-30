namespace Blazor.Services;

/// <summary>Filen är uppladdad/raderad i lagringen, men indexern kunde inte startas. Blir sökbart vid nästa körning.</summary>
public sealed class IndexerTriggerException(string message, Exception inner) : Exception(message, inner);
