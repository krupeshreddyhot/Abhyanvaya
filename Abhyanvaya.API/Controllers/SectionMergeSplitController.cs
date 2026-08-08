using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.DTOs.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

[ApiController]
[Route("api/sections")]
[Authorize]
public sealed class SectionMergeSplitController : ControllerBase
{
    private readonly ISectionMergeService _merge;
    private readonly ISectionSplitService _split;

    public SectionMergeSplitController(ISectionMergeService merge, ISectionSplitService split)
    {
        _merge = merge;
        _split = split;
    }

    [HttpPost("merge/validate")]
    [Authorize(Policy = AuthorizationPolicies.CanMergeSections)]
    public async Task<ActionResult<SectionMergePreviewDto>> ValidateMerge([FromBody] SectionMergeValidateRequest request, CancellationToken cancellationToken)
        => Ok(await _merge.ValidateAsync(request, cancellationToken));

    [HttpPost("merge/preview")]
    [Authorize(Policy = AuthorizationPolicies.CanMergeSections)]
    public async Task<ActionResult<SectionMergePreviewDto>> PreviewMerge([FromBody] SectionMergeValidateRequest request, CancellationToken cancellationToken)
        => Ok(await _merge.PreviewAsync(request, cancellationToken));

    [HttpPost("merge/commit")]
    [Authorize(Policy = AuthorizationPolicies.CanMergeSections)]
    public async Task<ActionResult<SectionMergeTransactionDto>> CommitMerge([FromBody] SectionMergeCommitRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await _merge.CommitAsync(request, cancellationToken)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("merge/{transactionId:guid}/reverse")]
    [Authorize(Policy = AuthorizationPolicies.CanMergeSections)]
    public async Task<ActionResult<SectionMergeTransactionDto>> ReverseMerge(Guid transactionId, CancellationToken cancellationToken)
    {
        try { return Ok(await _merge.ReverseAsync(transactionId, cancellationToken)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("merge/history")]
    [Authorize(Policy = AuthorizationPolicies.CanMergeSections)]
    public async Task<ActionResult<IReadOnlyList<SectionMergeTransactionDto>>> MergeHistory(CancellationToken cancellationToken)
        => Ok(await _merge.GetHistoryAsync(cancellationToken));

    [HttpPost("split/validate")]
    [Authorize(Policy = AuthorizationPolicies.CanSplitSections)]
    public async Task<ActionResult<SectionSplitPreviewDto>> ValidateSplit([FromBody] SectionSplitValidateRequest request, CancellationToken cancellationToken)
        => Ok(await _split.ValidateAsync(request, cancellationToken));

    [HttpPost("split/preview")]
    [Authorize(Policy = AuthorizationPolicies.CanSplitSections)]
    public async Task<ActionResult<SectionSplitPreviewDto>> PreviewSplit([FromBody] SectionSplitValidateRequest request, CancellationToken cancellationToken)
        => Ok(await _split.PreviewAsync(request, cancellationToken));

    [HttpPost("split/commit")]
    [Authorize(Policy = AuthorizationPolicies.CanSplitSections)]
    public async Task<ActionResult<SectionSplitTransactionDto>> CommitSplit([FromBody] SectionSplitCommitRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await _split.CommitAsync(request, cancellationToken)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("split/{transactionId:guid}/reverse")]
    [Authorize(Policy = AuthorizationPolicies.CanSplitSections)]
    public async Task<ActionResult<SectionSplitTransactionDto>> ReverseSplit(Guid transactionId, CancellationToken cancellationToken)
    {
        try { return Ok(await _split.ReverseAsync(transactionId, cancellationToken)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("split/history")]
    [Authorize(Policy = AuthorizationPolicies.CanSplitSections)]
    public async Task<ActionResult<IReadOnlyList<SectionSplitTransactionDto>>> SplitHistory(CancellationToken cancellationToken)
        => Ok(await _split.GetHistoryAsync(cancellationToken));

    [HttpGet("{sectionId:int}/lineage")]
    [Authorize(Policy = AuthorizationPolicies.CanViewSections)]
    public async Task<ActionResult<IReadOnlyList<SectionLineageDto>>> Lineage(int sectionId, CancellationToken cancellationToken)
        => Ok(await _split.GetLineageAsync(sectionId, cancellationToken));
}
