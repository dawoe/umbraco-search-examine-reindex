# Umbraco 18 upgrade — design

Date: 2026-09-16

## Goal

Make `Umbraco.Community.Search.Examine.Reindex` compatible with Umbraco 18. The headline
breaking change is that Umbraco 18 replaces Swashbuckle with `Microsoft.AspNetCore.OpenApi`
for OpenAPI document generation, which removes every API this package uses to register and
shape its Management API document.

Reference: [Umbraco 18 breaking changes](https://docs.umbraco.com/umbraco-cms/get-started/upgrading-and-migrating/version-specific).

## Branch and version strategy

- `develop` / `main` become the v18 line. This work lands on `feature/v18-version` and is
  merged into `develop`.
- `v17/develop` becomes the 17.x maintenance line and keeps receiving CI and releases.
- No multi-targeting. Umbraco 17 and 18 both target `net10.0`, so there is no TFM to pivot on,
  and the old and new OpenAPI APIs are mutually exclusive.

## Dependencies

`src/Directory.Build.props` — package version `17.0.0` → `18.0.0` for `AssemblyVersion`,
`VersionPrefix` and `InformationalVersion`.

`src/Directory.Packages.props`:

| Package | From | To |
| --- | --- | --- |
| `Umbraco.Cms.Web.Website` | `[17.0.0,18.0.0)` | `[18.0.0,19.0.0)` |
| `Umbraco.Cms.Core` | `[17.0.0,18.0.0)` | `[18.0.0,19.0.0)` |
| `Umbraco.Cms.Api.Management` | `[17.0.0,18.0.0)` | `[18.0.0,19.0.0)` |
| `Umbraco.Cms.Search.Provider.Examine` | `17.1.0-beta.1` | `18.1.0-beta.1` |

Both `packages.lock.json` files regenerate. There is no Swashbuckle `PackageReference` to
remove: it only ever arrived transitively through `Umbraco.Cms.Api.Management`, so it drops
out on its own.

`Umbraco.Cms.Search.Provider.Examine 18.1.0-beta.1` is a prerelease. This matches the posture
of the 17 line, which depends on `17.1.0-beta.1`.

## OpenAPI migration

All server-side changes are contained in
`src/code/Umbraco.Community.Search.Examine.Reindex/DependencyInjection/UmbracoBuilderExtensions.cs`.

### What is removed

| Removed in Umbraco 18 | Used by this package as |
| --- | --- |
| `IConfigureOptions<SwaggerGenOptions>` / `SwaggerDoc` | document registration |
| `BackOfficeSecurityRequirementsOperationFilterBase` | `ReindexOperationSecurityFilter` |
| `IOperationIdHandler` / `OperationIdHandler` | `ReindexOperationIdHandler` |

Both nested classes are deleted. The `Swashbuckle.AspNetCore.SwaggerGen`, `Microsoft.OpenApi`,
`Asp.Versioning`, `Microsoft.AspNetCore.Mvc.ApiExplorer`,
`Microsoft.AspNetCore.Mvc.Controllers` and `Microsoft.Extensions.Options` usings become unused
and are removed.

### What replaces it

A single builder call inside `AddSearchExamineReindex`:

```csharp
builder.AddBackOfficeOpenApiDocument(
    Constants.Api.Name,
    document => document
        .WithTitle("Umbraco Search Examine Reindex API")
        .WithBackOfficeAuthentication()
        .ConfigureOpenApiOptions(options =>
            options.AddOperationTransformer((operation, context, _) =>
            {
                var actionName = $"{context.Description.ActionDescriptor.RouteValues["action"]}";
                operation.OperationId = actionName.Length == 0
                    ? actionName
                    : string.Concat(char.ToLowerInvariant(actionName[0]), actionName[1..]);
                return Task.CompletedTask;
            })));
```

Consequences:

- `WithBackOfficeAuthentication()` registers the backoffice OAuth2 security scheme and marks
  operations as requiring authentication, replacing the security operation filter entirely.
- The operation transformer is registered on **this document's** options, so it only ever sees
  this package's operations. The namespace-based `CanHandle` check that `OperationIdHandler`
  needed as a global singleton is no longer necessary, and `Constants.Api.ControllerNamespace`
  becomes unused — it is removed along with it.
- Operation IDs stay byte-identical to the 17 line (`reindex`, `getReindexStatus`). This is a
  deliberate choice: it keeps the generated TypeScript SDK's function names stable, so any diff
  in `src/api` after regeneration is attributable to the OpenAPI 3.1 switch alone rather than
  to renaming.
- `WithTitle(...)` replaces `SwaggerDoc(...)` / `OpenApiInfo`.

The existing idempotency guard at the top of `AddSearchExamineReindex` (an early return when
`IIndexContentReindexer` is already registered) stays, and now also guards the document
registration.

### Project file

`Umbraco.Community.Search.Examine.Reindex.csproj` gains:

```xml
<InterceptorsNamespaces>$(InterceptorsNamespaces);Microsoft.AspNetCore.OpenApi.Generated</InterceptorsNamespaces>
```

The OpenAPI source generator propagates through project references but is not auto-enabled in
class libraries. Without this property the build fails with
`error CS9137: The 'interceptors' feature is not enabled`.

### Controllers

`[ApiController]`, `[BackOfficeRoute]`, `[Authorize]`, `[MapToApi]` and `[ApiVersion("1.0")]`
all stay as they are. Two points are resolved by inspection during implementation rather than
assumed up front:

1. `[ApiExplorerSettings(GroupName = "Umbraco Search Examine Reindex")]` on
   `ReindexApiControllerBase` was a Swashbuckle grouping mechanism. Document membership now
   comes from `[MapToApi]`. The default is to remove it; it is kept only if removing it
   demonstrably changes the generated document.
2. If `ManagementApiControllerBase` does not already apply the backoffice JSON serialization
   options, the base class gains `[JsonOptionsName(Constants.JsonOptionsNames.BackOffice)]` and
   the document gains `.WithJsonOptions(Constants.JsonOptionsNames.BackOffice)`, so the
   generated schema matches runtime serialization.

Either way the decision is recorded in the implementation plan, not left open.

### Unit tests

The 25 NUnit tests exercise the services and the controller directly and touch no OpenAPI
plumbing. They are expected to pass unchanged. Any test change would be a signal that
something unintended moved.

## Test site

A new `test-sites/Website-V18` is added. `test-sites/Website-V17` stays on disk but is removed
from the solution.

The V18 site mirrors the V17 site so that nothing downstream has to change:

- `net10.0`, `RootNamespace` `Website_V18`, same csproj shape: `CompressionEnabled=false`,
  `CopyRazorGenerateFilesToPublishDirectory=true`, `RazorCompileOnBuild/Publish=false`,
  app-local ICU.
- `Umbraco.Cms` and `Umbraco.Cms.DevelopmentMode.Backoffice` 18.1.1, `Clean` 8.0.1,
  `Umbraco.Cms.Search.Core` and `Umbraco.Cms.Search.BackOffice` 18.1.0,
  `Umbraco.Cms.Search.Provider.Examine` 18.1.0-beta.1, and a `ProjectReference` to the package
  project.
- `WebsiteComposer` is unchanged apart from its namespace:
  `AddSearchCore().AddBackOfficeSearch().AddExamineSearchProvider()`.
- `Program.cs` is unchanged.
- The same `appsettings.json` and `appsettings.Development.json`: SQLite, unattended install as
  `admin@example.com` / `1234567890`.
- **The same port, 44310.** Only one test site runs at a time, so sharing the port costs
  nothing and leaves the E2E `.env`, `playwright.config.ts` and the client's `generate-api`
  script working untouched.
- Views are not copied from the V17 site. Clean 8 installs its own.
- The site-level `.gitignore` carries over (SQLite database, `umbraco/Data/TEMP/`, logs, media).

In `src/Umbraco.Community.Search.Examine.Reindex.slnx`, the
`<Project Path="../test-sites/Website-V17/Website-V17.csproj">` entry is replaced by the V18
equivalent, keeping the `<Build Solution="Release|*" Project="false" />` guard so CI still
skips building it.

## Client

The spec endpoint moved from `/umbraco/swagger/{documentName}/swagger.json` to
`/umbraco/openapi/{documentName}.json`, so `generate-api` becomes:

```
curl -k -o openapi.json https://localhost:44310/umbraco/openapi/search-examine-reindex.json && openapi-ts
```

The downloaded file is renamed `swagger.json` → `openapi.json`, with matching updates to
`openapi-ts.config.ts` (`input`) and the client `.gitignore`. The file is not committed, so the
rename is free.

`@umbraco-cms/backoffice` and `@umbraco-cms/search` bump to 18.x. `src/api` is regenerated
against the OpenAPI **3.1** document and committed.

`@hey-api/openapi-ts` stays at `^0.85.2` unless it mishandles OpenAPI 3.1, in which case it is
bumped. Any bump must respect the TypeScript 6.0 pin documented in CLAUDE.md: the generator
cannot run on TypeScript 7, and TypeScript 5.8/5.9 fail `tsc --noEmit` on the generated client.

`tsc && vite build` must stay clean. `src/repository/reindex.repository.ts` and
`src/detailboxes/reindex-detail-box.element.ts` are expected to need no edits, because the
operation IDs are unchanged.

## E2E

`@umbraco/playwright-testhelpers` bumps to 18.0.5. The suite runs against the V18 site on the
existing URL and credentials.

Two assumptions are re-verified on 18 rather than carried over: the `data-mark` selectors used
by the specs, and the "Search Index Rebuild Completed" toast text that the rebuild spec waits
for. The existing decision not to assert an intermediate `Running` status stands — small sites
finish before the first poll.

## CI

- `pr-validation.yml`: add `v17/**` to the `branches:` filter alongside `develop`.
- `beta-release.yml`: add `v17/develop` to the push branches so the 17 line keeps producing
  beta packages.
- `release.yml`: relax the `if: github.ref == 'refs/heads/main'` guard so a manual
  `workflow_dispatch` from `v17/develop` can also publish a 17 patch release.

No other pipeline changes. Both workflows already build the client before `dotnet build` and
run on `dotnet-version: 10.x`.

## Documentation

- `README.md`: "Requires Umbraco 17 with Umbraco Search Core and the Examine provider
  configured" → Umbraco 18, with a note that the 17.x line lives on `v17/develop`.
- `CLAUDE.md`: update the test-site path and the `generate-api` URL, record the
  `InterceptorsNamespaces` requirement, and replace the Swashbuckle-era gotchas with their
  Umbraco 18 equivalents.
- `umbraco-marketplace.json` carries no version information and is unchanged.

## Verification

Each step gates the next:

1. `dotnet build` in `src/` succeeds with no analyzer warnings.
2. `dotnet test` — 25 unit tests green, unchanged.
3. The V18 test site boots on https://localhost:44310 and installs unattended.
4. `GET /umbraco/openapi/search-examine-reindex.json` returns a document containing the
   `reindex` and `getReindexStatus` operation IDs, with the backoffice security scheme applied.
5. `npm run generate-api` followed by `npm run build` succeeds; the `src/api` diff is reviewed
   and explained.
6. The Playwright suite passes: 1 auth setup plus 4 specs.

## Risks

**OpenAPI 3.1 output.** The most likely source of surprise in the generated client, especially
nullable handling (`type: [x, "null"]` instead of `nullable: true`). Mitigated by keeping
operation IDs stable so the diff is readable, and by reviewing the `src/api` diff explicitly
rather than accepting it wholesale.

**The Examine provider prerelease.** `18.1.0-beta.1` is the piece most likely to have moved its
own public API. If it has breaking changes beyond the OpenAPI migration, that is a scope
expansion to be flagged, not absorbed silently.

**Cold application-part discovery.** A brand-new test site means a cold `project.assets.json`.
Per the existing CLAUDE.md gotcha, MVC application-part discovery is incremental, so the
package's controllers may 404 until a `dotnet clean` and full rebuild in `test-sites/Website-V18`.
This is not to be worked around with `AddControllers().AddApplicationPart(...)`.

## Out of scope

- Any change to the reindex behaviour, the detail box UI, or the public C# API surface.
- Backporting anything to the 17 line beyond the CI trigger changes.
- Refactoring unrelated to the upgrade.
