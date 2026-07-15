using System.Diagnostics;
using Abhyanvaya.API.Media;
using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace Abhyanvaya.API.Controllers;

/// <summary>
/// AI19.MEDIA.3.2 — provider-aware media retrieval. Serves <c>GET /media/{**key}</c> by reading the
/// requested object through <see cref="IMediaObjectReader"/> (which delegates to whichever
/// <c>IStorageProvider</c> is currently active — local disk or Cloudflare R2/S3), instead of the
/// local-disk-only <c>UseStaticFiles("/media")</c> middleware registered in <c>Program.cs</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> AI19.MEDIA.2 established that <c>/media/*</c> was served exclusively by
/// <c>UseStaticFiles</c> over a local <c>PhysicalFileProvider</c>, which never consulted
/// <c>IStorageProviderFactory</c>. Once uploads (recognition thumbnails, classroom photos, student/branding
/// images — all via <c>IMediaStorageService</c> → <c>IStorageProviderFactory</c>) started landing on
/// Cloudflare R2, that static-file route could never find them, producing a 404 that looked (in the browser)
/// identical to a null thumbnail URL. This controller closes that gap by using the same
/// <c>IStorageProviderFactory</c> selection the upload path already uses (see
/// <c>docs/AI19_MEDIA3_PROVIDER_AWARE_MEDIA_ARCHITECTURE.md</c> for the full design).
/// </para>
/// <para>
/// <b>Existing URLs are unchanged.</b> <c>AttendanceSessionMediaPaths.BuildMediaUrl</c> and
/// <c>StudentMediaPaths.BuildVariantPath</c> already emit <c>/media/{key}?v=...</c> — this controller's
/// catch-all route (<c>{**key}</c>) matches exactly the same paths the old static-file middleware matched.
/// No DTO, React, or URL-generation change is required or was made.
/// </para>
/// <para>
/// <b>Coexists with the existing static-file middleware.</b> <c>UseStaticFiles("/media")</c> in
/// <c>Program.cs</c> is registered before <c>MapControllers()</c> and is left untouched by this milestone
/// (see AI19.MEDIA.3.4 for the separate retirement review): if it finds a file on local disk it serves it
/// directly and this controller is never reached; if it does not, the request falls through to this
/// controller, which reads through the active provider (local or R2) instead.
/// </para>
/// </remarks>
[ApiController]
[AllowAnonymous]
public sealed class MediaController : ControllerBase
{
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();
    private const string FallbackContentType = "application/octet-stream";

    private readonly IMediaObjectReader _mediaReader;
    private readonly IStorageProviderFactory _providerFactory;
    private readonly ILogger<MediaController> _logger;

    public MediaController(
        IMediaObjectReader mediaReader,
        IStorageProviderFactory providerFactory,
        ILogger<MediaController> logger)
    {
        _mediaReader = mediaReader;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Serves any object previously written through <see cref="IMediaStorageService"/>, regardless of
    /// which storage provider is currently active. <paramref name="key"/> is the catch-all remainder of
    /// the URL path after <c>/media/</c> (e.g. <c>recognitions/1/{sessionId}/faces/00003.webp</c>).
    /// </summary>
    [HttpGet("/media/{**key}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMedia(string key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return BadRequest();
        }

        var executionTraceId = HttpContext.TraceIdentifier;
        var providerName = _providerFactory.GetActiveProviderName();
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Media Request Started. StorageKey={StorageKey} Provider={Provider} ExecutionTraceId={ExecutionTraceId}",
            key,
            providerName,
            executionTraceId);

        Stream stream;
        try
        {
            stream = await _mediaReader.OpenReadAsync(key, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "Media Request Failed. StorageKey={StorageKey} Provider={Provider} ExecutionTraceId={ExecutionTraceId} DurationMs={DurationMs} Reason=NotFound",
                key,
                providerName,
                executionTraceId,
                stopwatch.ElapsedMilliseconds);
            return NotFound();
        }

        // Any exception other than FileNotFoundException (storage misconfiguration, R2 auth/network
        // failure, cancellation, etc.) is intentionally left uncaught here — it propagates to the
        // existing global exception handler (Program.cs: app.UseExceptionHandler()), unchanged.
        var contentType = ResolveContentType(key);
        stopwatch.Stop();
        _logger.LogInformation(
            "Media Request Completed. StorageKey={StorageKey} Provider={Provider} ExecutionTraceId={ExecutionTraceId} DurationMs={DurationMs} ContentType={ContentType}",
            key,
            providerName,
            executionTraceId,
            stopwatch.ElapsedMilliseconds,
            contentType);

        Response.Headers.Append("Cache-Control", "public,max-age=86400");
        Response.Headers.Append("Access-Control-Allow-Origin", "*");

        return File(stream, contentType, enableRangeProcessing: true);
    }

    private static string ResolveContentType(string key) =>
        ContentTypeProvider.TryGetContentType(key, out var contentType)
            ? contentType
            : FallbackContentType;
}
