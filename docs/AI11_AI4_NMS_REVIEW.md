# AI11.AI.4 — Non-Maximum Suppression (NMS) Review

**Type:** Read-only investigation. No code changed.
**Verdict:** NMS **is implemented, is executed, and uses the configured `NmsThreshold = 0.4`.** The
algorithm (greedy IoU suppression) is correct. It cannot recover detection quality, though, because
it receives corrupted candidates from the parsing stage (AI11.AI.2).

---

## 1. Is NMS executed? Where?

**Yes.** It is invoked at the end of `DetectFaces`, after parsing:

```103:115:Abhyanvaya.Infrastructure/InsightFace/InsightFaceEngine.cs
    private IReadOnlyList<InsightFaceImageMath.FaceCandidate> DetectFaces(Image<Rgb24> image)
    {
        var session = _modelHost.GetDetectionSession();
        var inputSize = _options.DetectionInputSize;
        var inputTensor = InsightFaceImageMath.BuildDetectionInput(image, inputSize, out var scale, out var padX, out var padY);
        var inputName = session.InputMetadata.Keys.First();

        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };
        using var outputs = session.Run(inputs);

        var candidates = ParseDetectionOutputs(outputs, inputSize, scale, padX, padY, image.Width, image.Height);
        return InsightFaceImageMath.ApplyNms(candidates, _options.NmsThreshold);
    }
```

## 2. Is `InsightFace:NmsThreshold = 0.4` actually applied?

**Yes.** `_options.NmsThreshold` (bound from `appsettings.json` → default `0.4f`) is passed straight
into `ApplyNms`. No hard-coded literal.

## 3. The NMS algorithm

```123:139:Abhyanvaya.Infrastructure/InsightFace/InsightFaceImageMath.cs
    public static IReadOnlyList<FaceCandidate> ApplyNms(IReadOnlyList<FaceCandidate> candidates, float threshold)
    {
        var ordered = candidates.OrderByDescending(c => c.Score).ToList();
        var kept = new List<FaceCandidate>();

        while (ordered.Count > 0)
        {
            var current = ordered[0];
            kept.Add(current);
            ordered.RemoveAt(0);
            ordered = ordered
                .Where(c => IoU(current, c) < threshold)
                .ToList();
        }

        return kept;
    }
```

Standard **greedy NMS**: sort by score desc, keep the top box, drop every remaining box whose IoU with
it is `>= threshold`, repeat. IoU is computed with correct intersection/union geometry:

```150:161:Abhyanvaya.Infrastructure/InsightFace/InsightFaceImageMath.cs
    private static float IoU(FaceCandidate a, FaceCandidate b)
    {
        var x1 = MathF.Max(a.X1, b.X1);
        var y1 = MathF.Max(a.Y1, b.Y1);
        var x2 = MathF.Min(a.X2, b.X2);
        var y2 = MathF.Min(a.Y2, b.Y2);
        var intersection = MathF.Max(0, x2 - x1) * MathF.Max(0, y2 - y1);
        var areaA = MathF.Max(0, a.X2 - a.X1) * MathF.Max(0, a.Y2 - a.Y1);
        var areaB = MathF.Max(0, b.X2 - b.X1) * MathF.Max(0, b.Y2 - b.Y1);
        var union = areaA + areaB - intersection;
        return union <= 0 ? 0 : intersection / union;
    }
```

Assessment: correct and class-agnostic (fine for single-class face detection). Complexity is O(n²)
(acceptable for normal candidate counts; note it becomes costly when the parser floods it with
~1,800 garbage candidates).

## 4. Counts (real image, session `fc2ab073…`, 2048×1228)

| Stage | Current code (buggy parse) | Correct SCRFD |
|-------|----------------------------|---------------|
| Raw detections (post-threshold candidates) | 1,786 | 72 |
| After NMS (`0.4`) | **584** | **19** |
| NMS threshold used | 0.4 | 0.4 |

NMS reduced 1,786 → 584 in the buggy path — it *is* running and suppressing overlaps, but the inputs
are spurious 1-pixel-tall boxes strung along image edges (see AI11.AI.5), which mostly don't overlap,
so 584 survive. With correct candidates, NMS behaves exactly as expected (72 → 19).

## 5. Is NMS missing or broken?

**No.** NMS is present, invoked, config-driven (0.4), and algorithmically correct. The inflated
post-NMS count is a **downstream symptom of the parsing bug (AI11.AI.2)**, not an NMS defect.

## 6. Verdict

| Question | Finding |
|----------|---------|
| Is NMS executed? | ✅ Yes (`DetectFaces` → `ApplyNms`) |
| Is `NmsThreshold=0.4` applied? | ✅ Yes (from config) |
| Algorithm correct? | ✅ Greedy IoU suppression, correct geometry |
| Why 584 survive (buggy path)? | Garbage non-overlapping candidates from AI11.AI.2, not NMS |
| Action needed on NMS | None — fix parsing upstream |
