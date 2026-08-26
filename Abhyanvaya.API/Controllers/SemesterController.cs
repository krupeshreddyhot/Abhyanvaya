using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Application.DTOs.Semester;
using Abhyanvaya.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Abhyanvaya.API.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.API.Controllers
{
    [ApiController]
    [Route("api/semester")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSemesters)]
    public class SemesterController : ControllerBase
    {
        private readonly IApplicationDbContext _context;
        private readonly ICacheService _cache;
        private readonly ICurrentUserService _currentUser;
        private readonly ILegacySemesterMigrationAuditService _legacyMigrationAudit;
        private readonly ILegacySemesterMigrationDecisionPlanService _legacyMigrationDecisionPlan;
        private readonly ISemesterIiiSplitStudentRemapMigrationService _semesterIiiSplitMigration;
        private readonly ISemesterPostMigrationIntegrityAuditService _postMigrationIntegrityAudit;
        private readonly ILegacySemesterDownstreamRemediationService _downstreamRemediation;
        private readonly ILegacySemesterFinalizationAuditService _legacyFinalizationAudit;
        private readonly ILegacySemesterFinalizationExecutionService _legacyFinalizationExecution;
        private readonly ITeachingGroupSemesterRemediationService _teachingGroupSemesterRemediation;
        private readonly ITeachingGroupRemediationReadinessService _teachingGroupRemediationReadiness;
        private readonly ISectionSemesterRemediationService _sectionSemesterRemediation;
        private readonly IFinanceSectionSemesterRemediationService _financeSectionSemesterRemediation;
        private readonly ISectionSemesterRemediationAuditService _sectionSemesterRemediationAudit;
        private readonly ISubjectCatalogSemesterRemediationService _subjectCatalogSemesterRemediation;
        private readonly IPrompt3HPostSectionIntegrityAuditService _prompt3HIntegrityAudit;
        private readonly ILegacySemesterWildcardRetirementService _legacyWildcardRetirement;
        private readonly ISemesterSchemaHardeningReadinessService _schemaHardeningReadiness;
        private readonly ILegacySemesterFinalDispositionReadinessService _finalDispositionReadiness;
        private readonly ILegacySemesterHistoricalDispositionService _legacyHistoricalDisposition;
        private readonly IHistoricalSemesterDispositionAuditService _historicalDispositionAudit;
        private readonly IHistoricalSemesterDispositionExecutionService _historicalDispositionExecution;

        public SemesterController(
            IApplicationDbContext context,
            ICacheService cache,
            ICurrentUserService currentUser,
            ILegacySemesterMigrationAuditService legacyMigrationAudit,
            ILegacySemesterMigrationDecisionPlanService legacyMigrationDecisionPlan,
            ISemesterIiiSplitStudentRemapMigrationService semesterIiiSplitMigration,
            ISemesterPostMigrationIntegrityAuditService postMigrationIntegrityAudit,
            ILegacySemesterDownstreamRemediationService downstreamRemediation,
            ILegacySemesterFinalizationAuditService legacyFinalizationAudit,
            ILegacySemesterFinalizationExecutionService legacyFinalizationExecution,
            ITeachingGroupSemesterRemediationService teachingGroupSemesterRemediation,
            ITeachingGroupRemediationReadinessService teachingGroupRemediationReadiness,
            ISectionSemesterRemediationService sectionSemesterRemediation,
            IFinanceSectionSemesterRemediationService financeSectionSemesterRemediation,
            ISectionSemesterRemediationAuditService sectionSemesterRemediationAudit,
            ISubjectCatalogSemesterRemediationService subjectCatalogSemesterRemediation,
            IPrompt3HPostSectionIntegrityAuditService prompt3HIntegrityAudit,
            ILegacySemesterWildcardRetirementService legacyWildcardRetirement,
            ISemesterSchemaHardeningReadinessService schemaHardeningReadiness,
            ILegacySemesterFinalDispositionReadinessService finalDispositionReadiness,
            ILegacySemesterHistoricalDispositionService legacyHistoricalDisposition,
            IHistoricalSemesterDispositionAuditService historicalDispositionAudit,
            IHistoricalSemesterDispositionExecutionService historicalDispositionExecution)
        {
            _context = context;
            _cache = cache;
            _currentUser = currentUser;
            _legacyMigrationAudit = legacyMigrationAudit;
            _legacyMigrationDecisionPlan = legacyMigrationDecisionPlan;
            _semesterIiiSplitMigration = semesterIiiSplitMigration;
            _legacyWildcardRetirement = legacyWildcardRetirement;
            _schemaHardeningReadiness = schemaHardeningReadiness;
            _finalDispositionReadiness = finalDispositionReadiness;
            _legacyHistoricalDisposition = legacyHistoricalDisposition;
            _historicalDispositionAudit = historicalDispositionAudit;
            _historicalDispositionExecution = historicalDispositionExecution;
            _postMigrationIntegrityAudit = postMigrationIntegrityAudit;
            _downstreamRemediation = downstreamRemediation;
            _legacyFinalizationAudit = legacyFinalizationAudit;
            _legacyFinalizationExecution = legacyFinalizationExecution;
            _teachingGroupSemesterRemediation = teachingGroupSemesterRemediation;
            _teachingGroupRemediationReadiness = teachingGroupRemediationReadiness;
            _sectionSemesterRemediation = sectionSemesterRemediation;
            _financeSectionSemesterRemediation = financeSectionSemesterRemediation;
            _sectionSemesterRemediationAudit = sectionSemesterRemediationAudit;
            _subjectCatalogSemesterRemediation = subjectCatalogSemesterRemediation;
            _prompt3HIntegrityAudit = prompt3HIntegrityAudit;
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3J —
        /// Read-only Subject Catalog Semester remediation preview (legacy NULL-group → Group-specific).
        /// </summary>
        [HttpGet("subject-catalog-remediation-preview")]
        public async Task<IActionResult> GetSubjectCatalogRemediationPreview(CancellationToken cancellationToken)
        {
            var report = await _subjectCatalogSemesterRemediation.PreviewAsync(cancellationToken);
            return Ok(report);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3J —
        /// Execute deterministic Subject.SemesterId remaps only (SAFE_TO_REMAP). Zero partial commits.
        /// </summary>
        [HttpPost("subject-catalog-remediation/execute")]
        public async Task<IActionResult> ExecuteSubjectCatalogRemediation(CancellationToken cancellationToken)
        {
            var result = await _subjectCatalogSemesterRemediation.ExecuteAsync(cancellationToken);
            if (string.Equals(result.ExecutionStatus, "Aborted", StringComparison.OrdinalIgnoreCase))
                return Conflict(result);
            await _cache.RemoveAsync("master:semester");
            return Ok(result);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3I —
        /// Read-only preview of Finance Section Semester remediation (Sem 3 → Sem 10).
        /// </summary>
        [HttpGet("finance-section-remediation-preview")]
        public async Task<IActionResult> GetFinanceSectionRemediationPreview(CancellationToken cancellationToken)
        {
            var report = await _financeSectionSemesterRemediation.PreviewAsync(cancellationToken);
            return Ok(report);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3I —
        /// Execute Finance Section.SemesterId remediation (approved Finance Sections only).
        /// </summary>
        [HttpPost("finance-section-remediation/execute")]
        public async Task<IActionResult> ExecuteFinanceSectionRemediation(CancellationToken cancellationToken)
        {
            var result = await _financeSectionSemesterRemediation.ExecuteAsync(cancellationToken);
            if (string.Equals(result.ExecutionStatus, "Aborted", StringComparison.OrdinalIgnoreCase))
                return Conflict(result);
            await _cache.RemoveAsync("master:semester");
            return Ok(result);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3H —
        /// Read-only post–Prompt 3G integrity audit and schema-hardening readiness. Zero mutations.
        /// </summary>
        [HttpGet("post-section-remediation-integrity-audit")]
        [HttpGet("post-section-integrity-schema-readiness")]
        public async Task<IActionResult> GetPostSectionIntegritySchemaReadiness(CancellationToken cancellationToken)
        {
            var report = await _prompt3HIntegrityAudit.BuildAuditAsync(cancellationToken);
            return Ok(report);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3G.1 —
        /// Read-only Section Semester remediation post-execution audit &amp; readiness. Zero mutations.
        /// </summary>
        [HttpGet("section-semester-remediation-audit")]
        public async Task<IActionResult> GetSectionSemesterRemediationAudit(CancellationToken cancellationToken)
        {
            var report = await _sectionSemesterRemediationAudit.BuildAuditAsync(cancellationToken);
            return Ok(report);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3G —
        /// Read-only preview of Section Semester remediation (Sem 3 → Sem 11). Does not mutate Teaching Groups.
        /// </summary>
        [HttpGet("section-semester-remediation-preview")]
        public async Task<IActionResult> GetSectionSemesterRemediationPreview(CancellationToken cancellationToken)
        {
            var report = await _sectionSemesterRemediation.PreviewAsync(cancellationToken);
            return Ok(report);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3G —
        /// Execute Section.SemesterId remediation for approved CA Sections only.
        /// Does not mutate TeachingGroup / TeachingGroupSection / SA / TT / Attendance / StudentSection.
        /// </summary>
        [HttpPost("section-semester-remediation/execute")]
        public async Task<IActionResult> ExecuteSectionSemesterRemediation(CancellationToken cancellationToken)
        {
            var result = await _sectionSemesterRemediation.ExecuteAsync(cancellationToken);
            if (string.Equals(result.ExecutionStatus, "Aborted", StringComparison.OrdinalIgnoreCase))
                return Conflict(result);
            await _cache.RemoveAsync("master:semester");
            return Ok(result);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3H (TG readiness / PromptCode P1-4-3H2) —
        /// Read-only post-Section Teaching Group remediation readiness. Does not execute Prompt 3F.
        /// </summary>
        [HttpGet("teaching-group-remediation-readiness")]
        public async Task<IActionResult> GetTeachingGroupRemediationReadiness(CancellationToken cancellationToken)
        {
            var report = await _teachingGroupRemediationReadiness.BuildAsync(cancellationToken);
            return Ok(report);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3F —
        /// Read-only preview of Teaching Group Semester remediation (approved TG IDs only).
        /// </summary>
        [HttpGet("teaching-group-remediation-preview")]
        public async Task<IActionResult> GetTeachingGroupRemediationPreview(CancellationToken cancellationToken)
        {
            var report = await _teachingGroupSemesterRemediation.PreviewAsync(cancellationToken);
            return Ok(report);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3F —
        /// Execute TeachingGroup.SemesterId remediation for the two approved residuals only.
        /// Does not mutate TeachingGroupSection, membership, Attendance, or StudentSection.
        /// </summary>
        [HttpPost("teaching-group-remediation/execute")]
        public async Task<IActionResult> ExecuteTeachingGroupRemediation(CancellationToken cancellationToken)
        {
            var result = await _teachingGroupSemesterRemediation.ExecuteAsync(cancellationToken);
            if (string.Equals(result.ExecutionStatus, "Aborted", StringComparison.OrdinalIgnoreCase))
                return Conflict(result);
            await _cache.RemoveAsync("master:semester");
            return Ok(result);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3E —
        /// Read-only preview of legacy Semester disposition finalization (no mutation).
        /// </summary>
        [HttpGet("legacy-finalization-execution-preview")]
        public async Task<IActionResult> GetLegacyFinalizationExecutionPreview(CancellationToken cancellationToken)
        {
            var report = await _legacyFinalizationExecution.PreviewAsync(cancellationToken);
            return Ok(report);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3E —
        /// Execute controlled legacy Semester disposition finalization (RETAIN_HISTORICAL journal).
        /// Teaching Groups remain out of scope. No schema hardening.
        /// </summary>
        [HttpPost("legacy-finalization/execute")]
        public async Task<IActionResult> ExecuteLegacyFinalization(CancellationToken cancellationToken)
        {
            var result = await _legacyFinalizationExecution.ExecuteAsync(cancellationToken);
            if (string.Equals(result.ExecutionStatus, "Aborted", StringComparison.OrdinalIgnoreCase))
                return Conflict(result);
            await _cache.RemoveAsync("master:semester");
            return Ok(result);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3D —
        /// Read-only legacy Semester finalization & DB hardening discovery audit. No mutation.
        /// </summary>
        [HttpGet("legacy-finalization-audit")]
        public async Task<IActionResult> GetLegacyFinalizationAudit(CancellationToken cancellationToken)
        {
            var report = await _legacyFinalizationAudit.BuildAuditAsync(cancellationToken);
            return Ok(report);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3L (package 3I1) —
        /// Preview legacy disposition + operational wildcard retirement readiness. Read-only.
        /// </summary>
        [HttpGet("legacy-wildcard-retirement-preview")]
        public async Task<IActionResult> GetLegacyWildcardRetirementPreview(CancellationToken cancellationToken)
        {
            var report = await _legacyWildcardRetirement.PreviewAsync(cancellationToken);
            return Ok(report);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3I (package 3I3 / PromptCode P1-4-3I3) —
        /// Read-only wildcard retirement readiness contract. Zero mutations.
        /// </summary>
        [HttpGet("legacy-wildcard-retirement-readiness")]
        public async Task<IActionResult> GetLegacyWildcardRetirementReadiness(CancellationToken cancellationToken)
        {
            var report = await _legacyWildcardRetirement.BuildReadinessAsync(cancellationToken);
            return Ok(report);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3L (package 3I1) —
        /// Journal disposition + OPERATIONAL_WILDCARD_RETIRED evidence. No Semester deletes / GroupId guesses / TG mutation.
        /// </summary>
        [HttpPost("legacy-wildcard-retirement/execute")]
        public async Task<IActionResult> ExecuteLegacyWildcardRetirement(CancellationToken cancellationToken)
        {
            var result = await _legacyWildcardRetirement.ExecuteAsync(cancellationToken);
            if (string.Equals(result.ExecutionStatus, "Aborted", StringComparison.OrdinalIgnoreCase))
                return Conflict(result);
            return Ok(result);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3J (package 3J3 / PromptCode P1-4-3J3) —
        /// Final Semester schema-hardening readiness GO/NO-GO contract. Read-only; zero DDL/mutation.
        /// </summary>
        [HttpGet("schema-hardening-readiness")]
        public async Task<IActionResult> GetSchemaHardeningReadiness(CancellationToken cancellationToken)
        {
            var report = await _schemaHardeningReadiness.BuildAsync(cancellationToken);
            return Ok(report);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3I (package 3I2 / PromptCode P1-4-3N) —
        /// Final legacy Semester disposition + schema hardening readiness gate. Read-only; zero DDL/mutation.
        /// </summary>
        [HttpGet("legacy-final-disposition-schema-hardening-readiness")]
        public async Task<IActionResult> GetLegacyFinalDispositionSchemaHardeningReadiness(CancellationToken cancellationToken)
        {
            var report = await _finalDispositionReadiness.BuildAsync(cancellationToken);
            return Ok(report);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3K-A (package 3KA / PromptCode P1-4-3KA) —
        /// Read-only historical Semester disposition &amp; archive architecture discovery audit. Zero mutations.
        /// </summary>
        [HttpGet("historical-disposition-audit")]
        public async Task<IActionResult> GetHistoricalDispositionAudit(CancellationToken cancellationToken)
        {
            var report = await _historicalDispositionAudit.BuildAuditAsync(cancellationToken);
            return Ok(report);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3K-B (package 3KB / PromptCode P1-4-3KB) —
        /// Controlled HISTORICAL_ARCHIVE for ARCHIVE_ELIGIBLE Semesters only. ALL_OR_NOTHING; no Group inventing.
        /// </summary>
        [HttpPost("historical-disposition/execute")]
        public async Task<IActionResult> ExecuteHistoricalDisposition(
            [FromBody] HistoricalSemesterDispositionExecuteRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _historicalDispositionExecution.ExecuteAsync(request, cancellationToken);
            if (!result.IsSuccessful
                && !string.Equals(result.ExecutionStatus, "AlreadyComplete", StringComparison.OrdinalIgnoreCase))
                return Conflict(result);
            if (result.Archived > 0)
                await _cache.RemoveAsync("master:semester");
            return Ok(result);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3J-A (PromptCode P1-4-3JA) —
        /// Read-only legacy historical disposition preview. Zero mutations.
        /// </summary>
        [HttpGet("legacy-historical-disposition-preview")]
        public async Task<IActionResult> GetLegacyHistoricalDispositionPreview(CancellationToken cancellationToken)
        {
            var report = await _legacyHistoricalDisposition.PreviewAsync(cancellationToken);
            return Ok(report);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3J-A (PromptCode P1-4-3JA) —
        /// Execute explicit per-Semester historical dispositions. No archive-all; no Group guessing; no DDL.
        /// </summary>
        [HttpPost("legacy-historical-disposition/execute")]
        public async Task<IActionResult> ExecuteLegacyHistoricalDisposition(
            [FromBody] LegacySemesterHistoricalDispositionExecuteRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _legacyHistoricalDisposition.ExecuteAsync(request, cancellationToken);
            if (!result.IsSuccessful
                && !string.Equals(result.ExecutionStatus, "AlreadyComplete", StringComparison.OrdinalIgnoreCase))
                return Conflict(result);
            await _cache.RemoveAsync("master:semester");
            return Ok(result);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3C —
        /// Read-only audit of downstream legacy Semester III references.
        /// </summary>
        [HttpGet("downstream-remediation-audit")]
        public async Task<IActionResult> GetDownstreamRemediationAudit(CancellationToken cancellationToken)
        {
            var report = await _downstreamRemediation.AuditAsync(cancellationToken);
            return Ok(report);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3C —
        /// Read-only preview of proposed SemesterId remediations (no mutation).
        /// </summary>
        [HttpGet("downstream-remediation-preview")]
        public async Task<IActionResult> GetDownstreamRemediationPreview(CancellationToken cancellationToken)
        {
            var report = await _downstreamRemediation.PreviewAsync(cancellationToken);
            return Ok(report);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3C —
        /// Execute controlled remediation for AttendanceSession / SubjectAllocation / TimetableEntry.
        /// TeachingGroup remains identify-only.
        /// </summary>
        [HttpPost("downstream-remediation/execute")]
        public async Task<IActionResult> ExecuteDownstreamRemediation(CancellationToken cancellationToken)
        {
            var result = await _downstreamRemediation.ExecuteAsync(cancellationToken);
            if (string.Equals(result.ExecutionStatus, "Aborted", StringComparison.OrdinalIgnoreCase))
                return Conflict(result);
            await _cache.RemoveAsync("master:semester");
            return Ok(result);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3B-A —
        /// Read-only post-migration integrity audit. No mutation / repair.
        /// </summary>
        [HttpGet("post-migration-integrity-audit")]
        public async Task<IActionResult> GetPostMigrationIntegrityAudit(CancellationToken cancellationToken)
        {
            var report = await _postMigrationIntegrityAudit.BuildAuditAsync(cancellationToken);
            return Ok(report);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 2B —
        /// Read-only legacy Semester migration mapping worksheet. No mutate / migrate / split execution.
        /// </summary>
        [HttpGet("legacy-migration-audit")]
        public async Task<IActionResult> GetLegacyMigrationAudit(CancellationToken cancellationToken)
        {
            var report = await _legacyMigrationAudit.BuildAuditAsync(cancellationToken);
            return Ok(report);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3A —
        /// Read-only explicit migration decision plan. No execution.
        /// </summary>
        [HttpGet("legacy-migration-decision-plan")]
        public async Task<IActionResult> GetLegacyMigrationDecisionPlan(CancellationToken cancellationToken)
        {
            var plan = await _legacyMigrationDecisionPlan.BuildDecisionPlanAsync(cancellationToken);
            return Ok(plan);
        }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3B —
        /// Controlled admin-only Semester III split + Student.SemesterId remap. Not a generic reassignment API.
        /// </summary>
        [HttpPost("migrations/semester-iii-split-student-remap")]
        public async Task<IActionResult> ExecuteSemesterIiiSplitStudentRemap(CancellationToken cancellationToken)
        {
            var result = await _semesterIiiSplitMigration.ExecuteAsync(cancellationToken);
            if (string.Equals(result.Status, "Aborted", StringComparison.OrdinalIgnoreCase))
                return Conflict(result);
            await _cache.RemoveAsync("master:semester");
            return Ok(result);
        }

        /// <summary>
        /// Lists Semesters.
        /// Default: operational only (Group-specific, not historical archive).
        /// includeHistorical=true: include explicitly archived historical rows.
        /// includeNullGroupLegacy=true: include legacy NULL-group rows that are not yet archived.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int? courseId = null,
            [FromQuery] int? groupId = null,
            [FromQuery] bool includeHistorical = false,
            [FromQuery] bool includeNullGroupLegacy = false)
        {
            var query = _context.Semesters.AsNoTracking().AsQueryable();
            if (courseId is > 0)
                query = query.Where(x => x.CourseId == courseId.Value);
            if (groupId is > 0)
                query = query.Where(x => x.GroupId == groupId.Value);

            if (!includeHistorical)
                query = query.Where(x => !x.IsHistoricalArchive);
            if (!includeNullGroupLegacy)
                query = query.Where(x => x.GroupId != null || x.IsHistoricalArchive);

            var data = await query
                .OrderBy(x => x.CourseId)
                .ThenBy(x => x.GroupId == null ? 0 : 1)
                .ThenBy(x => x.GroupId)
                .ThenBy(x => x.Number)
                .Select(x => new
                {
                    x.Id,
                    x.Number,
                    x.Name,
                    x.CourseId,
                    CourseName = x.Course != null ? x.Course.Name : "",
                    x.GroupId,
                    GroupName = x.Group != null ? x.Group.Name : (string?)null,
                    IsLegacyCourseWide = x.GroupId == null,
                    x.IsHistoricalArchive
                })
                .ToListAsync();

            return Ok(data);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateSemesterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Semester name is required.");
            if (request.Number < 1)
                return BadRequest("Semester number must be at least 1.");

            var tenantId = _currentUser.TenantId;
            var group = await LoadGroupSnapshotAsync(request.GroupId);
            var decision = SemesterGroupOwnershipRules.EvaluateWrite(
                tenantId, request.GroupId, request.CourseId, group);
            if (!decision.Accepted)
                return BadRequest(decision.Error);

            if (await DuplicateExistsAsync(decision.AlignedGroupId, request.Number, excludeId: null))
                return BadRequest(SemesterGroupOwnershipRules.DuplicateNumberMessage);

            var semester = new Semester
            {
                Name = request.Name.Trim(),
                Number = request.Number,
                CourseId = decision.AlignedCourseId,
                GroupId = decision.AlignedGroupId,
                TenantId = tenantId,
                CreatedDate = DateTime.UtcNow
            };

            await _context.AddAsync(semester);
            await _context.SaveChangesAsync();
            await _cache.RemoveAsync("master:semester");
            return Ok(semester);
        }

        [HttpPut]
        public async Task<IActionResult> Update(UpdateSemesterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Semester name is required.");
            if (request.Number < 1)
                return BadRequest("Semester number must be at least 1.");

            var semester = await _context.Semesters.FirstOrDefaultAsync(x => x.Id == request.Id);
            if (semester == null)
                return NotFound();

            var tenantId = _currentUser.TenantId;
            var group = await LoadGroupSnapshotAsync(request.GroupId);
            var decision = SemesterGroupOwnershipRules.EvaluateWrite(
                tenantId, request.GroupId, request.CourseId, group);
            if (!decision.Accepted)
                return BadRequest(decision.Error);

            // Moving to another Course via Group is rejected by EvaluateWrite when CourseId hint mismatches;
            // also reject when aligned course differs from existing without explicit Group change intent —
            // Group.CourseId is authoritative.
            if (await DuplicateExistsAsync(decision.AlignedGroupId, request.Number, excludeId: request.Id))
                return BadRequest(SemesterGroupOwnershipRules.DuplicateNumberMessage);

            semester.Name = request.Name.Trim();
            semester.Number = request.Number;
            semester.CourseId = decision.AlignedCourseId;
            semester.GroupId = decision.AlignedGroupId;
            semester.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await _cache.RemoveAsync("master:semester");
            return Ok(semester);
        }

        private async Task<SemesterGroupOwnershipRules.GroupSnapshot?> LoadGroupSnapshotAsync(int groupId)
        {
            if (groupId <= 0) return null;
            return await _context.Groups.AsNoTracking()
                .Where(g => g.Id == groupId)
                .Select(g => new SemesterGroupOwnershipRules.GroupSnapshot(
                    g.Id, g.TenantId, g.CourseId, g.IsDeleted))
                .FirstOrDefaultAsync();
        }

        private async Task<bool> DuplicateExistsAsync(int groupId, int number, int? excludeId)
        {
            return await _context.Semesters.AnyAsync(x =>
                x.TenantId == _currentUser.TenantId
                && x.GroupId == groupId
                && x.Number == number
                && (excludeId == null || x.Id != excludeId.Value));
        }
    }
}
