# Umbraco 18 Upgrade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `Umbraco.Community.Search.Examine.Reindex` build, run and ship against Umbraco 18, whose replacement of Swashbuckle with `Microsoft.AspNetCore.OpenApi` removes every API the package uses to register its Management API document.

**Architecture:** The server-side change is confined to one file — the Swashbuckle document registration, security operation filter and operation-id handler collapse into a single `builder.AddBackOfficeOpenApiDocument(...)` call. Everything else is version bumps plus a new Umbraco 18 test site that replaces the 17 one in the solution. Operation IDs are deliberately held byte-identical to the 17 line so the regenerated TypeScript client diff is attributable to the OpenAPI 3.0 → 3.1 switch alone.

**Tech Stack:** .NET 10 / C# (NUnit + Moq), Umbraco CMS 18, Umbraco Search 18 with the Examine provider, TypeScript + Lit + Vite for the backoffice client, `@hey-api/openapi-ts` for the generated API client, Playwright for E2E.

**Spec:** `docs/superpowers/specs/2026-09-16-umbraco-18-upgrade-design.md`

## Global Constraints

- Branch: work on `feature/v18-version`, which merges into `develop`. `v17/develop` is the 17.x maintenance line and is never modified by this plan.
- Package version becomes `18.0.0` (`AssemblyVersion`, `VersionPrefix`, `InformationalVersion`).
- Umbraco CMS package version range: `[18.0.0,19.0.0)`.
- `Umbraco.Cms.Search.Provider.Examine`: `18.1.0-beta.1` (prerelease, matching the posture of the 17 line).
- Test site packages: `Umbraco.Cms` and `Umbraco.Cms.DevelopmentMode.Backoffice` `18.1.1`, `Umbraco.Cms.Search.Core` and `Umbraco.Cms.Search.BackOffice` `18.1.0`, `Clean` `8.0.1`.
- npm: `@umbraco-cms/backoffice` `^18.0.0`, `@umbraco-cms/search` `^18.1.0`, `@umbraco/playwright-testhelpers` `18.0.5` (exact pin, matching existing style).
- TypeScript stays pinned at `^6.0.3`. The generator cannot run on TypeScript 7 (fails silently) and TypeScript 5.8/5.9 fail `tsc --noEmit` on the generated client. Do not "fix" this pin.
- Operation IDs must stay `reindex` and `getReindexStatus`. Route templates, the `search-examine-reindex` API name and the `/umbraco/search-examine-reindex/api/v1/...` URLs are unchanged.
- The V18 test site listens on **https://localhost:44310** with unattended install as `admin@example.com` / `1234567890`, exactly like the V17 site, so E2E config and the client's `generate-api` script keep working.
- `test-sites/Website-V17` stays on disk untouched; only the solution entry is swapped.
- `src/.editorconfig` and `src/stylecop.json` are authoritative. Analyzer warnings are fixed, never suppressed. Every C# file keeps its StyleCop copyright header. XML docs on all public and internal members.
- Only depend on public Umbraco APIs.

---

### Task 1: Upgrade dependencies and migrate the OpenAPI registration

The dependency bump and the OpenAPI migration are one task: bumping to Umbraco 18 alone leaves the solution uncompilable (`SwaggerGenOptions`, `BackOfficeSecurityRequirementsOperationFilterBase` and `IOperationIdHandler` no longer exist), so there is no point at which the intermediate state is independently testable.

**Files:**
- Modify: `src/Directory.Build.props` (version properties)
- Modify: `src/Directory.Packages.props` (package versions)
- Modify: `src/code/Umbraco.Community.Search.Examine.Reindex/Umbraco.Community.Search.Examine.Reindex.csproj`
- Modify: `src/code/Umbraco.Community.Search.Examine.Reindex/DependencyInjection/UmbracoBuilderExtensions.cs` (full rewrite)
- Modify: `src/code/Umbraco.Community.Search.Examine.Reindex/Constants.cs`
- Modify: `src/code/Umbraco.Community.Search.Examine.Reindex/Controllers/ReindexApiControllerBase.cs`
- Regenerated: `src/code/Umbraco.Community.Search.Examine.Reindex/packages.lock.json`, `src/tests/Umbraco.Community.Search.Examine.Reindex.Tests/packages.lock.json`
- Test: `src/tests/Umbraco.Community.Search.Examine.Reindex.Tests/**` (unchanged — must stay green)

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: the public surface is unchanged — `UmbracoBuilderExtensions.AddSearchExamineReindex(this IUmbracoBuilder builder) : IUmbracoBuilder`, and an OpenAPI document named `search-examine-reindex` containing operations with `operationId` `reindex` and `getReindexStatus`. Task 2 boots a site against this; Task 3 generates a client from that document.

- [ ] **Step 1: Confirm the current build is green before changing anything**

Run:

```bash
cd src && dotnet build
```

Expected: `Build succeeded`, 0 warnings, 0 errors. If this fails, stop — the failure is pre-existing and not caused by this plan.

- [ ] **Step 2: Bump the package version in `src/Directory.Build.props`**

Replace these three lines:

```xml
      <AssemblyVersion>17.0.0</AssemblyVersion>
      <VersionPrefix>17.0.0</VersionPrefix>
      <InformationalVersion>17.0.0</InformationalVersion>
```

with:

```xml
      <AssemblyVersion>18.0.0</AssemblyVersion>
      <VersionPrefix>18.0.0</VersionPrefix>
      <InformationalVersion>18.0.0</InformationalVersion>
```

