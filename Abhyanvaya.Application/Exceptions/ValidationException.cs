namespace Abhyanvaya.Application.Exceptions;

/// <summary>
/// Raised when request or domain validation fails.
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException(string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>
        {
            [string.Empty] = [message]
        };
    }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = new Dictionary<string, string[]>(errors);
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
