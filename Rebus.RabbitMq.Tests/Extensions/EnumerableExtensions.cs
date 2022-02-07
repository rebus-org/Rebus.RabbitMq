using System.Collections.Generic;
using System.Linq;
// ReSharper disable ArgumentsStyleNamedExpression

namespace Rebus.RabbitMq.Tests.Extensions;

static class EnumerableExtensions
{
    public static IEnumerable<IReadOnlyList<T>> Batch<T>(this IEnumerable<T> items, int batchSize)
    {
        List<T> CreateNewList() => new(capacity: batchSize);

        var list = CreateNewList();

        foreach (var item in items)
        {
            list.Add(item);
                
            if (list.Count < batchSize) continue;
                
            yield return list;
            list = CreateNewList();
        }

        if (!list.Any()) yield break;

        yield return list;
    }
}