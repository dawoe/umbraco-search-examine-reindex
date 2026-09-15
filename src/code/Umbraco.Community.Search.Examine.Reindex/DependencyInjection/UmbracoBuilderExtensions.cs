// <copyright file="UmbracoBuilderExtensions.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Asp.Versioning;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
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

        builder.Services.AddSingleton<IOperationIdHandler, ReindexOperationIdHandler>();
        builder.Services.Configure<SwaggerGenOptions>(options =>
        {
            options.SwaggerDoc(Constants.Api.Name, new OpenApiInfo
            {
                Title = "Umbraco Search Examine Reindex API",
                Version = "1.0",
            });
            options.OperationFilter<ReindexOperationSecurityFilter>();
        });

        return builder;
    }

    private sealed class ReindexOperationSecurityFilter : BackOfficeSecurityRequirementsOperationFilterBase
    {
        protected override string ApiName => Constants.Api.Name;
    }

    private sealed class ReindexOperationIdHandler(IOptions<ApiVersioningOptions> apiVersioningOptions)
        : OperationIdHandler(apiVersioningOptions)
    {
        public override string Handle(ApiDescription apiDescription)
        {
            var actionName = $"{apiDescription.ActionDescriptor.RouteValues["action"]}";
            return actionName.Length == 0 ? actionName : string.Concat(char.ToLowerInvariant(actionName[0]), actionName[1..]);
        }

        protected override bool CanHandle(ApiDescription apiDescription, ControllerActionDescriptor controllerActionDescriptor)
            => controllerActionDescriptor.ControllerTypeInfo.Namespace?.StartsWith(Constants.Api.ControllerNamespace, StringComparison.OrdinalIgnoreCase) is true;
    }
}
