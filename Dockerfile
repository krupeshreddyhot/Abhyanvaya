# Build from repository root (folder containing Abhyanvaya.API/).
# Example: docker build -t abhyanvaya-api .
#
# AI13.DEPLOY.1 — This Dockerfile is completely Git-independent. It never runs `git`, `git lfs`,
# or any network clone/checkout, and it never downloads anything at container runtime. The two
# InsightFace ONNX models (det_10g.onnx, w600k_r50.onnx) are Git-LFS-tracked in this repository,
# but Git LFS materialization happens *outside* this Dockerfile, in the CI/CD build environment
# (see docs/AI13_DEPLOY1_CICD_MODEL_MATERIALIZATION.md). The already-materialized model bytes are
# published as a tiny, versioned OCI image (docker/models.Dockerfile) that this Dockerfile pulls
# as an ordinary Docker build stage — the same mechanism used for any other build dependency, and
# fully supported on every Docker-compatible builder (Docker Desktop, Render, Azure, Kubernetes).
#
# Stages:
#   models  -> read-only source of the two real ONNX files (no git, no OS layer — FROM scratch)
#   build   -> dotnet restore + dotnet publish (models copied in before publish so the project's
#              Content glob in Abhyanvaya.API.csproj packages the *real* files, and the MSBuild
#              ValidateInsightFaceModelsBeforePublish target enforces this — see the .csproj)
#   runtime -> COPY the published output only. No SDK, no source, no models stage, no git.

# Override at build time if you host the models image elsewhere, e.g.:
#   docker build --build-arg MODELS_IMAGE=ghcr.io/<owner>/abhyanvaya-insightface-models:v1 .
ARG MODELS_IMAGE=ghcr.io/krupeshreddyhot/abhyanvaya-insightface-models:latest

FROM ${MODELS_IMAGE} AS models

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first (layer cache) — project files only
COPY Abhyanvaya.Domain/Abhyanvaya.Domain.csproj Abhyanvaya.Domain/
COPY Abhyanvaya.Application/Abhyanvaya.Application.csproj Abhyanvaya.Application/
COPY Abhyanvaya.Infrastructure/Abhyanvaya.Infrastructure.csproj Abhyanvaya.Infrastructure/
COPY Abhyanvaya.API/Abhyanvaya.API.csproj Abhyanvaya.API/
RUN dotnet restore Abhyanvaya.API/Abhyanvaya.API.csproj

# Full source (models/insightface/*.onnx here are still Git LFS pointer stubs — never used directly)
COPY . .

# Overwrite the pointer stubs with the real, CI-materialized model bytes from the `models` stage.
# This is a plain image-to-image file copy — no git, no LFS client, no network call.
COPY --from=models /models/insightface/det_10g.onnx Abhyanvaya.API/models/insightface/det_10g.onnx
COPY --from=models /models/insightface/w600k_r50.onnx Abhyanvaya.API/models/insightface/w600k_r50.onnx

WORKDIR /src/Abhyanvaya.API
# The ValidateInsightFaceModelsBeforePublish MSBuild target (Abhyanvaya.API.csproj) fails this step
# if either model is missing or still LFS-pointer-sized, so a bad `models` image can never reach
# the runtime stage below.
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Abhyanvaya.API.dll"]