Leave every other property in that file alone.

- [ ] **Step 3: Bump the Umbraco packages in `src/Directory.Packages.props`**

Replace this `ItemGroup`:

```xml
  <ItemGroup>
    <PackageVersion Include="Umbraco.Cms.Search.Provider.Examine" Version="17.1.0-beta.1" />
    <PackageVersion Include="Umbraco.Cms.Web.Website" Version="[17.0.0,18.0.0)" />
    <PackageVersion Include="Umbraco.Cms.Core" Version="[17.0.0,18.0.0)" />
    <PackageVersion Include="Umbraco.Cms.Api.Management" Version="[17.0.0,18.0.0)" />
  </ItemGroup>
```

with:

```xml
  <ItemGroup>
    <PackageVersion Include="Umbraco.Cms.Search.Provider.Examine" Version="18.1.0-beta.1" />
    <PackageVersion Include="Umbraco.Cms.Web.Website" Version="[18.0.0,19.0.0)" />
    <PackageVersion Include="Umbraco.Cms.Core" Version="[18.0.0,19.0.0)" />
    <PackageVersion Include="Umbraco.Cms.Api.Management" Version="[18.0.0,19.0.0)" />
  </ItemGroup>
```

There is no Swashbuckle entry to remove — it only ever arrived transitively through `Umbraco.Cms.Api.Management`.

- [ ] **Step 4: Restore and watch the build fail on the removed Swashbuckle APIs**

Run:

```bash
cd src && dotnet restore --force-evaluate && dotnet build
```

Expected: restore succeeds and rewrites both `packages.lock.json` files; **build FAILS**. You should see errors in `DependencyInjection/UmbracoBuilderExtensions.cs` along the lines of `CS0246: The type or namespace name 'Swashbuckle' could not be found`, plus `CS0246` for `SwaggerGenOptions`, `IOperationIdHandler`, `OperationIdHandler` and `BackOfficeSecurityRequirementsOperationFilterBase`.

This failure is the point of the step: it confirms you are genuinely on Umbraco 18 and enumerates exactly what Task 1 has to replace. Record the error list; every one of them must be gone by Step 9.

- [ ] **Step 5: Enable OpenAPI interceptors in the package csproj**

In `src/code/Umbraco.Community.Search.Examine.Reindex/Umbraco.Community.Search.Examine.Reindex.csproj`, replace:

```xml
  <PropertyGroup>
    <StaticWebAssetBasePath>/</StaticWebAssetBasePath>
  </PropertyGroup>
```

with:

```xml
  <PropertyGroup>
    <StaticWebAssetBasePath>/</StaticWebAssetBasePath>
    <!-- The Microsoft.AspNetCore.OpenApi source generator propagates through project references
         but is not auto-enabled in class libraries. Without this, registering an OpenAPI document
         fails with "error CS9137: The 'interceptors' feature is not enabled". -->
    <InterceptorsNamespaces>$(InterceptorsNamespaces);Microsoft.AspNetCore.OpenApi.Generated</InterceptorsNamespaces>
  </PropertyGroup>
```

- [ ] **Step 6: Rewrite `UmbracoBuilderExtensions.cs`**

Replace the entire contents of `src/code/Umbraco.Community.Search.Examine.Reindex/DependencyInjection/UmbracoBuilderExtensions.cs` with:

```csharp
// <copyright file="UmbracoBuilderExtensions.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Microsoft.AspNetCore.OpenApi;
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
```

What changed and why:

- `builder.Services.Configure<SwaggerGenOptions>(...)` + `options.SwaggerDoc(...)` → `builder.AddBackOfficeOpenApiDocument(name, ...)` + `.WithTitle(...)`.
- `ReindexOperationSecurityFilter : BackOfficeSecurityRequirementsOperationFilterBase` → `.WithBackOfficeAuthentication()`. The nested class is gone.
- `builder.Services.AddSingleton<IOperationIdHandler, ReindexOperationIdHandler>()` and the `ReindexOperationIdHandler` nested class → the inline operation transformer. Both nested classes are deleted.
- The `Asp.Versioning`, `Microsoft.AspNetCore.Mvc.ApiExplorer`, `Microsoft.AspNetCore.Mvc.Controllers`, `Microsoft.Extensions.Options`, `Microsoft.OpenApi` and `Swashbuckle.AspNetCore.SwaggerGen` usings are all dropped because nothing references them any more.

If the compiler reports `AddOperationTransformer` as unresolved, the `using Microsoft.AspNetCore.OpenApi;` line is the one that supplies it; if it reports that using as unnecessary (IDE0005 / StyleCop), remove it — `ConfigureOpenApiOptions` may already surface the method. Do not add any other using to work around it.

- [ ] **Step 7: Drop the now-unused `ControllerNamespace` constant**

The transformer is scoped to this document, so nothing checks controller namespaces any more. In `src/code/Umbraco.Community.Search.Examine.Reindex/Constants.cs`, delete this member and its doc comment:

```csharp
        /// <summary>
        /// The namespace prefix of the package's API controllers.
        /// </summary>
        public const string ControllerNamespace = "Umbraco.Community.Search.Examine.Reindex.Controllers";
```

Also update the remaining member's doc comment, which mentions Swagger. Replace:

```csharp
        /// <summary>
        /// The API name used for routing, Swagger and <c>MapToApi</c>.
        /// </summary>
        public const string Name = "search-examine-reindex";
```

