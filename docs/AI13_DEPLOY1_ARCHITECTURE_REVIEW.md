# AI13.DEPLOY.1.REVIEW — Deployment Architecture Validation

Status: **Reviewed — read-only validation, no code changes made in this pass.**
Reviewed implementation: AI13.DEPLOY.1 (see
[`docs/AI13_DEPLOY1_CICD_MODEL_MATERIALIZATION.md`](./AI13_DEPLOY1_CICD_MODEL_MATERIALIZATION.md)).

This review re-examines the current state of the repository (`Dockerfile`, `docker/models.Dockerfile`,
`.github/workflows/build-models-image.yml`, `Abhyanvaya.API.csproj`, `render.yaml`, `.dockerignore`,
`Program.cs`, `Diagnostics/ModelAvailabilityChecker.cs`) plus the empirical build/run evidence
gathered during implementation, and independently re-verifies each requirement against source.

`git status` at the time of this review confirms the working tree matches the prior implementation
exactly (`Abhyanvaya.API.csproj`, `Dockerfile`, `docs/DEPLOYMENT_RENDER.md`,
`docs/DEPLOYMENT_CLOUDFLARE_UI_AND_API.md`, `scripts/render-build.sh` modified; `.github/`,
`docker/`, `docs/AI13_DEPLOY1_CICD_MODEL_MATERIALIZATION.md` added) — nothing has drifted since
implementation.

---

## 1. Architecture Compliance

