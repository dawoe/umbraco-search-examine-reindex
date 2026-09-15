// <copyright file="IIndexContentEnumerator.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Umbraco.Cms.Core.Models;

namespace Umbraco.Community.Search.Examine.Reindex.Services;

/// <summary>
/// Enumerates all content items of an object type in pages.
/// </summary>
internal interface IIndexContentEnumerator
{
    /// <summary>
    /// Enumerates all items of the given object type. Only documents, media and members are supported;
    /// other object types yield nothing.
    /// </summary>
    /// <param name="objectType">The object type.</param>
    /// <returns>Pages of items.</returns>
    IEnumerable<ContentBatch> Enumerate(UmbracoObjectTypes objectType);
}
