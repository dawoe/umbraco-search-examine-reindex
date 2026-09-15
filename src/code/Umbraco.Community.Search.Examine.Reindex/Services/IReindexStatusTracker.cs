// <copyright file="IReindexStatusTracker.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Umbraco.Community.Search.Examine.Reindex.Models;

namespace Umbraco.Community.Search.Examine.Reindex.Services;

/// <summary>
/// Tracks the reindex status per index alias (in memory, per server instance).
/// </summary>
public interface IReindexStatusTracker
{
    /// <summary>
    /// Gets the status for an index alias. Unknown aliases report <see cref="ReindexState.Idle"/>.
    /// </summary>
    /// <param name="indexAlias">The index alias.</param>
    /// <returns>The status.</returns>
    ReindexStatus Get(string indexAlias);

    /// <summary>
    /// Stores the status unless a reindex is already running for the alias.
    /// </summary>
    /// <param name="status">The running status to store.</param>
    /// <returns><c>true</c> when stored, <c>false</c> when a reindex is already running.</returns>
    bool TryStart(ReindexStatus status);

    /// <summary>
    /// Overwrites the status for the alias.
    /// </summary>
    /// <param name="status">The status.</param>
    void Update(ReindexStatus status);
}
