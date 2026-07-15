# AI19.MEDIA.3.4 — Static File Retirement Review

**Type:** Review only. No production code was modified to produce this document.

---

## Question 1 — Can `UseStaticFiles("/media")` (`Program.cs:398-403`) Now Be Removed?

```csharp
var mediaPhysical = ResolveLocalMediaPhysicalRoot(app.Configuration, app.Environment);
Directory.CreateDirectory(mediaPhysical);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mediaPhysical),
    RequestPath = "/media",
    OnPrepareResponse = AddPublicBrandingHeaders,
});
```

### Do Recognition Thumbnails, Classroom Photos, Attendance Photos, and Student Photos still need it?

**No** — per AI19.MEDIA.3.3's empirical validation, all four categories are now fully servable through `MediaController` (`Abhyanvaya.API/Controllers/MediaController.cs`) → `IMediaObjectReader.OpenReadAsync` → `IStorageProviderFactory.GetActiveProvider()`, for **both** the `local` and `s3` provider.

### Does the local (non-R2) case lose anything if it's removed?

**No — same physical location, verified identical:**

- `ResolveLocalMediaPhysicalRoot` (`Program.cs:372-382`, used to configure `UseStaticFiles`): `Media:PhysicalRoot` → fallback `Branding:PhysicalRoot` → fallback `{webRoot}/branding`.
- `LocalStorageProvider.ResolveRootDirectory()` (`LocalStorageProvider.cs:31-39`, used by `MediaController` via `MediaObjectReader`/`IStorageProviderFactory`): `_mediaOptions.PhysicalRoot` → fallback `{webRoot}/branding`.

Both resolve to the **exact same directory** for the local provider (the only difference — `Media:PhysicalRoot` vs. `Branding:PhysicalRoot` as the first fallback key — is immaterial because `MediaOptions` itself is bound from `Media:*` with a fallback to `Branding:*`, per `MediaOptions.cs:4`, so both code paths ultimately read the same configuration value). This was independently confirmed empirically in AI19.MEDIA.3.3: the harness wrote through `LocalStorageProvider` and read back through `MediaObjectReader`/`MediaController` against the same directory with zero discrepancy.

**Conclusion: the `/media` static-file middleware can be safely removed from a "will the file still be found" standpoint — `MediaController` is a strict superset for every media category this system produces.**

### What would be lost by removing it (minor, non-blocking caveats)

