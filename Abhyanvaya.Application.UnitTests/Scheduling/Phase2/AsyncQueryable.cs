using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase2;

internal static class AsyncQueryable
{
    public static IQueryable<T> AsAsyncQueryable<T>(this IEnumerable<T> source) => new TestAsyncEnumerable<T>(source);
}

internal sealed class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    private readonly IEnumerable<T> _source;

    public TestAsyncEnumerable(IEnumerable<T> source) : base(source)
    {
        _source = source;
    }

    public TestAsyncEnumerable(Expression expression) : base(expression)
    {
        _source = Array.Empty<T>();
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(_source);
}

internal sealed class TestAsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
{
    public T Current => inner.Current;
    public ValueTask DisposeAsync() { inner.Dispose(); return ValueTask.CompletedTask; }
    public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(inner.MoveNext());
}

internal sealed class TestAsyncQueryProvider<TEntity>(IEnumerable<TEntity> source) : IAsyncQueryProvider
{
    private readonly IQueryProvider _syncProvider = new EnumerableQuery<TEntity>(source).AsQueryable().Provider;

    public IQueryable CreateQuery(Expression expression) => new TestAsyncEnumerable<object>(expression);
    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new TestAsyncEnumerable<TElement>(expression);

    public object? Execute(Expression expression) => _syncProvider.Execute(expression);

    public TResult Execute<TResult>(Expression expression) => _syncProvider.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        var elementType = typeof(TResult).GetGenericArguments()[0];
        var result = typeof(IQueryProvider)
            .GetMethods()
            .Single(m => m.Name == nameof(IQueryProvider.Execute) && m.IsGenericMethod)
            .MakeGenericMethod(elementType)
            .Invoke(_syncProvider, [expression]);

        return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(elementType)
            .Invoke(null, [result])!;
    }
}
