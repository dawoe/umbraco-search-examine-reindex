// <copyright file="ReindexState.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Text.Json.Serialization;

namespace Umbraco.Community.Search.Examine.Reindex.Models;

/// <summary>
/// The state of a reindex operation for an index.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReindexState
{
    /// <summary>
    /// No reindex is running.
    /// </summary>
    Idle,

    /// <summary>
    /// A reindex is running.
    /// </summary>
    Running,

    /// <summary>
    /// The last reindex failed.
    /// </summary>
    Failed,
}