| Capability | `UseStaticFiles` (today) | `MediaController` (AI19.MEDIA.3.2) | Impact |
|---|---|---|---|
| Conditional GET (`ETag`/`Last-Modified` → `304 Not Modified`) | Automatic, computed from file metadata | Not implemented — `Controller.File(stream, contentType, enableRangeProcessing: true)` does not set an `ETag` or `Last-Modified` header in the current implementation | Browsers will re-download the full body on any request that bypasses their own `Cache-Control: max-age=86400` cache (e.g. after 24h, or a hard refresh) instead of getting a cheap `304`. Bandwidth-only concern, not a correctness concern — every response is still byte-identical. |
| `Range`/`If-Range` (partial content, `206`) | Automatic | Explicitly preserved — `enableRangeProcessing: true` is passed to `File(...)` in `MediaController.GetMedia` | No regression. |
| `HEAD` requests | Automatic (returns headers only, no body) | Not implemented — the action is `[HttpGet]` only | Only a regression if some client issues `HEAD /media/...` (not observed anywhere in this codebase's React client, which only ever sets `<img src>`/`Avatar src>`, i.e. browser-issued `GET`). |
| Directory listing | Not enabled today either (`UseDirectoryBrowser` is not registered) | Not applicable | No change either way. |

**Recommendation:** these gaps are minor and do not block removal, but if `ETag`/`304` behavior is considered valuable for bandwidth reasons on a Render Starter instance, it can be added later as a small, additive `MediaController` enhancement (e.g. passing a `lastModified`/`entityTag` computed from the object, if `IStorageProvider` is extended to expose it) — **not required for this milestone** and explicitly out of scope here (documentation only).

### Rollout guidance (documentation only — not executed here)

Because `UseStaticFiles("/media")` is registered *before* `MapControllers()` in the pipeline (`Program.cs:398` vs. `:415`), it and `MediaController` already coexist safely today: the static middleware only "wins" when it finds a file on local disk, and falls through to the controller otherwise (see AI19.MEDIA.3.1, Risks table). This means retirement carries **zero urgency** — the two mechanisms do not conflict, and the static middleware is not doing anything harmful by continuing to exist. A safe rollout sequence, if retirement is later desired, would be: (1) confirm in production logs that `MediaController`'s "Media Request Started/Completed" log lines are firing for the expected volume of `/media/*` traffic (i.e., the static middleware is genuinely a no-op fallback with the `s3` provider active in production, since R2-stored objects never exist on local disk), then (2) remove the `UseStaticFiles("/media")` registration in a follow-up change, independent of this milestone.

---

## Question 2 — Should `/branding` Remain Static?

This is a **separate URL namespace from `/media`**, and it is **not covered by `MediaController`** (`GetMedia` only maps `GET /media/{**key}`). Investigating it surfaced an architecturally significant finding.

### How branding logos are uploaded

`CollegeBrandingService.SaveLogoForTenantAsync` (`Abhyanvaya.API/Services/CollegeBrandingService.cs:71`):

```csharp
await _imageStorage.SaveVariantsAsync($"{key:D}", variants, cancellationToken);
```

`_imageStorage` is `Abhyanvaya.API.Media.IMediaStorageService` → `MediaStorageService.SaveVariantsAsync` (`MediaStorageService.cs:38-59`) → `IStorageProviderFactory.GetActiveProvider()` — **the identical provider-selection call every other media category uses.** When `MediaOptions`'s active provider is `s3` (Cloudflare R2) — which, per `MediaOptions.cs:4`, is driven by `Media:Provider` falling back to `Branding:Provider` — **branding logos are written to R2 exactly like recognition thumbnails were before AI19.MEDIA.3.2.**

### How branding logos are retrieved

`CollegeBrandingService.BuildLogoPath` (`CollegeBrandingService.cs:42-51`):

```csharp
return $"/branding/{accessKey:D}/{variant}.webp?v={v}";
```

This URL is served **only** by:

1. A dedicated `UseStaticFiles` block for `/branding` (`Program.cs:384-394`) — but this is registered **only if `Branding:PhysicalRoot` is a non-empty configuration value** (`if (!string.IsNullOrEmpty(brandingPhysical))`, `Program.cs:385`). The shipped `appsettings.json` sets `"Branding": { "PhysicalRoot": "" }` (`appsettings.json:58`) — empty by default. Unless an environment variable (`Branding__PhysicalRoot`) explicitly overrides this in a given deployment, **this middleware is never registered at all.**
2. A generic, un-prefixed `UseStaticFiles()` fallback (`Program.cs:405-412`) that serves from the default `wwwroot` folder (adding branding headers only when the path starts with `/branding`) — but `Abhyanvaya.API/wwwroot` contains no real content in this repository (confirmed: only a `.gitkeep` placeholder), so this fallback cannot serve any branding logo that was ever uploaded through `IStorageProviderFactory`/R2.

### Finding

**`/branding/*` has the same provider-blind defect `/media/*` had before AI19.MEDIA.3.2, and it is currently uncovered by the AI19.MEDIA.3.2 fix, which was scoped only to `GET /media/{**key}`.** If a tenant's college logo was ever uploaded while the active provider was `s3` (R2) — the same production configuration that caused recognition thumbnails to 404 — that logo is almost certainly **unreachable today**, for the identical root cause AI19.MEDIA.2 diagnosed for recognition thumbnails, just under a different URL prefix that this milestone's controller does not yet serve.

This is evidence-based (traced to the exact `SaveVariantsAsync`/`BuildLogoPath`/`Program.cs` lines above), but it is **outside the literal scope of AI19.MEDIA.3.2** ("Create MediaController that serves `GET /media/{**key}`") and is **not fixed by this review**, which is documentation-only. It is flagged here because it directly answers this task's question: *"should `/branding` remain static?"*

### Recommendation

**No, `/branding` should not remain purely static long-term** — it needs the same provider-aware treatment `/media` just received, via one of:

- **Option A (smallest change):** Add a second catch-all route to the existing `MediaController` — e.g. `[HttpGet("/branding/{**key}")]` calling the identical `IMediaObjectReader.OpenReadAsync` — since branding logos are stored under the same `IStorageProviderFactory` abstraction, just with a `{guid}/{variant}.webp` key shape instead of `{category}/{ids}/{variant}.webp`. No URL change required for `BuildLogoPath`.
- **Option B (URL-unifying, larger blast radius):** Migrate branding URLs to live under `/media/branding/{key}` so a single controller/prefix covers everything — this would require changing `BuildLogoPath` (a public URL contract change) and is a larger, riskier change than Option A.

**Option A is recommended** as the natural, minimal follow-up once explicitly scoped as its own milestone — it is intentionally **not implemented here**, since this document's mandate is review only and the user's AI19.MEDIA.3.2 instruction scoped the controller strictly to `/media`.

### Summary Table

| Route prefix | Covered by `MediaController` today? | Provider-aware today? | Risk in production (provider = `s3`) |
|---|---|---|---|
| `/media/*` (recognition, classroom, attendance, student) | Yes (AI19.MEDIA.3.2) | Yes | Resolved by this milestone |
| `/branding/*` (college logos) | **No** | **No** — still local-disk-only via static middleware | **Same 404 defect as recognition thumbnails, still present** — flagged for follow-up, not fixed here |

---

## Constraints Confirmed

No code, configuration, or routing changes were made to answer either question in this document. `UseStaticFiles("/media")` and `UseStaticFiles("/branding")` remain registered exactly as before.
