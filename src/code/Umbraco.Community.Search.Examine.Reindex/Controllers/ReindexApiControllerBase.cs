// <copyright file="ReindexApiControllerBase.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Cms.Web.Common.Routing;

namespace Umbraco.Community.Search.Examine.Reindex.Controllers;

/// <summary>
/// Base class for the package's Management API controllers.
/// </summary>
[ApiController]
[BackOfficeRoute(Constants.Api.Name + "/api/v{version:apiVersion}")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
[MapToApi(Constants.Api.Name)]
public abstract class ReindexApiControllerBase : ManagementApiControllerBase
{
}
