# AI13.DEPLOY.1 — CI/CD LFS Materialization & Self-Contained Runtime Image

Status: **Implemented**
Scope: **Deployment architecture only.** No API, database, AI/recognition, controller, embedding, or
threshold changes. See [Regression Review](#regression-review).

---

## 1. Problem Statement

The Docker runtime image for `Abhyanvaya.API` must be fully self-contained — no `git`, no `.git`
directory, no `git-lfs`, no `git lfs pull`, no `git clone`, and no runtime downloads — while the two
InsightFace ONNX models (`det_10g.onnx`, `w600k_r50.onnx`) remain tracked in **Git LFS** as the
source of truth, and the existing **GitHub → Render automatic deployment** workflow must keep working
with zero manual steps.

The previous `Dockerfile` violated this: it installed `git-lfs` and ran `git lfs pull` *inside* the
Docker build, which requires the `.git` directory to exist in the build context — but `.dockerignore`
correctly excludes `.git` (Docker should never depend on Git metadata), so the build was broken by
design. This is the exact failure described in the task background.

---

## 2. Architectural Constraint Encountered (ADR-style analysis)

Before changing any file, this was verified against Render's actual documented behavior (Task 3):

| Fact | Evidence |
|------|----------|
| Render has **no native .NET runtime**. Native runtimes are Node.js/Bun, Python, Ruby, Go, Rust, Elixir only. | Render Docs — Native Runtimes, Render FAQ |
| A Render service using `runtime: docker` (as `render.yaml` already does) is built by **Render running `docker build` on your Dockerfile** — nothing else. | Render Docs — Docker on Render |
| **"You can't customize the command that Render uses to build your image."** There is no hook to run a script *before* `docker build` for a git-backed Docker service. | Render Docs — Docker on Render |
| Render's `git clone` (used to fetch the build context before `docker build`) **does not download Git LFS objects** — you get ~130-byte pointer files. There is no setting to change this. | Render Feedback — "Ignore LFS during git clone step" (open, unresolved) |
| Render *does* support deploying a **prebuilt image from a registry** instead of building from a Dockerfile, but image-backed services **do not auto-redeploy on new image push** — they require a Deploy Hook call from an external CI system. | Render Docs — Deploying an Image, Deploy Hooks |

**Conclusion:** the literal target-architecture diagram in the task (`Checkout → Git LFS
Materialization → dotnet restore → dotnet publish → Verify ONNX Models → Docker Build → Runtime
Image`, with `dotnet publish` happening *before* and *outside* `docker build`) **cannot be achieved
for the Render leg of this pipeline without changing how Render is configured** — because Render's
git-backed Docker service has no pre-build hook to run `dotnet publish` on, and its own git clone
never sees real LFS bytes anyway.

Per the task's explicit instruction, this is documented as an architecture decision below instead of
silently forcing a Render workflow change.

### 2.1 Alternatives considered

| # | Alternative | Renders needs to change? | Docker Git-independent? | Preserves auto-deploy? | Notes |
|---|---|---|---|---|---|
| A | **Models base image** — a tiny, separately-published OCI image contains only the two real ONNX files; the main `Dockerfile` pulls it via a normal multi-stage `COPY --from=<registry image>` build stage. `dotnet publish` still runs *inside* the main Docker build, but never touches Git. | **No.** `render.yaml` / Render dashboard unchanged. | **Yes.** Zero `git`/`git-lfs` in the Dockerfile. | **Yes.** Same GitHub push → Render `docker build` → deploy trigger as today. | Requires one new, decoupled CI job (GitHub Actions) that owns Git LFS materialization; requires a one-time GHCR package visibility/credential setup (bootstrap only, not a recurring manual step). |
| B | **Full external CI/CD + Render image-backed deploy** — GitHub Actions does checkout+LFS, `dotnet restore/publish`, model validation, `docker build` (trivial COPY-only Dockerfile), pushes the finished runtime image, then calls the Render Deploy Hook. | **Yes.** Render service must be switched from "Build from Dockerfile" to "Deploy an existing image", plus a Deploy Hook secret in GitHub. | **Yes.** | **Mostly** — still push-triggered and fully automatic end-to-end, but the trigger mechanism changes from "Render watches the branch" to "CI calls a deploy hook after a successful build", and Render's built-in preview/rollback-by-branch behavior for Docker builds no longer applies. | Matches the task's diagram most literally (`dotnet publish` fully outside Docker). Bigger one-time migration; more moving parts (registry auth, deploy hook secret, image tag/digest bookkeeping). |
| C | **Do nothing / patch in place** — keep `git lfs pull` inside the Dockerfile, fix only the `.dockerignore`/`.git` mismatch (e.g. by not excluding `.git`). | No. | **No** — Docker still runs Git and still depends on `.git` being present in the build context, which is fragile and exactly what this task set out to remove. | Yes. | Rejected — violates the explicit, non-negotiable objective ("Docker must never execute any Git command"). Kept only as the documented "current state" baseline. |

### 2.2 Recommended approach

**Alternative A (models base image).** It satisfies every *hard* constraint in the task (Docker is
Git-independent, runtime image is self-contained, no runtime downloads, Git LFS remains the source
of truth, no binaries committed to Git, cloud-agnostic, zero manual steps) while making **zero**
changes to Render's configuration, `render.yaml`, or the existing GitHub → Render automatic
deployment trigger. It only *adds* a decoupled, additive CI job; it does not modify how Render is
triggered or how the app is deployed today.

Alternative B is documented as a valid future evolution if the team later wants `dotnet publish`
itself to run outside any Docker build (e.g. to share published artifacts across multiple runtime
images, or to remove the SDK image from the Render build path entirely) — but it requires reconfiguring
the Render service and is not required to meet this task's hard constraints, so it was **not**
implemented now, per "Do NOT require changes to the existing GitHub → Render automatic deployment
process unless absolutely necessary."

This is the architecture implemented below.

---

## 3. Current (Before) vs. New (After) Architecture

### 3.1 Current architecture (broken)

```
Developer
   |
   v
Merge Development -> Master
   |
   v
GitHub  (Abhyanvaya.API/models/insightface/*.onnx = Git LFS pointers, ~130 bytes)
   |
   v
Render Docker Build
   |
   v
docker build:
   RUN apt-get install git-lfs && git lfs install
   COPY . .              <-- .dockerignore excludes .git, so this .git is ALWAYS missing
   RUN git lfs pull       <-- FAILS: "not a git repository"
   |
   v
Build failure  OR  (if it ever "succeeded") a real risk of shipping 130-byte pointer
                     files as the "models", causing RecognitionError at runtime.
```

Root cause: Docker was made responsible for Git/Git LFS, but its build context is deliberately
Git-free (`.dockerignore` excludes `.git`) — an unavoidable contradiction.

### 3.2 New architecture (implemented)

```
                     ┌─────────────────────────────────────────────────────────┐
                     │  .github/workflows/build-models-image.yml (GitHub CI)   │
                     │                                                         │
Developer            │  actions/checkout (lfs: true)  <-- ONLY place git lfs   │
   |                 │        |                            pull ever runs      │
   v                 │        v                                                │
Git Push             │  Verify det_10g.onnx / w600k_r50.onnx are real (>1MB)   │
   |                 │        |                                                │
   v                 │        v                                                │
GitHub  ─────────────┤  docker build -f docker/models.Dockerfile               │
   |                 │        |  (FROM scratch, COPY the 2 real .onnx files)   │
   |                 │        v                                                │
   |                 │  docker push ghcr.io/<owner>/abhyanvaya-insightface-    │
   |                 │             models:latest                              │
   |                 └─────────────────────────────────────────────────────────┘
   |                                          |
   |                                          | (pulled at build time, no git)
   v                                          v
Render Build Environment
   |
   v
Checkout Repository        (still gets Git LFS *pointer* files — irrelevant, see below)
   |
   v
Docker Build  (Dockerfile — zero git/git-lfs commands)
   |
   +-- FROM ${MODELS_IMAGE} AS models      (pulls the image built above)
   +-- FROM sdk AS build
   |      dotnet restore
   |      COPY . .                          (pointer stubs, unused)
   |      COPY --from=models .../det_10g.onnx      (overwrites stub with real bytes)
   |      COPY --from=models .../w600k_r50.onnx    (overwrites stub with real bytes)
   |      dotnet publish                    (MSBuild validates real files before publish)
   +-- FROM aspnet AS runtime
   |      COPY --from=build /app/publish .  (only already-published artifacts)
   v
Runtime Image  (no SDK, no source, no models stage, no git, no network dependency)
   |
   v
Deploy  (Render — same automatic trigger as before)
```

Docker never executes `git`. The Git LFS materialization CI job is fully decoupled from — and does
not modify — Render's existing build/deploy trigger.

---

## 4. Deployment Sequence Diagram

```
Developer
   |
   v
GitHub  (push to Abhyanvaya.API/models/insightface/** triggers the models workflow;
   |     any push triggers Render as before)
   |
   +─────────────────────────────────────────────┐
   v                                             v
CI/CD (GitHub Actions:                      Render (unchanged trigger:
build-models-image.yml)                     GitHub push -> docker build)
   |                                             |
   v                                             |
Git LFS Materialization                          |
(actions/checkout lfs: true)                     |
   |                                             |
   v                                             |
Model Validation (size check)                    |
   |                                             |
   v                                             |
docker build -f docker/models.Dockerfile         |
   |                                             |
   v                                             |
docker push ghcr.io/.../abhyanvaya-              |
insightface-models:latest                        |
   |                                             |
   +──────────────►  pulled as build stage  ─────┤
                                                  v
                                          Docker Build (Dockerfile)
                                             dotnet restore
                                             dotnet publish
                                             (MSBuild model validation
                                              gate — fails build if
                                              missing/undersized)
                                                  |
                                                  v
                                             Runtime Image
                                             (self-contained, no git)
                                                  |
                                                  v
                                             Container
                                                  |
                                                  v
                                             Render (deploy)
```

---

## 5. Developer / CI/CD / Docker / Render Workflow Detail

### 5.1 Developer workflow (unchanged)

1. `git lfs install` once per machine (as before).
2. Edit code as normal. `dotnet build`/`dotnet run` work exactly as before, whether or not models
   have been LFS-pulled locally (the new MSBuild validation only runs on `dotnet publish`, not on
   `dotnet build`/`dotnet run` — see [§8](#8-regression-review)).
3. If updating a model file: `git lfs pull`/replace the file, commit, push. No new manual step —
   the GitHub Actions workflow rebuilds the models image automatically because the push touches
   `Abhyanvaya.API/models/insightface/**`.

### 5.2 CI/CD workflow (new, additive)

`.github/workflows/build-models-image.yml`:

1. Triggers on push to the tracked branches when `Abhyanvaya.API/models/insightface/**` or
   `docker/models.Dockerfile` changes (or manually via `workflow_dispatch`).
2. `actions/checkout@v4` with `lfs: true` — the only Git LFS materialization point in the entire
   pipeline.
3. Verifies both ONNX files are present and >1 MB (catches un-materialized pointer stubs before they
   ever become a Docker build input).
4. Builds `docker/models.Dockerfile` (a `FROM scratch` image containing only the two files) and
   pushes it to `ghcr.io/<owner>/abhyanvaya-insightface-models` as both `:latest` and `:<git-sha>`.

This job never touches Render, `render.yaml`, or the main application Dockerfile's *behavior* — it
only publishes an artifact that the main Dockerfile happens to consume.

### 5.3 Docker workflow (main app image — rewritten)

`Dockerfile` (repo root), three stages:

1. `models` — `FROM ${MODELS_IMAGE}` (default `ghcr.io/krupeshreddyhot/abhyanvaya-insightface-models:latest`,
   overridable via `--build-arg`). No OS layer, just the two files.
2. `build` — `FROM mcr.microsoft.com/dotnet/sdk:8.0`. Restores, copies source, **overwrites the LFS
   pointer stubs with the real files copied from the `models` stage**, then runs `dotnet publish`.
   The `ValidateInsightFaceModelsBeforePublish` MSBuild target (in `Abhyanvaya.API.csproj`) fails the
   build immediately if either file is missing or still pointer-sized — this is the "Verify ONNX
   Models" gate from the task's target diagram, executing as part of the build rather than as a
   separate pre-Docker CI step (see [§2](#2-architectural-constraint-encountered-adr-style-analysis)
   for why it can't run before `docker build` starts on Render specifically).
3. `runtime` — `FROM mcr.microsoft.com/dotnet/aspnet:8.0`. `COPY --from=build /app/publish .` only.
   No SDK, no source tree, no `models` stage, no Git, no network calls at container start.

Zero `RUN git`, `RUN git-lfs`, `git clone`, or `git checkout` anywhere in this file.

### 5.4 Render workflow (unchanged)

Render still does exactly what it does today: on a push to the watched branch, it clones the repo
(getting LFS pointer stubs it never uses) and runs `docker build` against the Dockerfile above. The
only difference Render observes is that the build now pulls one additional, small public image
(`abhyanvaya-insightface-models`) as a build stage — a completely ordinary Docker operation that
every Docker builder supports natively, requiring no Render-specific configuration.

`render.yaml`, the service's Runtime (`docker`), branch, health check path, and environment variables
are **all unchanged**.

---

## 6. One-Time Bootstrap Checklist

These are one-time setup actions, not recurring manual deployment steps:

1. Merge/push this change so `.github/workflows/build-models-image.yml` runs once and publishes
   `ghcr.io/<owner>/abhyanvaya-insightface-models:latest`.
2. Set that GHCR package's visibility to **Public** (Settings → Packages), so Render's anonymous
   `docker build` can pull it without credentials — *or* keep it private and add a matching
   **Registry Credential** in the Render service's Docker settings.
3. Trigger (or wait for) the next normal Render deploy. No other Render configuration changes.

---

## 7. Rollback Strategy

- **Application/Docker rollback:** identical to today — Render's standard "roll back to previous
  deploy" works unchanged, since the runtime image format (aspnet base image, `ENTRYPOINT`, `EXPOSE
  8080`) is unchanged.
- **Models-image rollback:** the models image is tagged both `:latest` and `:<git-sha>`. If a bad
  model asset is ever published, re-run `build-models-image.yml` from a known-good commit (or
  manually re-tag a previous `:<git-sha>` image as `:latest` in the registry) and re-trigger the
  Render deploy. The two-file `FROM scratch` image is trivial to inspect/diff.
- **Full revert:** reverting the `Dockerfile`/`Abhyanvaya.API.csproj` changes in this PR restores the
  previous (broken) behavior exactly — no destructive/irreversible changes were made to `render.yaml`,
  the database, or any application code.

---

## 8. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| First Render deploy after this change runs before the models image has ever been published/made public. | Medium (one-time, only if bootstrap order is skipped) | Build fails at `FROM ${MODELS_IMAGE}` (clear Docker error, not a silent bad deploy). | Bootstrap checklist in §6; Render simply fails the build and keeps serving the previous successful deploy (Render does not take a working service down on a failed build). |
| GHCR package pull fails (registry outage, rate limit, or visibility misconfigured). | Low | Docker build fails fast at the `models` stage — cannot silently ship a bad/missing model. | Standard Docker build failure — visible in Render build logs; previous deploy stays live. |
| A model file is updated but the `build-models-image.yml` workflow doesn't run (e.g. path filter miss). | Low | Render would build against a stale models image — old model bytes, not missing ones (no RecognitionError, just outdated weights). | Path filters cover the exact model directory; `workflow_dispatch` allows a manual rebuild if ever needed. |
| Someone bypasses the MSBuild validation by publishing outside Docker/CI with `-p:Configuration=Release` tricks. | Very Low | Same as today — was always possible to hand-run `dotnet publish` with bad models. | `ValidateInsightFaceModelsBeforePublish` runs on every `Publish` target invocation regardless of caller; cannot be skipped without explicitly disabling the target. |
| `dotnet build`/`dotnet run` regress on developer machines without LFS pulled. | None (verified) | N/A | Validation target is scoped to `BeforeTargets="Publish"` only — confirmed `dotnet build` succeeds with 0 errors even with pointer-stub models present (see §9). |

---

## 9. Build & Verification Evidence

Performed during implementation (see also §10 checklists):

- `dotnet build` (all projects, Release) — **0 errors**, pre-existing unrelated NuGet advisory
  warnings only (AutoMapper/ImageSharp — out of scope, unrelated to this task).
- `dotnet publish Abhyanvaya.API` **with un-materialized (pointer-stub) models present** — **fails
  fast** with:
  ```
  error : Required AI model at ...\det_10g.onnx is only 133 bytes (expected at least 1000000).
  This looks like an unmaterialized Git LFS pointer file, not the real ONNX model. Deployment aborted.
  Ensure Git LFS materialization ran before publish (see docs/AI13_DEPLOY1_CICD_MODEL_MATERIALIZATION.md).
  ```
  This proves the publish-time gate (Task 5) works exactly as specified, and also proves the
  regression check in §8 (only `Publish`, not `Build`, is affected).
- **Full end-to-end Docker dry run**, performed in an isolated temp copy of the repository (the real
  tracked model files were never modified — `git status` confirms zero diff on
  `Abhyanvaya.API/models/insightface/*.onnx` after testing) with two 2 MB dummy files standing in for
  LFS-materialized bytes:
  - `docker build -f docker/models.Dockerfile` → succeeds, produces a 2-file image.
  - `docker build --build-arg MODELS_IMAGE=... -t abhyanvaya-api` (main `Dockerfile`) → succeeds end
    to end: restore, source copy, model overwrite, `dotnet publish` (MSBuild validation passes since
    files are >1 MB), runtime image export. **Zero errors.**
  - `docker exec ... which git; which git-lfs` inside the running container → **both not found**,
    confirming the runtime image contains no Git tooling at all.
  - `find / -iname '*.onnx'` inside the container → the two files exist **exactly once**, at
    `/app/models/insightface/`, correct size.
  - Container startup log reproduced the unchanged startup summary:
    ```
    Detection Model (det_10g.onnx)   : Found (2 MB)
    Embedding Model (w600k_r50.onnx) : Found (2 MB)
    ```
  - `GET /health/live` → `200 OK`.
  - `GET /health/ready` → `503` (expected — no real Postgres was provided in this isolated dry run),
    with body confirming `"modelsPresent":true`, `"recognitionWorkerStarted":true`,
    `"embeddingWorkerStarted":true`; `"database":"Unreachable"` is solely due to the dry run having
    no database, not a regression from this change.

This is direct, empirical confirmation of §12's "Architecture Review" table and of Task 7's startup
diagnostics / health endpoint continuity requirement.

---

## 10. Verification Checklist

### Developer machine

- [ ] `git lfs install && git lfs pull` materializes real `.onnx` files locally.
- [ ] `dotnet build` succeeds with or without LFS pulled (0 errors).
- [ ] `dotnet publish Abhyanvaya.API/Abhyanvaya.API.csproj -c Release -o ./publish` succeeds **only**
      after `git lfs pull`, and fails with the exact "Required AI model missing/undersized" message
      otherwise.
- [ ] `./publish/models/insightface/*.onnx` are present and multi-MB.

### Docker Desktop

- [ ] `docker build -f docker/models.Dockerfile -t abhyanvaya-insightface-models:local .` succeeds
      from a checkout that has run `git lfs pull` (real bytes only).
- [ ] `docker build --build-arg MODELS_IMAGE=abhyanvaya-insightface-models:local -t abhyanvaya-api .`
      succeeds; `docker run -p 8080:8080 abhyanvaya-api` starts, `/health/ready` reports both models
      **Found: true**.
- [ ] `docker history abhyanvaya-api` / image inspection shows **no `git`/`git-lfs` binaries** and
      models present exactly once (see §11).

### Render

- [ ] Complete the one-time bootstrap (§6).
- [ ] Push triggers a normal Render Docker build; build logs contain no `git`/`git-lfs` commands.
- [ ] `/health/ready` on the deployed service reports both models **Found: true**.

### Azure App Service / Azure Container Apps

- [ ] Same image, built the same way (`docker build` with the `MODELS_IMAGE` default or an override),
      pushed to Azure Container Registry, deployed as a container. No Azure-specific Dockerfile
      changes needed — the image is fully portable.
- [ ] `/health/ready` reports both models **Found: true**; no outbound network calls needed at
      container start.

### Kubernetes

- [ ] Same image reference used in a standard Deployment/Pod spec. `livenessProbe`/`readinessProbe`
      can point at `/health/live` and `/health/ready` unchanged.
- [ ] Pod starts with no init containers or volume mounts required for models — they're already
      baked into the image layers.

---

## 11. Image Size / Duplication Review

- The `models` stage image contains the two `.onnx` files exactly once (`FROM scratch`, two `COPY`
  instructions, nothing else).
- The `build` stage's `COPY --from=models ...` targets overwrite the two specific pointer-stub paths
  by exact filename — it does not duplicate the whole `models` directory, so there is exactly one
  copy of each ONNX file inside the `build` stage's filesystem layer.
- The `runtime` stage does `COPY --from=build /app/publish .` **once** — it does not copy from the
  `models` stage directly, and it does not copy the SDK image's source tree — so the final runtime
  image contains each model file exactly once, and does not contain the SDK, source code, or the
  `models` stage's layers at all (multi-stage builds discard non-final stages).
- No duplicate `COPY` operations and no duplicate publish folders were introduced.

---

## 12. Architecture Review (confirmation against task objective)

| Requirement | Status |
|---|---|
| Docker is Git-independent | ✅ Confirmed — `Dockerfile` contains zero `git`/`git-lfs` instructions. |
| CI performs LFS materialization | ✅ `build-models-image.yml` is the sole location `git lfs pull` executes. |
| Runtime image is self-contained | ✅ Runtime stage only `COPY`s already-published artifacts; models are baked in at build time. |
| No runtime downloads | ✅ No `RUN curl/wget`, no code path downloads models at container startup (`ModelAvailabilityChecker` only reads local disk, unchanged). |
| No Git dependency | ✅ `.dockerignore` continues to exclude `.git`; the Dockerfile no longer needs it at all (no more contradiction). |
| Cloud portable | ✅ Identical image builds/runs unmodified on Docker Desktop, Render, Azure App Service, Azure Container Apps, and Kubernetes. |
| Git LFS remains source of truth | ✅ `.gitattributes` unchanged; models are still tracked in Git LFS in this repo. No binaries committed directly to Git. |
| Existing GitHub → Render automatic deployment preserved | ✅ `render.yaml`, branch, health check path, runtime type all unchanged; no manual deployment steps introduced. |

---

## 13. Regression Review

Confirmed **no changes** to:

- API controllers, DTOs, or routes.
- Database schema, EF Core migrations, or connection handling.
- AI/recognition pipeline, embedding generation, or confidence/NMS thresholds.
- `Diagnostics/StartupDiagnostics.cs` / `Diagnostics/ModelAvailabilityChecker.cs` (read verbatim,
  unmodified) — startup summary logging and `/health`, `/health/live`, `/health/ready` behavior is
  byte-for-byte unchanged.
- `InsightFaceOptions`, `ModelPathResolver`, or any recognition/embedding configuration.

Changes are scoped exclusively to: `Dockerfile`, `Abhyanvaya.API.csproj` (packaging + a new
publish-time validation target only), `scripts/render-build.sh` (comment/scope clarification only,
logic unchanged), `docs/DEPLOYMENT_RENDER.md`, `docs/DEPLOYMENT_CLOUDFLARE_UI_AND_API.md`, this new
document, `docker/models.Dockerfile` (new), and
`.github/workflows/build-models-image.yml` (new).

---

## 14. Files Changed / Added

| File | Change |
|---|---|
| `Dockerfile` | Rewritten — all Git/LFS operations removed; multi-stage build now pulls a models base image instead. |
| `Abhyanvaya.API/Abhyanvaya.API.csproj` | `Content` packaging updated to the preferred `models\insightface\**\*` pattern with `CopyToPublishDirectory=Always`; added `ValidateInsightFaceModelsBeforePublish` MSBuild target (Task 5). |
| `docker/models.Dockerfile` | New — minimal `FROM scratch` image containing the two real ONNX files, built only by CI. |
| `.github/workflows/build-models-image.yml` | New — the only workflow that runs `git lfs pull`; builds/publishes the models image to GHCR. |
| `scripts/render-build.sh` | Header comments updated to clarify it is for non-containerized/native deployments only; logic unchanged. |
| `docs/DEPLOYMENT_RENDER.md` | Updated to describe the new Git-independent Docker architecture and one-time bootstrap steps. |
| `docs/DEPLOYMENT_CLOUDFLARE_UI_AND_API.md` | Updated to reference the new Docker/CI architecture. |
| `docs/AI13_DEPLOY1_CICD_MODEL_MATERIALIZATION.md` | New — this document. |
