// <copyright file="ReindexStatus.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Umbraco.Community.Search.Examine.Reindex.Models;

/// <summary>
/// The status of a reindex operation for a single index.
/// </summary>
/// <param name="IndexAlias">The index alias.</param>
/// <param name="State">The current state.</param>
/// <param name="RebuildIndex">Whether the operation also rebuilds the index.</param>
/// <param name="ProcessedItems">The number of items processed so far.</param>
/// <param name="TotalItems">The total number of items, when known.</param>
/// <param name="StartedAt">When the operation started.</param>
/// <param name="CompletedAt">When the operation completed.</param>
/// <param name="ErrorMessage">The error message when the operation failed.</param>
public sealed record ReindexStatus(
    string IndexAlias,
    ReindexState State,
    bool RebuildIndex,
    long ProcessedItems,
    long? TotalItems,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage)
{
    /// <summary>
    /// Creates an idle status for the given index alias.
    /// </summary>
    /// <param name="indexAlias">The index alias.</param>
    /// <returns>An idle status.</returns>
    public static ReindexStatus Idle(string indexAlias)
        => new(indexAlias, ReindexState.Idle, false, 0, null, null, null, null);
}
