// <copyright file="IndexContentReindexerTests.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.HostedServices;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Search.Core.Configuration;
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Cms.Search.Core.Services.ContentIndexing;
using Umbraco.Cms.Search.Provider.Examine.Services;
using Umbraco.Community.Search.Examine.Reindex.Models;
using Umbraco.Community.Search.Examine.Reindex.Services;

namespace Umbraco.Community.Search.Examine.Reindex.Tests.Services;

/// <summary>
/// Tests for <see cref="IndexContentReindexer"/>.
/// </summary>
[TestFixture]
public class IndexContentReindexerTests
{
    private const string PublishedAlias = "Umb_PublishedContent";
    private const string DraftAlias = "Umb_Content";
    private const string CustomAlias = "Custom";

    private Mock<IIndexContentEnumerator> enumerator = null!;
    private Mock<IDistributedContentIndexRefresher> refresher = null!;
    private Mock<IIndexDocumentService> indexDocumentService = null!;
    private Mock<IDistributedContentIndexRebuilder> rebuilder = null!;
    private Mock<IBackgroundTaskQueue> backgroundTaskQueue = null!;
    private ReindexStatusTracker tracker = null!;
    private CancellationToken queueToken;

    /// <summary>
    /// Creates fresh mocks. The background queue runs queued work inline.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.enumerator = new Mock<IIndexContentEnumerator>();
        this.enumerator.Setup(e => e.Enumerate(It.IsAny<UmbracoObjectTypes>())).Returns([]);
        this.refresher = new Mock<IDistributedContentIndexRefresher>();
        this.indexDocumentService = new Mock<IIndexDocumentService>();
        this.rebuilder = new Mock<IDistributedContentIndexRebuilder>();
        this.rebuilder.Setup(r => r.Rebuild(It.IsAny<string>())).Returns(true);
        this.tracker = new ReindexStatusTracker();
        this.queueToken = CancellationToken.None;
        this.backgroundTaskQueue = new Mock<IBackgroundTaskQueue>();
        this.backgroundTaskQueue
            .Setup(q => q.QueueBackgroundWorkItem(It.IsAny<Func<CancellationToken, Task>>()))
            .Callback<Func<CancellationToken, Task>>(work => work(this.queueToken).GetAwaiter().GetResult());
    }

    /// <summary>
    /// Unknown aliases are rejected.
    /// </summary>
    [Test]
    public void Start_UnknownAlias_ReturnsIndexNotFound()
    {
        IndexContentReindexer sut = this.CreateSut();

        Attempt<ReindexStatus, ReindexOperationStatus> attempt = sut.Start("Nope", false);

        Assert.Multiple(() =>
        {
            Assert.That(attempt.Success, Is.False);
            Assert.That(attempt.Status, Is.EqualTo(ReindexOperationStatus.IndexNotFound));
        });
        this.backgroundTaskQueue.Verify(q => q.QueueBackgroundWorkItem(It.IsAny<Func<CancellationToken, Task>>()), Times.Never);
    }

    /// <summary>
    /// A second start while running is rejected with the current status.
    /// </summary>
    [Test]
    public void Start_WhenAlreadyRunning_ReturnsAlreadyRunning()
    {
        this.tracker.TryStart(ReindexStatus.Idle(PublishedAlias) with { State = ReindexState.Running, ProcessedItems = 7 });
        IndexContentReindexer sut = this.CreateSut();

        Attempt<ReindexStatus, ReindexOperationStatus> attempt = sut.Start(PublishedAlias, false);

        Assert.Multiple(() =>
        {
            Assert.That(attempt.Success, Is.False);
            Assert.That(attempt.Status, Is.EqualTo(ReindexOperationStatus.AlreadyRunning));
            Assert.That(attempt.Result?.ProcessedItems, Is.EqualTo(7));
        });
    }

    /// <summary>
    /// Published index: only published documents are refreshed with the published state.
    /// </summary>
    [Test]
    public void Start_PublishedIndex_RefreshesPublishedDocumentsOnly()
    {
        IContent published = Mock.Of<IContent>(c => c.Published == true && c.Key == Guid.NewGuid());
        IContent draft = Mock.Of<IContent>(c => c.Published == false && c.Key == Guid.NewGuid());
        this.SetupBatches(UmbracoObjectTypes.Document, new ContentBatch([published, draft], 2));
        IndexContentReindexer sut = this.CreateSut();

        Attempt<ReindexStatus, ReindexOperationStatus> attempt = sut.Start(PublishedAlias, false);

        Assert.That(attempt.Success, Is.True);
        this.refresher.Verify(
            r => r.RefreshContent(
                It.Is<IEnumerable<IContent>>(items => items.SequenceEqual(new[] { published })),
                ContentState.Published),
            Times.Once);
        this.refresher.Verify(r => r.RefreshContent(It.IsAny<IEnumerable<IContent>>(), ContentState.Draft), Times.Never);
        this.indexDocumentService.Verify(s => s.DeleteAsync(It.IsAny<Guid[]>(), It.IsAny<bool>()), Times.Never);
        this.rebuilder.Verify(r => r.Rebuild(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// Draft index: documents, media and members are refreshed with the draft state.
    /// </summary>
    [Test]
    public void Start_DraftIndex_RefreshesAllObjectTypes()
    {
        IContent document = Mock.Of<IContent>(c => c.Published == false && c.Key == Guid.NewGuid());
        IMedia media = Mock.Of<IMedia>(m => m.Key == Guid.NewGuid());
        IMember member = Mock.Of<IMember>(m => m.Key == Guid.NewGuid());
        this.SetupBatches(UmbracoObjectTypes.Document, new ContentBatch([document], 1));
        this.SetupBatches(UmbracoObjectTypes.Media, new ContentBatch([media], 1));
        this.SetupBatches(UmbracoObjectTypes.Member, new ContentBatch([member], 1));
        IndexContentReindexer sut = this.CreateSut();

        sut.Start(DraftAlias, false);

        this.refresher.Verify(r => r.RefreshContent(It.Is<IEnumerable<IContent>>(items => items.Single() == document), ContentState.Draft), Times.Once);
        this.refresher.Verify(r => r.RefreshMedia(It.Is<IEnumerable<IMedia>>(items => items.Single() == media)), Times.Once);
        this.refresher.Verify(r => r.RefreshMember(It.Is<IEnumerable<IMember>>(items => items.Single() == member)), Times.Once);
        Assert.That(this.tracker.Get(DraftAlias).ProcessedItems, Is.EqualTo(3));
    }

    /// <summary>
    /// A custom strategy refreshes documents in both states.
    /// </summary>
    [Test]
    public void Start_CustomStrategy_RefreshesBothStates()
    {
        IContent published = Mock.Of<IContent>(c => c.Published == true && c.Key == Guid.NewGuid());
        this.SetupBatches(UmbracoObjectTypes.Document, new ContentBatch([published], 1));
        IndexContentReindexer sut = this.CreateSut();

        sut.Start(CustomAlias, false);

        this.refresher.Verify(r => r.RefreshContent(It.IsAny<IEnumerable<IContent>>(), ContentState.Draft), Times.Once);
        this.refresher.Verify(r => r.RefreshContent(It.IsAny<IEnumerable<IContent>>(), ContentState.Published), Times.Once);
    }

    /// <summary>
    /// Rebuild mode flushes the cache per batch and then triggers the rebuild.
    /// </summary>
    [Test]
    public void Start_RebuildMode_FlushesCacheThenRebuilds()
    {
        IContent document = Mock.Of<IContent>(c => c.Published == true && c.Key == Guid.NewGuid());
        this.SetupBatches(UmbracoObjectTypes.Document, new ContentBatch([document], 1));
        IndexContentReindexer sut = this.CreateSut();

        sut.Start(PublishedAlias, true);

        this.indexDocumentService.Verify(s => s.DeleteAsync(It.Is<Guid[]>(keys => keys.Single() == document.Key), true), Times.Once);
        this.indexDocumentService.Verify(s => s.DeleteAsync(It.IsAny<Guid[]>(), false), Times.Never);
        this.refresher.Verify(r => r.RefreshContent(It.IsAny<IEnumerable<IContent>>(), It.IsAny<ContentState>()), Times.Never);
        this.rebuilder.Verify(r => r.Rebuild(PublishedAlias), Times.Once);
        Assert.That(this.tracker.Get(PublishedAlias).State, Is.EqualTo(ReindexState.Idle));
    }

    /// <summary>
    /// Rebuild mode flushes media and member cache entries with the draft flag.
    /// </summary>
    [Test]
    public void Start_RebuildMode_DraftIndex_FlushesMediaAndMembersAsDraft()
    {
        IMedia media = Mock.Of<IMedia>(m => m.Key == Guid.NewGuid());
        IMember member = Mock.Of<IMember>(m => m.Key == Guid.NewGuid());
        this.SetupBatches(UmbracoObjectTypes.Media, new ContentBatch([media], 1));
        this.SetupBatches(UmbracoObjectTypes.Member, new ContentBatch([member], 1));
        IndexContentReindexer sut = this.CreateSut();

        sut.Start(DraftAlias, true);

        this.indexDocumentService.Verify(s => s.DeleteAsync(It.Is<Guid[]>(keys => keys.Single() == media.Key), false), Times.Once);
        this.indexDocumentService.Verify(s => s.DeleteAsync(It.Is<Guid[]>(keys => keys.Single() == member.Key), false), Times.Once);
        this.indexDocumentService.Verify(s => s.DeleteAsync(It.IsAny<Guid[]>(), true), Times.Never);
    }

    /// <summary>
    /// A false result from the rebuilder is recorded as a failure.
    /// </summary>
    [Test]
    public void Start_RebuildReturnsFalse_RecordsFailure()
    {
        this.rebuilder.Setup(r => r.Rebuild(PublishedAlias)).Returns(false);
        IndexContentReindexer sut = this.CreateSut();

        sut.Start(PublishedAlias, true);

        ReindexStatus status = this.tracker.Get(PublishedAlias);
        Assert.Multiple(() =>
        {
            Assert.That(status.State, Is.EqualTo(ReindexState.Failed));
            Assert.That(status.ErrorMessage, Is.Not.Null.And.Not.Empty);
        });
    }

    /// <summary>
    /// Exceptions during enumeration are recorded as a failure.
    /// </summary>
    [Test]
    public void Start_EnumerationThrows_RecordsFailure()
    {
        this.enumerator.Setup(e => e.Enumerate(UmbracoObjectTypes.Document)).Throws(new InvalidOperationException("boom"));
        IndexContentReindexer sut = this.CreateSut();

        sut.Start(PublishedAlias, false);

        ReindexStatus status = this.tracker.Get(PublishedAlias);
        Assert.Multiple(() =>
        {
            Assert.That(status.State, Is.EqualTo(ReindexState.Failed));
            Assert.That(status.ErrorMessage, Is.EqualTo("boom"));
            Assert.That(status.CompletedAt, Is.Not.Null);
        });
    }

    /// <summary>
    /// Cancellation stops the job and records a failure.
    /// </summary>
    [Test]
    public void Start_Cancelled_RecordsFailureWithoutRefreshing()
    {
        IContent document = Mock.Of<IContent>(c => c.Published == true && c.Key == Guid.NewGuid());
        this.SetupBatches(UmbracoObjectTypes.Document, new ContentBatch([document], 1));
        this.queueToken = new CancellationToken(canceled: true);
        IndexContentReindexer sut = this.CreateSut();

        sut.Start(PublishedAlias, false);

        Assert.That(this.tracker.Get(PublishedAlias).State, Is.EqualTo(ReindexState.Failed));
        this.refresher.Verify(r => r.RefreshContent(It.IsAny<IEnumerable<IContent>>(), It.IsAny<ContentState>()), Times.Never);
    }

    /// <summary>
    /// Progress totals are the sum of the totals reported per object type.
    /// </summary>
    [Test]
    public void Start_TracksProcessedAndTotalItems()
    {
        IContent document = Mock.Of<IContent>(c => c.Published == false && c.Key == Guid.NewGuid());
        IMedia media = Mock.Of<IMedia>(m => m.Key == Guid.NewGuid());
        this.SetupBatches(UmbracoObjectTypes.Document, new ContentBatch([document], 10));
        this.SetupBatches(UmbracoObjectTypes.Media, new ContentBatch([media], 5));
        IndexContentReindexer sut = this.CreateSut();

        sut.Start(DraftAlias, false);

        ReindexStatus status = this.tracker.Get(DraftAlias);
        Assert.Multiple(() =>
        {
            Assert.That(status.State, Is.EqualTo(ReindexState.Idle));
            Assert.That(status.ProcessedItems, Is.EqualTo(2));
            Assert.That(status.TotalItems, Is.EqualTo(15));
            Assert.That(status.StartedAt, Is.Not.Null);
            Assert.That(status.CompletedAt, Is.Not.Null);
        });
    }

    private void SetupBatches(UmbracoObjectTypes objectType, params ContentBatch[] batches)
        => this.enumerator.Setup(e => e.Enumerate(objectType)).Returns(batches);

    private IndexContentReindexer CreateSut()
    {
        var options = new IndexOptions();
        options.RegisterContentIndex<Indexer, Searcher, IPublishedContentChangeStrategy>(PublishedAlias, UmbracoObjectTypes.Document);
        options.RegisterContentIndex<Indexer, Searcher, IDraftContentChangeStrategy>(DraftAlias, UmbracoObjectTypes.Document, UmbracoObjectTypes.Media, UmbracoObjectTypes.Member);
        options.RegisterContentIndex<Indexer, Searcher, IContentChangeStrategy>(CustomAlias, UmbracoObjectTypes.Document);

        return new IndexContentReindexer(
            Options.Create(options),
            this.enumerator.Object,
            this.refresher.Object,
            this.indexDocumentService.Object,
            this.rebuilder.Object,
            this.tracker,
            this.backgroundTaskQueue.Object,
            TimeProvider.System,
            NullLogger<IndexContentReindexer>.Instance);
    }
}