| # | Requirement | Status | Evidence |
|---|---|---|---|
| 1 | Docker no longer executes Git or Git LFS commands | ✅ **Pass** | `Dockerfile` (repo root, 57 lines) contains zero `RUN git`, `RUN git-lfs`, `git clone`, or `git checkout` instructions — confirmed by re-reading the full file. `docker/models.Dockerfile` is `FROM scratch` with two `COPY` instructions only, no shell, no package manager, no git. Empirically confirmed: `docker exec <container> which git; which git-lfs` returned **not found** for both inside the built runtime image. |
| 2 | Git LFS materialization occurs before publish | ✅ **Pass** | `.github/workflows/build-models-image.yml` runs `actions/checkout@v4` with `lfs: true` — this is the only place in the entire pipeline `git lfs pull`-equivalent materialization happens — and it completes (builds + pushes `docker/models.Dockerfile`) *before* the main `Dockerfile`'s `build` stage ever runs `dotnet publish`. Inside the main `Dockerfile`, the `models` stage (pulling the pre-materialized image) is declared and copied from (lines 24, 41–42) strictly before `RUN dotnet publish` (line 48), i.e. materialized bytes are guaranteed present in the build filesystem before publish executes. |
| 3 | Published artifacts contain fully materialized ONNX models (not LFS pointer files) | ✅ **Pass** | The `ValidateInsightFaceModelsBeforePublish` MSBuild target (`Abhyanvaya.API.csproj`, lines 85–100) runs `BeforeTargets="Publish"` and fails the build if either `det_10g.onnx` or `w600k_r50.onnx` is missing **or** smaller than 1,000,000 bytes (an LFS pointer stub is ~130 bytes; real files are multi-MB) — this specifically detects "exists but is a pointer" as a failure, not just "missing". Empirically verified twice: (a) with real pointer stubs present, `dotnet publish` failed with the exact "unmaterialized Git LFS pointer file" error; (b) with the `models` stage's real bytes copied in during the Docker dry run, `dotnet publish` succeeded and the publish output contained the real 2 MB test files. |
| 4 | Docker runtime image contains the ONNX models exactly once | ✅ **Pass** | The `runtime` stage does a single `COPY --from=build /app/publish .` (line 52) — it never copies from the `models` stage directly, and multi-stage builds discard the `models` and `build` stage layers from the final image, so only the `Abhyanvaya.API.csproj` `Content` glob's copy into `/app/publish/models/insightface/` survives into the runtime image. Empirically verified: `find / -iname '*.onnx'` inside the running dry-run container returned exactly two results, both at `/app/models/insightface/`, no duplicates elsewhere in the filesystem. |
| 5 | Startup diagnostics correctly report model availability | ✅ **Pass** | `Diagnostics/ModelAvailabilityChecker.cs` and `Diagnostics/StartupDiagnostics.cs` are byte-for-byte unmodified by AI13.DEPLOY.1 (confirmed by reading both files against the pre-change versions reviewed at the start of implementation). Empirically verified: the dry-run container's startup log printed `Detection Model (det_10g.onnx) : Found (2 MB)` and `Embedding Model (w600k_r50.onnx) : Found (2 MB)`, exactly matching the pre-existing startup summary format. |
| 6 | `/health` and `/health/ready` report both models as Found | ✅ **Pass** | `Program.cs` lines 629–673 (`/health/ready`) and 675–~760 (`/health`) both call the unmodified `ModelAvailabilityChecker.Check(...)` and expose `modelsPresent` (ready) / `detectionModel.status` + `embeddingModel.status` as `"Found"`/`"Missing"` (health) — logic unchanged. Empirically verified: dry-run container's `GET /health/ready` returned `"modelsPresent":true` in the JSON body (503 overall status was solely due to the isolated dry run having no real Postgres connection — unrelated to this change, and not a regression). `GET /health/live` returned `200 OK`. |
| 7 | Render auto-deployment from GitHub still works without manual intervention | ✅ **Pass, with one required one-time bootstrap** | `render.yaml` is completely unmodified: `runtime: docker`, same `branch`, same `dockerfilePath: ./Dockerfile`, same `healthCheckPath: /health/ready`. Render's existing push-triggered `git clone` + `docker build` flow requires no reconfiguration — the only new dependency is that the main `Dockerfile`'s `FROM ${MODELS_IMAGE}` stage must be able to pull `ghcr.io/krupeshreddyhot/abhyanvaya-insightface-models:latest` at build time. This requires the **one-time bootstrap** documented in the implementation doc (§6): push once so the GitHub Actions workflow publishes the image, and make the GHCR package public (or add a Render registry credential). This is a one-time setup action, not a recurring manual deployment step — see [§2 Remaining Risks](#2-remaining-risks) for what happens if it is skipped. |
| 8 | Deployment remains cloud-agnostic and portable to Azure, AWS, and Kubernetes | ✅ **Pass** | The runtime image is a standard multi-stage Docker build using only `mcr.microsoft.com/dotnet/sdk:8.0`, `mcr.microsoft.com/dotnet/aspnet:8.0`, and an OCI image reference (`ARG MODELS_IMAGE`, overridable via `--build-arg`) — no Render-specific, GitHub-specific, or platform-specific instructions anywhere in `Dockerfile` or `docker/models.Dockerfile`. The same image build/run procedure applies unmodified to Docker Desktop, Azure Container Registry + App Service/Container Apps, AWS ECR + ECS/EKS, or any Kubernetes cluster — all of which support pulling a build-time base image from a registry as an ordinary `docker build`/BuildKit operation. No code path in the application performs runtime downloads (`ModelAvailabilityChecker` only reads local disk). |

**Overall: 8/8 requirements independently re-verified as satisfied**, with one operational
prerequisite (Render auto-deploy) that depends on a one-time, already-documented bootstrap step
rather than an ongoing manual step.

---

## 2. Remaining Risks

| Risk | Severity | Description | Current Mitigation |
|---|---|---|---|
| **Bootstrap-ordering risk** | Medium (one-time) | If the `Dockerfile`/`.csproj` changes are deployed to Render *before* `build-models-image.yml` has ever successfully run and published `ghcr.io/krupeshreddyhot/abhyanvaya-insightface-models:latest` (or before that package's visibility is set to Public / a Render registry credential is configured), the Render Docker build will fail at the `FROM ${MODELS_IMAGE}` stage. | Documented explicitly in the implementation doc §6 checklist; failure mode is a **loud, immediate build failure**, not a silent bad deploy — Render keeps serving the last successful deploy. Not yet independently confirmed against a live Render service or a live GHCR package in this review (no Render/GitHub credentials available in this environment). |
| **GHCR package visibility / registry auth not yet actually configured** | Medium | This review confirms the *code and workflow* are correct, but cannot confirm from this environment whether `ghcr.io/krupeshreddyhot/abhyanvaya-insightface-models` has actually been published yet, or whether its visibility has been set to Public. | Requires a manual, one-time verification/action by someone with GitHub repository/package admin access — tracked as an open action item, not a code defect. |
| **Models-image staleness on partial path-filter miss** | Low | `build-models-image.yml` triggers on changes to `Abhyanvaya.API/models/insightface/**`, `docker/models.Dockerfile`, or the workflow file itself. A model update landing via an unusual path (e.g. a force-push that rewrites history without touching those paths in the diff GitHub computes) could theoretically be missed. | `workflow_dispatch` is available for manual re-trigger; risk is "stale but present" models, not missing/broken models — no RecognitionError, just outdated weights, and would be caught by comparing model file hashes/dates during a release check. |
| **`render.yaml` header comment is stale** | Low (cosmetic/documentation only) | Lines 3–6 of `render.yaml` still say `# For an existing Render Web Service (manual setup), set: Build Command: bash scripts/render-build.sh ...` — a leftover from before this review confirmed Render has no native .NET runtime and this "Native/Shell" path never actually applied to Render for this project (see `docs/DEPLOYMENT_RENDER.md`, updated section "If your service still shows an older Runtime = Native / Shell"). | No functional impact (Render's actual `runtime: docker` block below it is authoritative and unaffected) — but not fixed in this review since **no code changes** were permitted; flagged here for a future doc-only cleanup pass. |
| **First-ever GHCR pull latency on Render** | Low | Every Render build will now pull one additional small image layer (~a few MB — just two ONNX files, no OS) from GHCR. If GHCR is briefly unavailable, the Render build fails at that stage rather than proceeding. | Acceptable trade-off — this is a normal, well-understood Docker build dependency risk, equivalent to any base-image pull (e.g. `mcr.microsoft.com/dotnet/sdk:8.0` already carries the same class of risk). No caching/pinning-by-digest was implemented; `:latest` is used by default (mitigated by `ARG MODELS_IMAGE` being overridable to a pinned digest/tag if stronger determinism is desired later). |
| **Determinism of `:latest` tag** | Low | Using `${IMAGE_NAME}:latest` as the default `MODELS_IMAGE` means the exact model bytes pulled by a given Render build are not pinned to an immutable reference — two builds on the same day could theoretically pull different content if the models workflow runs in between. | Every models image is also tagged `:<git-sha>` (see workflow `tags:` block), so pinning is available immediately by overriding `MODELS_IMAGE` — just not the default. This is a reasonable default for this project's low frequency of model changes; noted as a recommendation below. |
| **`Abhyanvaya.API/Abhyanvaya.API.csproj` `RoslynCodeTaskFactory` portability** | Very Low | The publish-time validation target relies on `RoslynCodeTaskFactory` and `$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll`, which is bundled with the .NET SDK and was confirmed working under the SDK version pinned in the Dockerfile's `build` stage (`mcr.microsoft.com/dotnet/sdk:8.0`) and locally (SDK 8.0.400) during implementation. | Already empirically verified working in both the local dev environment and inside the Linux-based Docker `build` stage during the dry run — no portability issue observed. |

No risk identified above requires reverting or blocking the AI13.DEPLOY.1 change; all are either
one-time setup items, already-mitigated, or cosmetic.

---

## 3. Recommendations

1. **Complete the one-time bootstrap before relying on Render auto-deploy for this change.** Push to
   trigger `build-models-image.yml`, confirm it succeeds, and set the `abhyanvaya-insightface-models`
   GHCR package to Public (simplest) or add a Render registry credential. Then trigger/observe one
   Render deploy end-to-end and confirm `/health/ready` reports `modelsPresent: true` in production.
2. **Clean up the stale `render.yaml` header comment** (lines 3–6) referencing a "Build Command:
   bash scripts/render-build.sh" native/manual setup path that does not apply to this project (Render
   has no native .NET runtime). Documentation-only; no functional risk, but likely to confuse a future
   reader. (Not changed in this review per "no code changes.")
3. **Consider pinning `MODELS_IMAGE` to `:<git-sha>` instead of `:latest`** in a future change if fully
   deterministic, reproducible builds become a hard requirement (e.g. for compliance/audit purposes).
   This would require a small mechanism to propagate the current model image's sha into the app
   Dockerfile's default `ARG` (e.g. a committed version file bumped by the CI workflow) — an
   incremental improvement, not a blocker.
4. **Add a scheduled or PR-time smoke check** that runs `docker build --build-arg
   MODELS_IMAGE=ghcr.io/krupeshreddyhot/abhyanvaya-insightface-models:latest` and asserts the two
   ONNX files land in the publish output at expected sizes, so a broken/missing GHCR package is
   caught by CI before a Render deploy attempt, rather than being discovered only when Render's build
   fails. Optional hardening, not required for correctness today (Render's own build failure is
   already a safe, loud failure mode).
5. **Track the GHCR package's storage/bandwidth cost** the same way Git LFS bandwidth was previously
   tracked in `docs/DEPLOYMENT_RENDER.md` — the models image is tiny (a few MB), so this is low
   priority, but worth a one-line mention if the team formalizes a cost-monitoring checklist later.

None of these recommendations require immediate action; #1 is the only one that gates a successful
production deploy of this specific change.

---

## 4. Production Readiness Assessment

| Dimension | Assessment |
|---|---|
| **Correctness** | All 8 reviewed requirements pass on independent re-verification of source plus prior empirical build/run evidence. The publish-time validation gate provides a genuine fail-fast safety net that did not exist before this change. |
| **Regression risk** | None identified in application code — `Diagnostics/StartupDiagnostics.cs`, `Diagnostics/ModelAvailabilityChecker.cs`, `/health`, `/health/live`, `/health/ready` route handlers, and all controllers/AI/recognition/embedding code are unmodified (confirmed by direct inspection, consistent with the AI13.DEPLOY.1 regression review). |
| **Operational readiness** | **Conditional** — production-ready *after* the one-time bootstrap (§2/§3 recommendation #1) is completed and verified against the real Render service. The architecture itself introduces no new runtime failure modes beyond a standard Docker base-image pull dependency. |
| **Rollback safety** | High — reverting the `Dockerfile`/`.csproj` changes restores prior (broken) behavior exactly with no destructive changes to `render.yaml`, the database, or application code; the models image is independently tagged by commit SHA for targeted rollback if a bad model asset is ever published. |
| **Portability** | High — verified cloud-agnostic; no Render-specific or GitHub-specific logic exists inside the Docker build or runtime image itself. |
| **Observability** | Unchanged and adequate — existing startup summary and health endpoints continue to surface model presence/size exactly as before; no new blind spots introduced. |

### Overall verdict

**Architecturally sound and ready for production, pending completion of the one-time GHCR bootstrap
step (§2/§3 #1).** No code-level defects, regressions, or unmet requirements were found in this
review. The only open item is an operational/administrative action (publishing and exposing the
models image) rather than a code change, consistent with "no code changes" for this review pass.
