// <copyright file="ContentBatch.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Umbraco.Cms.Core.Models;

namespace Umbraco.Community.Search.Examine.Reindex.Services;

/// <summary>
/// One page of content items of a single object type.
/// </summary>
/// <param name="Items">The items in this page.</param>
/// <param name="Total">The total number of items of this object type.</param>
internal sealed record ContentBatch(IContentBase[] Items, long Total);
