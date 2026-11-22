using GamQuickstart.Paging;

namespace GamQuickstart.Tests;

public class PagerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
        {
            list.Add(item);
        }

        return list;
    }

    /// <summary>A fake data source with <paramref name="total"/> numbered items that records each call.</summary>
    private static (Func<int, int, CancellationToken, Task<Page<int>>> Fetch, List<(int Offset, int Limit)> Calls) Source(int total)
    {
        var calls = new List<(int Offset, int Limit)>();
        Task<Page<int>> Fetch(int offset, int limit, CancellationToken _)
        {
            calls.Add((offset, limit));
            var items = Enumerable.Range(offset, Math.Max(0, Math.Min(limit, total - offset))).ToList();
            return Task.FromResult(new Page<int>(items, total));
        }

        return (Fetch, calls);
    }

    [Fact]
    public async Task Reads_all_pages_and_moves_the_offset_by_the_page_size()
    {
        var (fetch, calls) = Source(total: 5);

        var items = await ToListAsync(Pager.ReadAllAsync(fetch, pageSize: 2, cancellationToken: Ct));

        Assert.Equal([0, 1, 2, 3, 4], items);
        Assert.Equal([(0, 2), (2, 2), (4, 2)], calls);
    }

    [Fact]
    public async Task Does_not_make_an_extra_call_when_the_total_is_an_exact_multiple_of_the_page_size()
    {
        var (fetch, calls) = Source(total: 4);

        var items = await ToListAsync(Pager.ReadAllAsync(fetch, pageSize: 2, cancellationToken: Ct));

        Assert.Equal(4, items.Count);
        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public async Task Stops_at_max_items_and_asks_only_for_what_is_still_needed()
    {
        var (fetch, calls) = Source(total: 10);

        var items = await ToListAsync(Pager.ReadAllAsync(fetch, pageSize: 4, maxItems: 6, cancellationToken: Ct));

        Assert.Equal([0, 1, 2, 3, 4, 5], items);
        Assert.Equal([(0, 4), (4, 2)], calls);
    }

    [Fact]
    public async Task Max_items_larger_than_the_total_returns_everything()
    {
        var (fetch, _) = Source(total: 3);

        var items = await ToListAsync(Pager.ReadAllAsync(fetch, pageSize: 500, maxItems: 100, cancellationToken: Ct));

        Assert.Equal(3, items.Count);
    }

    [Fact]
    public async Task Empty_result_makes_one_call_and_returns_nothing()
    {
        var (fetch, calls) = Source(total: 0);

        var items = await ToListAsync(Pager.ReadAllAsync(fetch, pageSize: 500, cancellationToken: Ct));

        Assert.Empty(items);
        Assert.Single(calls);
    }

    [Fact]
    public async Task An_empty_page_ends_the_loop_even_if_the_total_says_there_is_more()
    {
        // A misbehaving source: claims 100 results but returns none.
        var calls = 0;
        Task<Page<int>> Fetch(int offset, int limit, CancellationToken _)
        {
            calls++;
            return Task.FromResult(new Page<int>([], 100));
        }

        var items = await ToListAsync(Pager.ReadAllAsync(Fetch, pageSize: 10, cancellationToken: Ct));

        Assert.Empty(items);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(Pager.MaxPageSize + 1)]
    public void Invalid_page_size_is_rejected_immediately(int pageSize)
    {
        var (fetch, _) = Source(total: 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => Pager.ReadAllAsync(fetch, pageSize, cancellationToken: Ct));
    }

    [Fact]
    public void Max_items_below_one_is_rejected_immediately()
    {
        var (fetch, _) = Source(total: 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => Pager.ReadAllAsync(fetch, pageSize: 10, maxItems: 0, cancellationToken: Ct));
    }

    [Fact]
    public async Task Cancellation_stops_the_loop_between_pages()
    {
        var (fetch, calls) = Source(total: 10);
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in Pager.ReadAllAsync(fetch, pageSize: 2, cancellationToken: cts.Token))
            {
                if (item == 1)
                {
                    await cts.CancelAsync();
                }
            }
        });

        Assert.Single(calls);
    }
}
