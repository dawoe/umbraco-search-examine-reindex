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
- The client pins TypeScript 6.0: `@hey-api/openapi-ts` cannot run on TypeScript 7 (the native
  rewrite removes the classic compiler API), so `npm run generate-api` fails silently with it.
  TypeScript 5.8/5.9 install and generate fine but fail `tsc --noEmit` on the generated
  `src/api/client/client.gen.ts` (`error TS2578: Unused '@ts-expect-error' directive` at the
  `beforeRequest` destructure) — TS 6.0.3 (matching the pin already used by the sibling
  `Umbraco.Cms.Search` client projects) is the lowest version that both runs the generator and
  type-checks the generated output cleanly.
- (add entries here as implementation reveals them)
