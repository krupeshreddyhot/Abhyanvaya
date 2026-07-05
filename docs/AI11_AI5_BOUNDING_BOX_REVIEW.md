# AI11.AI.5 — Bounding Box Coordinate Scaling Review

**Type:** Read-only investigation. No code changed.
**Verdict:** The **letterbox pre-processing and the coordinate scale-back math are correct.** The
garbage/edge/1-pixel boxes seen in the current output are produced by the parsing bug (AI11.AI.2),
**not** by the scaling code. When the correct candidates are fed through the *same* scale-back
formula, boxes land precisely on faces.

---

## 1. Pre-processing (resize + letterbox)

```27:34:Abhyanvaya.Infrastructure/InsightFace/InsightFaceImageMath.cs
    public static DenseTensor<float> BuildDetectionInput(Image<Rgb24> image, int inputSize, out float scale, out int padX, out int padY)
    {
        scale = Math.Min((float)inputSize / image.Width, (float)inputSize / image.Height);
        var resizedWidth = (int)Math.Round(image.Width * scale);
        var resizedHeight = (int)Math.Round(image.Height * scale);
        padX = (inputSize - resizedWidth) / 2;
        padY = (inputSize - resizedHeight) / 2;
```

- **Aspect ratio: preserved** — a single uniform `scale = min(640/W, 640/H)` is used for both axes.
- **Letterboxing: yes** — the resized image is centred in a 640×640 canvas with symmetric `padX`/`padY`.
- Padded border pixels are left as tensor `0.0` (not normalized). Minor deviation from some reference
  implementations (which pad with the normalized value of 0), but immaterial to geometry.

For the traced image (2048×1228):

| Quantity | Value |
|----------|-------|
| scale X = scale Y | **0.31250** (uniform → no aspect distortion) |
| resized | 640 × 384 |
| padX | 0 |
| padY | 128 (64 top + 64 bottom) |

## 2. Scale-back (model space → original image)

```172:182:Abhyanvaya.Infrastructure/InsightFace/InsightFaceEngine.cs
                    var anchorX = (x * stride) - padX;
                    var anchorY = (y * stride) - padY;
                    var x1 = (anchorX - boxes[boxOffset] * stride) / scale;
                    var y1 = (anchorY - boxes[boxOffset + 1] * stride) / scale;
                    var x2 = (anchorX + boxes[boxOffset + 2] * stride) / scale;
                    var y2 = (anchorY + boxes[boxOffset + 3] * stride) / scale;

                    x1 = Math.Clamp(x1, 0, originalWidth - 1);
                    y1 = Math.Clamp(y1, 0, originalHeight - 1);
                    x2 = Math.Clamp(x2, x1 + 1, originalWidth);
                    y2 = Math.Clamp(y2, y1 + 1, originalHeight);
```

Formula: `orig = (model_coord − pad) / scale`. This is the **correct inverse** of the letterbox
transform:
- `−padX / −padY` removes the centring offset.
- `/ scale` undoes the uniform resize.
- Distance decode `centre ± distance*stride` is the correct SCRFD box reconstruction.
- Clamping keeps boxes inside the image and guarantees `x2>x1`, `y2>y1`.

Landmarks use the same reversal:

```184:192:Abhyanvaya.Infrastructure/InsightFace/InsightFaceEngine.cs
                    var landmarkOffset = index * 10;
                    var landmarkPoints = new float[10];
                    if (landmarkOffset + 9 < landmarks.Length)
                    {
                        for (var i = 0; i < 5; i++)
                        {
                            landmarkPoints[i * 2] = ((x * stride) + landmarks[landmarkOffset + i * 2] * stride - padX) / scale;
                            landmarkPoints[i * 2 + 1] = ((y * stride) + landmarks[landmarkOffset + i * 2 + 1] * stride - padY) / scale;
                        }
                    }
```

Also correct in structure. The final integer box is produced by `ToBoundingBox` (floor/ceil + min size
1), which is fine.

## 3. Are boxes shifted / mis-scaled / duplicated / out of bounds?

The behaviour depends entirely on whether the *inputs* to this correct formula are valid:

| Symptom (current output) | True cause |
|--------------------------|------------|
| Boxes 1px tall, glued to top/bottom edges | AI11.AI.2: "boxes" read from a `[…,1]` score tensor → `boxes[boxOffset+…]` mostly out of range / zero → degenerate rectangles, then clamped to edges |
| Hundreds of duplicated-looking boxes | AI11.AI.2: regression tensors misread as scores → mass false positives |
| Out-of-bounds before clamp | Only because the decoded coords are nonsense inputs |

None of these are caused by the scaling arithmetic itself.

## 4. Empirical proof — same scaling, correct inputs

Feeding **correctly-parsed** SCRFD candidates through the identical letterbox reversal on the real
classroom image (session `fc2ab073…`) yields properly placed, face-sized boxes:

![Correct SCRFD detections drawn on the classroom image](assets/AI11_AI_correct_detections.png)

19 boxes, each ~45×60 px, centred on real faces — e.g. `[1276.3,472.0,1319.9,529.1]` (score 0.90),
`[225.4,452.9,280.5,515.2]` (0.87). This confirms the scale/pad reversal is geometrically sound.

## 5. Verdict

| Question | Finding |
|----------|---------|
| Scale X / Scale Y | 0.3125 / 0.3125 (uniform) |
| Aspect ratio preserved | ✅ Yes |
| Padding / letterboxing | ✅ Centred, symmetric (padX 0, padY 128) |
| Scale-back formula | ✅ Correct inverse transform |
| Boxes shifted/mis-scaled | ❌ Not by scaling — caused by AI11.AI.2 parsing bug |
| Correct inputs → correct boxes | ✅ Verified (annotated image, 19 faces on-target) |

**No change required in the scaling code.** Fixing AI11.AI.2 restores correct boxes through this exact
math.
