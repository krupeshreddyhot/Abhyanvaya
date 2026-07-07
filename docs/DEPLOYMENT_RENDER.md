# Deploying Abhyanvaya API on Render (+ Cloudflare Pages UI)

Your setup:

| Layer | Host |
|-------|------|
| **UI** | Cloudflare Pages (`*.pages.dev`) |
| **API** | Render (Web Service) |
| **Database** | Render PostgreSQL (recommended) |

The InsightFace ONNX models **must be present on Render**, not on Cloudflare Pages.

---

## Root cause of `det_10g.onnx not found` on Render

Render clones your Git repo but **does not download Git LFS objects by default**. You get ~130-byte **pointer files** instead of the real ~16 MB / ~166 MB ONNX files.

**Fix:** run `git lfs pull` in the **Render build command** before `dotnet publish`.

This repo includes `scripts/render-build.sh` which:

1. Installs `git-lfs` if missing  
2. Runs `git lfs pull`  
3. Verifies both ONNX files are > 1 MB  
4. Runs `dotnet publish` (models copied via `Abhyanvaya.API.csproj`)

---

## Option A — Update existing Render Web Service (recommended)

Check your service **Runtime** in Render Dashboard:

### If Runtime = **Docker**

Push the updated `Dockerfile` (it runs `git lfs pull` during the image build). Trigger **Manual Deploy → Clear build cache & deploy**.

No custom build command needed — Render uses the Dockerfile.

### If Runtime = **Native / Shell** (custom build, not Docker)

In **Render Dashboard → your API service → Settings**:

#### Build Command

```bash
bash scripts/render-build.sh
```

(Or inline:)

```bash
git lfs install && git lfs pull && dotnet publish Abhyanvaya.API/Abhyanvaya.API.csproj -c Release -o ./publish && ls -lh ./publish/models/insightface/
```

### Start Command

```bash
cd ./publish && dotnet Abhyanvaya.API.dll
```

### Root Directory

Leave **empty** (repository root), unless you use a monorepo filter.

### Health Check Path

```
/health/ready
```

Should return models as found after a good deploy.

---

## Option B — Render Blueprint

Commit `render.yaml` and deploy via **New → Blueprint** in Render. Adjust `branch`, `region`, and `plan` as needed.

---

## Required environment variables (Render → Environment)

| Variable | Example / notes |
|----------|-----------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | Render Postgres **Internal** URL (`postgres://...` or Npgsql format) |
| `Jwt__Key` | Long random secret (Render can generate) |
| `Cors__AllowCloudflarePages` | `true` — allows any `https://*.pages.dev` origin |
| `Cors__ReactOrigin` | Optional: `https://abhyanvaya-ui.pages.dev` (comma-separated for multiple) |
| `UseRedis` | `false` unless you added Render Redis |
| `PORT` | Set automatically by Render — do not override |

### Cloudflare Pages UI

Set in Cloudflare Pages → Environment variables:

```
VITE_API_BASE_URL=https://<your-render-service>.onrender.com/api
```

Replace `<your-render-service>` with your actual hostname (e.g. `abhyanvaya-api.onrender.com`).

---

## Render + Git LFS note

Render's default `git clone` does **not** download LFS objects. Without `git lfs pull`:

- Local/dev: models work after `git lfs pull`
- Render build: only pointer files → **RecognitionError**

Both fixes in this repo address that:

| Deploy type | Fix |
|-------------|-----|
| **Docker** (Render) | Updated `Dockerfile` runs `git lfs pull` during build |
| **Shell build** | `scripts/render-build.sh` runs `git lfs pull` before `dotnet publish` |

---

## After deploy — verify

1. **Build logs** on Render should show:
   ```
   OK: Abhyanvaya.API/models/insightface/det_10g.onnx (16923827 bytes)
   OK: Abhyanvaya.API/models/insightface/w600k_r50.onnx (...)
   ```
2. Open: `https://<your-api>.onrender.com/health/ready`  
   Models should report **Found: true** with sizes in MB.
3. From Cloudflare UI, run AI photo attendance again.

---

## Git LFS bandwidth / billing

GitHub LFS has storage and bandwidth limits. Render pulls LFS on **every build**. If builds fail with LFS errors:

- Confirm LFS objects are pushed: `git lfs ls-files`
- Check GitHub LFS quota
- Consider caching models on Render disk or hosting ONNX on S3/R2 (future optimization)

---

## Migrations

Run EF migrations against Render Postgres before or after first deploy:

```bash
dotnet ef database update --project Abhyanvaya.Infrastructure --startup-project Abhyanvaya.API
```

Use the **External** database URL from your local machine, or a one-off Render shell job.

---

## Related docs

- `docs/DEPLOYMENT_CLOUDFLARE_UI_AND_API.md` — UI vs API split, LFS overview
