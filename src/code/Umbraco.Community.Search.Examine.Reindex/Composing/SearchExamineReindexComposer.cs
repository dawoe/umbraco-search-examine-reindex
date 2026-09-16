// <copyright file="SearchExamineReindexComposer.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.Search.Examine.Reindex.DependencyInjection;

namespace Umbraco.Community.Search.Examine.Reindex.Composing;

/// <summary>
/// Registers the reindex package when the assembly is present.
/// </summary>
public sealed class SearchExamineReindexComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder)
        => builder.AddSearchExamineReindex();
}
