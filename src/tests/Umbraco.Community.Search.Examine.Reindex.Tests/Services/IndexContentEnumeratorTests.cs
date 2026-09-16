// <copyright file="IndexContentEnumeratorTests.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.Search.Examine.Reindex.Services;

namespace Umbraco.Community.Search.Examine.Reindex.Tests.Services;

/// <summary>
/// Tests for <see cref="IndexContentEnumerator"/>.
/// </summary>
[TestFixture]
public class IndexContentEnumeratorTests
{
    private delegate IEnumerable<IContent> GetPagedContent(int id, long pageIndex, int pageSize, out long total, IQuery<IContent>? filter, Ordering? ordering);

    private delegate IEnumerable<IMedia> GetPagedMedia(int id, long pageIndex, int pageSize, out long total, IQuery<IMedia>? filter, Ordering? ordering);

    private delegate IEnumerable<IMember> GetPagedMembers(long pageIndex, int pageSize, out long total, string orderBy, Direction direction, bool orderBySystemField, string? memberTypeAlias, string filter);

    /// <summary>
    /// Documents are enumerated from the root in pages until the total is reached.
    /// </summary>
    [Test]
    public void Enumerate_Documents_PagesUntilTotal()
    {
        var page0 = Enumerable.Range(0, IndexContentEnumerator.PageSize).Select(_ => Mock.Of<IContent>()).ToArray();
        var page1 = new[] { Mock.Of<IContent>(), Mock.Of<IContent>() };
        var total = page0.Length + page1.Length;
        var contentService = new Mock<IContentService>();
        contentService
            .Setup(s => s.GetPagedDescendants(-1, It.IsAny<long>(), IndexContentEnumerator.PageSize, out It.Ref<long>.IsAny, null, null))
            .Returns(new GetPagedContent((int _, long pageIndex, int _, out long t, IQuery<IContent>? _, Ordering? _) =>
            {
                t = total;
                return pageIndex == 0 ? page0 : page1;
            }));
        IndexContentEnumerator sut = CreateSut(contentService: contentService);

        ContentBatch[] batches = sut.Enumerate(UmbracoObjectTypes.Document).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(batches, Has.Length.EqualTo(2));
            Assert.That(batches[0].Items, Has.Length.EqualTo(IndexContentEnumerator.PageSize));
            Assert.That(batches[0].Total, Is.EqualTo(total));
            Assert.That(batches[1].Items, Has.Length.EqualTo(2));
        });
    }

    /// <summary>
    /// An empty result yields no batches.
    /// </summary>
    [Test]
    public void Enumerate_Documents_Empty_YieldsNothing()
    {
        var contentService = new Mock<IContentService>();
        contentService
            .Setup(s => s.GetPagedDescendants(-1, It.IsAny<long>(), IndexContentEnumerator.PageSize, out It.Ref<long>.IsAny, null, null))
            .Returns(new GetPagedContent((int _, long _, int _, out long t, IQuery<IContent>? _, Ordering? _) =>
            {
                t = 0;
                return [];
            }));
        IndexContentEnumerator sut = CreateSut(contentService: contentService);

        Assert.That(sut.Enumerate(UmbracoObjectTypes.Document), Is.Empty);
    }

    /// <summary>
    /// Media is enumerated from the media root.
    /// </summary>
    [Test]
    public void Enumerate_Media_UsesMediaService()
    {
        var items = new[] { Mock.Of<IMedia>() };
        var mediaService = new Mock<IMediaService>();
        mediaService
            .Setup(s => s.GetPagedDescendants(-1, It.IsAny<long>(), IndexContentEnumerator.PageSize, out It.Ref<long>.IsAny, null, null))
            .Returns(new GetPagedMedia((int _, long _, int _, out long t, IQuery<IMedia>? _, Ordering? _) =>
            {
                t = 1;
                return items;
            }));
        IndexContentEnumerator sut = CreateSut(mediaService: mediaService);

        ContentBatch[] batches = sut.Enumerate(UmbracoObjectTypes.Media).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(batches, Has.Length.EqualTo(1));
            Assert.That(batches[0].Items, Is.EqualTo(items));
        });
    }

    /// <summary>
    /// Members are enumerated with the paged GetAll overload.
    /// </summary>
    [Test]
    public void Enumerate_Members_UsesMemberService()
    {
        var items = new[] { Mock.Of<IMember>(), Mock.Of<IMember>() };
        var memberService = new Mock<IMemberService>();
        memberService
            .Setup(s => s.GetAll(It.IsAny<long>(), IndexContentEnumerator.PageSize, out It.Ref<long>.IsAny, It.IsAny<string>(), It.IsAny<Direction>(), It.IsAny<bool>(), null, It.IsAny<string>()))
            .Returns(new GetPagedMembers((long _, int _, out long t, string _, Direction _, bool _, string? _, string _) =>
            {
                t = 2;
                return items;
            }));
        IndexContentEnumerator sut = CreateSut(memberService: memberService);

        ContentBatch[] batches = sut.Enumerate(UmbracoObjectTypes.Member).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(batches, Has.Length.EqualTo(1));
            Assert.That(batches[0].Total, Is.EqualTo(2));
        });
    }

    /// <summary>
    /// Unsupported object types yield nothing.
    /// </summary>
    [Test]
    public void Enumerate_UnsupportedType_YieldsNothing()
    {
        IndexContentEnumerator sut = CreateSut();

        Assert.That(sut.Enumerate(UmbracoObjectTypes.DataType), Is.Empty);
    }

    private static IndexContentEnumerator CreateSut(
        Mock<IContentService>? contentService = null,
        Mock<IMediaService>? mediaService = null,
        Mock<IMemberService>? memberService = null)
        => new(
            (contentService ?? new Mock<IContentService>()).Object,
            (mediaService ?? new Mock<IMediaService>()).Object,
            (memberService ?? new Mock<IMemberService>()).Object);
}
