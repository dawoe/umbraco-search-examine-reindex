// <copyright file="ReindexOperationStatus.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Umbraco.Community.Search.Examine.Reindex.Models;

/// <summary>
/// The outcome of a request to start a reindex.
/// </summary>
public enum ReindexOperationStatus
{
    /// <summary>
    /// The reindex was started.
    /// </summary>
    Success,

    /// <summary>
    /// No content index is registered for the given alias.
    /// </summary>
    IndexNotFound,

    /// <summary>
    /// A reindex is already running for the given alias.
    /// </summary>
    AlreadyRunning,
}
