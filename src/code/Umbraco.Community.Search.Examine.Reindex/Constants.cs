// <copyright file="Constants.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Umbraco.Community.Search.Examine.Reindex;

/// <summary>
/// Constants for the package.
/// </summary>
internal static class Constants
{
    /// <summary>
    /// Management API related constants.
    /// </summary>
    public static class Api
    {
        /// <summary>
        /// The API name used for routing, Swagger and <c>MapToApi</c>.
        /// </summary>
        public const string Name = "search-examine-reindex";

        /// <summary>
        /// The namespace prefix of the package's API controllers.
        /// </summary>
        public const string ControllerNamespace = "Umbraco.Community.Search.Examine.Reindex.Controllers";
    }
}
