# Personalhandboksassistent

## Struktur

```text
Personalhandboksassistent.sln
src/Blazor/   # Blazor Web App (.NET 10)
src/Backend/  # Console (.NET 10), pratar mot Azure OpenAI + AI Search
src/Shared/   # Delade DTOs
```

Paket i Blazor + Backend: `OpenAI`, `Azure.Identity`, `Azure.Search.Documents`.

## Köra

```bash
dotnet build Personalhandboksassistent.sln
dotnet run --project src/Blazor
dotnet run --project src/Backend
```

