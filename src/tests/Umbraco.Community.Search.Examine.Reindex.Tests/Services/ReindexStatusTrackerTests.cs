// <copyright file="ReindexStatusTrackerTests.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Umbraco.Community.Search.Examine.Reindex.Models;
using Umbraco.Community.Search.Examine.Reindex.Services;

namespace Umbraco.Community.Search.Examine.Reindex.Tests.Services;

/// <summary>
/// Tests for <see cref="ReindexStatusTracker"/>.
/// </summary>
[TestFixture]
public class ReindexStatusTrackerTests
{
    private const string Alias = "Umb_PublishedContent";

    /// <summary>
    /// Unknown aliases report an idle status.
    /// </summary>
    [Test]
    public void Get_UnknownAlias_ReturnsIdle()
    {
        var tracker = new ReindexStatusTracker();

        ReindexStatus status = tracker.Get(Alias);

        Assert.Multiple(() =>
        {
            Assert.That(status.IndexAlias, Is.EqualTo(Alias));
            Assert.That(status.State, Is.EqualTo(ReindexState.Idle));
            Assert.That(status.ProcessedItems, Is.Zero);
            Assert.That(status.TotalItems, Is.Null);
        });
    }

    /// <summary>
    /// Starting when idle succeeds and stores the status.
    /// </summary>
    [Test]
    public void TryStart_WhenIdle_StoresRunningStatus()
    {
        var tracker = new ReindexStatusTracker();
        ReindexStatus running = ReindexStatus.Idle(Alias) with { State = ReindexState.Running, RebuildIndex = true };

        var started = tracker.TryStart(running);

        Assert.Multiple(() =>
        {
            Assert.That(started, Is.True);
            Assert.That(tracker.Get(Alias), Is.EqualTo(running));
        });
    }

    /// <summary>
    /// Starting while already running is refused and keeps the existing status.
    /// </summary>
    [Test]
    public void TryStart_WhenRunning_ReturnsFalseAndKeepsExisting()
    {
        var tracker = new ReindexStatusTracker();
        ReindexStatus first = ReindexStatus.Idle(Alias) with { State = ReindexState.Running, ProcessedItems = 5 };
        tracker.TryStart(first);

        var started = tracker.TryStart(ReindexStatus.Idle(Alias) with { State = ReindexState.Running });

        Assert.Multiple(() =>
        {
            Assert.That(started, Is.False);
            Assert.That(tracker.Get(Alias).ProcessedItems, Is.EqualTo(5));
        });
    }

    /// <summary>
    /// Starting after a failure replaces the failed status.
    /// </summary>
    [Test]
    public void TryStart_WhenFailed_ReplacesStatus()
    {
        var tracker = new ReindexStatusTracker();
        tracker.Update(ReindexStatus.Idle(Alias) with { State = ReindexState.Failed, ErrorMessage = "boom" });

        var started = tracker.TryStart(ReindexStatus.Idle(Alias) with { State = ReindexState.Running });

        Assert.Multiple(() =>
        {
            Assert.That(started, Is.True);
            Assert.That(tracker.Get(Alias).State, Is.EqualTo(ReindexState.Running));
            Assert.That(tracker.Get(Alias).ErrorMessage, Is.Null);
        });
    }

    /// <summary>
    /// Update overwrites the stored status for the alias.
    /// </summary>
    [Test]
    public void Update_OverwritesStatus()
    {
        var tracker = new ReindexStatusTracker();
        tracker.TryStart(ReindexStatus.Idle(Alias) with { State = ReindexState.Running });

        tracker.Update(ReindexStatus.Idle(Alias) with { State = ReindexState.Running, ProcessedItems = 500, TotalItems = 1000 });

        ReindexStatus status = tracker.Get(Alias);
        Assert.Multiple(() =>
        {
            Assert.That(status.ProcessedItems, Is.EqualTo(500));
            Assert.That(status.TotalItems, Is.EqualTo(1000));
        });
    }
}
