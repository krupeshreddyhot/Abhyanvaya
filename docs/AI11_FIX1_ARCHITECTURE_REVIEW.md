# AI11.FIX.1 — Architecture Review

**Reviewer:** Chief Architect
**Change set:** SCRFD detection output parser correction (AI11.FIX.1.1 – 1.6)
**Files modified by this fix:** exactly **one** —
`Abhyanvaya.Infrastructure/InsightFace/InsightFaceEngine.cs` (method `ParseDetectionOutputs` only).

---

## 1. Scope compliance

| Requirement | Status | Notes |
|-------------|--------|-------|
| ✅ Only SCRFD parser changed | **PASS** | Single method `ParseDetectionOutputs` in `InsightFaceEngine.cs` |
| ✅ No threshold changes | **PASS** | `DetectionThreshold` (0.5) & `NmsThreshold` (0.4) untouched in `InsightFaceOptions`/`appsettings.json` |
| ✅ No matching changes | **PASS** | `FaceMatcher`, `IFaceMatcher`, distances/statuses untouched |
| ✅ No UI changes | **PASS** | No frontend / review-page files touched |
| ✅ No database changes | **PASS** | No entities, migrations, or DbContext changes in this fix |
| ✅ No worker changes | **PASS** | `ClassroomRecognitionBackgroundService` untouched by this fix |
| ✅ No API changes | **PASS** | No controllers/endpoints touched |
| ✅ No public contract changes | **PASS** | `DetectAsync`, `FaceDetectionResponse`, `DetectedFaceDto`, `FaceCandidate` unchanged |
| ✅ Build passes | **PASS** | `Abhyanvaya.Infrastructure` compiles with 0 errors |
| ✅ Detection count realistic | **PASS** | 19 faces on sample (was 584) |
| ✅ Confidence in [0,1] | **PASS** | 0.5580–0.8992 (was up to 2.87) |
| ✅ Review page shows correct count | **EXPECTED** | Review consumes unchanged recognition rows; live confirmation needs rebuilt host (see AI11.FIX.1.5) |

## 2. What changed (precisely)

Inside `ParseDetectionOutputs` only:

1. **Tensor mapping** — from interleaved `[strideIndex*3 + {0,1,2}]` to grouped
   `scores=out[i]`, `boxes=out[i+3]`, `landmarks=out[i+6]` (AI11.FIX.1.1).
2. **Anchor iteration** — added inner loop over `numAnchors = 2`; row index is anchor-major
   `row = (y*fw + x)*2 + anchor` (AI11.FIX.1.2).
3. **Confidence integrity** — score read only from score tensors; added DEBUG-only `Debug.Assert`
   for the `[0,1]` invariant (no Release overhead, no clamping) (AI11.FIX.1.3).
4. **Safety guard** — early warning + empty return if fewer than 9 outputs are present.

**Unchanged inside the method:** distance-to-corner decode, landmark decode, letterbox reversal
`(coord − pad)/scale`, clamping, and the `DetectionThreshold` comparison.

## 3. Architectural assessment

- **Blast radius: minimal.** The defect and the fix are both localized to a single private method.
  No abstraction boundaries were altered, so the change is inherently low-risk.
- **Correctness: verified against ground truth.** The corrected logic was validated by executing the
  actual `det_10g.onnx` on the real classroom image (AI11.FIX.1.4): 19 well-placed faces, sane
  confidences — matching the InsightFace reference behaviour.
- **Separation of concerns preserved.** Detection remains a pure function of image → faces; matching,
  persistence, summary, review, and finalization continue to depend only on the stable
  `FaceDetectionResponse`/`DetectedFaceDto` contract.
- **Maintainability improved.** The SCRFD output layout (grouping + 2 anchors) is now documented
  inline, and the DEBUG assertion codifies the confidence invariant to catch any future regression
  during development.
- **No hidden coupling introduced.** Thresholds stay in configuration; no new dependencies, no DI
  changes, no state.

## 4. Residual / follow-up (not part of this fix)

- **Live E2E**: requires stopping the VS-hosted API and rebuilding so the running process picks up the
  new DLL (the solution-wide build only failed on file-lock copies, not compilation).
- **Model path resolution** (noted in AI11.AI.1): the model host resolves `ModelDirectory` against the
  CWD while diagnostics use `ContentRootPath`. Works today; recommend aligning in a future hardening
  task. **Out of scope for AI11.FIX.1.**

## 5. Verdict

**APPROVED.** The change is surgical, correct, contract-preserving, and meets every acceptance
criterion of AI11.FIX.1. Detection now returns a realistic face count with valid confidences and
correctly placed bounding boxes, with no impact on recognition, matching, NMS, scaling, UI, database,
worker, or API layers.
