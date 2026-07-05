namespace Abhyanvaya.Domain.Exceptions;

/// <summary>
/// Thrown when a domain invariant or state transition rule is violated.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
