using System.Collections.Generic;
using System.Linq;

namespace Rebus.Internals;

static class EnumerableExtensions
{
    public static IEnumerable<IReadOnlyList<TItem>> Batch<TItem>(this IEnumerable<TItem> items, int maxBatchSize)
    {
        List<TItem> CreateList() => new(capacity: maxBatchSize);

        var list = CreateList();

        foreach (var item in items)
        {
            list.Add(item);

            if (list.Count < maxBatchSize) continue;

            yield return list;
            
            list = CreateList();
        }

        if (list.Any())
        {
            yield return list;
        }
    }
}