# Deploying Abhyanvaya — Cloudflare Pages (UI) + API (InsightFace models)

## Why you see "model not found" after deploying to Cloudflare Pages

**Cloudflare Pages hosts only the React UI** (`abhyanvaya-ui`). It does **not** run the .NET API and **cannot** serve ONNX model files.

AI photo attendance calls your **backend API** (configured via `VITE_API_BASE_URL`). The error:

```
InsightFace detection model not found at 'models/insightface/det_10g.onnx'
```

comes from the **API server**, not from Cloudflare Pages.

```
┌─────────────────────────┐         HTTPS API calls          ┌──────────────────────────────┐
│  Cloudflare Pages       │  ─────────────────────────────►  │  Abhyanvaya.API (.NET)       │
│  abhyanvaya-ui.pages.dev│                                  │  + PostgreSQL                │
│  (static React build)   │                                  │  + models/insightface/*.onnx │
└─────────────────────────┘                                  └──────────────────────────────┘
```

---

## What you must deploy separately

| Component | Where | Includes ONNX? |
|-----------|--------|----------------|
| **UI** | Cloudflare Pages | No |
| **API** | VPS / Render / Azure / Docker / IIS | **Yes — required for AI attendance** |

Set in Cloudflare Pages environment:

```
VITE_API_BASE_URL=https://your-api-host.example.com/api
```

(`abhyanvaya-ui/.env.production.example`)

---

## ONNX models and Git LFS

Models live in the repo at:

```
Abhyanvaya.API/models/insightface/det_10g.onnx   (~16 MB)
Abhyanvaya.API/models/insightface/w600k_r50.onnx (~166 MB)
```

They are tracked with **Git LFS** (see `.gitattributes`: `*.onnx filter=lfs`).

If your API deployment checkout does **not** run LFS, you get tiny pointer files (~130 bytes) instead of real ONNX — recognition will fail.

### Building/publishing the API directly on a machine with Git (no Docker)

```bash
git lfs install
git lfs pull
dotnet publish Abhyanvaya.API/Abhyanvaya.API.csproj -c Release -o ./publish
```

Verify file sizes (not ~130 bytes):

```bash
ls -lh Abhyanvaya.API/models/insightface/
```

`Abhyanvaya.API.csproj` includes a publish-time MSBuild validation target
(`ValidateInsightFaceModelsBeforePublish`) that fails the build with a clear error if either model
is missing or still LFS-pointer-sized, so a bad publish can never silently ship.

### Docker builds (AI13.DEPLOY.1)

The `Dockerfile` at the repo root never runs Git or Git LFS. It pulls the already-materialized
models as a small OCI image built by `.github/workflows/build-models-image.yml`
(the only place `git lfs pull` runs in the whole pipeline). See
[`docs/AI13_DEPLOY1_CICD_MODEL_MATERIALIZATION.md`](./AI13_DEPLOY1_CICD_MODEL_MATERIALIZATION.md)
for the full architecture.

---

## Publish the API with models included

The API project copies ONNX files into publish output:

```xml
<!-- Abhyanvaya.API.csproj -->
<Content Include="models\insightface\**\*">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  <CopyToPublishDirectory>Always</CopyToPublishDirectory>
</Content>
```

Publish:

```bash
dotnet publish Abhyanvaya.API/Abhyanvaya.API.csproj -c Release -o ./publish
ls ./publish/models/insightface/
```

Both `.onnx` files must appear in the publish folder with multi-megabyte sizes.

---

## Verify after API deploy

1. Open `https://your-api-host/health/ready` (or check startup logs).
2. Confirm model diagnostics report:
   - `Resolved Model Directory` exists
   - `det_10g.onnx` and `w600k_r50.onnx` **Found: true** with size in MB (not 0.0).

---

## Quick checklist

- [ ] UI deployed to Cloudflare Pages with correct `VITE_API_BASE_URL`
- [ ] API deployed separately (not on Cloudflare Pages)
- [ ] `git lfs pull` run before API build
- [ ] `publish/models/insightface/*.onnx` present and large
- [ ] `/health/ready` shows models present
- [ ] CORS allows your Pages origin (`Cors:AllowCloudflarePages` in production settings)
