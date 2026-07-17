# AI20.PHASE2.1.5G — Storage Pipeline Metadata Framework

**Milestone:** Metadata-only enhancement — no execution logic changes.

## Objective

Extend `IEnrollmentStorageStep` with self-describing metadata so the storage pipeline executor becomes discoverable for future UI, diagnostics, and documentation.

## Pipeline Metadata Model

### StorageStepMetadata

Immutable record describing a single pipeline step:

| Field | Type | Description |
|-------|------|-------------|
| `Name` | `string` | Step identifier (e.g. `ValidateInput`) |
| `Category` | `string` | Functional category (see below) |
| `Version` | `string` | Step contract version (default `1.0`) |
| `Order` | `int` | Execution order |
| `SupportsRollback` | `bool` | Whether rollback applies to artifacts produced by this step |
| `Optional` | `bool` | Whether the step can be skipped via feature flag |
| `FeatureFlag` | `string?` | Feature flag key when optional |
| `Description` | `string` | Human-readable purpose |

### IEnrollmentStorageStep Extensions

| Property | Description |
|----------|-------------|
| `Category` | Step category constant |
| `SupportsRollback` | Rollback participation |
| `IsOptional` | Optional step marker |
| `FeatureFlag` | Nullable feature flag identifier |
| `Description` | Step description |
| `Version` | Step version |

Existing properties retained: `Name`, `Order`, `Enabled`, `ExecuteAsync`.

### Discovery API

`IEnrollmentStoragePipelineExecutor.DescribePipeline()` returns ordered `StorageStepMetadata` for all registered steps plus rollback metadata.

## Step Categories

| Category | Steps |
|----------|-------|
| **Validation** | ValidateInput, DuplicateDetection |
| **Preparation** | ResolvePolicy, PrepareArtifacts |
| **Checksum** | Checksum |
| **Compression** | Compression (optional, feature-flagged) |
| **Encryption** | Encryption (optional, feature-flagged) |
| **Upload** | Upload |
| **Metadata** | Metadata |
| **Manifest** | Manifest |
| **Rollback** | Rollback (failure cleanup) |

### Future Categories (reserved)

- Audit
- Replication
- Archive

Defined in `StorageStepCategory` with `IsKnown()` validation.

## Current Pipeline Descriptor

| Order | Name | Category | Optional | Feature Flag | Supports Rollback |
|------:|------|----------|:--------:|--------------|:-----------------:|
| 10 | ValidateInput | Validation | | | |
| 20 | ResolvePolicy | Preparation | | | |
| 30 | PrepareArtifacts | Preparation | | | |
| 40 | Checksum | Checksum | | | |
| 45 | Compression | Compression | ✓ | `EnrollmentStorage.Compression` | |
| 47 | Encryption | Encryption | ✓ | `EnrollmentStorage.Encryption` | |
| 50 | DuplicateDetection | Validation | | | |
| 60 | Upload | Upload | | | ✓ |
| 70 | Metadata | Metadata | | | ✓ |
| 80 | Manifest | Manifest | | | |
| 999 | Rollback | Rollback | | | |

## Extension Strategy

1. **New step:** Implement `IEnrollmentStorageStep`, set metadata properties, register in DI.
2. **Optional step:** Set `IsOptional = true` and `FeatureFlag`; gate via `Enabled` override.
3. **Discovery:** UI/diagnostics call `DescribePipeline()` — no hard-coded step lists.
4. **Category validation:** Use `StorageStepCategory.IsKnown()` when rendering or validating descriptors.

## Future Dynamic Pipeline

Metadata enables a future dynamic pipeline without changing `EnrollmentStorageService`:

```csharp
// Future: filter steps by feature flags at runtime
var activeSteps = executor.DescribePipeline()
    .Where(s => !s.Optional || _flags.IsEnabled(s.FeatureFlag));
```

Potential extensions:

- Admin UI pipeline diagram from `DescribePipeline()`
- OpenAPI-style pipeline documentation generation
- Runtime step enable/disable via feature flag service
- Audit/Replication/Archive steps added with new categories

## Testing

| Test | Validates |
|------|-----------|
| `DescribePipeline_ReturnsOrderedMetadataIncludingRollback` | 11 steps, ordered, rollback last |
| `DescribePipeline_AllStepsHaveKnownCategories` | Category integrity |
| `DescribePipeline_OptionalStepsHaveFeatureFlags` | Feature flag parsing |
| `DescribePipeline_RollbackCapableStepsAreUploadAndMetadata` | Rollback metadata |
| `StepMetadata_OrderMatchesExecutionContract` | Order integrity |

## Files Created

| File |
|------|
| `Abhyanvaya.Application/Enrollment/Storage/StorageStepMetadata.cs` |
| `Abhyanvaya.Application/Enrollment/Storage/StorageStepCategory.cs` |
| `Abhyanvaya.Application/Enrollment/Storage/StorageStepMetadataExtensions.cs` |
| `Abhyanvaya.Application.UnitTests/Enrollment/Storage/EnrollmentStoragePipelineMetadataTests.cs` |
| `docs/AI20_PHASE2_IMPLEMENTATION_5G_PIPELINE_METADATA.md` |

## Files Modified

| File | Change |
|------|--------|
| `Abhyanvaya.Application/Common/Interfaces/IEnrollmentStorageStep.cs` | Metadata properties |
| `Abhyanvaya.Application/Common/Interfaces/IEnrollmentStoragePipelineExecutor.cs` | `DescribePipeline()` |
| `Abhyanvaya.Infrastructure/Enrollment/Storage/Pipeline/EnrollmentStoragePipelineSteps.cs` | Metadata on all steps + `RollbackStep.Metadata` |
| `Abhyanvaya.Infrastructure/Enrollment/Storage/Pipeline/EnrollmentStoragePipelineExecutor.cs` | `DescribePipeline()` implementation |

## Verification

- 0 build errors
- Metadata only — no execution logic changes
- 79/79 enrollment unit tests passing
- Backward compatibility maintained
