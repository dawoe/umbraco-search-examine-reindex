# Reindex Detail Box Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a detail box to the Umbraco Search index workspace that reindexes all content of the current Examine-backed index (cache flush + in-place refresh), optionally followed by a full index rebuild, with a polled status endpoint.

**Architecture:** A C# singleton `IndexContentReindexer` queues a background job that pages through the index's content, media and members and calls Umbraco Search's public `IDistributedContentIndexRefresher` (reindex) or `IIndexDocumentService` + `IDistributedContentIndexRebuilder` (rebuild). Progress lives in an in-memory `ReindexStatusTracker` exposed through a small Management API. A Lit detail box element in the client calls the API through a generated hey-api client and a repository, and polls the status endpoint.

**Tech Stack:** .NET 10, Umbraco CMS 17.1, Umbraco.Cms.Search 17.1 (Core + Examine provider), NUnit 4 + Moq, TypeScript + Lit + Vite, `@umbraco-cms/backoffice` 17, `@umbraco-cms/search` 17.1 types, `@hey-api/openapi-ts`, Playwright + `@umbraco/playwright-testhelpers`.

Spec: `docs/superpowers/specs/2026-09-15-reindex-detail-box-design.md`

## Global Constraints

- C# follows `src/.editorconfig` and `src/stylecop.json`: usings outside namespace, file-scoped namespaces, expression-bodied members where possible, `var` for built-in and apparent types, braces always, XML docs on all public and internal members, every file starts with the StyleCop header below.
- StyleCop header (company `Umbraco community`, copyright text verbatim):
  ```csharp
  // <copyright file="{FileName}.cs" company="Umbraco community">
  // Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
  // </copyright>
  ```
- Nullable enabled, `WarningsAsErrors` for nullable. Do not add `#pragma` suppressions.
- Package versions are managed centrally in `src/Directory.Packages.props`. Do not put versions in `.csproj` files.
- API name and route base: `search-examine-reindex`, `[BackOfficeRoute("search-examine-reindex/api/v{version:apiVersion}")]`, policy `AuthorizationPolicies.SectionAccessSettings`.
- Client element tag: `search-examine-reindex-detail-box`. Localization dictionary key: `searchExamineReindex`. `data-mark` attributes: `search-examine-reindex:toggle-rebuild`, `search-examine-reindex:button-reindex`, `search-examine-reindex:progress`.
- Detail box condition: `{ alias: 'Umb.Search.Condition.IndexProviderName', match: 'search-examine-provider' }`.
- Test site: `test-sites/Website-V17`, URL `https://localhost:44310`, admin `admin@example.com` / `1234567890` (unattended install).
- Commit after every task with a short imperative message and the trailer `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.
- Run commands from the repo root `C:\forks\umbraco-search-examine-reindex` unless a step says otherwise.
- Reference sources for lookups only: `C:\forks\Umbraco.Cms.Search` (Search), `C:\forks\Umbraco-CMS-release-17.4.2\Umbraco-CMS-release-17.4.2` (CMS).

---

## File structure

Server (`src/code/Umbraco.Community.Search.Examine.Reindex/`):

| File | Responsibility |
|------|----------------|
| `Constants.cs` | API name and controller namespace prefix |
| `Models/ReindexState.cs` | `Idle`, `Running`, `Failed` enum (string JSON) |
| `Models/ReindexOperationStatus.cs` | `Success`, `IndexNotFound`, `AlreadyRunning` enum for `Attempt` |
| `Models/ReindexStatus.cs` | Status record returned by the API |
| `Models/ReindexRequestModel.cs` | POST body |
| `Services/IReindexStatusTracker.cs`, `Services/ReindexStatusTracker.cs` | Thread-safe per-alias status |
| `Services/ContentBatch.cs`, `Services/IIndexContentEnumerator.cs`, `Services/IndexContentEnumerator.cs` | Paging through documents, media, members |
| `Services/IIndexContentReindexer.cs`, `Services/IndexContentReindexer.cs` | Start + background job |
| `Controllers/ReindexApiControllerBase.cs`, `Controllers/ReindexApiController.cs` | Management API |
| `DependencyInjection/UmbracoBuilderExtensions.cs` | `AddSearchExamineReindex()` |
| `Composing/SearchExamineReindexComposer.cs` | Auto registration |

Tests (`src/tests/Umbraco.Community.Search.Examine.Reindex.Tests/`): `Services/ReindexStatusTrackerTests.cs`, `Services/IndexContentEnumeratorTests.cs`, `Services/IndexContentReindexerTests.cs`, `Controllers/ReindexApiControllerTests.cs`.

Client (`src/code/Umbraco.Community.Search.Examine.Reindex.Client/src/`): `api/` (generated), `hey-api.ts`, `types.ts`, `repository/reindex.repository.ts`, `lang/en.ts`, `lang/manifests.ts`, `detailboxes/reindex-detail-box.element.ts`, `detailboxes/manifests.ts`, `manifests.ts`.

E2E (`src/tests/Umbraco.Community.Search.Examine.Reindex.E2E/`): `package.json`, `playwright.config.ts`, `tests/auth.setup.ts`, `tests/reindex-detail-box.spec.ts`, `.env.example`, `.gitignore`.

---

### Task 1: Repository housekeeping and CLAUDE.md skeleton

**Files:**
- Modify: `src/Directory.Build.props`
- Create: `src/assets/logo.png`, `src/assets/readme.md`
- Modify: `.gitignore`
- Create: `CLAUDE.md`

**Interfaces:** none.

- [ ] **Step 1: Fix package metadata in `src/Directory.Build.props`**

Replace the two URL lines:

```xml
      <PackageProjectUrl>https://github.com/dawoe/umbraco-search-examine-reindex</PackageProjectUrl>
      <RepositoryUrl>https://github.com/dawoe/umbraco-search-examine-reindex</RepositoryUrl>
```

and change the `PackageTags` line to:

```xml
      <PackageTags>umbraco;umbraco-marketplace;search;examine</PackageTags>
```

- [ ] **Step 2: Create the NuGet readme `src/assets/readme.md`**

```markdown
# Umbraco.Community.Search.Examine.Reindex

Adds a **Reindex** box to the index details page of Umbraco Search (Examine provider) in the
Umbraco backoffice.

- **Reindex**: flushes the cached index values for every item in the index and re-collects them
  from the content, so changes to property value handlers and custom content indexers are applied.
  The index stays searchable while this runs.
- **Also rebuild the index**: flushes the cache and then triggers Umbraco Search's index rebuild,
  which wipes and repopulates the Examine index.

## Installation

```
dotnet add package Umbraco.Community.Search.Examine.Reindex
```

The package registers itself through a composer. It requires Umbraco Search Core and the Examine
provider to be configured (`AddSearchCore()` and `AddExamineSearchProvider()`).

## Usage

