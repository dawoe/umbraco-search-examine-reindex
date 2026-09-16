// <copyright file="ReindexApiControllerTests.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Community.Search.Examine.Reindex.Controllers;
using Umbraco.Community.Search.Examine.Reindex.Models;
using Umbraco.Community.Search.Examine.Reindex.Services;

namespace Umbraco.Community.Search.Examine.Reindex.Tests.Controllers;

/// <summary>
/// Tests for <see cref="ReindexApiController"/>.
/// </summary>
[TestFixture]
public class ReindexApiControllerTests
{
    private const string Alias = "Umb_PublishedContent";

    /// <summary>
    /// A successful start returns 200 with the status.
    /// </summary>
    [Test]
    public void Reindex_Success_ReturnsOkWithStatus()
    {
        ReindexStatus status = ReindexStatus.Idle(Alias) with { State = ReindexState.Running };
        var reindexer = new Mock<IIndexContentReindexer>();
        reindexer.Setup(r => r.Start(Alias, true)).Returns(Attempt.SucceedWithStatus(ReindexOperationStatus.Success, status));
        var sut = new ReindexApiController(reindexer.Object, new Mock<IReindexStatusTracker>().Object);

        IActionResult result = sut.Reindex(Alias, new ReindexRequestModel { RebuildIndex = true });

        var ok = result as OkObjectResult;
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.Not.Null);
            Assert.That(ok!.Value, Is.EqualTo(status));
        });
    }

    /// <summary>
    /// An unknown index returns 400.
    /// </summary>
    [Test]
    public void Reindex_IndexNotFound_ReturnsBadRequest()
    {
        var reindexer = new Mock<IIndexContentReindexer>();
        reindexer.Setup(r => r.Start(Alias, false)).Returns(Attempt.FailWithStatus(ReindexOperationStatus.IndexNotFound, ReindexStatus.Idle(Alias)));
        var sut = new ReindexApiController(reindexer.Object, new Mock<IReindexStatusTracker>().Object);

        IActionResult result = sut.Reindex(Alias, new ReindexRequestModel());

        var badRequest = result as ObjectResult;
        Assert.Multiple(() =>
        {
            Assert.That(badRequest, Is.Not.Null);
            Assert.That(badRequest!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(badRequest.Value, Is.InstanceOf<ProblemDetails>());
        });
    }

    /// <summary>
    /// An already running reindex returns 409 with the current status.
    /// </summary>
    [Test]
    public void Reindex_AlreadyRunning_ReturnsConflictWithStatus()
    {
        ReindexStatus current = ReindexStatus.Idle(Alias) with { State = ReindexState.Running, ProcessedItems = 3 };
        var reindexer = new Mock<IIndexContentReindexer>();
        reindexer.Setup(r => r.Start(Alias, false)).Returns(Attempt.FailWithStatus(ReindexOperationStatus.AlreadyRunning, current));
        var sut = new ReindexApiController(reindexer.Object, new Mock<IReindexStatusTracker>().Object);

        IActionResult result = sut.Reindex(Alias, new ReindexRequestModel());

        var conflict = result as ConflictObjectResult;
        Assert.Multiple(() =>
        {
            Assert.That(conflict, Is.Not.Null);
            Assert.That(conflict!.Value, Is.EqualTo(current));
        });
    }

    /// <summary>
    /// The status endpoint returns the tracked status.
    /// </summary>
    [Test]
    public void GetReindexStatus_ReturnsTrackedStatus()
    {
        ReindexStatus status = ReindexStatus.Idle(Alias) with { State = ReindexState.Failed, ErrorMessage = "boom" };
        var tracker = new Mock<IReindexStatusTracker>();
        tracker.Setup(t => t.Get(Alias)).Returns(status);
        var sut = new ReindexApiController(new Mock<IIndexContentReindexer>().Object, tracker.Object);

        IActionResult result = sut.GetReindexStatus(Alias);

        var ok = result as OkObjectResult;
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.Not.Null);
            Assert.That(ok!.Value, Is.EqualTo(status));
        });
    }
}
