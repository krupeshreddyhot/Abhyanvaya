using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Application.Common.Interfaces
{
    public interface IJwtService
    {
        string GenerateToken(User user);
    }
}