Settings → Advanced → Search → pick an index → the **Reindex** box on the right.
```

- [ ] **Step 3: Create a placeholder logo `src/assets/logo.png`**

Run in PowerShell from the repo root (replace with a real logo later):

```powershell
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap 128,128
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(255,58,63,129))
$font = New-Object System.Drawing.Font 'Segoe UI',48,[System.Drawing.FontStyle]::Bold
$g.DrawString('R',$font,[System.Drawing.Brushes]::White,30,24)
$bmp.Save("$PWD\src\assets\logo.png",[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
```

Expected: `src/assets/logo.png` exists (about 1 KB).

- [ ] **Step 4: Extend `.gitignore`**

Append:

```
node_modules/
.env
.auth/
test-results/
playwright-report/
src/code/Umbraco.Community.Search.Examine.Reindex.Client/swagger.json
```

- [ ] **Step 5: Create `CLAUDE.md` at the repo root**

```markdown
# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project overview

`Umbraco.Community.Search.Examine.Reindex` is an add-on for Umbraco Search (the new search
abstraction for Umbraco CMS 17+) with the Examine provider. It adds a **Reindex** detail box to
the index workspace in the backoffice. Reindex flushes the database cache of index values and
re-collects them from content; the optional rebuild flushes the cache and triggers Umbraco
Search's own index rebuild.

Design spec: `docs/superpowers/specs/2026-09-15-reindex-detail-box-design.md`.
Implementation plan: `docs/superpowers/plans/2026-09-15-reindex-detail-box.md`.

## Solution layout

- `src/Umbraco.Community.Search.Examine.Reindex.slnx` — solution (slnx format)
- `src/code/Umbraco.Community.Search.Examine.Reindex/` — NuGet package (Razor SDK project, ships
  `wwwroot/App_Plugins/searchreindex` built by the client)
- `src/code/Umbraco.Community.Search.Examine.Reindex.Client/` — backoffice client (TypeScript,
  Lit, Vite). Output goes to the package's `wwwroot/App_Plugins/searchreindex` (gitignored)
- `src/tests/Umbraco.Community.Search.Examine.Reindex.Tests/` — NUnit + Moq unit tests
- `src/tests/Umbraco.Community.Search.Examine.Reindex.E2E/` — Playwright end-to-end tests
- `test-sites/Website-V17/` — Umbraco 17 test site referencing the package project
- `src/assets/` — NuGet readme and icon

## Build and test

```bash
# Client (must run before dotnet build so App_Plugins exists)
cd src/code/Umbraco.Community.Search.Examine.Reindex.Client
npm ci
npm run build

# .NET
cd src
dotnet restore
dotnet build
dotnet test

# Test site (https://localhost:44310, admin@example.com / 1234567890)
cd test-sites/Website-V17
dotnet run

# Regenerate the TypeScript API client (test site must be running)
cd src/code/Umbraco.Community.Search.Examine.Reindex.Client
npm run generate-api

# E2E tests (test site must be running)
cd src/tests/Umbraco.Community.Search.Examine.Reindex.E2E
npm ci && npx playwright install chromium
npm run test:e2e
```

## Coding conventions

### C#

- `src/.editorconfig` and `src/stylecop.json` are authoritative. Analyzer warnings must be fixed,
  not suppressed.
- Every file starts with the StyleCop header:
  `// <copyright file="X.cs" company="Umbraco community">` / copyright line / `// </copyright>`.
- Usings outside the namespace, file-scoped namespaces, expression-bodied members where possible,
  `var` when the type is built-in or apparent, braces always.
- XML documentation on all public and internal members (`documentInternalElements` is on).
- Service registration through `IUmbracoBuilder` extension methods plus an `IComposer`.
- Only depend on public Umbraco Search APIs (`Umbraco.Cms.Search.Core.Services.ContentIndexing`).

### Client

- Follow the Umbraco Search client patterns: repository → server data via generated hey-api
  client, `tryExecute` for errors, `UmbLitElement` elements, localization through
  `this.localize.term('searchExamineReindex_...')`.
- Types for Umbraco Search come from the `@umbraco-cms/search` npm package (`/global`,
  `/settings`). At runtime they resolve through Umbraco Search's importmap, so they stay
  external in the Vite build.
- Interactive elements carry `data-mark` attributes for E2E selectors.

## Reference sources on disk

- Umbraco Search: `C:\forks\Umbraco.Cms.Search` (see its `CLAUDE.md`)
- Umbraco CMS 17.4.2: `C:\forks\Umbraco-CMS-release-17.4.2\Umbraco-CMS-release-17.4.2`

## Gotchas

- Umbraco Search's built-in "Rebuild" repopulates the Examine index from the **database cache**
  of index documents (`ContentIndexingDataCollectionService.CollectAsync` reads the cache first).
  Only flushing the cache makes handler changes visible. That is the reason this package exists.
- (add entries here as implementation reveals them)
```

- [ ] **Step 6: Verify the solution still builds**

Run: `cd src && dotnet build`
Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "Fix package metadata, add assets, gitignore and CLAUDE.md

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: Models and status tracker

**Files:**
- Create: `src/code/Umbraco.Community.Search.Examine.Reindex/Constants.cs`
- Create: `src/code/Umbraco.Community.Search.Examine.Reindex/Models/ReindexState.cs`
- Create: `src/code/Umbraco.Community.Search.Examine.Reindex/Models/ReindexOperationStatus.cs`
- Create: `src/code/Umbraco.Community.Search.Examine.Reindex/Models/ReindexStatus.cs`
- Create: `src/code/Umbraco.Community.Search.Examine.Reindex/Models/ReindexRequestModel.cs`
- Create: `src/code/Umbraco.Community.Search.Examine.Reindex/Services/IReindexStatusTracker.cs`
- Create: `src/code/Umbraco.Community.Search.Examine.Reindex/Services/ReindexStatusTracker.cs`
- Test: `src/tests/Umbraco.Community.Search.Examine.Reindex.Tests/Services/ReindexStatusTrackerTests.cs`

**Interfaces:**
- Produces:
  - `enum ReindexState { Idle, Running, Failed }`
  - `enum ReindexOperationStatus { Success, IndexNotFound, AlreadyRunning }`
  - `record ReindexStatus(string IndexAlias, ReindexState State, bool RebuildIndex, long ProcessedItems, long? TotalItems, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, string? ErrorMessage)` with `static ReindexStatus Idle(string indexAlias)`
  - `class ReindexRequestModel { bool RebuildIndex }`
  - `IReindexStatusTracker`: `ReindexStatus Get(string indexAlias)`, `bool TryStart(ReindexStatus status)`, `void Update(ReindexStatus status)`
  - `Constants.Api.Name = "search-examine-reindex"`, `Constants.Api.ControllerNamespace`

- [ ] **Step 1: Write the failing tests**

`src/tests/Umbraco.Community.Search.Examine.Reindex.Tests/Services/ReindexStatusTrackerTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd src && dotnet test --filter "FullyQualifiedName~ReindexStatusTrackerTests"`
Expected: build errors about missing `ReindexStatusTracker`, `ReindexStatus`, `ReindexState`.

- [ ] **Step 3: Create `Constants.cs`**

```csharp
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
```

- [ ] **Step 4: Create the models**

`Models/ReindexState.cs`:

```csharp
// <copyright file="ReindexState.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Text.Json.Serialization;

namespace Umbraco.Community.Search.Examine.Reindex.Models;

/// <summary>
/// The state of a reindex operation for an index.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReindexState
{
    /// <summary>
    /// No reindex is running.
    /// </summary>
    Idle,

    /// <summary>
    /// A reindex is running.
    /// </summary>
    Running,

    /// <summary>
    /// The last reindex failed.
    /// </summary>
    Failed,
}
```

`Models/ReindexOperationStatus.cs`:

```csharp
// <copyright file="ReindexOperationStatus.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Umbraco.Community.Search.Examine.Reindex.Models;

/// <summary>
/// The outcome of a request to start a reindex.
/// </summary>
public enum ReindexOperationStatus
{
    /// <summary>
    /// The reindex was started.
    /// </summary>
    Success,

    /// <summary>
    /// No content index is registered for the given alias.
    /// </summary>
    IndexNotFound,

    /// <summary>
    /// A reindex is already running for the given alias.
    /// </summary>
    AlreadyRunning,
}
```

`Models/ReindexStatus.cs`:

```csharp
// <copyright file="ReindexStatus.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Umbraco.Community.Search.Examine.Reindex.Models;

/// <summary>
/// The status of a reindex operation for a single index.
/// </summary>
/// <param name="IndexAlias">The index alias.</param>
/// <param name="State">The current state.</param>
/// <param name="RebuildIndex">Whether the operation also rebuilds the index.</param>
/// <param name="ProcessedItems">The number of items processed so far.</param>
/// <param name="TotalItems">The total number of items, when known.</param>
/// <param name="StartedAt">When the operation started.</param>
/// <param name="CompletedAt">When the operation completed.</param>
/// <param name="ErrorMessage">The error message when the operation failed.</param>
public sealed record ReindexStatus(
    string IndexAlias,
    ReindexState State,
    bool RebuildIndex,
    long ProcessedItems,
    long? TotalItems,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage)
{
    /// <summary>
    /// Creates an idle status for the given index alias.
    /// </summary>
    /// <param name="indexAlias">The index alias.</param>
    /// <returns>An idle status.</returns>
    public static ReindexStatus Idle(string indexAlias)
        => new(indexAlias, ReindexState.Idle, false, 0, null, null, null, null);
}
```

`Models/ReindexRequestModel.cs`:

```csharp
// <copyright file="ReindexRequestModel.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Umbraco.Community.Search.Examine.Reindex.Models;

/// <summary>
/// Request body for starting a reindex.
/// </summary>
public sealed class ReindexRequestModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the index should be rebuilt after flushing the cache.
    /// </summary>
    public bool RebuildIndex { get; set; }
}
```

- [ ] **Step 5: Create the tracker**

`Services/IReindexStatusTracker.cs`:

```csharp
// <copyright file="IReindexStatusTracker.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Umbraco.Community.Search.Examine.Reindex.Models;

namespace Umbraco.Community.Search.Examine.Reindex.Services;

/// <summary>
/// Tracks the reindex status per index alias (in memory, per server instance).
/// </summary>
public interface IReindexStatusTracker
{
    /// <summary>
    /// Gets the status for an index alias. Unknown aliases report <see cref="ReindexState.Idle"/>.
    /// </summary>
    /// <param name="indexAlias">The index alias.</param>
    /// <returns>The status.</returns>
    ReindexStatus Get(string indexAlias);

    /// <summary>
    /// Stores the status unless a reindex is already running for the alias.
    /// </summary>
    /// <param name="status">The running status to store.</param>
    /// <returns><c>true</c> when stored, <c>false</c> when a reindex is already running.</returns>
    bool TryStart(ReindexStatus status);

    /// <summary>
    /// Overwrites the status for the alias.
    /// </summary>
    /// <param name="status">The status.</param>
    void Update(ReindexStatus status);
}
```

`Services/ReindexStatusTracker.cs`:

```csharp
// <copyright file="ReindexStatusTracker.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Collections.Concurrent;
using Umbraco.Community.Search.Examine.Reindex.Models;

namespace Umbraco.Community.Search.Examine.Reindex.Services;

/// <inheritdoc />
internal sealed class ReindexStatusTracker : IReindexStatusTracker
{
    private readonly ConcurrentDictionary<string, ReindexStatus> statuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock startLock = new();

    /// <inheritdoc />
    public ReindexStatus Get(string indexAlias)
        => this.statuses.TryGetValue(indexAlias, out ReindexStatus? status) ? status : ReindexStatus.Idle(indexAlias);

    /// <inheritdoc />
    public bool TryStart(ReindexStatus status)
    {
        lock (this.startLock)
        {
            if (this.Get(status.IndexAlias).State == ReindexState.Running)
            {
                return false;
            }

            this.statuses[status.IndexAlias] = status;
            return true;
        }
    }

    /// <inheritdoc />
    public void Update(ReindexStatus status)
        => this.statuses[status.IndexAlias] = status;
}
```

Note: `src/.editorconfig` sets `dotnet_style_qualification_for_field/property/method/event = true:warning`, and StyleCop SA1101 is active. Always write `this.` when accessing instance members, in production and test code.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `cd src && dotnet test --filter "FullyQualifiedName~ReindexStatusTrackerTests"`
Expected: `Passed! - Failed: 0, Passed: 5`. Also run `dotnet build` and confirm there are no analyzer warnings for the new files (fix any that appear).

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "Add reindex models and status tracker

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: Index content enumerator

**Files:**
- Create: `src/code/Umbraco.Community.Search.Examine.Reindex/Services/ContentBatch.cs`
- Create: `src/code/Umbraco.Community.Search.Examine.Reindex/Services/IIndexContentEnumerator.cs`
- Create: `src/code/Umbraco.Community.Search.Examine.Reindex/Services/IndexContentEnumerator.cs`
- Test: `src/tests/Umbraco.Community.Search.Examine.Reindex.Tests/Services/IndexContentEnumeratorTests.cs`

**Interfaces:**
- Produces:
  - `internal sealed record ContentBatch(IContentBase[] Items, long Total)`
  - `internal interface IIndexContentEnumerator { IEnumerable<ContentBatch> Enumerate(UmbracoObjectTypes objectType); }`
  - `IndexContentEnumerator(IContentService, IMediaService, IMemberService)`, `internal const int PageSize = 500`

- [ ] **Step 1: Write the failing tests**

`src/tests/Umbraco.Community.Search.Examine.Reindex.Tests/Services/IndexContentEnumeratorTests.cs`:

```csharp
// <copyright file="IndexContentEnumeratorTests.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Persistence.DatabaseModelDefinitions;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.Search.Examine.Reindex.Services;

namespace Umbraco.Community.Search.Examine.Reindex.Tests.Services;

/// <summary>
/// Tests for <see cref="IndexContentEnumerator"/>.
/// </summary>
[TestFixture]
public class IndexContentEnumeratorTests
{
    private delegate IEnumerable<IContent> GetPagedContent(int id, long pageIndex, int pageSize, out long total, IQuery<IContent>? filter, Ordering? ordering);

    private delegate IEnumerable<IMedia> GetPagedMedia(int id, long pageIndex, int pageSize, out long total, IQuery<IMedia>? filter, Ordering? ordering);

    private delegate IEnumerable<IMember> GetPagedMembers(long pageIndex, int pageSize, out long total, string orderBy, Direction direction, bool orderBySystemField, string? memberTypeAlias, string filter);

    /// <summary>
    /// Documents are enumerated from the root in pages until the total is reached.
    /// </summary>
    [Test]
    public void Enumerate_Documents_PagesUntilTotal()
    {
        var page0 = Enumerable.Range(0, IndexContentEnumerator.PageSize).Select(_ => Mock.Of<IContent>()).ToArray();
        var page1 = new[] { Mock.Of<IContent>(), Mock.Of<IContent>() };
        var total = page0.Length + page1.Length;
        var contentService = new Mock<IContentService>();
        contentService
            .Setup(s => s.GetPagedDescendants(-1, It.IsAny<long>(), IndexContentEnumerator.PageSize, out It.Ref<long>.IsAny, null, null))
            .Returns(new GetPagedContent((int _, long pageIndex, int _, out long t, IQuery<IContent>? _, Ordering? _) =>
            {
                t = total;
                return pageIndex == 0 ? page0 : page1;
            }));
        IndexContentEnumerator sut = CreateSut(contentService: contentService);

        ContentBatch[] batches = sut.Enumerate(UmbracoObjectTypes.Document).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(batches, Has.Length.EqualTo(2));
            Assert.That(batches[0].Items, Has.Length.EqualTo(IndexContentEnumerator.PageSize));
            Assert.That(batches[0].Total, Is.EqualTo(total));
            Assert.That(batches[1].Items, Has.Length.EqualTo(2));
        });
    }

    /// <summary>
    /// An empty result yields no batches.
    /// </summary>
    [Test]
    public void Enumerate_Documents_Empty_YieldsNothing()
    {
        var contentService = new Mock<IContentService>();
        contentService
            .Setup(s => s.GetPagedDescendants(-1, It.IsAny<long>(), IndexContentEnumerator.PageSize, out It.Ref<long>.IsAny, null, null))
            .Returns(new GetPagedContent((int _, long _, int _, out long t, IQuery<IContent>? _, Ordering? _) =>
            {
                t = 0;
                return [];
            }));
        IndexContentEnumerator sut = CreateSut(contentService: contentService);

        Assert.That(sut.Enumerate(UmbracoObjectTypes.Document), Is.Empty);
    }

    /// <summary>
    /// Media is enumerated from the media root.
    /// </summary>
    [Test]
    public void Enumerate_Media_UsesMediaService()
    {
        var items = new[] { Mock.Of<IMedia>() };
        var mediaService = new Mock<IMediaService>();
        mediaService
            .Setup(s => s.GetPagedDescendants(-1, It.IsAny<long>(), IndexContentEnumerator.PageSize, out It.Ref<long>.IsAny, null, null))
            .Returns(new GetPagedMedia((int _, long _, int _, out long t, IQuery<IMedia>? _, Ordering? _) =>
            {
                t = 1;
                return items;
            }));
        IndexContentEnumerator sut = CreateSut(mediaService: mediaService);

        ContentBatch[] batches = sut.Enumerate(UmbracoObjectTypes.Media).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(batches, Has.Length.EqualTo(1));
            Assert.That(batches[0].Items, Is.EqualTo(items));
        });
    }

    /// <summary>
    /// Members are enumerated with the paged GetAll overload.
    /// </summary>
    [Test]
    public void Enumerate_Members_UsesMemberService()
    {
        var items = new[] { Mock.Of<IMember>(), Mock.Of<IMember>() };
        var memberService = new Mock<IMemberService>();
        memberService
            .Setup(s => s.GetAll(It.IsAny<long>(), IndexContentEnumerator.PageSize, out It.Ref<long>.IsAny, It.IsAny<string>(), It.IsAny<Direction>(), It.IsAny<bool>(), null, It.IsAny<string>()))
            .Returns(new GetPagedMembers((long _, int _, out long t, string _, Direction _, bool _, string? _, string _) =>
            {
                t = 2;
                return items;
            }));
        IndexContentEnumerator sut = CreateSut(memberService: memberService);

        ContentBatch[] batches = sut.Enumerate(UmbracoObjectTypes.Member).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(batches, Has.Length.EqualTo(1));
            Assert.That(batches[0].Total, Is.EqualTo(2));
        });
    }

    /// <summary>
    /// Unsupported object types yield nothing.
    /// </summary>
    [Test]
    public void Enumerate_UnsupportedType_YieldsNothing()
    {
        IndexContentEnumerator sut = CreateSut();

        Assert.That(sut.Enumerate(UmbracoObjectTypes.DataType), Is.Empty);
    }

    private static IndexContentEnumerator CreateSut(
        Mock<IContentService>? contentService = null,
        Mock<IMediaService>? mediaService = null,
        Mock<IMemberService>? memberService = null)
        => new(
            (contentService ?? new Mock<IContentService>()).Object,
            (mediaService ?? new Mock<IMediaService>()).Object,
            (memberService ?? new Mock<IMemberService>()).Object);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd src && dotnet test --filter "FullyQualifiedName~IndexContentEnumeratorTests"`
Expected: build errors about missing `IndexContentEnumerator` and `ContentBatch`.

- [ ] **Step 3: Create `ContentBatch.cs` and the interface**

`Services/ContentBatch.cs`:

```csharp
// <copyright file="ContentBatch.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Umbraco.Cms.Core.Models;

