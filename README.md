# Personalhandboksassistent

> Skolprojekt i utbildningssyfte. Chattassistent i Blazor som svarar på frågor om personalhandboken med hjälp av Azure OpenAI och Azure AI Search.

![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![C#](https://img.shields.io/badge/C%23-239120?logo=csharp&logoColor=white)
![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4)
![Azure OpenAI](https://img.shields.io/badge/Azure-OpenAI-0078D4)
![Azure AI Search](https://img.shields.io/badge/Azure-AI_Search-0078D4)
![Status](https://img.shields.io/badge/Status-Skolprojekt-yellow)

## Vad är detta

Skolprojekt utvecklat i utbildningssyfte för att öva RAG i praktiken, alltså att kombinera sökning i egna dokument med generativ AI. Användaren chattar på sidan `Home`, laddar upp och hanterar dokument via sidan `Admin` och får strömmade svar med källhänvisning till handboken.

Allt ligger i `src/Blazor` som en Blazor Web App i `.NET 10`. Flödet går via `Endpoints/ChatEndpoints.cs` med `MapChatStream`, vidare till `Services/ChatService.cs` som söker relevanta avsnitt med `AzureSearchService` och formulerar svaret med `AzureOpenAiService`. Uppladdade dokument delas upp med `TextChunker` och sparas via `DocumentService`.

## Struktur

```text
Personalhandboksassistent.sln
src/Blazor/              # Blazor Web App (.NET 10)
src/Blazor/Components/   # Sidorna Home, Admin, Error och layout
src/Blazor/Endpoints/    # ChatEndpoints med MapChatStream
src/Blazor/Services/     # ChatService, AzureOpenAiService, AzureSearchService, DocumentService
src/Blazor/Models/       # ChatRequest, ChatResponse, SearchChunk, DocumentMetadata
src/Blazor/Config/       # EnvLoader för lokal .env
```

Använda paket är bland annat `OpenAI`, `Azure.Identity` och `Azure.Search.Documents`.

## Kom igång

```bash
dotnet build Personalhandboksassistent.sln
dotnet run --project src/Blazor
```

Kopiera `src/Blazor/.env.example` till `src/Blazor/.env` och fyll i nycklar för Azure OpenAI och Azure AI Search. De laddas med `Config/EnvLoader.cs` och committas aldrig.
