// <copyright file="UmbracoBuilderExtensions.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.Search.Examine.Reindex.Services;

namespace Umbraco.Community.Search.Examine.Reindex.DependencyInjection;

/// <summary>
/// Registration of the reindex package.
/// </summary>
public static class UmbracoBuilderExtensions
{
    /// <summary>
    /// Adds the services and API of the Umbraco Search Examine reindex package.
    /// </summary>
    /// <remarks>
    /// Requires <c>AddSearchCore()</c> and <c>AddExamineSearchProvider()</c> to be called by the host.
    /// This method is idempotent.
    /// </remarks>
    /// <param name="builder">The Umbraco builder.</param>
    /// <returns>The Umbraco builder for method chaining.</returns>
    public static IUmbracoBuilder AddSearchExamineReindex(this IUmbracoBuilder builder)
    {
        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(IIndexContentReindexer)))
        {
            return builder;
        }

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IReindexStatusTracker, ReindexStatusTracker>();
        builder.Services.AddSingleton<IIndexContentEnumerator, IndexContentEnumerator>();
        builder.Services.AddTransient<IIndexContentReindexer, IndexContentReindexer>();

        builder.AddBackOfficeOpenApiDocument(
            Constants.Api.Name,
            document => document
                .WithTitle("Umbraco Search Examine Reindex API")
                .WithBackOfficeAuthentication()
                .ConfigureOpenApiOptions(options => options.AddOperationTransformer(
                    (operation, context, _) =>
                    {
                        // Mirrors the operation IDs the Swashbuckle-era OperationIdHandler produced,
                        // so the generated TypeScript SDK keeps the same function names. The
                        // transformer is scoped to this document, so no controller-namespace check
                        // is needed: it only ever sees this package's operations.
                        var actionName = $"{context.Description.ActionDescriptor.RouteValues["action"]}";
                        operation.OperationId = actionName.Length == 0
                            ? actionName
                            : string.Concat(char.ToLowerInvariant(actionName[0]), actionName[1..]);
                        return Task.CompletedTask;
                    })));

        return builder;
    }
}
