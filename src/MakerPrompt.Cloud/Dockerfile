# Cloud — Production (x64)
# Build: docker build -f Dockerfile -t makerprompt/cloud:latest .
# (Run from repo root)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore layer — only copy project files first to cache packages.
COPY MakerPrompt.sln .
COPY src/MakerPrompt.Core/MakerPrompt.Core.csproj                           src/MakerPrompt.Core/
COPY src/MakerPrompt.Infrastructure/MakerPrompt.Infrastructure.csproj       src/MakerPrompt.Infrastructure/
COPY src/MakerPrompt.UI.Components/MakerPrompt.UI.Components.csproj         src/MakerPrompt.UI.Components/
COPY src/MakerPrompt.UI.Blazor/MakerPrompt.UI.Blazor.csproj                 src/MakerPrompt.UI.Blazor/
COPY src/MakerPrompt.Cloud/MakerPrompt.Cloud.csproj                         src/MakerPrompt.Cloud/
RUN dotnet restore src/MakerPrompt.Cloud/MakerPrompt.Cloud.csproj

# Publish (includes Blazor WASM bundle via project reference).
COPY src/ src/
RUN dotnet publish src/MakerPrompt.Cloud/MakerPrompt.Cloud.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o /app/publish

# ── Runtime image ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser && chown appuser /app
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "MakerPrompt.Cloud.dll"]
