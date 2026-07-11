using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.Infrastructure.Services
{
    /// <summary>
    /// Scoped, per-job recognition execution context (AI15.DIAGNOSTICS.2A). Mirrors
    /// <see cref="TenantContextAccessor"/> exactly: state lives only in instance fields guarded by a
    /// lock (no static state, no <c>AsyncLocal</c>, no <c>ThreadStatic</c>), registered with a Scoped
    /// lifetime so each classroom recognition job — one DI scope per dequeued message — owns its own
    /// binding.
    /// </summary>
    public sealed class RecognitionExecutionContext : IRecognitionExecutionContext
    {
        private readonly object _gate = new();
        private bool _initialized;
        private Guid _executionTraceId;
        private Guid _attendanceSessionId;
        private int _tenantId;
        private DateTime _queueStartUtc;
        private DateTime _pipelineStartUtc = DateTime.MinValue;
        private int _recognitionAttempt;

        public Guid ExecutionTraceId
        {
            get { lock (_gate) { return _executionTraceId; } }
        }

        public Guid AttendanceSessionId
        {
            get { lock (_gate) { return _attendanceSessionId; } }
        }

        public int TenantId
        {
            get { lock (_gate) { return _tenantId; } }
        }

        public DateTime QueueStartUtc
        {
            get { lock (_gate) { return _queueStartUtc; } }
        }

        public DateTime PipelineStartUtc
        {
            get { lock (_gate) { return _pipelineStartUtc; } }
        }

        public int RecognitionAttempt
        {
            get { lock (_gate) { return _recognitionAttempt; } }
        }

        public void Initialize(Guid sessionId, int tenantId, int attempt, DateTime queueUtc)
        {
            lock (_gate)
            {
                _executionTraceId = Guid.NewGuid();
                _attendanceSessionId = sessionId;
                _tenantId = tenantId;
                _recognitionAttempt = attempt;
                _queueStartUtc = queueUtc;
                _pipelineStartUtc = DateTime.MinValue;
                _initialized = true;
            }
        }

        public void MarkPipelineStarted()
        {
            lock (_gate)
            {
                if (!_initialized)
                {
                    // Diagnostics-only guard: a missing Initialize() call must never throw into the
                    // recognition pipeline. Falling back to "now" keeps Elapsed Since Pipeline Start
                    // computable (as ~0) instead of producing a nonsensical negative/huge value.
                    _pipelineStartUtc = DateTime.UtcNow;
                    return;
                }

                _pipelineStartUtc = DateTime.UtcNow;
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _initialized = false;
                _executionTraceId = Guid.Empty;
                _attendanceSessionId = Guid.Empty;
                _tenantId = 0;
                _recognitionAttempt = 0;
                _queueStartUtc = DateTime.MinValue;
                _pipelineStartUtc = DateTime.MinValue;
            }
        }
    }
}
