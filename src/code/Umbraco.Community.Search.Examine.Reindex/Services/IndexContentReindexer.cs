// <copyright file="IndexContentReindexer.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.HostedServices;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Search.Core.Configuration;
using Umbraco.Cms.Search.Core.Models.Configuration;
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Cms.Search.Core.Services.ContentIndexing;
using Umbraco.Community.Search.Examine.Reindex.Models;

namespace Umbraco.Community.Search.Examine.Reindex.Services;

/// <inheritdoc />
internal sealed class IndexContentReindexer : IIndexContentReindexer
{
    private readonly IndexOptions indexOptions;
    private readonly IIndexContentEnumerator enumerator;
    private readonly IDistributedContentIndexRefresher refresher;
    private readonly IIndexDocumentService indexDocumentService;
    private readonly IDistributedContentIndexRebuilder rebuilder;
    private readonly IReindexStatusTracker statusTracker;
    private readonly IBackgroundTaskQueue backgroundTaskQueue;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<IndexContentReindexer> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexContentReindexer"/> class.
    /// </summary>
    /// <param name="indexOptions">The Umbraco Search index options.</param>
    /// <param name="enumerator">The content enumerator.</param>
    /// <param name="refresher">The distributed content index refresher.</param>
    /// <param name="indexDocumentService">The index document (cache) service.</param>
    /// <param name="rebuilder">The distributed index rebuilder.</param>
    /// <param name="statusTracker">The status tracker.</param>
    /// <param name="backgroundTaskQueue">The background task queue.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    public IndexContentReindexer(
        IOptions<IndexOptions> indexOptions,
        IIndexContentEnumerator enumerator,
        IDistributedContentIndexRefresher refresher,
        IIndexDocumentService indexDocumentService,
        IDistributedContentIndexRebuilder rebuilder,
        IReindexStatusTracker statusTracker,
        IBackgroundTaskQueue backgroundTaskQueue,
        TimeProvider timeProvider,
        ILogger<IndexContentReindexer> logger)
    {
        this.indexOptions = indexOptions.Value;
        this.enumerator = enumerator;
        this.refresher = refresher;
        this.indexDocumentService = indexDocumentService;
        this.rebuilder = rebuilder;
        this.statusTracker = statusTracker;
        this.backgroundTaskQueue = backgroundTaskQueue;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    /// <inheritdoc />
    public Attempt<ReindexStatus, ReindexOperationStatus> Start(string indexAlias, bool rebuildIndex)
    {
        ContentIndexRegistration? registration = this.indexOptions.GetContentIndexRegistration(indexAlias);
        if (registration is null)
        {
            return Attempt.FailWithStatus(ReindexOperationStatus.IndexNotFound, this.statusTracker.Get(indexAlias));
        }

        ReindexStatus status = ReindexStatus.Idle(indexAlias) with
        {
            State = ReindexState.Running,
            RebuildIndex = rebuildIndex,
            StartedAt = this.timeProvider.GetUtcNow(),
        };

        if (this.statusTracker.TryStart(status) is false)
        {
            return Attempt.FailWithStatus(ReindexOperationStatus.AlreadyRunning, this.statusTracker.Get(indexAlias));
        }

        this.backgroundTaskQueue.QueueBackgroundWorkItem(cancellationToken => this.RunAsync(registration, status, cancellationToken));
        return Attempt.SucceedWithStatus(ReindexOperationStatus.Success, status);
    }

    private static ContentState[] ResolveContentStates(Type strategyType)
    {
        if (typeof(IPublishedContentChangeStrategy).IsAssignableFrom(strategyType))
        {
            return [ContentState.Published];
        }

        if (typeof(IDraftContentChangeStrategy).IsAssignableFrom(strategyType))
        {
            return [ContentState.Draft];
        }

        return [ContentState.Draft, ContentState.Published];
    }

    private async Task RunAsync(ContentIndexRegistration registration, ReindexStatus status, CancellationToken cancellationToken)
    {
        var indexAlias = registration.IndexAlias;
        this.logger.LogInformation("Starting reindex of index {IndexAlias} (rebuild: {RebuildIndex})", indexAlias, status.RebuildIndex);

        try
        {
            ContentState[] states = ResolveContentStates(registration.ContentChangeStrategy);
            var totals = new Dictionary<UmbracoObjectTypes, long>();

            foreach (UmbracoObjectTypes objectType in registration.ContainedObjectTypes)
            {
                foreach (ContentBatch batch in this.enumerator.Enumerate(objectType))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await this.ProcessBatchAsync(objectType, batch.Items, states, status.RebuildIndex);

                    totals[objectType] = batch.Total;
                    status = status with
                    {
                        ProcessedItems = status.ProcessedItems + batch.Items.Length,
                        TotalItems = totals.Values.Sum(),
                    };
                    this.statusTracker.Update(status);
                }
            }

            if (status.RebuildIndex && this.rebuilder.Rebuild(indexAlias) is false)
            {
                throw new InvalidOperationException($"Could not trigger a rebuild of index '{indexAlias}'. See the log for details.");
            }

            this.statusTracker.Update(status with { State = ReindexState.Idle, CompletedAt = this.timeProvider.GetUtcNow() });
            this.logger.LogInformation("Finished reindex of index {IndexAlias}: {ProcessedItems} items processed", indexAlias, status.ProcessedItems);
        }
        catch (OperationCanceledException)
        {
            this.logger.LogWarning("Reindex of index {IndexAlias} was cancelled", indexAlias);
            this.Fail(status, "The reindex was cancelled because the application is shutting down.");
        }
        catch (Exception exception)
        {
            this.logger.LogError(exception, "Reindex of index {IndexAlias} failed", indexAlias);
            this.Fail(status, exception.Message);
        }
    }

    private async Task ProcessBatchAsync(UmbracoObjectTypes objectType, IContentBase[] items, ContentState[] states, bool rebuildIndex)
    {
        if (rebuildIndex)
        {
            Guid[] keys = items.Select(item => item.Key).ToArray();
            bool[] publishedFlags = objectType == UmbracoObjectTypes.Document
                ? states.Select(state => state == ContentState.Published).ToArray()
                : [false];

            foreach (var published in publishedFlags)
            {
                await this.indexDocumentService.DeleteAsync(keys, published);
            }

            return;
        }

        switch (objectType)
        {
            case UmbracoObjectTypes.Document:
                foreach (ContentState state in states)
                {
                    IContent[] documents = items
                        .OfType<IContent>()
                        .Where(content => state == ContentState.Draft || content.Published)
                        .ToArray();
                    if (documents.Length > 0)
                    {
                        this.refresher.RefreshContent(documents, state);
                    }
                }

                break;
            case UmbracoObjectTypes.Media:
                this.refresher.RefreshMedia(items.OfType<IMedia>().ToArray());
                break;
            case UmbracoObjectTypes.Member:
                this.refresher.RefreshMember(items.OfType<IMember>().ToArray());
                break;
            default:
                this.logger.LogWarning("Object type {ObjectType} is not supported for reindexing and was skipped", objectType);
                break;
        }
    }

    private void Fail(ReindexStatus status, string message)
        => this.statusTracker.Update(status with
        {
            State = ReindexState.Failed,
            ErrorMessage = message,
            CompletedAt = this.timeProvider.GetUtcNow(),
        });
}
