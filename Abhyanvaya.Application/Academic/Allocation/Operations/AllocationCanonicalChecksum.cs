using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Abhyanvaya.Domain.Entities.Academic;

namespace Abhyanvaya.Application.Academic.Allocation;

/// <summary>
/// AI29.1C.5A — Deterministic canonical checksum for scenario versions.
/// Covers: scenario data, context version/checksum, strategy/constraint config versions,
/// score, trace, and lifecycle state. Uses SHA-256 (platform-approved).
/// </summary>
public static class AllocationCanonicalChecksum
{
    /// <summary>
    /// Builds a deterministic UTF-8 payload then hashes with SHA-256 (hex uppercase).
    /// Property ordering is forced via sorted JSON tree — never hash raw arbitrary JSON order.
    /// </summary>
    public static string Compute(AllocationScenarioVersionChecksumInput input)
    {
        var node = new JsonObject
        {
            ["scenarioId"] = input.ScenarioId.ToString("D"),
            ["versionNumber"] = input.VersionNumber,
            ["contextVersion"] = input.ContextVersion ?? "",
            ["contextChecksum"] = (input.ContextChecksum ?? "").ToLowerInvariant(),
            ["strategyConfigurationVersion"] = input.StrategyConfigurationVersion ?? "",
            ["constraintConfigurationVersion"] = input.ConstraintConfigurationVersion ?? "",
            ["lifecycleStatus"] = input.LifecycleStatus ?? "",
            ["operation"] = input.Operation ?? "",
            ["score"] = Math.Round(input.Score, 6),
            ["scenario"] = CanonicalizeJson(input.ScenarioJson),
            ["trace"] = CanonicalizeJson(input.TraceJson),
            ["config"] = CanonicalizeJson(input.ConfigJson),
        };
        var canonical = node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static string Sha256Utf8(string payload)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload ?? "")));

    private static JsonNode CanonicalizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new JsonObject();
        try
        {
            var node = JsonNode.Parse(json);
            return CanonicalizeNode(node) ?? new JsonObject();
        }
        catch (JsonException)
        {
            // Non-JSON payloads hashed as opaque string leaf.
            return JsonValue.Create(json)!;
        }
    }

    private static JsonNode? CanonicalizeNode(JsonNode? node)
    {
        if (node is null) return null;
        if (node is JsonObject obj)
        {
            var ordered = new JsonObject();
            foreach (var kv in obj.OrderBy(k => k.Key, StringComparer.Ordinal))
                ordered[kv.Key] = CanonicalizeNode(kv.Value)?.DeepClone();
            return ordered;
        }

        if (node is JsonArray arr)
        {
            var next = new JsonArray();
            foreach (var item in arr)
                next.Add(CanonicalizeNode(item)?.DeepClone());
            return next;
        }

        return node.DeepClone();
    }
}

public sealed class AllocationScenarioVersionChecksumInput
{
    public Guid ScenarioId { get; init; }
    public int VersionNumber { get; init; }
    public string? ContextVersion { get; init; }
    public string? ContextChecksum { get; init; }
    public string? StrategyConfigurationVersion { get; init; }
    public string? ConstraintConfigurationVersion { get; init; }
    public string? LifecycleStatus { get; init; }
    public string? Operation { get; init; }
    public double Score { get; init; }
    public string? ScenarioJson { get; init; }
    public string? TraceJson { get; init; }
    public string? ConfigJson { get; init; }
}

/// <summary>AI29.1C.5A — Detects contradictory Status vs LifecycleStatus combinations.</summary>
public static class AllocationStatusConsistency
{
    private static readonly HashSet<string> GovernanceOnly = new(StringComparer.OrdinalIgnoreCase)
    {
        AllocationScenarioLifecycle.Approved,
        AllocationScenarioLifecycle.Rejected,
        AllocationScenarioLifecycle.Reviewed,
        AllocationScenarioLifecycle.Archived,
        AllocationScenarioLifecycle.Compared,
        AllocationScenarioLifecycle.Saved,
        AllocationScenarioLifecycle.Draft,
        AllocationScenarioLifecycle.Simulated,
        AllocationScenarioLifecycle.SimulationAccepted,
    };

    public static bool IsContradictory(string? status, string? lifecycleStatus)
    {
        if (string.IsNullOrWhiteSpace(status) || string.IsNullOrWhiteSpace(lifecycleStatus))
            return false;

        // Status holds a governance value that disagrees with LifecycleStatus.
        if (GovernanceOnly.Contains(status)
            && !string.Equals(status, lifecycleStatus, StringComparison.OrdinalIgnoreCase)
            && !(status == AllocationScenarioLifecycle.Generated
                 && AllocationScenarioLifecycle.Normalize(lifecycleStatus) == AllocationScenarioLifecycle.Draft)
            && !(status == AllocationScenarioLifecycle.SimulationAccepted
                 && AllocationScenarioLifecycle.Normalize(lifecycleStatus) == AllocationScenarioLifecycle.Simulated))
        {
            return true;
        }

        // Explicit invalid pair called out in the prompt.
        if (string.Equals(status, AllocationScenarioLifecycle.Approved, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(lifecycleStatus, AllocationScenarioLifecycle.Approved, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(status, AllocationScenarioLifecycle.Approved, StringComparison.OrdinalIgnoreCase)
            && string.Equals(lifecycleStatus, AllocationScenarioLifecycle.Reviewed, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
