// <copyright file="ReindexStatusTracker.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Collections.Concurrent;
using Umbraco.Community.Search.Examine.Reindex.Models;

namespace Umbraco.Community.Search.Examine.Reindex.Services;

/// <inheritdoc />
internal sealed class ReindexStatusTracker : IReindexStatusTracker
{
    private readonly ConcurrentDictionary<string, ReindexStatus> statuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock startLock = new();

    /// <inheritdoc />
    public ReindexStatus Get(string indexAlias)
        => this.statuses.TryGetValue(indexAlias, out ReindexStatus? status) ? status : ReindexStatus.Idle(indexAlias);

    /// <inheritdoc />
    public bool TryStart(ReindexStatus status)
    {
        lock (this.startLock)
        {
            if (this.Get(status.IndexAlias).State == ReindexState.Running)
            {
                return false;
            }

            this.statuses[status.IndexAlias] = status;
            return true;
        }
    }

    /// <inheritdoc />
    public void Update(ReindexStatus status)
        => this.statuses[status.IndexAlias] = status;
}
