using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Application.Common.Interfaces
{
    public interface IJwtService
    {
        Task<string> GenerateTokenAsync(User user, CancellationToken cancellationToken = default);
    }
}
