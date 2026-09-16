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
- `src/tests/Umbraco.Community.Search.Examine.Reindex.Tests/` — NUnit + Moq unit tests (25 tests)
- `src/tests/Umbraco.Community.Search.Examine.Reindex.E2E/` — Playwright end-to-end tests (5
  tests: 1 auth setup + 4 specs)
- `test-sites/Website-V18/` — Umbraco 18 test site referencing the package project (in the
  solution). `test-sites/Website-V17/` is kept on disk for the 17.x line but is not in the
  solution.
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
cd test-sites/Website-V18
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
- The client pins TypeScript 6.0: `@hey-api/openapi-ts` (now 0.99.0 — `@umbraco-cms/backoffice`
  18 peer-requires `>=0.97.0 <1.0.0`, so the old 0.85.2 pin fails `npm install` with ERESOLVE)
  still cannot run on TypeScript 7 (the native rewrite removes the classic compiler API), so
  `npm run generate-api` fails silently with it. The old TS2578 failure on 5.8/5.9 (`Unused
  '@ts-expect-error' directive` in the generated `client.gen.ts`) is gone under 0.99.0 — TS 5.9
  now type-checks the generated output cleanly too — but the pin stays at 6.0.3 to match the
  sibling `Umbraco.Cms.Search` client projects.
- `@hey-api/openapi-ts` 0.99 resolves `runtimeConfigPath` relative to `process.cwd()`, not the
  output directory as earlier versions did; that is why `openapi-ts.config.ts` now points it at
  `./src/hey-api.ts`.
- The client build must run before `dotnet build`: the Razor SDK project ships whatever is in
  `wwwroot/App_Plugins/searchreindex`, which is gitignored.
- `npm run generate-api` needs the test site running on https://localhost:44310. The generated
  `src/api` folder is committed; `openapi.json` is not.
- Umbraco 18 replaced Swashbuckle with `Microsoft.AspNetCore.OpenApi`. The document is registered
  with `builder.AddBackOfficeOpenApiDocument(name, ...)`; `WithBackOfficeAuthentication()` replaces
  `BackOfficeSecurityRequirementsOperationFilterBase`, and operation IDs come from an
  `AddOperationTransformer` callback because `IOperationIdHandler` was removed. The transformer is
  scoped to this package's document, so unlike the old global `OperationIdHandler` it needs no
  controller-namespace check.
- A class library that registers an OpenAPI document needs
  `<InterceptorsNamespaces>$(InterceptorsNamespaces);Microsoft.AspNetCore.OpenApi.Generated</InterceptorsNamespaces>`
  in its csproj, or the build fails with `CS9137: The 'interceptors' feature is not enabled`.
- The OpenAPI document moved from `/umbraco/swagger/{name}/swagger.json` to
  `/umbraco/openapi/{name}.json`, and is now OpenAPI 3.1 rather than 3.0. It is pretty-printed, so
  any grep against it must tolerate whitespace after the colon (`'"operationId": *"..."'`, not
  `'"operationId":"..."'`). OpenAPI 3.1 also renders a C# `long` as `"type": ["integer","string"]`,
  so generated int64 fields (`ReindexStatus.processedItems`/`totalItems`) arrive as
  `number | string`; `ReindexRepository#map` coerces them with `Number(...)`, while the app's own
  `ReindexStatus` in `src/types.ts` stays `number`.
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
- `Direction` (member ordering) lives in `Umbraco.Cms.Core`, not in a `Persistence` namespace.
- MVC application-part discovery for the test site is incremental on `project.assets.json`: after
  adding the first controller to a referenced project, run `dotnet clean` in
  `test-sites/Website-V18` or the controllers 404 until the next full rebuild. Never work around
  this with `AddControllers().AddApplicationPart(...)` from a composer.
- `IIndexContentReindexer` is registered transient (not singleton) because it captures Umbraco
  Search's transient `IDistributedContentIndexRefresher` and `IDistributedContentIndexRebuilder`;
  the shared state lives in the singleton `IReindexStatusTracker`.
- `tryExecute` shows Umbraco's generic error toast by default; the repository passes
  `{ disableNotifications: true }` because the element owns all user-facing messages (409 must
  attach silently, polling errors must stay silent).
- Notification data uses `headline`, not `title`; `title` is silently ignored and the toast shows
  only the message.
- E2E: Playwright's built-in `request` fixture is not authenticated against the backoffice API;
  use `umbracoApi.get(...)` from the testhelpers fixture.
- `@umbraco/playwright-testhelpers` is deprecated: Umbraco 18 moved backoffice auth to httpOnly
  OAuth2/PKCE cookies (`__Host-umbAccessToken`), and that package's last release still expects
  Umbraco 17's `localStorage` token, so it crashes in fixture setup. Use
  `@umbraco-cms/acceptance-test-helpers` instead — same `test`/`ApiHelpers`/`UiHelpers`/
  `ConstantHelper` surface, but it reads `STORAGE_STATE_PATH` (the old package read the typo'd
  `STORAGE_STAGE_PATH`) and brings its own `tslib`, so the E2E project no longer needs an explicit
  `tslib` dependency.
- Booting the test site makes Umbraco append an `Imaging.HMACSecretKey` block to
  `test-sites/Website-V18/appsettings.json`. It is a generated secret and must never be committed
  — `git checkout -- test-sites/Website-V18/appsettings.json` after any local run that dirties it.
- Start the test site with plain `dotnet run`. Passing `--no-launch-profile` skips the launch
  profile that sets `ASPNETCORE_ENVIRONMENT=Development`, so the site runs as Production, the
  unattended install silently never happens, and the site appears up but has no database.
- `"DOM.Iterable"` is in the client tsconfig `lib` because the generated hey-api client iterates
  `URLSearchParams`.
- The E2E specs deliberately do not assert an intermediate `Running` status because small sites
  finish before the first poll.
