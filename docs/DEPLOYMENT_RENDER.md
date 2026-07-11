# Deploying Abhyanvaya API on Render (+ Cloudflare Pages UI)

Your setup:

| Layer | Host |
|-------|------|
| **UI** | Cloudflare Pages (`*.pages.dev`) |
| **API** | Render (Web Service) |
| **Database** | Render PostgreSQL (recommended) |

The InsightFace ONNX models **must be present on Render**, not on Cloudflare Pages.

Render deploys this API using **Docker** (`runtime: docker` in `render.yaml`) — Render has no native
.NET runtime, so this is the only supported Render deployment mode for this repo (see
[Render FAQ](https://render.com/docs/faq)).

---

## AI13.DEPLOY.1 — Architecture: Docker is Git-independent

As of AI13.DEPLOY.1, the `Dockerfile` at the repo root **never runs `git` or `git lfs`**, and never
downloads anything at container runtime. The two ONNX models are baked into the image at build time
by copying them in from a small, separately-published **models asset image**
(`ghcr.io/<owner>/abhyanvaya-insightface-models`), which is built by
`.github/workflows/build-models-image.yml` — the only place `git lfs pull` runs in this pipeline.

Render's Docker build simply pulls that models image as an ordinary build stage (`ARG MODELS_IMAGE`
in the `Dockerfile`) — no Render-specific configuration is required for this to work, and Render's
**GitHub → automatic Docker build/deploy** trigger is completely unchanged.

Full rationale, sequence diagrams, and the alternatives considered (including why Render's git-backed
Docker build cannot run a pre-build CI step directly) are documented in
[`docs/AI13_DEPLOY1_CICD_MODEL_MATERIALIZATION.md`](./AI13_DEPLOY1_CICD_MODEL_MATERIALIZATION.md).

### One-time bootstrap (only needed once, before the first deploy after this change)

1. Push this branch so `.github/workflows/build-models-image.yml` runs at least once and publishes
   `ghcr.io/<owner>/abhyanvaya-insightface-models:latest`.
2. In GitHub → **Packages** → `abhyanvaya-insightface-models` → **Package settings**, set visibility
   to **Public** (simplest — no Render registry credential needed), or keep it **Private** and add a
   matching **Registry Credential** in the Render service's Docker settings.
3. Trigger a normal Render deploy (push to the branch Render watches, or **Manual Deploy**).

No manual model download/copy/upload is ever required by a developer — the GitHub Actions workflow
is the only thing that touches Git LFS.

### If your service still shows an older Runtime = **Native / Shell**

That mode never worked for this project (Render doesn't support .NET natively) and is not part of
this architecture. Switch the service **Runtime** to **Docker** in Render Dashboard → Settings.

---

## Render Blueprint

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

## Render + Git LFS note (AI13.DEPLOY.1)

Render's `git clone` (used to fetch your Dockerfile/source before invoking `docker build`) does
**not** download Git LFS objects — this is a
[known Render limitation](https://feedback.render.com/features/p/ignore-lfs-during-git-clone-step)
with no built-in workaround, and Render's Docker build step **cannot run a custom command before
`docker build`**, so `git lfs pull` cannot run "just before" the build even if we wanted it to.

That's exactly why LFS materialization was moved out of Render entirely:

| Component | Role |
|-----------|------|
| `.github/workflows/build-models-image.yml` | The only place `git lfs pull` runs. Publishes real ONNX bytes as `ghcr.io/<owner>/abhyanvaya-insightface-models`. |
| `Dockerfile` (repo root) | Pulls that models image as a normal build stage. Never runs git. |
| Render | Unchanged — still does `git clone` + `docker build` on push, same as before. The pointer stubs Render's clone produces are simply never used; they're overwritten by the real bytes copied in from the models image. |

See [`docs/AI13_DEPLOY1_CICD_MODEL_MATERIALIZATION.md`](./AI13_DEPLOY1_CICD_MODEL_MATERIALIZATION.md)
for the full architecture, the alternatives considered, and why this preserves the existing
GitHub → Render automatic deployment workflow unchanged.

---

## After deploy — verify

1. **GitHub Actions** run for `build-models-image.yml` should show:
   ```
   OK: Abhyanvaya.API/models/insightface/det_10g.onnx (16923827 bytes)
   OK: Abhyanvaya.API/models/insightface/w600k_r50.onnx (...)
   ```
2. **Render build logs** should show a normal multi-stage `docker build` with no `git`/`git-lfs`
   commands at all.
3. Open: `https://<your-api>.onrender.com/health/ready`  
   Models should report **Found: true** with sizes in MB.
4. From Cloudflare UI, run AI photo attendance again.

---

## Git LFS bandwidth / billing

GitHub LFS has storage and bandwidth limits. With AI13.DEPLOY.1, LFS is only pulled by
`build-models-image.yml`, and only when `Abhyanvaya.API/models/insightface/**` actually changes —
not on every Render build. If that workflow fails with LFS errors:

- Confirm LFS objects are pushed: `git lfs ls-files`
- Check GitHub LFS quota

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
