namespace Abhyanvaya.Domain.Enums;

/// <summary>
/// When set, attendance lists only students whose first/second language matches <see cref="Subject.TeachingLanguageId"/>.
/// </summary>
public enum SubjectLanguageSlot
{
    None = 0,
    FirstLanguage = 1,
    SecondLanguage = 2,
}
