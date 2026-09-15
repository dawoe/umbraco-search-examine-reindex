// <copyright file="ReindexRequestModel.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Umbraco.Community.Search.Examine.Reindex.Models;

/// <summary>
/// Request body for starting a reindex.
/// </summary>
public sealed class ReindexRequestModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the index should be rebuilt after flushing the cache.
    /// </summary>
    public bool RebuildIndex { get; set; }
}
