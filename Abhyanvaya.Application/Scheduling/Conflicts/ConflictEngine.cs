namespace Abhyanvaya.Application.Scheduling.Conflicts;

/// <summary>
/// Executes registered <see cref="IConflictRule"/> plugins. Detects only — never mutates timetables.
/// </summary>
public sealed class ConflictEngine
{
    private readonly IReadOnlyList<IConflictRule> _rules;

    public ConflictEngine(IEnumerable<IConflictRule> rules)
    {
        _rules = rules.ToList();
    }

    public IReadOnlyList<IConflictRule> RegisteredRules => _rules;

    public async Task<ConflictResultBag> ExecuteAsync(ConflictAnalysisContext context, CancellationToken cancellationToken = default)
    {
        var bag = new ConflictResultBag();
        foreach (var rule in _rules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await rule.AnalyzeAsync(context, bag, cancellationToken);
        }
        return bag;
    }
}