namespace Umbraco.Community.Search.Examine.Reindex.Services;

/// <summary>
/// One page of content items of a single object type.
/// </summary>
/// <param name="Items">The items in this page.</param>
/// <param name="Total">The total number of items of this object type.</param>
internal sealed record ContentBatch(IContentBase[] Items, long Total);
```

`Services/IIndexContentEnumerator.cs`:

```csharp
// <copyright file="IIndexContentEnumerator.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Umbraco.Cms.Core.Models;

namespace Umbraco.Community.Search.Examine.Reindex.Services;

/// <summary>
/// Enumerates all content items of an object type in pages.
/// </summary>
internal interface IIndexContentEnumerator
{
    /// <summary>
    /// Enumerates all items of the given object type. Only documents, media and members are supported;
    /// other object types yield nothing.
    /// </summary>
    /// <param name="objectType">The object type.</param>
    /// <returns>Pages of items.</returns>
    IEnumerable<ContentBatch> Enumerate(UmbracoObjectTypes objectType);
}
```

- [ ] **Step 4: Create `IndexContentEnumerator.cs`**

```csharp
// <copyright file="IndexContentEnumerator.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Persistence.DatabaseModelDefinitions;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Community.Search.Examine.Reindex.Services;

/// <inheritdoc />
internal sealed class IndexContentEnumerator : IIndexContentEnumerator
{
    /// <summary>
    /// The number of items per page.
    /// </summary>
    internal const int PageSize = 500;

    private readonly IContentService contentService;
    private readonly IMediaService mediaService;
    private readonly IMemberService memberService;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexContentEnumerator"/> class.
    /// </summary>
    /// <param name="contentService">The content service.</param>
    /// <param name="mediaService">The media service.</param>
    /// <param name="memberService">The member service.</param>
    public IndexContentEnumerator(IContentService contentService, IMediaService mediaService, IMemberService memberService)
    {
        this.contentService = contentService;
        this.mediaService = mediaService;
        this.memberService = memberService;
    }

    private delegate IEnumerable<IContentBase> GetPage(long pageIndex, out long total);

    /// <inheritdoc />
    public IEnumerable<ContentBatch> Enumerate(UmbracoObjectTypes objectType)
        => objectType switch
        {
            UmbracoObjectTypes.Document => Page((long pageIndex, out long total) => this.contentService.GetPagedDescendants(Cms.Core.Constants.System.Root, pageIndex, PageSize, out total)),
            UmbracoObjectTypes.Media => Page((long pageIndex, out long total) => this.mediaService.GetPagedDescendants(Cms.Core.Constants.System.Root, pageIndex, PageSize, out total)),
            UmbracoObjectTypes.Member => Page((long pageIndex, out long total) => this.memberService.GetAll(pageIndex, PageSize, out total, "LoginName", Direction.Ascending, true, null, string.Empty)),
            _ => [],
        };

    private static IEnumerable<ContentBatch> Page(GetPage getPage)
    {
        long pageIndex = 0;
        long total;
        do
        {
            IContentBase[] items = getPage(pageIndex, out total).ToArray();
            if (items.Length == 0)
            {
                yield break;
            }

            yield return new ContentBatch(items, total);
            pageIndex++;
        }
        while (pageIndex * PageSize < total);
    }
}
```

Note: the generic `IEnumerable<IContent>` returned by the services converts to `IEnumerable<IContentBase>` through covariance, so the lambdas compile against the `GetPage` delegate (lambdas with an `out` parameter need explicit types on every parameter). The `Constants.System.Root` constant is `-1`; the fully qualified `Cms.Core.Constants` avoids a clash with the package's own `Constants` class. StyleCop element order is by kind (fields, constructors, delegates, methods), which is why the delegate sits between the constructor and the methods.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `cd src && dotnet test --filter "FullyQualifiedName~IndexContentEnumeratorTests"`
Expected: `Passed! - Failed: 0, Passed: 5`.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Add paged index content enumerator

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: Index content reindexer

**Files:**
- Create: `src/code/Umbraco.Community.Search.Examine.Reindex/Services/IIndexContentReindexer.cs`
- Create: `src/code/Umbraco.Community.Search.Examine.Reindex/Services/IndexContentReindexer.cs`
- Test: `src/tests/Umbraco.Community.Search.Examine.Reindex.Tests/Services/IndexContentReindexerTests.cs`

**Interfaces:**
- Consumes: `IReindexStatusTracker`, `IIndexContentEnumerator`, `ContentBatch`, `ReindexStatus`, `ReindexOperationStatus` from Tasks 2 and 3. Umbraco Search: `IOptions<IndexOptions>`, `IDistributedContentIndexRefresher`, `IIndexDocumentService`, `IDistributedContentIndexRebuilder`, `IPublishedContentChangeStrategy`, `IDraftContentChangeStrategy`, `ContentState`. Umbraco: `IBackgroundTaskQueue` (`Umbraco.Cms.Core.HostedServices`), `Attempt<TResult, TStatus>` (`Umbraco.Cms.Core`), `TimeProvider`.
- Produces: `public interface IIndexContentReindexer { Attempt<ReindexStatus, ReindexOperationStatus> Start(string indexAlias, bool rebuildIndex); }`

- [ ] **Step 1: Write the failing tests**

`src/tests/Umbraco.Community.Search.Examine.Reindex.Tests/Services/IndexContentReindexerTests.cs`:

```csharp
// <copyright file="IndexContentReindexerTests.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.HostedServices;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Search.Core.Configuration;
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Cms.Search.Core.Services.ContentIndexing;
using Umbraco.Cms.Search.Provider.Examine.Services;
using Umbraco.Community.Search.Examine.Reindex.Models;
using Umbraco.Community.Search.Examine.Reindex.Services;

namespace Umbraco.Community.Search.Examine.Reindex.Tests.Services;

/// <summary>
/// Tests for <see cref="IndexContentReindexer"/>.
/// </summary>
[TestFixture]
public class IndexContentReindexerTests
{
    private const string PublishedAlias = "Umb_PublishedContent";
    private const string DraftAlias = "Umb_Content";
    private const string CustomAlias = "Custom";

