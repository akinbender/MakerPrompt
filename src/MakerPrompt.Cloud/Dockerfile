# Cloud — Production (x64)
# Build: docker build -f src/MakerPrompt.Cloud/Dockerfile -t makerprompt/cloud:latest .
# (Run from repo root)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore layer — only copy project files first to cache packages.
COPY MakerPrompt.sln .
COPY src/MakerPrompt.Core/MakerPrompt.Core.csproj                           src/MakerPrompt.Core/
COPY src/MakerPrompt.Infrastructure/MakerPrompt.Infrastructure.csproj       src/MakerPrompt.Infrastructure/
COPY src/MakerPrompt.Infrastructure.Sqlite/MakerPrompt.Infrastructure.Sqlite.csproj src/MakerPrompt.Infrastructure.Sqlite/
COPY src/MakerPrompt.Cloud/MakerPrompt.Cloud.csproj                         src/MakerPrompt.Cloud/
RUN dotnet restore src/MakerPrompt.Cloud/MakerPrompt.Cloud.csproj

# Publish the API host.
COPY src/ src/
RUN dotnet publish src/MakerPrompt.Cloud/MakerPrompt.Cloud.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o /app/publish

# ── Runtime image ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app

RUN mkdir -p /app/data \
    && chown -R $APP_UID:$APP_UID /app
USER $APP_UID

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "MakerPrompt.Cloud.dll"]
