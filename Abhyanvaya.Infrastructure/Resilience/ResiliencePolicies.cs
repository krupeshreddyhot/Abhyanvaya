using Polly;
using Polly.CircuitBreaker;
using Polly.Fallback;
using Polly.Retry;

namespace Abhyanvaya.Infrastructure.Resilience
{
    public static class ResiliencePolicies
    {
        public static AsyncPolicy WrapPolicy =>
            Policy.WrapAsync(
                TimeoutPolicy,
                RetryPolicy,
                CircuitBreakerPolicy
            );
        public static IAsyncPolicy TimeoutPolicy =>
            Policy.TimeoutAsync(TimeSpan.FromSeconds(3));
        public static AsyncRetryPolicy RetryPolicy =>
            Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(3, retry => TimeSpan.FromSeconds(2));

        public static AsyncCircuitBreakerPolicy CircuitBreakerPolicy =>
            Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(3, TimeSpan.FromSeconds(30));
    }
}