with:

```csharp
        /// <summary>
        /// The API name used for routing, the OpenAPI document and <c>MapToApi</c>.
        /// </summary>
        public const string Name = "search-examine-reindex";
```

`Constants.Api.Name` must keep the value `search-examine-reindex` — it is baked into the route, the client's generate script and the E2E status URL.

- [ ] **Step 8: Remove the Swashbuckle-era grouping attribute from the controller base**

`[ApiExplorerSettings(GroupName = ...)]` was a Swashbuckle grouping mechanism; document membership now comes from `[MapToApi]`. In `src/code/Umbraco.Community.Search.Examine.Reindex/Controllers/ReindexApiControllerBase.cs`, delete this line:

```csharp
[ApiExplorerSettings(GroupName = "Umbraco Search Examine Reindex")]
```

The file afterwards reads:

```csharp
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
```

Keep `using Microsoft.AspNetCore.Mvc;` — `[ApiController]` still needs it. Task 3 Step 4 verifies against the real document that removing the attribute changed nothing; if it turns out the document loses its title or grouping, put the attribute back then, not now.

`ReindexApiController.cs` is not modified in this task at all.

- [ ] **Step 9: Build and verify every error from Step 4 is gone**

Run:

```bash
cd src && dotnet build
```

Expected: `Build succeeded`, 0 warnings, 0 errors. In particular there must be no `CS9137` (that would mean Step 5's `InterceptorsNamespaces` did not take) and no leftover `CS0246`.

If StyleCop flags an unused using in `UmbracoBuilderExtensions.cs`, delete that using rather than suppressing the warning.

- [ ] **Step 10: Run the unit tests — they must pass completely unchanged**

Run:

```bash
cd src && dotnet test
```

Expected: `Passed! - Failed: 0, Passed: 25`. The tests exercise the services and `ReindexApiController` directly and touch no OpenAPI plumbing, so any failure here means something unintended moved — investigate it rather than editing the test.

- [ ] **Step 11: Commit**

```bash
git add src/Directory.Build.props src/Directory.Packages.props src/code/Umbraco.Community.Search.Examine.Reindex src/tests/Umbraco.Community.Search.Examine.Reindex.Tests/packages.lock.json
git commit -m "Migrate the Management API document to Microsoft.AspNetCore.OpenApi for Umbraco 18"
```

---

### Task 2: Add the Website-V18 test site and swap it into the solution

**Files:**
- Create: `test-sites/Website-V18/Website-V18.csproj`
- Create: `test-sites/Website-V18/Program.cs`
- Create: `test-sites/Website-V18/WebsiteComposer.cs`
- Create: `test-sites/Website-V18/appsettings.json`
- Create: `test-sites/Website-V18/appsettings.Development.json`
- Create: `test-sites/Website-V18/Properties/launchSettings.json`
- Create: `test-sites/Website-V18/.gitignore` (copied verbatim from `test-sites/Website-V17/.gitignore`)
- Modify: `src/Umbraco.Community.Search.Examine.Reindex.slnx`
- Untouched: everything under `test-sites/Website-V17/`

**Interfaces:**
- Consumes: the package project built in Task 1, referenced via `ProjectReference`, registering itself through `SearchExamineReindexComposer`.
- Produces: a running Umbraco 18 backoffice at `https://localhost:44310`, logged in as `admin@example.com` / `1234567890`, serving the OpenAPI document at `/umbraco/openapi/search-examine-reindex.json` and the API at `/umbraco/search-examine-reindex/api/v1/index/{indexAlias}/reindex[/status]`. Tasks 3 and 4 both require this site to be running.

- [ ] **Step 1: Create the project file**

Create `test-sites/Website-V18/Website-V18.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Website_V18</RootNamespace>
    <CompressionEnabled>false</CompressionEnabled> <!-- Disable compression. E.g. for umbraco backoffice files. These files should be precompressed by node and not let dotnet handle it -->
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Clean" Version="8.0.1" />
    <PackageReference Include="Umbraco.Cms" Version="18.1.1" />
    <PackageReference Include="Umbraco.Cms.DevelopmentMode.Backoffice" Version="18.1.1" />
  </ItemGroup>

  <ItemGroup>
    <!-- Opt-in to app-local ICU to ensure consistent globalization APIs across different platforms -->
    <PackageReference Include="Microsoft.ICU.ICU4C.Runtime" Version="72.1.0.3" />
    <PackageReference Include="Umbraco.Cms.Search.BackOffice" Version="18.1.0" />
    <PackageReference Include="Umbraco.Cms.Search.Core" Version="18.1.0" />
    <PackageReference Include="Umbraco.Cms.Search.Provider.Examine" Version="18.1.0-beta.1" />
    <ProjectReference Include="..\..\src\code\Umbraco.Community.Search.Examine.Reindex\Umbraco.Community.Search.Examine.Reindex.csproj" />
    <RuntimeHostConfigurationOption Include="System.Globalization.AppLocalIcu" Value="72.1.0.3" Condition="$(RuntimeIdentifier.StartsWith('linux')) or $(RuntimeIdentifier.StartsWith('win')) or ('$(RuntimeIdentifier)' == '' and !$([MSBuild]::IsOSPlatform('osx')))" />
  </ItemGroup>

  <PropertyGroup>
    <!-- Razor files are needed for the backoffice to work correctly -->
    <CopyRazorGenerateFilesToPublishDirectory>true</CopyRazorGenerateFilesToPublishDirectory>
  </PropertyGroup>

  <PropertyGroup>
    <!-- Remove RazorCompileOnBuild and RazorCompileOnPublish when not using ModelsMode InMemoryAuto -->
    <RazorCompileOnBuild>false</RazorCompileOnBuild>
    <RazorCompileOnPublish>false</RazorCompileOnPublish>
  </PropertyGroup>

</Project>
```

This project sits outside `src/`, so it does not inherit `src/Directory.Build.props` or central package management — the versions are pinned inline, exactly as the V17 site does it.

- [ ] **Step 2: Create `Program.cs`**

Create `test-sites/Website-V18/Program.cs`:

```csharp
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

WebApplication app = builder.Build();

await app.BootUmbracoAsync();


app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
```

`AddComposers()` is what picks up the package's `SearchExamineReindexComposer` — there is no explicit `AddSearchExamineReindex()` call anywhere in the site.

- [ ] **Step 3: Create `WebsiteComposer.cs`**

Create `test-sites/Website-V18/WebsiteComposer.cs`:

```csharp
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Search.BackOffice.DependencyInjection;
using Umbraco.Cms.Search.Core.DependencyInjection;
using Umbraco.Cms.Search.Provider.Examine.DependencyInjection;

namespace Website_V18
{
	public class WebsiteComposer : IComposer
	{
		public void Compose(IUmbracoBuilder builder)
		{
			builder
				// add core services for search abstractions
				.AddSearchCore()
				.AddBackOfficeSearch()
				// add the Examine search provider
				.AddExamineSearchProvider();
		}
	}
}
```

Identical to the V17 site apart from the namespace. Test sites are not subject to the package's StyleCop rules.

- [ ] **Step 4: Create the appsettings files**

Create `test-sites/Website-V18/appsettings.json`:

```json
{
  "$schema": "appsettings-schema.json",
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "System": "Warning"
      }
    }
  },
  "Umbraco": {
    "CMS": {
      "Global": {
        "Id": "b5e2f1c4-9a63-4e8d-8f21-7c3d5a06be94",
        "SanitizeTinyMce": true
      },
      "Content": {
        "AllowEditInvariantFromNonDefault": true,
        "ContentVersionCleanupPolicy": {
          "EnableCleanup": true
        }
      },
      "Unattended": {
        "UpgradeUnattended": true
      },
      "Security": {
        "AllowConcurrentLogins": false
      }
    }
  }
}
```

The `Global.Id` is a fresh GUID, deliberately different from the V17 site's — it identifies this installation for telemetry and must not be duplicated.

Create `test-sites/Website-V18/appsettings.Development.json`:

```json
{
  "$schema": "appsettings-schema.json",
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    },
    "WriteTo": [
      {
        "Name": "Async",
        "Args": {
          "configure": [
            {
              "Name": "Console"
            }
          ]
        }
      }
    ]
  },
  "ConnectionStrings": {
    "umbracoDbDSN": "Data Source=|DataDirectory|/Umbraco.sqlite.db;Cache=Shared;Foreign Keys=True;Pooling=True",
    "umbracoDbDSN_ProviderName": "Microsoft.Data.Sqlite"
  },
  "Umbraco": {
    "CMS": {
      "Unattended": {
        "InstallUnattended": true,
        "UnattendedUserName": "admin@example.com",
        "UnattendedUserEmail": "admin@example.com",
        "UnattendedUserPassword": "1234567890",
        "UnattendedTelemetryLevel": "Detailed"
      },
      "Content": {
        "MacroErrors": "Throw"
      },
      "Hosting": {
        "Debug": true
      }
    }
  }
}
```

The credentials must match `src/tests/Umbraco.Community.Search.Examine.Reindex.E2E/.env.example` exactly (`admin@example.com` / `1234567890`) or the E2E auth setup in Task 4 fails.

- [ ] **Step 5: Create `Properties/launchSettings.json`**

Create `test-sites/Website-V18/Properties/launchSettings.json`:

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "iisSettings": {
    "windowsAuthentication": false,
    "anonymousAuthentication": true,
    "iisExpress": {
      "applicationUrl": "http://localhost:45018",
      "sslPort": 44310
    }
  },
  "profiles": {
    "IIS Express": {
      "commandName": "IISExpress",
      "launchBrowser": true,
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "Umbraco.Web.UI": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:44310;http://localhost:45018",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

Port 44310 is reused on purpose: only one test site runs at a time, and keeping the port means the E2E config and the client's `generate-api` script need no change.

- [ ] **Step 6: Copy the site `.gitignore`**

```bash
cp test-sites/Website-V17/.gitignore test-sites/Website-V18/.gitignore
```

This is the standard Visual Studio + Umbraco ignore file; the entries that matter are `/umbraco/Data/*.sqlite.db*`, `/umbraco/Data/TEMP/`, `/umbraco/Logs/`, `/wwwroot/media/` and `appsettings-schema*.json`.

- [ ] **Step 7: Swap the test site in the solution**

In `src/Umbraco.Community.Search.Examine.Reindex.slnx`, replace:

```xml
  <Project Path="../test-sites/Website-V17/Website-V17.csproj">
    <Build Solution="Release|*" Project="false" />
  </Project>
```

with:

```xml
  <Project Path="../test-sites/Website-V18/Website-V18.csproj">
    <Build Solution="Release|*" Project="false" />
  </Project>
```

Keep the `<Build Solution="Release|*" Project="false" />` guard — it is what stops CI from building the test site in Release.

- [ ] **Step 8: Build the site**

Run:

```bash
cd test-sites/Website-V18 && dotnet build
```

Expected: `Build succeeded`. Clean 8.0.1 copies its views and assets into the project during restore/build, so a `Views/` folder and `wwwroot` content appear — that is expected, not a mistake.

If the build fails resolving `Umbraco.Cms.Search.Provider.Examine 18.1.0-beta.1`, confirm your NuGet config allows prereleases from nuget.org. If it fails with errors from inside the Examine provider's own API, stop and report it: that is the scope expansion the spec flags as a risk, not something to work around here.

- [ ] **Step 9: Clear the incremental application-part cache**

MVC application-part discovery is incremental on `project.assets.json`. A brand-new site has a cold cache, so the package's controllers can 404 until a full rebuild. Pre-empt it:

```bash
cd test-sites/Website-V18 && dotnet clean && dotnet build
```

Never work around a 404 here with `AddControllers().AddApplicationPart(...)` from a composer.

- [ ] **Step 10: Run the site and verify it installs**

Run:

```bash
cd test-sites/Website-V18 && dotnet run
```

Expected: console logs show the unattended install running and the app listening on `https://localhost:44310`. Leave it running — Steps 11 and 12, and all of Tasks 3 and 4, need it.

- [ ] **Step 11: Verify the OpenAPI document exists at the new URL**

In a second shell:

```bash
curl -k -s https://localhost:44310/umbraco/openapi/search-examine-reindex.json | head -c 400
```

Expected: JSON starting with `{"openapi":"3.1.` and containing `"title":"Umbraco Search Examine Reindex API"`. A 404 means the document was not registered — re-check Task 1 Steps 5 and 6. Note the URL: `/umbraco/openapi/{documentName}.json`, **not** the old `/umbraco/swagger/{documentName}/swagger.json`.

- [ ] **Step 12: Verify the backoffice loads and the reindex box renders**

Open `https://localhost:44310/umbraco`, log in as `admin@example.com` / `1234567890`, and navigate to
`/umbraco/section/settings/workspace/search-index/edit/Umb_PublishedContent`.

Expected: the **Reindex** detail box renders with an enabled Reindex button and a rebuild toggle. This confirms the client assets shipped in `wwwroot/App_Plugins/searchreindex` still load under Umbraco 18. If `App_Plugins/searchreindex` is missing, build the client first (`cd src/code/Umbraco.Community.Search.Examine.Reindex.Client && npm ci && npm run build`) and rebuild the site — the folder is gitignored and produced by the client build.

- [ ] **Step 13: Commit**

```bash
git add test-sites/Website-V18 src/Umbraco.Community.Search.Examine.Reindex.slnx
git commit -m "Add an Umbraco 18 test site and use it in the solution"
```

If Clean 8 generated tracked content (views, wwwroot assets) under `test-sites/Website-V18`, include it — the V17 site tracks its Clean views the same way.

---

### Task 3: Regenerate the API client against the OpenAPI 3.1 document

**Files:**
- Modify: `src/code/Umbraco.Community.Search.Examine.Reindex.Client/package.json`
- Modify: `src/code/Umbraco.Community.Search.Examine.Reindex.Client/openapi-ts.config.ts`
- Modify: `.gitignore` (repo root, line 10)
- Regenerated: `src/code/Umbraco.Community.Search.Examine.Reindex.Client/src/api/**`
- Regenerated: `src/code/Umbraco.Community.Search.Examine.Reindex.Client/package-lock.json`
- Possibly modify: `src/code/Umbraco.Community.Search.Examine.Reindex/Controllers/ReindexApiControllerBase.cs` (only if Step 4 finds a schema/runtime mismatch)

**Interfaces:**
- Consumes: the running V18 site from Task 2, serving `/umbraco/openapi/search-examine-reindex.json`.
- Produces: `src/api/index.js` continues to export `reindex(options)` and `getReindexStatus(options)`, and `src/api/types.gen.ts` continues to export the type `ReindexStatus` with members `indexAlias: string`, `state: ReindexState`, `rebuildIndex: boolean`, `processedItems: number`, `totalItems`, `startedAt`, `completedAt`, `errorMessage`. `src/repository/reindex.repository.ts` consumes exactly these names and must not need editing.

- [ ] **Step 1: Point the generate script at the new endpoint and bump the Umbraco packages**

In `src/code/Umbraco.Community.Search.Examine.Reindex.Client/package.json`, replace the `generate-api` script:

```json
    "generate-api": "curl -k -o swagger.json https://localhost:44310/umbraco/swagger/search-examine-reindex/swagger.json && openapi-ts"
```

with:

```json
    "generate-api": "curl -k -o openapi.json https://localhost:44310/umbraco/openapi/search-examine-reindex.json && openapi-ts"
```

and replace the `devDependencies` block:

```json
  "devDependencies": {
    "@hey-api/openapi-ts": "^0.85.2",
    "@umbraco-cms/backoffice": "^17.0.0",
    "@umbraco-cms/search": "^17.1.0",
    "typescript": "^6.0.3",
    "vite": "^8.2.2"
  }
```

with:

```json
  "devDependencies": {
    "@hey-api/openapi-ts": "^0.85.2",
    "@umbraco-cms/backoffice": "^18.0.0",
    "@umbraco-cms/search": "^18.1.0",
    "typescript": "^6.0.3",
    "vite": "^8.2.2"
  }
```

Leave `typescript` at `^6.0.3` and `@hey-api/openapi-ts` at `^0.85.2` for now — Step 6 decides whether the generator needs bumping.

- [ ] **Step 2: Rename the downloaded spec file in the config and the gitignore**

In `src/code/Umbraco.Community.Search.Examine.Reindex.Client/openapi-ts.config.ts`, change:

```ts
  input: 'swagger.json',
```

to:

```ts
  input: 'openapi.json',
```

In the repo-root `.gitignore`, change line 10 from:

```
src/code/Umbraco.Community.Search.Examine.Reindex.Client/swagger.json
```

to:

```
src/code/Umbraco.Community.Search.Examine.Reindex.Client/openapi.json
```

The file is not committed, so the rename is free. It does have one visible effect: the generated `ClientOptions.baseUrl` template literal type in `src/api/types.gen.ts` is derived from the input filename, so `` `${string}://swagger.json` `` becomes `` `${string}://openapi.json` ``. That is cosmetic and expected.

Delete any stale `swagger.json` left in the client folder:

```bash
rm -f src/code/Umbraco.Community.Search.Examine.Reindex.Client/swagger.json
```

- [ ] **Step 3: Install the bumped packages**

Run:

```bash
cd src/code/Umbraco.Community.Search.Examine.Reindex.Client && npm install
```

Expected: `package-lock.json` updates; `@umbraco-cms/backoffice` resolves to an 18.x version and `@umbraco-cms/search` to 18.1.0. Both ship types only and stay external in the Vite build — they must never be bundled, because Umbraco Search's importmap resolves `@umbraco-cms/search/global` and `/settings` at runtime.

- [ ] **Step 4: Verify the document before generating from it**

With the V18 site from Task 2 running:

```bash
cd src/code/Umbraco.Community.Search.Examine.Reindex.Client
curl -k -s https://localhost:44310/umbraco/openapi/search-examine-reindex.json -o openapi.json
grep -o '"operationId":"[^"]*"' openapi.json
```

Expected, exactly these two lines in some order:

```
"operationId":"reindex"
"operationId":"getReindexStatus"
```

If they are PascalCase or prefixed, the operation transformer from Task 1 Step 6 is not being applied — fix that before generating.

Then check the schema property casing:

```bash
grep -o '"indexAlias"' openapi.json | head -1
```

Expected: one match. camelCase here means the document's schema agrees with the runtime JSON the E2E specs already read (`state`, `rebuildIndex`). If instead you find `"IndexAlias"` (PascalCase), the document is being generated with default serialization rather than the backoffice options: add `[JsonOptionsName(Constants.JsonOptionsNames.BackOffice)]` to `ReindexApiControllerBase` and `.WithJsonOptions(Constants.JsonOptionsNames.BackOffice)` to the document builder in `UmbracoBuilderExtensions.cs`, rebuild, restart the site, and re-run this step.

Finally confirm the title survived removing `[ApiExplorerSettings]` in Task 1 Step 8:

```bash
grep -o '"title":"Umbraco Search Examine Reindex API"' openapi.json
```

Expected: one match. If the title is missing or wrong, restore the `[ApiExplorerSettings(GroupName = "Umbraco Search Examine Reindex")]` attribute on `ReindexApiControllerBase`, rebuild, restart and re-check.

- [ ] **Step 5: Regenerate the client**

```bash
cd src/code/Umbraco.Community.Search.Examine.Reindex.Client && npm run generate-api
```

Expected: the script re-downloads `openapi.json` and `openapi-ts` rewrites `src/api/`.

- [ ] **Step 6: Review the generated diff deliberately**

```bash
git diff --stat src/code/Umbraco.Community.Search.Examine.Reindex.Client/src/api
git diff src/code/Umbraco.Community.Search.Examine.Reindex.Client/src/api/sdk.gen.ts src/code/Umbraco.Community.Search.Examine.Reindex.Client/src/api/types.gen.ts
```

Expected and acceptable:

- `ClientOptions.baseUrl` changes from `` `${string}://swagger.json` `` to `` `${string}://openapi.json` `` (Step 2's rename).
- Optional/nullable members may be re-expressed, because OpenAPI 3.1 emits `type: ["string","null"]` where 3.0 emitted `nullable: true`. `totalItems`, `startedAt`, `completedAt` and `errorMessage` must still each accept `undefined` — `reindex.repository.ts` maps them with `?? undefined`.
- The `ProblemDetails` index signature may be reshaped.

Not acceptable — stop and fix rather than accept:

- `reindex` or `getReindexStatus` renamed or missing from `sdk.gen.ts`.
- The URLs in `sdk.gen.ts` changing from `/umbraco/search-examine-reindex/api/v1/index/{indexAlias}/reindex` and `.../reindex/status`.
- `ReindexStatus`, `ReindexState`, `ReindexRequestModel` renamed, or any of the eight `ReindexStatus` members disappearing.
- The `security` block on either operation disappearing — that would mean `WithBackOfficeAuthentication()` is not doing its job.

If `@hey-api/openapi-ts` 0.85.2 errors on or mangles the 3.1 document, bump it to the latest release and regenerate. Do not touch the `typescript` pin while doing so: the generator cannot run on TypeScript 7, and 5.8/5.9 fail `tsc --noEmit` on the generated `client.gen.ts` with `TS2578`.

- [ ] **Step 7: Build the client**

```bash
cd src/code/Umbraco.Community.Search.Examine.Reindex.Client && npm run build
```

Expected: `tsc` reports no errors and Vite writes the bundle to the package's `wwwroot/App_Plugins/searchreindex`. There must be no edits needed in `src/repository/reindex.repository.ts` or `src/detailboxes/reindex-detail-box.element.ts`; if `tsc` demands one, that is a signal Step 6's "not acceptable" list was violated.

- [ ] **Step 8: Rebuild the site so it ships the new assets, and re-check the box**

```bash
cd test-sites/Website-V18 && dotnet build
```

Restart the site, reload `/umbraco/section/settings/workspace/search-index/edit/Umb_PublishedContent`, and confirm the Reindex box still renders with an enabled button.

- [ ] **Step 9: Commit**

```bash
git add .gitignore src/code/Umbraco.Community.Search.Examine.Reindex.Client
git commit -m "Regenerate the API client from the Umbraco 18 OpenAPI 3.1 document"
```

If Step 4 required the JSON-options or `ApiExplorerSettings` fix, `git add src/code/Umbraco.Community.Search.Examine.Reindex/Controllers/ReindexApiControllerBase.cs` and `.../DependencyInjection/UmbracoBuilderExtensions.cs` as well, and say so in the commit message.

---

### Task 4: Move the E2E suite to Umbraco 18 and run it

**Files:**
- Modify: `src/tests/Umbraco.Community.Search.Examine.Reindex.E2E/package.json`
- Regenerated: `src/tests/Umbraco.Community.Search.Examine.Reindex.E2E/package-lock.json`
- Possibly modify: `src/tests/Umbraco.Community.Search.Examine.Reindex.E2E/tests/reindex-detail-box.spec.ts` (only if an assertion genuinely broke on 18)
- Unchanged: `playwright.config.ts`, `tests/auth.setup.ts`, `.env.example`

**Interfaces:**
- Consumes: the running V18 site from Task 2, on `https://localhost:44310` with `admin@example.com` / `1234567890`; the `data-mark` test ids `search-examine-reindex:button-reindex` and `search-examine-reindex:toggle-rebuild` emitted by the element; the status endpoint `/umbraco/search-examine-reindex/api/v1/index/Umb_PublishedContent/reindex/status` returning `{ state, rebuildIndex }`.
- Produces: no downstream consumers — this is the last functional gate before docs and CI.

- [ ] **Step 1: Bump the testhelpers**

In `src/tests/Umbraco.Community.Search.Examine.Reindex.E2E/package.json`, change:

```json
    "@umbraco/playwright-testhelpers": "17.1.0-beta.7",
```

to:

```json
    "@umbraco/playwright-testhelpers": "18.0.5",
```

Keep the exact pin (no `^`) — that matches the existing style and this package tracks the CMS closely. Leave `tslib` in place: the testhelpers need it at runtime.

- [ ] **Step 2: Install and make sure the browser is present**

```bash
cd src/tests/Umbraco.Community.Search.Examine.Reindex.E2E && npm install && npx playwright install chromium
```

- [ ] **Step 3: Make sure the local `.env` exists**

The suite reads `UMBRACO_URL`, `UMBRACO_USER_LOGIN` and `UMBRACO_USER_PASSWORD`, and `.env` is gitignored:

```bash
cd src/tests/Umbraco.Community.Search.Examine.Reindex.E2E && [ -f .env ] || cp .env.example .env
```

The values in `.env.example` (`https://localhost:44310`, `admin@example.com`, `1234567890`) already match the V18 site from Task 2, so no edits are needed.

- [ ] **Step 4: Run the suite against the V18 site**

With the V18 site running:

```bash
cd src/tests/Umbraco.Community.Search.Examine.Reindex.E2E && npm run test:e2e
```

Expected: 5 passed — `authenticate` (setup) plus `shows the box on an Examine index`, `cancelling the confirm modal does not start a reindex`, `reindex runs and completes`, and `reindex with rebuild triggers the search index rebuild`.

- [ ] **Step 5: If something fails, fix the right thing**

Diagnose before editing. The three assumptions most likely to have moved between Umbraco 17 and 18:

1. **The confirm-modal locators.** `confirmModalButton` targets `uui-button[label="Reindex" i]:not([data-mark])`. If Umbraco 18's `umbConfirmModal` renders different markup, update that locator — but keep the `:not([data-mark])` disambiguation, which is what separates the modal's button from the element's own.
2. **The rebuild toast text.** The last test waits for `/has completed successfully/i`, which comes from Umbraco Search's own message, "The rebuild of search index "{0}" has completed successfully." If Umbraco Search 18 reworded it, read the actual toast and update the regex.
3. **The login flow.** `auth.setup.ts` drives `umbracoUi.login`. If testhelpers 18 renamed those helpers, follow the new API.

Do not add an assertion on an intermediate `Running` status — small sites finish before the first poll, which is why the specs deliberately omit it. Do not replace `umbracoApi.get(...)` with Playwright's built-in `request` fixture: it does not attach the backoffice bearer token and will 401.

Re-run Step 4 until green.

- [ ] **Step 6: Commit**

```bash
git add src/tests/Umbraco.Community.Search.Examine.Reindex.E2E
git commit -m "Run the E2E suite against Umbraco 18"
```

---

### Task 5: Update CI and documentation

**Files:**
- Modify: `.github/workflows/pr-validation.yml`
- Modify: `.github/workflows/beta-release.yml`
- Modify: `.github/workflows/release.yml`
- Modify: `README.md`
- Modify: `CLAUDE.md`
- Unchanged: `umbraco-marketplace.json` (carries no version information)

**Interfaces:**
- Consumes: the finished state of Tasks 1–4.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Let PR validation run for the 17 maintenance line**

In `.github/workflows/pr-validation.yml`, replace:

```yaml
on:
  pull_request:
    branches:
    - develop
```

with:

```yaml
on:
  pull_request:
    branches:
    - develop
    - 'v17/**'
```

- [ ] **Step 2: Let the beta release run from the 17 maintenance line**

In `.github/workflows/beta-release.yml`, replace:

```yaml
on:
  push:
    branches: [ "develop" ]
```

with:

```yaml
on:
  push:
    branches: [ "develop", "v17/develop" ]
```

- [ ] **Step 3: Let a 17 patch be released manually**

In `.github/workflows/release.yml`, replace:

```yaml
    if: github.ref == 'refs/heads/main'
```

with:

```yaml
    if: github.ref == 'refs/heads/main' || github.ref == 'refs/heads/v17/develop'
```

The workflow is `workflow_dispatch`-only, so this widens which branch a maintainer may dispatch it from; it does not add any automatic publishing.

- [ ] **Step 4: Update the README**

In `README.md`, replace:

```
Requires Umbraco 17 with Umbraco Search Core and the Examine provider configured.
```

with:

```
Requires Umbraco 18 with Umbraco Search Core and the Examine provider configured.

Umbraco 17 is supported by the 17.x releases of this package, maintained on the `v17/develop`
branch.
```

- [ ] **Step 5: Update CLAUDE.md**

Make these edits to `CLAUDE.md`:

In the **Solution layout** section, replace the `test-sites/Website-V17/` line with:

```
- `test-sites/Website-V18/` — Umbraco 18 test site referencing the package project (in the
  solution). `test-sites/Website-V17/` is kept on disk for the 17.x line but is not in the
  solution.
```

In the **Build and test** section, replace the test-site path and the generate-api command:

```bash
# Test site (https://localhost:44310, admin@example.com / 1234567890)
cd test-sites/Website-V18
dotnet run
```

The `npm run generate-api` block keeps its comment but now fetches
`https://localhost:44310/umbraco/openapi/search-examine-reindex.json`.

In **Gotchas**, replace:

```
- `npm run generate-api` needs the test site running on https://localhost:44310. The generated
  `src/api` folder is committed; `swagger.json` is not.
```

with:

```
- `npm run generate-api` needs the test site running on https://localhost:44310. The generated
  `src/api` folder is committed; `openapi.json` is not.
```

and in the application-part gotcha, change `run `dotnet clean` in `test-sites/Website-V17`` to
`run `dotnet clean` in `test-sites/Website-V18``.

Then add these gotchas:

```
- Umbraco 18 replaced Swashbuckle with `Microsoft.AspNetCore.OpenApi`. The document is registered
  with `builder.AddBackOfficeOpenApiDocument(name, ...)`; `WithBackOfficeAuthentication()` replaces
  `BackOfficeSecurityRequirementsOperationFilterBase`, and operation IDs come from an
  `AddOperationTransformer` callback because `IOperationIdHandler` was removed.
- A class library that registers an OpenAPI document needs
  `<InterceptorsNamespaces>$(InterceptorsNamespaces);Microsoft.AspNetCore.OpenApi.Generated</InterceptorsNamespaces>`
  in its csproj, or the build fails with `CS9137: The 'interceptors' feature is not enabled`.
- The OpenAPI document moved from `/umbraco/swagger/{name}/swagger.json` to
  `/umbraco/openapi/{name}.json`, and is now OpenAPI 3.1 rather than 3.0.
- The operation transformer is scoped to this package's document, so unlike the old global
  `OperationIdHandler` it needs no controller-namespace check.
```

If Task 3 Step 4 required the `JsonOptionsName` fix, add a gotcha describing it. If Task 3 Step 6 required bumping `@hey-api/openapi-ts`, update the TypeScript-pin gotcha to record the new generator version.

- [ ] **Step 6: Final full verification**

From a clean state, with nothing running:

```bash
cd src/code/Umbraco.Community.Search.Examine.Reindex.Client && npm ci && npm run build
cd ../../../src && dotnet restore && dotnet build && dotnet test
```

Expected: client build clean, `Build succeeded` with 0 warnings, `Passed! - Failed: 0, Passed: 25`.

Then start `test-sites/Website-V18` and re-run the E2E suite once more to confirm nothing in Task 5 disturbed it:

```bash
cd src/tests/Umbraco.Community.Search.Examine.Reindex.E2E && npm run test:e2e
```

Expected: 5 passed.

- [ ] **Step 7: Commit**

```bash
git add .github/workflows README.md CLAUDE.md
git commit -m "Update CI triggers and docs for the Umbraco 18 line"
```

---

## Completion

When all five tasks are done the branch should show: Umbraco 18 dependencies, an OpenAPI registration with no Swashbuckle references anywhere, a `Website-V18` test site in the solution, a client regenerated from an OpenAPI 3.1 document with unchanged operation IDs, a green E2E suite, and CI that still serves the `v17/develop` maintenance line.

Use the `superpowers:finishing-a-development-branch` skill to decide how to integrate the work.
