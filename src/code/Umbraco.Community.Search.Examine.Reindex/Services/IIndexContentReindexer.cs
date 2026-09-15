// <copyright file="IIndexContentReindexer.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Umbraco.Cms.Core;
using Umbraco.Community.Search.Examine.Reindex.Models;

namespace Umbraco.Community.Search.Examine.Reindex.Services;

/// <summary>
/// Starts reindex operations for content indexes.
/// </summary>
public interface IIndexContentReindexer
{
    /// <summary>
    /// Starts a reindex of all content contained in the index. The work runs in the background.
    /// </summary>
    /// <param name="indexAlias">The index alias.</param>
    /// <param name="rebuildIndex">
    /// When <c>true</c>, the cached index values are flushed and the index is rebuilt afterwards.
    /// When <c>false</c>, every item is refreshed in place.
    /// </param>
    /// <returns>
    /// A successful attempt with the new status, or a failed attempt with
    /// <see cref="ReindexOperationStatus.IndexNotFound"/> or <see cref="ReindexOperationStatus.AlreadyRunning"/>
    /// (the result then carries the current status).
    /// </returns>
    Attempt<ReindexStatus, ReindexOperationStatus> Start(string indexAlias, bool rebuildIndex);
}
