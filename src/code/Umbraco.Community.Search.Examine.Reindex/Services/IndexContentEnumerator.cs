// <copyright file="IndexContentEnumerator.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Community.Search.Examine.Reindex.Services;

/// <inheritdoc />
internal sealed class IndexContentEnumerator : IIndexContentEnumerator
{
    /// <summary>
    /// The number of items per page.
    /// </summary>
    internal const int PageSize = 500;

    private readonly IContentService contentService;
    private readonly IMediaService mediaService;
    private readonly IMemberService memberService;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexContentEnumerator"/> class.
    /// </summary>
    /// <param name="contentService">The content service.</param>
    /// <param name="mediaService">The media service.</param>
    /// <param name="memberService">The member service.</param>
    public IndexContentEnumerator(IContentService contentService, IMediaService mediaService, IMemberService memberService)
    {
        this.contentService = contentService;
        this.mediaService = mediaService;
        this.memberService = memberService;
    }

    private delegate IEnumerable<IContentBase> GetPage(long pageIndex, out long total);

    /// <inheritdoc />
    public IEnumerable<ContentBatch> Enumerate(UmbracoObjectTypes objectType)
        => objectType switch
        {
            UmbracoObjectTypes.Document => Page((long pageIndex, out long total) => this.contentService.GetPagedDescendants(Cms.Core.Constants.System.Root, pageIndex, PageSize, out total)),
            UmbracoObjectTypes.Media => Page((long pageIndex, out long total) => this.mediaService.GetPagedDescendants(Cms.Core.Constants.System.Root, pageIndex, PageSize, out total)),
            UmbracoObjectTypes.Member => Page((long pageIndex, out long total) => this.memberService.GetAll(pageIndex, PageSize, out total, "LoginName", Direction.Ascending, true, null, string.Empty)),
            _ => [],
        };

    private static IEnumerable<ContentBatch> Page(GetPage getPage)
    {
        long pageIndex = 0;
        long total;
        do
        {
            IContentBase[] items = getPage(pageIndex, out total).ToArray();
            if (items.Length == 0)
            {
                yield break;
            }

            yield return new ContentBatch(items, total);
            pageIndex++;
        }
        while (pageIndex * PageSize < total);
    }
}
