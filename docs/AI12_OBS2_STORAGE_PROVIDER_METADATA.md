# AI12.OBS.2 — Storage Provider Metadata

**Status: IMPLEMENTED**
**Date:** 2026-07-04
**Reviewer:** Chief Software Architect

---

## 1. Objective

Remove the storage-provider `switch` statement from `Program.cs` and instead have each `IStorageProvider` implementation expose its own display metadata, so `Program.cs` (and any future consumer, such as the AI12.OBS.6 health endpoints) can simply log/read `DisplayName` without knowing about specific provider types.

---

## 2. Before / after

**Before:**

```csharp
var storageProviderName = scope.ServiceProvider.GetRequiredService<IStorageProviderFactory>().GetActiveProviderName();
var storageProviderDisplayName = storageProviderName switch
{
    S3StorageProvider.ProviderName => "AWS S3",
    LocalStorageProvider.ProviderName => "Local File System",
    _ => storageProviderName,
};
logger.LogInformation("Storage Provider                    : {StorageProvider}", storageProviderDisplayName);
```

**After:**

```csharp
var storageProvider = scope.ServiceProvider.GetRequiredService<IStorageProviderFactory>().GetActiveProvider();
logger.LogInformation("Media Provider                      : {DisplayName}", storageProvider.DisplayName);
```

No `switch`, no `if`/`else` chain, no knowledge of concrete provider types in `Program.cs` — the active provider (already resolved via the existing `IStorageProviderFactory.GetActiveProvider()`) is asked for its own display name.

(The line label was changed from `Storage Provider` to `Media Provider` to align with the AI12.OBS.4 SaaS deployment metadata field name and the underlying `Media:Provider` configuration key — the value itself is unchanged and still comes from the same `IStorageProvider`.)

---

## 3. Interface change

`Abhyanvaya.API/Media/IStorageProvider.cs`:

```csharp
public interface IStorageProvider
{
    /// <summary>Stable machine-readable provider identifier (e.g. Local, S3, AzureBlob).</summary>
    string ProviderName { get; }

    /// <summary>Human-readable name for logs/diagnostics/UI (e.g. Local File System, Amazon S3).</summary>
    string DisplayName { get; }

    /// <summary>Category of the provider (e.g. FileSystem, Cloud Storage).</summary>
    string ProviderType { get; }

    // ... existing WriteObjectAsync / ReadObjectAsync / ExistsAsync / DeleteObjectAsync / CheckHealthAsync unchanged ...
}
```

The previous `string Name { get; }` member (only ever used internally by each provider to expose its own id, with no external callers) was removed and replaced by the three richer metadata members above.

### 3.1 Renamed internal constant to avoid a naming collision

