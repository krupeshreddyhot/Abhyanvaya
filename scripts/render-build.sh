#!/usr/bin/env bash
# Render.com build script for Abhyanvaya.API
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
