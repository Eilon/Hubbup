using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProjectKIssueList.Utils
{
    public static class AsyncParallelUtils
    {
        // Borrowed from http://blogs.msdn.com/b/pfxteam/archive/2012/03/05/10278165.aspx
        public static Task ForEachAsync<TSource>(IEnumerable<TSource> source, int maxDegreeOfParallelism, Func<TSource, Task> body)
        {
            return Task.WhenAll(
                from partition in Partitioner.Create(source).GetPartitions(maxDegreeOfParallelism)
                select Task.Run(async () =>
                {
                    using (partition)
                        while (partition.MoveNext())
                            await body(partition.Current);
                }));
        }
    }
}

