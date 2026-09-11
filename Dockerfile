# Build stage — cache NuGet restore via csproj copy first
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/AiDocAssistant.Core/AiDocAssistant.Core.csproj src/AiDocAssistant.Core/
COPY src/AiDocAssistant.Infrastructure/AiDocAssistant.Infrastructure.csproj src/AiDocAssistant.Infrastructure/
COPY ai-doc-assistant/ai-doc-assistant.csproj ai-doc-assistant/
RUN dotnet restore ai-doc-assistant/ai-doc-assistant.csproj

COPY src/ src/
COPY ai-doc-assistant/ ai-doc-assistant/
RUN dotnet publish ai-doc-assistant/ai-doc-assistant.csproj -c Release -o /app/publish

# Runtime — OCR tools for scanned PDFs
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

RUN apt-get update && apt-get install -y --no-install-recommends \
        tesseract-ocr tesseract-ocr-rus poppler-utils \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ai-doc-assistant.dll"]
