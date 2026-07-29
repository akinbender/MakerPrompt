# EdgeAgent — Production (ARM64 / Jetson Nano / Raspberry Pi)
# Build: docker build -f src/MakerPrompt.EdgeAgent/Dockerfile -t makerprompt/edge-agent:latest .
# (Run from repo root so the full src/ tree is in context)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and restore in a separate layer to cache NuGet packages.
COPY MakerPrompt.sln .
COPY src/MakerPrompt.Application/MakerPrompt.Application.csproj             src/MakerPrompt.Application/
COPY src/MakerPrompt.Core/MakerPrompt.Core.csproj                           src/MakerPrompt.Core/
COPY src/MakerPrompt.Infrastructure/MakerPrompt.Infrastructure.csproj       src/MakerPrompt.Infrastructure/
COPY src/MakerPrompt.Infrastructure.Sqlite/MakerPrompt.Infrastructure.Sqlite.csproj src/MakerPrompt.Infrastructure.Sqlite/
COPY src/MakerPrompt.EdgeAgent/MakerPrompt.EdgeAgent.csproj                 src/MakerPrompt.EdgeAgent/
RUN dotnet restore src/MakerPrompt.EdgeAgent/MakerPrompt.EdgeAgent.csproj

# Copy source and publish.
COPY src/ src/
RUN dotnet publish src/MakerPrompt.EdgeAgent/MakerPrompt.EdgeAgent.csproj \
    -c Release \
    -r linux-arm64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -o /app/publish

# ── Runtime image ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-arm64v8 AS runtime
WORKDIR /app

# Non-root user for security.
RUN adduser --disabled-password --gecos "" appuser \
    && mkdir -p /app/data \
    && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

# Allow overriding config at runtime via environment variables or volume mounts.
# Example: docker run -v /path/to/appsettings.Production.json:/app/appsettings.Production.json
EXPOSE 8080
ENTRYPOINT ["./MakerPrompt.EdgeAgent"]