Each provider previously exposed a `public const string ProviderName = "local" | "s3";` static field used by configuration/factory code (`MediaOptions.GetActiveProviderName()`, `StorageProviderFactory`, `ConfigureMediaOptions`, `MediaOptionsValidator`) to compare/default the configured provider id. Since the new **instance** property is now also named `ProviderName` (per this milestone's required API shape), the static field could not keep the same identifier (C# does not allow a static field and an instance member with an identical name in the same class).

The static id constant was renamed to `Id` in both providers:

```csharp
// LocalStorageProvider
public const string Id = "local";
public string ProviderName => Id;
public string DisplayName => "Local File System";
public string ProviderType => "FileSystem";

// S3StorageProvider
public const string Id = "s3";
public string ProviderName => Id;
public string DisplayName => "Amazon S3";
public string ProviderType => "Cloud Storage";
```

All 7 call sites that referenced the old `LocalStorageProvider.ProviderName` / `S3StorageProvider.ProviderName` static constants were updated to `.Id` (a pure rename, no logic change):

- `Abhyanvaya.API/Media/MediaOptions.cs` (default `Provider` value + `GetActiveProviderName()`)
- `Abhyanvaya.API/Media/ConfigureMediaOptions.cs` (default fallback when unconfigured)
- `Abhyanvaya.API/Media/StorageProviderFactory.cs` (`GetActiveProvider()` comparison)
- `Abhyanvaya.API/Media/MediaOptionsValidator.cs` (S3 bucket validation gate)

This is purely a rename of an internal identifier — the actual string values (`"local"`, `"s3"`) and all runtime behavior (which provider is selected for a given `Media:Provider` configuration value) are completely unchanged.

---

## 4. Extensibility for future providers

The interface is designed so a future `AzureBlobStorageProvider` (or any other provider) plugs in with zero changes to `Program.cs`, `MediaStorageService`, or any other consumer:

| Provider | `ProviderName` | `DisplayName` | `ProviderType` |
|----------|-----------------|----------------|------------------|
| `LocalStorageProvider` (implemented) | `local` | `Local File System` | `FileSystem` |
| `S3StorageProvider` (implemented) | `s3` | `Amazon S3` | `Cloud Storage` |
| *Future* `AzureBlobStorageProvider` | `AzureBlob` | `Azure Blob Storage` | `Cloud Storage` |

Adding Azure Blob support (out of scope for this milestone — no such provider exists in the codebase today) would only require: (1) a new class implementing `IStorageProvider` with these three properties, (2) registering it in `MediaStorageServiceCollectionExtensions.AddMediaStorage()`, and (3) extending `StorageProviderFactory.GetActiveProvider()`'s selection logic. No diagnostics/logging code would need to change, since all consumers read `DisplayName`/`ProviderType` polymorphically.

---

## 5. Architecture impact

- `Program.cs` no longer contains any provider-specific branching for storage — it is fully polymorphic.
- No behavior change: the active provider (Local vs S3) is still selected exactly as before, via `MediaOptions.GetActiveProviderName()` / `Media:Provider` configuration.
- `MediaStorageService`, `ApplicationMediaStorageService`, and `MediaObjectReader` are unaffected — none of them referenced the removed `Name` property.
- No new DI registrations were required; `LocalStorageProvider` and `S3StorageProvider` were already registered as singletons.

---

## 6. Verification steps

1. **Build:** `dotnet build Abhyanvaya.sln` — 0 errors.
2. **Local provider:** with `Media:Provider` unset (or `local`), confirm the startup log shows `Media Provider : Local File System`.
3. **S3 provider:** with `Media:Provider=s3` and a valid bucket configured, confirm the startup log shows `Media Provider : Amazon S3`.
4. **No behavior change:** confirm classroom photo upload, student photo upload, and branding asset storage continue to work identically to before this change (they all go through `IStorageProviderFactory.GetActiveProvider()`, which is unchanged).
5. **Code review:** confirm `Program.cs` no longer contains a `switch`/`if` chain over provider name strings for storage.

---

## 7. Files created/modified

| File | Change |
|------|--------|
| `Abhyanvaya.API/Media/IStorageProvider.cs` | Replaced `Name` with `ProviderName`, `DisplayName`, `ProviderType`. |
| `Abhyanvaya.API/Media/LocalStorageProvider.cs` | Renamed static `ProviderName` const → `Id`; implemented `ProviderName`, `DisplayName` (`Local File System`), `ProviderType` (`FileSystem`). |
| `Abhyanvaya.API/Media/S3StorageProvider.cs` | Renamed static `ProviderName` const → `Id`; implemented `ProviderName`, `DisplayName` (`Amazon S3`), `ProviderType` (`Cloud Storage`). |
| `Abhyanvaya.API/Media/MediaOptions.cs` | Updated references from `LocalStorageProvider.ProviderName`/`S3StorageProvider.ProviderName` to `.Id`. |
| `Abhyanvaya.API/Media/ConfigureMediaOptions.cs` | Same rename update. |
| `Abhyanvaya.API/Media/StorageProviderFactory.cs` | Same rename update. |
| `Abhyanvaya.API/Media/MediaOptionsValidator.cs` | Same rename update. |
| `Abhyanvaya.API/Program.cs` | Removed the storage provider `switch`; logs `storageProvider.DisplayName` directly. |

---

## 8. Build status

`dotnet build Abhyanvaya.sln` — **Build succeeded**, 0 errors.

## 9. Acceptance criteria

- ✅ Build succeeds.
- ✅ `Program.cs` simplified — no provider `switch`/`if` chain.
- ✅ No behavior changes — same provider selection logic, same runtime storage behavior.