    private Mock<IIndexContentEnumerator> enumerator = null!;
    private Mock<IDistributedContentIndexRefresher> refresher = null!;
    private Mock<IIndexDocumentService> indexDocumentService = null!;
    private Mock<IDistributedContentIndexRebuilder> rebuilder = null!;
    private Mock<IBackgroundTaskQueue> backgroundTaskQueue = null!;
    private ReindexStatusTracker tracker = null!;
    private CancellationToken queueToken;

    /// <summary>
    /// Creates fresh mocks. The background queue runs queued work inline.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.enumerator = new Mock<IIndexContentEnumerator>();
        this.enumerator.Setup(e => e.Enumerate(It.IsAny<UmbracoObjectTypes>())).Returns([]);
        this.refresher = new Mock<IDistributedContentIndexRefresher>();
        this.indexDocumentService = new Mock<IIndexDocumentService>();
        this.rebuilder = new Mock<IDistributedContentIndexRebuilder>();
        this.rebuilder.Setup(r => r.Rebuild(It.IsAny<string>())).Returns(true);
        this.tracker = new ReindexStatusTracker();
        this.queueToken = CancellationToken.None;
        this.backgroundTaskQueue = new Mock<IBackgroundTaskQueue>();
        this.backgroundTaskQueue
            .Setup(q => q.QueueBackgroundWorkItem(It.IsAny<Func<CancellationToken, Task>>()))
            .Callback<Func<CancellationToken, Task>>(work => work(this.queueToken).GetAwaiter().GetResult());
    }

    /// <summary>
    /// Unknown aliases are rejected.
    /// </summary>
    [Test]
    public void Start_UnknownAlias_ReturnsIndexNotFound()
    {
        IndexContentReindexer sut = this.CreateSut();

        Attempt<ReindexStatus, ReindexOperationStatus> attempt = sut.Start("Nope", false);

        Assert.Multiple(() =>
        {
            Assert.That(attempt.Success, Is.False);
            Assert.That(attempt.Status, Is.EqualTo(ReindexOperationStatus.IndexNotFound));
        });
        this.backgroundTaskQueue.Verify(q => q.QueueBackgroundWorkItem(It.IsAny<Func<CancellationToken, Task>>()), Times.Never);
    }

    /// <summary>
    /// A second start while running is rejected with the current status.
    /// </summary>
    [Test]
    public void Start_WhenAlreadyRunning_ReturnsAlreadyRunning()
    {
        this.tracker.TryStart(ReindexStatus.Idle(PublishedAlias) with { State = ReindexState.Running, ProcessedItems = 7 });
        IndexContentReindexer sut = this.CreateSut();

        Attempt<ReindexStatus, ReindexOperationStatus> attempt = sut.Start(PublishedAlias, false);

        Assert.Multiple(() =>
        {
            Assert.That(attempt.Success, Is.False);
            Assert.That(attempt.Status, Is.EqualTo(ReindexOperationStatus.AlreadyRunning));
            Assert.That(attempt.Result?.ProcessedItems, Is.EqualTo(7));
        });
    }

    /// <summary>
    /// Published index: only published documents are refreshed with the published state.
    /// </summary>
    [Test]
    public void Start_PublishedIndex_RefreshesPublishedDocumentsOnly()
    {
        IContent published = Mock.Of<IContent>(c => c.Published == true && c.Key == Guid.NewGuid());
        IContent draft = Mock.Of<IContent>(c => c.Published == false && c.Key == Guid.NewGuid());
        this.SetupBatches(UmbracoObjectTypes.Document, new ContentBatch([published, draft], 2));
        IndexContentReindexer sut = this.CreateSut();

        Attempt<ReindexStatus, ReindexOperationStatus> attempt = sut.Start(PublishedAlias, false);

        Assert.That(attempt.Success, Is.True);
        this.refresher.Verify(
            r => r.RefreshContent(
                It.Is<IEnumerable<IContent>>(items => items.SequenceEqual(new[] { published })),
                ContentState.Published),
            Times.Once);
        this.refresher.Verify(r => r.RefreshContent(It.IsAny<IEnumerable<IContent>>(), ContentState.Draft), Times.Never);
        this.indexDocumentService.Verify(s => s.DeleteAsync(It.IsAny<Guid[]>(), It.IsAny<bool>()), Times.Never);
        this.rebuilder.Verify(r => r.Rebuild(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// Draft index: documents, media and members are refreshed with the draft state.
    /// </summary>
    [Test]
    public void Start_DraftIndex_RefreshesAllObjectTypes()
    {
        IContent document = Mock.Of<IContent>(c => c.Published == false && c.Key == Guid.NewGuid());
        IMedia media = Mock.Of<IMedia>(m => m.Key == Guid.NewGuid());
        IMember member = Mock.Of<IMember>(m => m.Key == Guid.NewGuid());
        this.SetupBatches(UmbracoObjectTypes.Document, new ContentBatch([document], 1));
        this.SetupBatches(UmbracoObjectTypes.Media, new ContentBatch([media], 1));
        this.SetupBatches(UmbracoObjectTypes.Member, new ContentBatch([member], 1));
        IndexContentReindexer sut = this.CreateSut();

        sut.Start(DraftAlias, false);

        this.refresher.Verify(r => r.RefreshContent(It.Is<IEnumerable<IContent>>(items => items.Single() == document), ContentState.Draft), Times.Once);
        this.refresher.Verify(r => r.RefreshMedia(It.Is<IEnumerable<IMedia>>(items => items.Single() == media)), Times.Once);
        this.refresher.Verify(r => r.RefreshMember(It.Is<IEnumerable<IMember>>(items => items.Single() == member)), Times.Once);
        Assert.That(this.tracker.Get(DraftAlias).ProcessedItems, Is.EqualTo(3));
    }

    /// <summary>
    /// A custom strategy refreshes documents in both states.
    /// </summary>
    [Test]
    public void Start_CustomStrategy_RefreshesBothStates()
    {
        IContent published = Mock.Of<IContent>(c => c.Published == true && c.Key == Guid.NewGuid());
        this.SetupBatches(UmbracoObjectTypes.Document, new ContentBatch([published], 1));
        IndexContentReindexer sut = this.CreateSut();

        sut.Start(CustomAlias, false);

        this.refresher.Verify(r => r.RefreshContent(It.IsAny<IEnumerable<IContent>>(), ContentState.Draft), Times.Once);
        this.refresher.Verify(r => r.RefreshContent(It.IsAny<IEnumerable<IContent>>(), ContentState.Published), Times.Once);
    }

    /// <summary>
    /// Rebuild mode flushes the cache per batch and then triggers the rebuild.
    /// </summary>
    [Test]
    public void Start_RebuildMode_FlushesCacheThenRebuilds()
    {
        IContent document = Mock.Of<IContent>(c => c.Published == true && c.Key == Guid.NewGuid());
        this.SetupBatches(UmbracoObjectTypes.Document, new ContentBatch([document], 1));
        IndexContentReindexer sut = this.CreateSut();

        sut.Start(PublishedAlias, true);

        this.indexDocumentService.Verify(s => s.DeleteAsync(It.Is<Guid[]>(keys => keys.Single() == document.Key), true), Times.Once);
        this.indexDocumentService.Verify(s => s.DeleteAsync(It.IsAny<Guid[]>(), false), Times.Never);
        this.refresher.Verify(r => r.RefreshContent(It.IsAny<IEnumerable<IContent>>(), It.IsAny<ContentState>()), Times.Never);
        this.rebuilder.Verify(r => r.Rebuild(PublishedAlias), Times.Once);
        Assert.That(this.tracker.Get(PublishedAlias).State, Is.EqualTo(ReindexState.Idle));
    }

    /// <summary>
    /// Rebuild mode flushes media and member cache entries with the draft flag.
    /// </summary>
    [Test]
    public void Start_RebuildMode_DraftIndex_FlushesMediaAndMembersAsDraft()
    {
        IMedia media = Mock.Of<IMedia>(m => m.Key == Guid.NewGuid());
        IMember member = Mock.Of<IMember>(m => m.Key == Guid.NewGuid());
        this.SetupBatches(UmbracoObjectTypes.Media, new ContentBatch([media], 1));
        this.SetupBatches(UmbracoObjectTypes.Member, new ContentBatch([member], 1));
        IndexContentReindexer sut = this.CreateSut();

        sut.Start(DraftAlias, true);

        this.indexDocumentService.Verify(s => s.DeleteAsync(It.Is<Guid[]>(keys => keys.Single() == media.Key), false), Times.Once);
        this.indexDocumentService.Verify(s => s.DeleteAsync(It.Is<Guid[]>(keys => keys.Single() == member.Key), false), Times.Once);
        this.indexDocumentService.Verify(s => s.DeleteAsync(It.IsAny<Guid[]>(), true), Times.Never);
    }

    /// <summary>
    /// A false result from the rebuilder is recorded as a failure.
    /// </summary>
    [Test]
    public void Start_RebuildReturnsFalse_RecordsFailure()
    {
        this.rebuilder.Setup(r => r.Rebuild(PublishedAlias)).Returns(false);
        IndexContentReindexer sut = this.CreateSut();

        sut.Start(PublishedAlias, true);

        ReindexStatus status = this.tracker.Get(PublishedAlias);
        Assert.Multiple(() =>
        {
            Assert.That(status.State, Is.EqualTo(ReindexState.Failed));
            Assert.That(status.ErrorMessage, Is.Not.Null.And.Not.Empty);
        });
    }

    /// <summary>
    /// Exceptions during enumeration are recorded as a failure.
    /// </summary>
    [Test]
    public void Start_EnumerationThrows_RecordsFailure()
    {
        this.enumerator.Setup(e => e.Enumerate(UmbracoObjectTypes.Document)).Throws(new InvalidOperationException("boom"));
        IndexContentReindexer sut = this.CreateSut();

        sut.Start(PublishedAlias, false);

        ReindexStatus status = this.tracker.Get(PublishedAlias);
        Assert.Multiple(() =>
        {
            Assert.That(status.State, Is.EqualTo(ReindexState.Failed));
            Assert.That(status.ErrorMessage, Is.EqualTo("boom"));
            Assert.That(status.CompletedAt, Is.Not.Null);
        });
    }

    /// <summary>
    /// Cancellation stops the job and records a failure.
    /// </summary>
    [Test]
    public void Start_Cancelled_RecordsFailureWithoutRefreshing()
    {
        IContent document = Mock.Of<IContent>(c => c.Published == true && c.Key == Guid.NewGuid());
        this.SetupBatches(UmbracoObjectTypes.Document, new ContentBatch([document], 1));
        this.queueToken = new CancellationToken(canceled: true);
        IndexContentReindexer sut = this.CreateSut();

        sut.Start(PublishedAlias, false);

        Assert.That(this.tracker.Get(PublishedAlias).State, Is.EqualTo(ReindexState.Failed));
        this.refresher.Verify(r => r.RefreshContent(It.IsAny<IEnumerable<IContent>>(), It.IsAny<ContentState>()), Times.Never);
    }

    /// <summary>
    /// Progress totals are the sum of the totals reported per object type.
    /// </summary>
    [Test]
    public void Start_TracksProcessedAndTotalItems()
    {
        IContent document = Mock.Of<IContent>(c => c.Published == false && c.Key == Guid.NewGuid());
        IMedia media = Mock.Of<IMedia>(m => m.Key == Guid.NewGuid());
        this.SetupBatches(UmbracoObjectTypes.Document, new ContentBatch([document], 10));
        this.SetupBatches(UmbracoObjectTypes.Media, new ContentBatch([media], 5));
        IndexContentReindexer sut = this.CreateSut();

        sut.Start(DraftAlias, false);

        ReindexStatus status = this.tracker.Get(DraftAlias);
        Assert.Multiple(() =>
        {
            Assert.That(status.State, Is.EqualTo(ReindexState.Idle));
            Assert.That(status.ProcessedItems, Is.EqualTo(2));
            Assert.That(status.TotalItems, Is.EqualTo(15));
            Assert.That(status.StartedAt, Is.Not.Null);
            Assert.That(status.CompletedAt, Is.Not.Null);
        });
    }

    private void SetupBatches(UmbracoObjectTypes objectType, params ContentBatch[] batches)
        => this.enumerator.Setup(e => e.Enumerate(objectType)).Returns(batches);

    private IndexContentReindexer CreateSut()
    {
        var options = new IndexOptions();
        options.RegisterContentIndex<Indexer, Searcher, IPublishedContentChangeStrategy>(PublishedAlias, UmbracoObjectTypes.Document);
        options.RegisterContentIndex<Indexer, Searcher, IDraftContentChangeStrategy>(DraftAlias, UmbracoObjectTypes.Document, UmbracoObjectTypes.Media, UmbracoObjectTypes.Member);
        options.RegisterContentIndex<Indexer, Searcher, IContentChangeStrategy>(CustomAlias, UmbracoObjectTypes.Document);

        return new IndexContentReindexer(
            Options.Create(options),
            this.enumerator.Object,
            this.refresher.Object,
            this.indexDocumentService.Object,
            this.rebuilder.Object,
            this.tracker,
            this.backgroundTaskQueue.Object,
            TimeProvider.System,
            NullLogger<IndexContentReindexer>.Instance);
    }
}
```

Note: `Indexer` and `Searcher` are the public Examine provider classes (`Umbraco.Cms.Search.Provider.Examine.Services`); they only serve as type arguments for the registration. Interface types satisfy the `class` generic constraint, so `IPublishedContentChangeStrategy` etc. can be used as strategy type arguments.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd src && dotnet test --filter "FullyQualifiedName~IndexContentReindexerTests"`
Expected: build errors about missing `IndexContentReindexer`.

- [ ] **Step 3: Create the interface**

`Services/IIndexContentReindexer.cs`:

```csharp
// <copyright file="IIndexContentReindexer.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Umbraco.Cms.Core;
using Umbraco.Community.Search.Examine.Reindex.Models;

namespace Umbraco.Community.Search.Examine.Reindex.Services;

/// <summary>
/// Starts reindex operations for content indexes.
/// </summary>
public interface IIndexContentReindexer
{
    /// <summary>
    /// Starts a reindex of all content contained in the index. The work runs in the background.
    /// </summary>
    /// <param name="indexAlias">The index alias.</param>
    /// <param name="rebuildIndex">
    /// When <c>true</c>, the cached index values are flushed and the index is rebuilt afterwards.
    /// When <c>false</c>, every item is refreshed in place.
    /// </param>
    /// <returns>
    /// A successful attempt with the new status, or a failed attempt with
    /// <see cref="ReindexOperationStatus.IndexNotFound"/> or <see cref="ReindexOperationStatus.AlreadyRunning"/>
    /// (the result then carries the current status).
    /// </returns>
    Attempt<ReindexStatus, ReindexOperationStatus> Start(string indexAlias, bool rebuildIndex);
}
```

- [ ] **Step 4: Create `IndexContentReindexer.cs`**

```csharp
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
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `cd src && dotnet test --filter "FullyQualifiedName~IndexContentReindexerTests"`
Expected: `Passed! - Failed: 0, Passed: 11`. If Moq complains that `Mock.Of<IContent>(c => c.Published == true ...)` cannot set `Published` (it is read-only on `IContent`), replace those with `new Mock<IContent>()` plus `SetupGet(c => c.Published).Returns(true)` and `SetupGet(c => c.Key).Returns(Guid.NewGuid())`, then use `.Object`.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Add index content reindexer service

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 5: Management API, registration and composer

**Files:**
- Create: `src/code/Umbraco.Community.Search.Examine.Reindex/Controllers/ReindexApiControllerBase.cs`
- Create: `src/code/Umbraco.Community.Search.Examine.Reindex/Controllers/ReindexApiController.cs`
- Create: `src/code/Umbraco.Community.Search.Examine.Reindex/DependencyInjection/UmbracoBuilderExtensions.cs`
- Create: `src/code/Umbraco.Community.Search.Examine.Reindex/Composing/SearchExamineReindexComposer.cs`
- Test: `src/tests/Umbraco.Community.Search.Examine.Reindex.Tests/Controllers/ReindexApiControllerTests.cs`

**Interfaces:**
- Consumes: `IIndexContentReindexer.Start`, `IReindexStatusTracker.Get`, models from Task 2.
- Produces: HTTP `POST /umbraco/search-examine-reindex/api/v1/index/{indexAlias}/reindex` and `GET /umbraco/search-examine-reindex/api/v1/index/{indexAlias}/reindex/status`, Swagger doc at `/umbraco/swagger/search-examine-reindex/swagger.json` with operation ids `reindex` and `getReindexStatus`. `IUmbracoBuilder AddSearchExamineReindex()`.

- [ ] **Step 1: Write the failing controller tests**

`src/tests/Umbraco.Community.Search.Examine.Reindex.Tests/Controllers/ReindexApiControllerTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd src && dotnet test --filter "FullyQualifiedName~ReindexApiControllerTests"`
Expected: build errors about missing `ReindexApiController`.

- [ ] **Step 3: Create the controller base class**

`Controllers/ReindexApiControllerBase.cs`:

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
[ApiExplorerSettings(GroupName = "Umbraco Search Examine Reindex")]
public abstract class ReindexApiControllerBase : ManagementApiControllerBase
{
}
```

- [ ] **Step 4: Create the controller**

`Controllers/ReindexApiController.cs`:

```csharp
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
```

- [ ] **Step 5: Run the controller tests**

Run: `cd src && dotnet test --filter "FullyQualifiedName~ReindexApiControllerTests"`
Expected: `Passed! - Failed: 0, Passed: 4`. If `ManagementApiControllerBase` cannot be resolved, add `<PackageReference Include="Umbraco.Cms.Api.Management" />` to the package `.csproj` and `<PackageVersion Include="Umbraco.Cms.Api.Management" Version="[17.0.0,18.0.0)" />` to `src/Directory.Packages.props`.

- [ ] **Step 6: Create the builder extension**

`DependencyInjection/UmbracoBuilderExtensions.cs`:

```csharp
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
    /// <returns>The Umbraco builder.</returns>
    public static IUmbracoBuilder AddSearchExamineReindex(this IUmbracoBuilder builder)
    {
        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(IIndexContentReindexer)))
        {
            return builder;
        }

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IReindexStatusTracker, ReindexStatusTracker>();
        builder.Services.AddSingleton<IIndexContentEnumerator, IndexContentEnumerator>();
        builder.Services.AddSingleton<IIndexContentReindexer, IndexContentReindexer>();

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
            => $"{apiDescription.ActionDescriptor.RouteValues["action"]}";

        protected override bool CanHandle(ApiDescription apiDescription, ControllerActionDescriptor controllerActionDescriptor)
            => controllerActionDescriptor.ControllerTypeInfo.Namespace?.StartsWith(Constants.Api.ControllerNamespace, StringComparison.OrdinalIgnoreCase) is true;
    }
}
```

- [ ] **Step 7: Create the composer**

`Composing/SearchExamineReindexComposer.cs`:

```csharp
// <copyright file="SearchExamineReindexComposer.cs" company="Umbraco community">
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.Search.Examine.Reindex.DependencyInjection;

namespace Umbraco.Community.Search.Examine.Reindex.Composing;

/// <summary>
/// Registers the reindex package when the assembly is present.
/// </summary>
public sealed class SearchExamineReindexComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder)
        => builder.AddSearchExamineReindex();
}
```

- [ ] **Step 8: Build everything and run all tests**

Run: `cd src && dotnet build && dotnet test`
Expected: build succeeded with no warnings from the package or test project, all tests pass (5 + 5 + 11 + 4 = 25).

- [ ] **Step 9: Start the test site and verify the API**

Run in a separate terminal: `cd test-sites/Website-V17 && dotnet run` and wait for `Now listening on: https://localhost:44310`.

Then from PowerShell:

```powershell
curl.exe -k https://localhost:44310/umbraco/swagger/search-examine-reindex/swagger.json
```

Expected: JSON containing `"operationId": "reindex"` and `"operationId": "getReindexStatus"` and paths `/umbraco/search-examine-reindex/api/v1/index/{indexAlias}/reindex` and `.../reindex/status`. Also open `https://localhost:44310/umbraco/swagger` in a browser, choose the "Umbraco Search Examine Reindex API" document, authorize, and call `GET .../index/Umb_PublishedContent/reindex/status`. Expected: `{"indexAlias":"Umb_PublishedContent","state":"Idle", ...}`.

Leave the test site running for Task 6.

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "Add reindex Management API, registration and composer

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 6: Generated API client, types and repository

**Files:**
- Modify: `src/code/Umbraco.Community.Search.Examine.Reindex.Client/package.json`
- Modify: `src/code/Umbraco.Community.Search.Examine.Reindex.Client/openapi-ts.config.ts`
- Modify: `src/code/Umbraco.Community.Search.Examine.Reindex.Client/tsconfig.json`
- Modify: `src/code/Umbraco.Community.Search.Examine.Reindex.Client/src/hey-api.ts`
- Create: `src/code/Umbraco.Community.Search.Examine.Reindex.Client/src/api/*` (generated)
- Create: `src/code/Umbraco.Community.Search.Examine.Reindex.Client/src/types.ts`
- Create: `src/code/Umbraco.Community.Search.Examine.Reindex.Client/src/repository/reindex.repository.ts`

**Interfaces:**
- Consumes: the running test site's swagger document from Task 5.
- Produces:
  - Generated functions `reindex({ path: { indexAlias }, body: { rebuildIndex } })` and `getReindexStatus({ path: { indexAlias } })` in `src/api/sdk.gen.ts`, model `ReindexStatusModel` in `src/api/types.gen.ts` (name may differ, check the generated file).
  - `ReindexState`, `ReindexStatus` in `src/types.ts`.
  - `class ReindexRepository extends UmbRepositoryBase` with `start(indexAlias: string, rebuildIndex: boolean): Promise<{ data?: ReindexStatus; error?: UmbApiError | UmbCancelError }>` and `getStatus(indexAlias: string)` returning the same shape.

- [ ] **Step 1: Update `package.json`**

Replace the whole file:

```json
{
  "name": "umbraco-community-search-examine-reindex-client",
  "private": true,
  "version": "0.0.0",
  "type": "module",
  "scripts": {
    "dev": "vite",
    "build": "tsc && vite build",
    "watch": "vite build --watch",
    "preview": "vite preview",
    "generate-api": "curl -k -o swagger.json https://localhost:44310/umbraco/swagger/search-examine-reindex/swagger.json && openapi-ts"
  },
  "dependencies": {
    "lit": "^3.3.3"
  },
  "devDependencies": {
    "@hey-api/openapi-ts": "^0.85.2",
    "@umbraco-cms/backoffice": "^17.0.0",
    "@umbraco-cms/search": "^17.1.0",
    "typescript": "^7.0.2",
    "vite": "^8.2.2"
  }
}
```

Then run `npm install` in the client folder so `package-lock.json` picks up `@hey-api/openapi-ts`.

- [ ] **Step 2: Update `openapi-ts.config.ts`**

```ts
import { defineConfig } from '@hey-api/openapi-ts';

export default defineConfig({
  input: 'swagger.json',
  output: 'src/api',
  plugins: [
    {
      name: '@hey-api/client-fetch',
      runtimeConfigPath: '../hey-api.ts',
    },
    '@hey-api/sdk',
  ],
});
```

- [ ] **Step 3: Update `src/hey-api.ts`**

```ts
import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import type { CreateClientConfig } from './api/client/types.gen';

export const createClientConfig: CreateClientConfig = (config) => ({
  ...config,
  ...umbHttpClient.getConfig(),
});
```

- [ ] **Step 4: Remove the stale path mappings from `tsconfig.json`**

Delete the whole `"paths": { ... }` block (the two `@umbraco-cms/search/*` entries). Types now resolve from `node_modules/@umbraco-cms/search`.

- [ ] **Step 5: Generate the client (test site must be running)**

Run: `cd src/code/Umbraco.Community.Search.Examine.Reindex.Client && npm run generate-api`
Expected: `src/api/` now contains `client.gen.ts`, `sdk.gen.ts`, `types.gen.ts`, `index.ts`, `client/` and `core/` folders. Open `src/api/sdk.gen.ts` and confirm it exports `reindex` and `getReindexStatus`. Open `src/api/types.gen.ts` and note the exact name of the status model type (expected `ReindexStatusModel`) and of the state enum type (expected `ReindexStateModel`).

- [ ] **Step 6: Create `src/types.ts`**

```ts
export type ReindexState = 'Idle' | 'Running' | 'Failed';

export interface ReindexStatus {
  indexAlias: string;
  state: ReindexState;
  rebuildIndex: boolean;
  processedItems: number;
  totalItems?: number;
  startedAt?: string;
  completedAt?: string;
  errorMessage?: string;
}
```

- [ ] **Step 7: Create `src/repository/reindex.repository.ts`**

```ts
import { getReindexStatus, reindex } from '../api/index.js';
import type { ReindexStatusModel } from '../api/types.gen.js';
import type { ReindexStatus } from '../types.js';
import { UmbRepositoryBase } from '@umbraco-cms/backoffice/repository';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';

/**
 * Talks to the reindex Management API for a single index.
 */
export class ReindexRepository extends UmbRepositoryBase {
  constructor(host: UmbControllerHost) {
    super(host);
  }

  async start(indexAlias: string, rebuildIndex: boolean) {
    const { data, error } = await tryExecute(
      this,
      reindex({ path: { indexAlias }, body: { rebuildIndex } }),
    );
    return { data: data ? this.#map(data) : undefined, error };
  }

  async getStatus(indexAlias: string) {
    const { data, error } = await tryExecute(this, getReindexStatus({ path: { indexAlias } }));
    return { data: data ? this.#map(data) : undefined, error };
  }

  #map(model: ReindexStatusModel): ReindexStatus {
    return {
      indexAlias: model.indexAlias,
      state: model.state,
      rebuildIndex: model.rebuildIndex,
      processedItems: model.processedItems,
      totalItems: model.totalItems ?? undefined,
      startedAt: model.startedAt ?? undefined,
      completedAt: model.completedAt ?? undefined,
      errorMessage: model.errorMessage ?? undefined,
    };
  }
}
```

If the generated type names differ from `ReindexStatusModel`, use the generated names. If `model.state` is typed as a string union already, no cast is needed; otherwise cast with `model.state as ReindexState`.

- [ ] **Step 8: Type-check**

Run: `cd src/code/Umbraco.Community.Search.Examine.Reindex.Client && npx tsc --noEmit`
Expected: no errors. (The build still uses the placeholder element from the scaffold; that is replaced in Task 7.)

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "Add generated reindex API client and repository

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 7: Detail box element, localization and manifests

**Files:**
- Create: `src/code/Umbraco.Community.Search.Examine.Reindex.Client/src/lang/en.ts`
- Create: `src/code/Umbraco.Community.Search.Examine.Reindex.Client/src/lang/manifests.ts`
- Create: `src/code/Umbraco.Community.Search.Examine.Reindex.Client/src/detailboxes/reindex-detail-box.element.ts`
- Delete: `src/code/Umbraco.Community.Search.Examine.Reindex.Client/src/detailboxes/reindex.ts`
- Modify: `src/code/Umbraco.Community.Search.Examine.Reindex.Client/src/detailboxes/manifests.ts`
- Modify: `src/code/Umbraco.Community.Search.Examine.Reindex.Client/src/manifests.ts`
- Modify: `src/code/Umbraco.Community.Search.Examine.Reindex.Client/public/umbraco-package.json`

**Interfaces:**
- Consumes: `ReindexRepository`, `ReindexStatus` from Task 6; `UMB_SEARCH_WORKSPACE_CONTEXT` (`@umbraco-cms/search/settings`), `UMB_SEARCH_CONTEXT` (`@umbraco-cms/search/global`).
- Produces: custom element `search-examine-reindex-detail-box`, manifests registered by the entry point.

**Deviation from spec, on purpose:** the spec said to call `workspaceContext.setState('loading')` once the job finishes in rebuild mode. That races with Umbraco Search's `IndexRebuildCompleted` event on small sites (the event can arrive before our 3 second poll, leaving the workspace stuck in the loading view). Instead the element calls `searchContext.setUserWaitingForIndexUpdate(alias, true)` when the user confirms in rebuild mode. Umbraco Search then shows its own "rebuild completed" toast and reloads the workspace when the event arrives, and the stats box shows the `Rebuilding` health status meanwhile.

- [ ] **Step 1: Create `src/lang/en.ts`**

```ts
import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  searchExamineReindex: {
    boxLabel: 'Reindex',
    description:
      'Reindex flushes the cached index values of every item in this index and collects them again from the content, so changes to property value handlers and custom indexers are applied. The index stays searchable while this runs. Items that no longer exist are not removed; turn on rebuild for a clean index.',
    rebuildToggle: 'Also rebuild the index',
    button: 'Reindex',
    confirmHeadline: 'Reindex Search Index',
    confirmMessage:
      'Are you sure you want to reindex all content of index "{0}"? This may take a while depending on the size of your content.',
    confirmMessageRebuild:
      'Are you sure you want to reindex all content of index "{0}" and rebuild the index afterwards? The index will be rebuilt from scratch, which may take a while.',
    confirmLabel: 'Reindex',
    startedTitle: 'Reindex started',
    startedMessage:
      'The reindex of search index "{0}" has started. You can continue working while it runs in the background.',
    completedTitle: 'Reindex completed',
    completedMessage: 'All content of search index "{0}" has been queued for reindexing.',
    failedTitle: 'Reindex failed',
    progress: (processed: number, total: number | string) => `${processed} of ${total} items`,
  },
} satisfies UmbLocalizationDictionary;
```

- [ ] **Step 2: Create `src/lang/manifests.ts`**

```ts
import english from './en.js';

export const manifests: Array<UmbExtensionManifest> = [
  {
    type: 'localization',
    name: 'Umbraco Search Examine Reindex Localization - English',
    alias: 'Umbraco.Community.Search.Examine.Reindex.Localization.En',
    meta: { culture: 'en', localizations: english },
  },
];
```

- [ ] **Step 3: Create `src/detailboxes/reindex-detail-box.element.ts`**

```ts
import { ReindexRepository } from '../repository/reindex.repository.js';
import type { ReindexStatus } from '../types.js';
import { css, customElement, html, nothing, state } from '@umbraco-cms/backoffice/external/lit';
import type { UUIButtonState, UUIToggleElement } from '@umbraco-cms/backoffice/external/uui';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { umbConfirmModal } from '@umbraco-cms/backoffice/modal';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UmbApiError } from '@umbraco-cms/backoffice/resources';
import { UmbTextStyles } from '@umbraco-cms/backoffice/style';
import { UMB_SEARCH_CONTEXT } from '@umbraco-cms/search/global';
import { UMB_SEARCH_WORKSPACE_CONTEXT } from '@umbraco-cms/search/settings';

const POLL_INTERVAL_MS = 3000;

@customElement('search-examine-reindex-detail-box')
export class ReindexDetailBoxElement extends UmbLitElement {
  #repository = new ReindexRepository(this);
  #searchContext?: typeof UMB_SEARCH_CONTEXT.TYPE;
  #notificationContext?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;
  #pollTimer?: ReturnType<typeof setTimeout>;

  @state()
  private _indexAlias?: string;

  @state()
  private _rebuildIndex = false;

  @state()
  private _status?: ReindexStatus;

  @state()
  private _buttonState?: UUIButtonState;

  constructor() {
    super();

    this.consumeContext(UMB_SEARCH_WORKSPACE_CONTEXT, (context) => {
      this.observe(
        context?.unique,
        (unique) => {
          this._indexAlias = unique ?? undefined;
          this._status = undefined;
          this.#stopPolling();
          if (this._indexAlias) void this.#refreshStatus();
        },
        '_observeUnique',
      );
    });

    this.consumeContext(UMB_SEARCH_CONTEXT, (context) => (this.#searchContext = context));
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (context) => (this.#notificationContext = context));
  }

  override disconnectedCallback() {
    super.disconnectedCallback();
    this.#stopPolling();
  }

  async #refreshStatus() {
    const alias = this._indexAlias;
    if (!alias) return;

    const { data } = await this.#repository.getStatus(alias);
    if (!data || data.indexAlias !== alias) {
      // transient error or alias changed meanwhile: keep polling if we were running
      if (this._status?.state === 'Running') this.#schedulePoll();
      return;
    }

    this.#applyStatus(data);
  }

  #applyStatus(status: ReindexStatus) {
    const wasRunning = this._status?.state === 'Running';
    this._status = status;

    if (status.state === 'Running') {
      this.#schedulePoll();
      return;
    }

    this.#stopPolling();
    if (!wasRunning) return;

    if (status.state === 'Failed') {
      this.#notificationContext?.peek('danger', {
        data: {
          title: this.localize.term('searchExamineReindex_failedTitle'),
          message: status.errorMessage ?? '',
        },
      });
      return;
    }

    if (!status.rebuildIndex) {
      this.#notificationContext?.peek('positive', {
        data: {
          title: this.localize.term('searchExamineReindex_completedTitle'),
          message: this.localize.term('searchExamineReindex_completedMessage', status.indexAlias),
        },
      });
    }
    // In rebuild mode Umbraco Search shows its own "rebuild completed" toast and reloads the workspace.
  }

  #schedulePoll() {
    this.#stopPolling();
    this.#pollTimer = setTimeout(() => void this.#refreshStatus(), POLL_INTERVAL_MS);
  }

  #stopPolling() {
    if (this.#pollTimer) {
      clearTimeout(this.#pollTimer);
      this.#pollTimer = undefined;
    }
  }

  #onToggleChange(event: Event) {
    this._rebuildIndex = (event.target as UUIToggleElement).checked;
  }

  async #onReindexClick() {
    const alias = this._indexAlias;
    if (!alias) return;

    try {
      await umbConfirmModal(this, {
        color: 'warning',
        headline: this.localize.term('searchExamineReindex_confirmHeadline'),
        content: this.localize.term(
          this._rebuildIndex
            ? 'searchExamineReindex_confirmMessageRebuild'
            : 'searchExamineReindex_confirmMessage',
          alias,
        ),
        confirmLabel: this.localize.term('searchExamineReindex_confirmLabel'),
      });
    } catch {
      return; // cancelled
    }

    this._buttonState = 'waiting';
    if (this._rebuildIndex) {
      this.#searchContext?.setUserWaitingForIndexUpdate(alias, true);
    }

    const { data, error } = await this.#repository.start(alias, this._rebuildIndex);
    this._buttonState = undefined;

    if (data) {
      this.#notificationContext?.peek('warning', {
        data: {
          title: this.localize.term('searchExamineReindex_startedTitle'),
          message: this.localize.term('searchExamineReindex_startedMessage', alias),
        },
      });
      this._status = undefined;
      this.#applyStatus(data);
      return;
    }

    if (UmbApiError.isUmbApiError(error) && error.status === 409) {
      // already running: attach to the running job
      void this.#refreshStatus();
      return;
    }

    if (this._rebuildIndex) {
      this.#searchContext?.setUserWaitingForIndexUpdate(alias, false);
    }

    this.#notificationContext?.peek('danger', {
      data: {
        title: this.localize.term('searchExamineReindex_failedTitle'),
        message: error?.message ?? '',
      },
    });
  }

  override render() {
    const running = this._status?.state === 'Running';

    return html`
      <uui-box headline=${this.localize.term('searchExamineReindex_boxLabel')}>
        <p>${this.localize.term('searchExamineReindex_description')}</p>

        <uui-toggle
          data-mark="search-examine-reindex:toggle-rebuild"
          label=${this.localize.term('searchExamineReindex_rebuildToggle')}
          .checked=${this._rebuildIndex}
          ?disabled=${running}
          @change=${this.#onToggleChange}></uui-toggle>

        ${running ? this.#renderProgress() : nothing}
        ${this._status?.state === 'Failed'
          ? html`<p class="error">${this._status.errorMessage}</p>`
          : nothing}

        <uui-button
          data-mark="search-examine-reindex:button-reindex"
          look="primary"
          color="warning"
          label=${this.localize.term('searchExamineReindex_button')}
          .state=${this._buttonState}
          ?disabled=${running || !this._indexAlias}
          @click=${this.#onReindexClick}></uui-button>
      </uui-box>
    `;
  }

  #renderProgress() {
    const status = this._status!;
    const percentage = status.totalItems
      ? Math.min(100, Math.round((status.processedItems / status.totalItems) * 100))
      : 0;

    return html`
      <div class="progress" data-mark="search-examine-reindex:progress">
        <uui-progress-bar .progress=${percentage}></uui-progress-bar>
        <small>
          ${this.localize.term(
            'searchExamineReindex_progress',
            status.processedItems,
            status.totalItems ?? '?',
          )}
        </small>
      </div>
    `;
  }

  static override styles = [
    UmbTextStyles,
    css`
      :host {
        display: block;
      }

      uui-toggle,
      .progress {
        display: block;
        margin-bottom: var(--uui-size-space-4);
      }

      .error {
        color: var(--uui-color-danger);
      }
    `,
  ];
}

export default ReindexDetailBoxElement;

declare global {
  interface HTMLElementTagNameMap {
    'search-examine-reindex-detail-box': ReindexDetailBoxElement;
  }
}
```

- [ ] **Step 4: Delete the placeholder and rewrite `src/detailboxes/manifests.ts`**

Delete `src/detailboxes/reindex.ts`. New `src/detailboxes/manifests.ts`:

```ts
import ReindexDetailBoxElement from './reindex-detail-box.element.js';
import type { ManifestSearchIndexDetailBox } from '@umbraco-cms/search/global';

const detailBox: ManifestSearchIndexDetailBox = {
  type: 'searchIndexDetailBox',
  alias: 'Umbraco.Community.Search.Examine.Reindex.DetailBox',
  name: 'Umbraco Search Examine Reindex Detail Box',
  element: ReindexDetailBoxElement,
  weight: 150,
  meta: {
    label: '#searchExamineReindex_boxLabel',
    column: 'right',
  },
  conditions: [
    {
      alias: 'Umb.Search.Condition.IndexProviderName',
      match: 'search-examine-provider',
    },
  ],
};

export const manifests: Array<UmbExtensionManifest> = [detailBox];
```

If TypeScript rejects `conditions` on `ManifestSearchIndexDetailBox`, type the constant as `UmbExtensionManifest` instead (the `@umbraco-cms/search/global` type augments `UmbExtensionManifestMap`, so the `searchIndexDetailBox` type is known).

- [ ] **Step 5: Update `src/manifests.ts`**

```ts
import type { UmbBackofficeExtensionRegistry } from '@umbraco-cms/backoffice/extension-registry';
import { manifests as detailBoxManifests } from './detailboxes/manifests.js';
import { manifests as localizationManifests } from './lang/manifests.js';

export function registerManifest(registry: UmbBackofficeExtensionRegistry) {
  registry.registerMany([...localizationManifests, ...detailBoxManifests]);
}
```

- [ ] **Step 6: Fix the zero-width space in `public/umbraco-package.json`**

The current `"name"` value starts with an invisible U+200B character. Replace the file:

```json
{
  "$schema": "../node_modules/@umbraco-cms/backoffice/dist-cms/umbraco-package-schema.json",
  "name": "Umbraco Search Examine Reindex",
  "version": "17.0.0",
  "extensions": [
    {
      "type": "backofficeEntryPoint",
      "alias": "Umbraco.Community.Search.Examine.Reindex",
      "name": "Umbraco Search Examine Reindex Entry Point",
      "js": "/App_Plugins/searchreindex/entry-point.js?v=17.0.0"
    }
  ]
}
```

- [ ] **Step 7: Build the client**

Run: `cd src/code/Umbraco.Community.Search.Examine.Reindex.Client && npm run build`
Expected: `tsc` passes and Vite writes `../Umbraco.Community.Search.Examine.Reindex/wwwroot/App_Plugins/searchreindex/entry-point.js`. Check the bundle does not inline Umbraco code: `grep -c "from \"@umbraco-cms/search/settings\"" ../Umbraco.Community.Search.Examine.Reindex/wwwroot/App_Plugins/searchreindex/entry-point.js` prints `1`.

- [ ] **Step 8: Verify in the backoffice**

Restart the test site (`Ctrl+C`, then `dotnet run` in `test-sites/Website-V17`) so the new static assets are served. Log in at `https://localhost:44310/umbraco` (admin@example.com / 1234567890). Go to Settings → Advanced → Search → `Umb_PublishedContent`.

Check:
1. The "Reindex" box appears in the right column under the statistics box.
2. Click Reindex → confirm modal → Confirm → "Reindex started" toast, progress bar appears, then a "Reindex completed" toast within a few seconds. The document count in the stats box is unchanged.
3. Turn on "Also rebuild the index" → Reindex → Confirm → after the progress finishes, Umbraco Search shows "Search Index Rebuild Completed" and the stats box reloads.
4. Open the browser console: no errors.

If the box does not appear, check the console for a failed import of `@umbraco-cms/search/settings` (the Umbraco Search bundle must be installed on the site) and check the condition alias.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "Add reindex detail box element, localization and manifests

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 8: End-to-end tests

**Files:**
- Create: `src/tests/Umbraco.Community.Search.Examine.Reindex.E2E/package.json`
- Create: `src/tests/Umbraco.Community.Search.Examine.Reindex.E2E/playwright.config.ts`
- Create: `src/tests/Umbraco.Community.Search.Examine.Reindex.E2E/tsconfig.json`
- Create: `src/tests/Umbraco.Community.Search.Examine.Reindex.E2E/tests/auth.setup.ts`
- Create: `src/tests/Umbraco.Community.Search.Examine.Reindex.E2E/tests/reindex-detail-box.spec.ts`
- Create: `src/tests/Umbraco.Community.Search.Examine.Reindex.E2E/.env.example`
- Create: `src/tests/Umbraco.Community.Search.Examine.Reindex.E2E/.gitignore`

**Interfaces:**
- Consumes: the running test site with the package from Task 7; `data-mark` attributes `search-examine-reindex:toggle-rebuild`, `search-examine-reindex:button-reindex`, `search-examine-reindex:progress`; status endpoint `GET /umbraco/search-examine-reindex/api/v1/index/{alias}/reindex/status`.

- [ ] **Step 1: Create `package.json`**

```json
{
  "name": "umbraco-community-search-examine-reindex-e2e",
  "private": true,
  "version": "0.0.0",
  "type": "module",
  "scripts": {
    "test:e2e": "playwright test",
    "test:e2e:ui": "playwright test --ui",
    "test:e2e:debug": "playwright test --debug"
  },
  "devDependencies": {
    "@playwright/test": "^1.63.0",
    "@umbraco/playwright-testhelpers": "17.1.0-beta.7",
    "@types/node": "^24.0.0",
    "dotenv": "^16.4.5",
    "typescript": "^5.9.0"
  }
}
```

- [ ] **Step 2: Create `.env.example`, `.gitignore` and `tsconfig.json`**

`.env.example`:

```
UMBRACO_URL=https://localhost:44310
UMBRACO_USER_LOGIN=admin@example.com
UMBRACO_USER_PASSWORD=1234567890
```

`.gitignore`:

```
node_modules/
.env
.auth/
test-results/
playwright-report/
```

`tsconfig.json`:

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ESNext",
    "moduleResolution": "bundler",
    "strict": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "noEmit": true,
    "types": ["node"]
  },
  "include": ["playwright.config.ts", "tests"]
}
```

- [ ] **Step 3: Create `playwright.config.ts`**

```ts
import 'dotenv/config';
import { defineConfig, devices } from '@playwright/test';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));

export const STORAGE_STATE = join(__dirname, '.auth/user.json');

// The Umbraco testhelpers read the auth token from this file.
process.env.STORAGE_STAGE_PATH = STORAGE_STATE;
// The testhelpers also read URL from this variable.
process.env.URL = process.env.UMBRACO_URL ?? 'https://localhost:44310';

export default defineConfig({
  testDir: './tests',
  timeout: 60 * 1000,
  expect: { timeout: 10 * 1000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: 'list',
  use: {
    baseURL: process.env.UMBRACO_URL ?? 'https://localhost:44310',
    ignoreHTTPSErrors: true,
    trace: 'retain-on-failure',
    // Umbraco marks elements with data-mark, not data-testid.
    testIdAttribute: 'data-mark',
  },
  projects: [
    {
      name: 'setup',
      testMatch: '**/*.setup.ts',
    },
    {
      name: 'e2e',
      testMatch: '**/*.spec.ts',
      dependencies: ['setup'],
      use: {
        ...devices['Desktop Chrome'],
        ignoreHTTPSErrors: true,
        storageState: STORAGE_STATE,
      },
    },
  ],
});
```

- [ ] **Step 4: Create `tests/auth.setup.ts`**

```ts
import { test as setup } from '@playwright/test';
import { ConstantHelper, UiHelpers } from '@umbraco/playwright-testhelpers';
import { mkdirSync } from 'fs';
import { dirname } from 'path';
import { STORAGE_STATE } from '../playwright.config';

setup('authenticate', async ({ page }) => {
  mkdirSync(dirname(STORAGE_STATE), { recursive: true });

  const umbracoUi = new UiHelpers(page);
  await umbracoUi.goToBackOffice();
  await umbracoUi.login.enterEmail(process.env.UMBRACO_USER_LOGIN!);
  await umbracoUi.login.enterPassword(process.env.UMBRACO_USER_PASSWORD!);
  await umbracoUi.login.clickLoginButton();
  await umbracoUi.login.goToSection(ConstantHelper.sections.settings);
  await page.context().storageState({ path: STORAGE_STATE });
});
```

- [ ] **Step 5: Create `tests/reindex-detail-box.spec.ts`**

```ts
import { expect, type APIRequestContext } from '@playwright/test';
import { test } from '@umbraco/playwright-testhelpers';

const INDEX_ALIAS = 'Umb_PublishedContent';
const WORKSPACE_PATH = `/umbraco/section/settings/workspace/search-index/edit/${INDEX_ALIAS}`;
const STATUS_PATH = `/umbraco/search-examine-reindex/api/v1/index/${INDEX_ALIAS}/reindex/status`;

interface StatusBody {
  state: 'Idle' | 'Running' | 'Failed';
  rebuildIndex: boolean;
}

async function readStatus(request: APIRequestContext, baseURL: string): Promise<StatusBody> {
  const response = await request.get(baseURL + STATUS_PATH);
  expect(response.ok()).toBeTruthy();
  return (await response.json()) as StatusBody;
}

async function waitForIdle(request: APIRequestContext, baseURL: string) {
  await expect
    .poll(async () => (await readStatus(request, baseURL)).state, { timeout: 60_000, intervals: [1000] })
    .toBe('Idle');
}

test.describe('Reindex detail box', () => {
  test.beforeEach(async ({ umbracoUi, request, baseURL }) => {
    await waitForIdle(request, baseURL!);
    await umbracoUi.goToBackOffice();
    await umbracoUi.page.goto(WORKSPACE_PATH);
    await umbracoUi.page.locator('search-examine-reindex-detail-box').waitFor({ timeout: 30_000 });
  });

  test('shows the box on an Examine index', async ({ umbracoUi }) => {
    const box = umbracoUi.page.locator('search-examine-reindex-detail-box');
    await expect(box.getByText('Reindex', { exact: true }).first()).toBeVisible();
    await expect(umbracoUi.page.getByTestId('search-examine-reindex:button-reindex')).toBeEnabled();
    await expect(umbracoUi.page.getByTestId('search-examine-reindex:toggle-rebuild')).toBeVisible();
  });

  test('cancelling the confirm modal does not start a reindex', async ({ umbracoUi, request, baseURL }) => {
    await umbracoUi.page.getByTestId('search-examine-reindex:button-reindex').click();
    const dialog = umbracoUi.page.getByRole('dialog');
    await expect(dialog.getByText('Reindex Search Index')).toBeVisible();
    await dialog.getByRole('button', { name: 'Cancel' }).click();

    await expect(dialog).toBeHidden();
    expect((await readStatus(request, baseURL!)).state).toBe('Idle');
  });

  test('reindex runs and completes', async ({ umbracoUi, request, baseURL }) => {
    await umbracoUi.page.getByTestId('search-examine-reindex:button-reindex').click();
    await umbracoUi.page.getByRole('dialog').getByRole('button', { name: 'Reindex' }).click();

    await expect(umbracoUi.page.getByText('Reindex started')).toBeVisible();
    await waitForIdle(request, baseURL!);
    await expect(umbracoUi.page.getByText('Reindex completed')).toBeVisible({ timeout: 15_000 });
    expect((await readStatus(request, baseURL!)).rebuildIndex).toBe(false);
  });

  test('reindex with rebuild triggers the search index rebuild', async ({ umbracoUi, request, baseURL }) => {
    await umbracoUi.page.getByTestId('search-examine-reindex:toggle-rebuild').click();
    await umbracoUi.page.getByTestId('search-examine-reindex:button-reindex').click();
    await umbracoUi.page.getByRole('dialog').getByRole('button', { name: 'Reindex' }).click();

    await expect(umbracoUi.page.getByText('Reindex started')).toBeVisible();
    await waitForIdle(request, baseURL!);
    expect((await readStatus(request, baseURL!)).rebuildIndex).toBe(true);
    await expect(umbracoUi.page.getByText('Search Index Rebuild Completed')).toBeVisible({ timeout: 60_000 });
  });
});
```

Notes for the implementer:
- `request` here is Playwright's built-in API request fixture; it reuses the `storageState` cookies, and the backoffice API accepts the auth cookie for same-origin requests. If the status call returns 401, switch to `umbracoApi.get(baseURL + STATUS_PATH)` from the testhelpers fixture (it attaches the bearer token from `STORAGE_STAGE_PATH`) and read the body with `await response.json()`.
- If the direct workspace URL does not resolve, navigate through the UI instead: `await umbracoUi.content.goToSection(ConstantHelper.sections.settings)` (import `ConstantHelper` from the testhelpers), then click the `Search` link in the Advanced group of the settings sidebar, then click the `Umb_PublishedContent` row in the collection.

- [ ] **Step 6: Install and run**

```bash
cd src/tests/Umbraco.Community.Search.Examine.Reindex.E2E
npm install
npx playwright install chromium
copy .env.example .env
npm run test:e2e
```

Expected (test site running): `5 passed` (1 setup + 4 specs). Fix selectors as needed, using `npm run test:e2e:ui` to inspect.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "Add Playwright end-to-end tests for the reindex detail box

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 9: Documentation wrap-up

**Files:**
- Modify: `CLAUDE.md`
- Modify: `README.md`

**Interfaces:** none.

- [ ] **Step 1: Update the Gotchas section of `CLAUDE.md`**

Replace the `(add entries here ...)` placeholder with the lessons learned during Tasks 2 to 8. At minimum add these, plus anything the implementer hit:

```markdown
- The client build must run before `dotnet build`: the Razor SDK project ships whatever is in
  `wwwroot/App_Plugins/searchreindex`, which is gitignored.
- `npm run generate-api` needs the test site running on https://localhost:44310. The generated
  `src/api` folder is committed; `swagger.json` is not.
- Moq and `out` parameters: set up with `out It.Ref<long>.IsAny` and return through a custom
  delegate that has the same signature (see `IndexContentEnumeratorTests`).
- `IndexOptions.RegisterContentIndex<TIndexer, TSearcher, TStrategy>` accepts interface types as
  type arguments, which keeps tests free of fakes.
- In rebuild mode the element does not call `workspaceContext.setState('loading')`: Umbraco
  Search's `IndexRebuildCompleted` event can arrive before the next poll and would leave the
  workspace stuck in the loading view. It calls `setUserWaitingForIndexUpdate` instead.
- Umbraco Search's own toast for a finished rebuild reads "Search Index Rebuild Completed"; the
  E2E rebuild test waits for it.
- The `@umbraco-cms/search` npm package only ships types. Never bundle it; the importmap of the
  Umbraco Search package resolves `@umbraco-cms/search/global` and `/settings` at runtime.
```

- [ ] **Step 2: Update `README.md`**

```markdown
# umbraco-search-examine-reindex

Add-on for the Umbraco Search Examine provider that adds a **Reindex** box to the index details
page in the Umbraco backoffice.

## Why

Umbraco Search's built-in "Rebuild" repopulates the Examine index from a database cache of index
values. Changes to property value handlers or custom content indexers are therefore not applied to
content that is already cached. This package flushes that cache and re-collects the values.

## Features

- **Reindex**: refresh every content, media and member item in the index in place. The index
  stays searchable. Progress is shown in the box.
- **Also rebuild the index**: flush the cache and trigger Umbraco Search's rebuild afterwards.
- Load-balance aware: uses Umbraco Search's distributed refresher and rebuilder.
- Only shown on indexes served by the Examine provider.

## Installation

```
dotnet add package Umbraco.Community.Search.Examine.Reindex
```

Requires Umbraco 17 with Umbraco Search Core and the Examine provider configured.

## Development

See [CLAUDE.md](CLAUDE.md) for build, test and architecture notes, and
`docs/superpowers/specs` for the design.
```

- [ ] **Step 3: Final verification**

```bash
cd src/code/Umbraco.Community.Search.Examine.Reindex.Client && npm run build
cd ../../ && dotnet build && dotnet test
```

Expected: client build succeeds, `dotnet build` has 0 warnings and 0 errors, all 25 unit tests pass. With the test site running, `npm run test:e2e` in the E2E folder reports 5 passed.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "Document the reindex package in README and CLAUDE.md

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```
