#!/usr/bin/env bash
# AI13.DEPLOY.1 — Native (non-container) build script for Abhyanvaya.API.
#
# NOT used by Render or by the Docker image build. Render has no native .NET runtime, so the
# Render deployment in this repo always uses the Dockerfile (runtime: docker in render.yaml), and
# that Dockerfile is Git-independent by design (see Dockerfile header comment and
# docs/AI13_DEPLOY1_CICD_MODEL_MATERIALIZATION.md). Git LFS materialization for the Docker path is
# performed by .github/workflows/build-models-image.yml, not by this script.
#
# This script remains for genuinely non-containerized deployments (e.g. a bare VM or on-prem host
# with `git` and the .NET SDK installed directly) where the full working tree — including `.git` —
# is naturally available, so running `git lfs pull` in the build step is legitimate and simple.
# Ensures Git LFS ONNX models are real files before dotnet publish.
set -euo pipefail

echo "==> Checking Git LFS availability"
if ! command -v git-lfs >/dev/null 2>&1; then
  echo "git-lfs not found — installing (Render Ubuntu build image)"
  if command -v apt-get >/dev/null 2>&1; then
    apt-get update -qq
    DEBIAN_FRONTEND=noninteractive apt-get install -y -qq git-lfs
  else
    echo "ERROR: git-lfs is required but could not be installed automatically."
    exit 1
  fi
fi

git lfs install
git lfs pull

echo "==> Verifying ONNX model files"
MODEL_DIR="Abhyanvaya.API/models/insightface"
for f in det_10g.onnx w600k_r50.onnx; do
  path="${MODEL_DIR}/${f}"
  if [[ ! -f "$path" ]]; then
    echo "ERROR: Missing ${path}"
    exit 1
  fi
  size=$(stat -c%s "$path" 2>/dev/null || stat -f%z "$path")
  if [[ "$size" -lt 1000000 ]]; then
    echo "ERROR: ${path} is only ${size} bytes — likely a Git LFS pointer, not the real ONNX file."
    echo "       Ensure Git LFS is enabled for your Render repo connection and LFS bandwidth is available."
    exit 1
  fi
  echo "OK: ${path} (${size} bytes)"
done

echo "==> Publishing API"
dotnet publish Abhyanvaya.API/Abhyanvaya.API.csproj -c Release -o ./publish

echo "==> Publish output models"
ls -lh ./publish/models/insightface/
