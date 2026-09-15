// <copyright file="ReindexApiController.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Builders;
using Umbraco.Cms.Core;
using Umbraco.Community.Search.Examine.Reindex.Models;
using Umbraco.Community.Search.Examine.Reindex.Services;

namespace Umbraco.Community.Search.Examine.Reindex.Controllers;

/// <summary>
/// Starts and reports reindex operations for Umbraco Search content indexes.
/// </summary>
[ApiVersion("1.0")]
public class ReindexApiController : ReindexApiControllerBase
{
    private readonly IIndexContentReindexer reindexer;
    private readonly IReindexStatusTracker statusTracker;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReindexApiController"/> class.
    /// </summary>
    /// <param name="reindexer">The reindexer.</param>
    /// <param name="statusTracker">The status tracker.</param>
    public ReindexApiController(IIndexContentReindexer reindexer, IReindexStatusTracker statusTracker)
    {
        this.reindexer = reindexer;
        this.statusTracker = statusTracker;
    }

    /// <summary>
    /// Starts a reindex of all content in the index.
    /// </summary>
    /// <param name="indexAlias">The index alias.</param>
    /// <param name="model">The request options.</param>
    /// <returns>The status of the started operation.</returns>
    [HttpPost("index/{indexAlias}/reindex")]
    [ProducesResponseType<ReindexStatus>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ReindexStatus>(StatusCodes.Status409Conflict)]
    public IActionResult Reindex(string indexAlias, ReindexRequestModel model)
    {
        Attempt<ReindexStatus, ReindexOperationStatus> attempt = this.reindexer.Start(indexAlias, model.RebuildIndex);

        return attempt.Status switch
        {
            ReindexOperationStatus.Success => this.Ok(attempt.Result),
            ReindexOperationStatus.AlreadyRunning => this.Conflict(attempt.Result),
            _ => this.BadRequest(new ProblemDetailsBuilder()
                .WithTitle("Index not found")
                .WithDetail($"No content index is registered with alias '{indexAlias}'.")
                .WithOperationStatus(attempt.Status)
                .Build()),
        };
    }

    /// <summary>
    /// Gets the current reindex status of the index.
    /// </summary>
    /// <param name="indexAlias">The index alias.</param>
    /// <returns>The status.</returns>
    [HttpGet("index/{indexAlias}/reindex/status")]
    [ProducesResponseType<ReindexStatus>(StatusCodes.Status200OK)]
    public IActionResult GetReindexStatus(string indexAlias)
        => this.Ok(this.statusTracker.Get(indexAlias));
}
