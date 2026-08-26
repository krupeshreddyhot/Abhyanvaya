using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Abhyanvaya.Application.Academic.Allocation;

/// <summary>
/// AI29.1D.24B.4A.3 — Deterministic academic allocation-context integrity checksum (v2.0.0).
/// Hashes stable academic/configuration identity only — never ContextId, timestamps, or live occupancy.
/// Scenario/pipeline configuration remains under <see cref="AllocationCanonicalChecksum"/>.
/// </summary>
public static class AllocationAcademicContextIntegrity
{
    public const string AlgorithmVersion = "2.0.0";

    private static readonly JsonSerializerOptions CompactJson = new() { WriteIndented = false };

    /// <summary>SHA-256 (uppercase hex) of the canonical academic context payload.</summary>
    public static string Compute(SectionAllocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var canonical = BuildCanonicalPayload(context).ToJsonString(CompactJson);
        return Sha256Hex(canonical);
    }

    /// <summary>Builds the approved v2.0.0 canonical JSON payload (deterministic property and collection order).</summary>
    public static JsonObject BuildCanonicalPayload(SectionAllocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var hierarchy = context.Hierarchy ?? new AllocationHierarchyProjection();
        var capacityBySection = (context.Capacities ?? [])
            .GroupBy(c => c.SectionId)
            .ToDictionary(g => g.Key, g => g.First());

        var sections = new JsonArray();
        foreach (var section in (context.Sections ?? []).OrderBy(s => s.SectionId))
        {
            capacityBySection.TryGetValue(section.SectionId, out var cap);
            sections.Add(new JsonObject
            {
                ["sectionId"] = section.SectionId,
                ["maximumCapacity"] = cap?.MaximumCapacity ?? 0,
                ["minimumCapacity"] = cap?.MinimumCapacity ?? 0,
                ["reservedSeats"] = cap?.ReservedSeats ?? 0,
            });
        }

        var studentIds = (context.Students ?? []).Select(s => s.StudentId);
        var populationChecksum = ComputePopulationChecksum(studentIds);
        var facultyChecksum = ComputeFacultyAssignmentChecksum(context.FacultyAssignments ?? []);

        // Explicit property order matches the approved contract (do not alphabetize).
        return new JsonObject
        {
            ["integrityAlgorithmVersion"] = AlgorithmVersion,
            ["schemaVersion"] = context.SchemaVersion ?? "",
            ["hierarchy"] = new JsonObject
            {
                ["academicYearId"] = hierarchy.AcademicYearId,
                ["programId"] = hierarchy.ProgramId.HasValue
                    ? JsonValue.Create(hierarchy.ProgramId.Value)
                    : null,
                ["courseId"] = hierarchy.CourseId,
                ["groupId"] = hierarchy.GroupId,
                ["semesterId"] = hierarchy.SemesterId,
            },
            ["sections"] = sections,
            ["populationChecksum"] = populationChecksum,
            ["studentCount"] = (context.Students ?? []).Count,
            ["facultyAssignmentChecksum"] = facultyChecksum,
        };
    }

    /// <summary>
    /// PopulationChecksum = SHA256(canonical JSON array of ascending eligible StudentIds).
    /// </summary>
    public static string ComputePopulationChecksum(IEnumerable<int> studentIds)
    {
        ArgumentNullException.ThrowIfNull(studentIds);
        var ordered = studentIds.OrderBy(id => id).ToList();
        var array = new JsonArray();
        foreach (var id in ordered)
            array.Add(id);
        return Sha256Hex(array.ToJsonString(CompactJson));
    }

    /// <summary>
    /// FacultyAssignmentChecksum = SHA256(canonical rows ordered by SectionId, FacultyId, Role Ordinal).
    /// Empty collection hashes as <c>[]</c>.
    /// </summary>
    public static string ComputeFacultyAssignmentChecksum(
        IEnumerable<AllocationFacultyProjection> facultyAssignments)
    {
        ArgumentNullException.ThrowIfNull(facultyAssignments);
        var ordered = facultyAssignments
            .OrderBy(f => f.SectionId)
            .ThenBy(f => f.FacultyId)
            .ThenBy(f => f.Role ?? "", StringComparer.Ordinal)
            .ToList();

        var array = new JsonArray();
        foreach (var f in ordered)
        {
            array.Add(new JsonObject
            {
                ["sectionId"] = f.SectionId,
                ["facultyId"] = f.FacultyId,
                ["role"] = f.Role ?? "",
            });
        }

        return Sha256Hex(array.ToJsonString(CompactJson));
    }

    private static string Sha256Hex(string payload)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload ?? "")));
}
