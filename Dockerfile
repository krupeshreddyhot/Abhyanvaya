# Build from repository root (folder containing Abhyanvaya.API/).
# Example: docker build -t abhyanvaya-api .
#
# ONNX models (*.onnx) are in Git LFS. This Dockerfile runs `git lfs pull` during the
# image build so Render Docker deploys get real model files, not ~130-byte LFS pointers.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
RUN apt-get update \
    && apt-get install -y --no-install-recommends git-lfs \
    && git lfs install \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src

# Restore first (layer cache) — project files only
COPY Abhyanvaya.Domain/Abhyanvaya.Domain.csproj Abhyanvaya.Domain/
COPY Abhyanvaya.Application/Abhyanvaya.Application.csproj Abhyanvaya.Application/
COPY Abhyanvaya.Infrastructure/Abhyanvaya.Infrastructure.csproj Abhyanvaya.Infrastructure/
COPY Abhyanvaya.API/Abhyanvaya.API.csproj Abhyanvaya.API/
RUN dotnet restore Abhyanvaya.API/Abhyanvaya.API.csproj

# Full source + .git (Render clone includes .git — required for git lfs pull)
COPY . .

RUN git lfs pull \
    && test -f Abhyanvaya.API/models/insightface/det_10g.onnx \
    && test "$(stat -c%s Abhyanvaya.API/models/insightface/det_10g.onnx)" -gt 1000000

WORKDIR /src/Abhyanvaya.API
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Abhyanvaya.API.dll"]
