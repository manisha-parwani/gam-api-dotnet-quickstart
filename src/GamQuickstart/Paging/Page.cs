namespace GamQuickstart.Paging;

/// <summary>One page of results from a "get by statement" call.</summary>
/// <param name="Items">The results on this page.</param>
/// <param name="TotalResultSetSize">How many results match the query in total, across all pages.</param>
public sealed record Page<T>(IReadOnlyList<T> Items, int TotalResultSetSize);
