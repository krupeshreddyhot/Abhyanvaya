namespace Abhyanvaya.Domain.Exceptions;

/// <summary>
/// Raised when a requested resource does not exist.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
    }
}
