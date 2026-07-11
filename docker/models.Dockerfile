# AI13.DEPLOY.1 — InsightFace model asset image.
#
# This is the ONLY place in the whole deployment pipeline where a Git LFS-materialized checkout is
# turned into a Docker build input. It must be built from a working tree where
# `Abhyanvaya.API/models/insightface/*.onnx` are the REAL binaries (i.e. `git lfs pull` has already
# run) — see .github/workflows/build-models-image.yml, which is the only supported way to build and
# publish this image. Do not build this file by hand from a checkout that has not run `git lfs pull`;
# doing so would bake ~130-byte pointer stubs into the image instead of the real models.
#
# The resulting image contains nothing but the two ONNX files (FROM scratch — no OS, no shell, no
# git). The main application Dockerfile (repository root) consumes it purely as a Docker build
# stage via `COPY --from=`, so the application build/runtime never touches Git or Git LFS.
#
# Build context: repository root.
#   docker build -f docker/models.Dockerfile -t abhyanvaya-insightface-models:local .

FROM scratch
COPY Abhyanvaya.API/models/insightface/det_10g.onnx /models/insightface/det_10g.onnx
COPY Abhyanvaya.API/models/insightface/w600k_r50.onnx /models/insightface/w600k_r50.onnx
