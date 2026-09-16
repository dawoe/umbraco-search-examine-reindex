# Reindex detail box for Umbraco Search (Examine provider)

Date: 2026-09-15
Status: approved design, ready for implementation planning

## Goal

Add a detail box to the Umbraco Search index workspace in the backoffice that lets a user
re-collect index values for all content in the current index ("reindex"), optionally followed by
a full rebuild of the Examine index.

## Background

Umbraco Search already ships a "Rebuild" entity action. It wipes the Examine index and repopulates
it, but `ContentIndexingDataCollectionService.CollectAsync` reads field values from the database
cache of index documents first. Changes to property value handlers or custom content indexers are
therefore not reflected for content that already has a cached document.

Umbraco Search exposes the pieces needed to fix that:

- `IDistributedContentIndexRefresher.RefreshContent/RefreshMedia/RefreshMember` flushes the cached
  index documents for the given entities and propagates a refresh to all load-balanced instances.
- `IIndexDocumentService.DeleteAsync(keys, published)` flushes cached documents directly.
- `IDistributedContentIndexRebuilder.Rebuild(indexAlias)` triggers the existing rebuild.

## Operations

| Operation | Behaviour |
|-----------|-----------|
| Reindex | For every content, media or member item contained in the index, flush its cached index document and refresh it in place through the distributed refresher. The index stays searchable. Deleted items are not removed. |
| Reindex with rebuild | Flush the cached index documents for all items contained in the index, then call the core rebuild so the Examine index is wiped and repopulated from freshly collected values. |

The box is only shown on indexes served by the Examine provider, using the existing
`Umb.Search.Condition.IndexProviderName` condition with `match: 'search-examine-provider'`.

## Server side (`Umbraco.Community.Search.Examine.Reindex`)

Folder layout follows the Umbraco Search repository: `Controllers`, `Services`, `Models`,
`DependencyInjection`, `Composing`.

### `IIndexContentReindexer` / `IndexContentReindexer` (singleton)

`Attempt<ReindexStatus, ReindexOperationStatus> Start(string indexAlias, bool rebuildIndex)`
with `enum ReindexOperationStatus { Success, IndexNotFound, AlreadyRunning }`.

1. Resolve the `ContentIndexRegistration` from `IOptions<IndexOptions>`. Unknown alias or a
   registration that is not a content index returns `IndexNotFound` (controller maps to 400).
2. If the status tracker reports `Running` for the alias, return `AlreadyRunning` carrying the
   current status (controller maps to 409).
3. Mark the alias `Running`, queue the job on `IBackgroundTaskQueue`, return `Success` with the
   new status.

The background job:

- Determines the content state from the registration's `ContentChangeStrategy` type:
  assignable to `IPublishedContentChangeStrategy` means published, assignable to
  `IDraftContentChangeStrategy` means draft, anything else means both states.
- For each object type in `ContainedObjectTypes`, pages through items in batches of 500:
  - Documents: `IContentService.GetPagedDescendants(-1, page, 500, out total)`. For the
    published state only items with `Published == true` are used.
  - Media: `IMediaService.GetPagedDescendants(-1, page, 500, out total)`.
  - Members: `IMemberService.GetAll(page, 500, out total)`.
- Reindex mode: per batch call the matching `IDistributedContentIndexRefresher.Refresh*` method
  with the resolved state.
- Rebuild mode: per batch call `IIndexDocumentService.DeleteAsync(keys, published)` for each
  applicable state. After the last batch call `IDistributedContentIndexRebuilder.Rebuild(alias)`.
  A `false` return is recorded as a failure.
- Updates the tracker after every batch (`ProcessedItems`, `TotalItems`).
- Checks the cancellation token between batches; cancellation records `Failed` with a
  "cancelled" message.
- Wraps the work in try/catch, logs exceptions with the alias, records `Failed` with the message.
- Marks the alias `Idle` with `CompletedAt` on success.

### `IReindexStatusTracker` / `ReindexStatusTracker` (singleton)

Backed by `ConcurrentDictionary<string, ReindexStatus>`. Unknown aliases return an `Idle` status.
A failed status stays until the next successful `Start` for that alias replaces it.

### Models

```csharp
public enum ReindexState { Idle, Running, Failed }   // JsonStringEnumConverter

public sealed record ReindexStatus(
    string IndexAlias,
    ReindexState State,
    bool RebuildIndex,
    long ProcessedItems,
    long? TotalItems,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage);

public sealed class ReindexRequestModel { public bool RebuildIndex { get; set; } }
```

### Controllers

Base class mirrors `ExamineApiControllerBase`:
`[ApiController]`, `[BackOfficeRoute("search-examine-reindex/api/v{version:apiVersion}")]`,
`[Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]`,
`[MapToApi("search-examine-reindex")]`, derives from `ManagementApiControllerBase`.

| Method | Route | Responses |
|--------|-------|-----------|
| POST | `index/{indexAlias}/reindex` body `{ rebuildIndex }` | 200 `ReindexStatus`, 400 unknown alias, 409 already running (body is current status) |
| GET | `index/{indexAlias}/reindex/status` | 200 `ReindexStatus` |

A custom `OperationIdHandler` scoped to the package's controller namespace yields the operation ids
`reindex` and `getReindexStatus`. A `SwaggerDoc` named `search-examine-reindex` with the
`BackOfficeSecurityRequirementsOperationFilterBase` filter provides the swagger document used
for client generation.

### Registration

`UmbracoBuilderExtensions.AddSearchExamineReindex(this IUmbracoBuilder builder)` registers the
services, swagger config and operation id handler. `SearchExamineReindexComposer : IComposer`
calls it so installing the package is sufficient. The package does not call
`AddExamineSearchProvider`; the host does that.

## Client side (`Umbraco.Community.Search.Examine.Reindex.Client`)

Types come from the `@umbraco-cms/search` npm package (`/global` and `/settings` entry points).
At runtime the imports resolve through Umbraco Search's importmap, so the bundle marks
`@umbraco-cms/*` as external.

### Generated API client

`src/api/` generated with `@hey-api/openapi-ts` from `swagger.json`. The `generate-api` npm script
downloads `https://localhost:44310/umbraco/swagger/search-examine-reindex/swagger.json` from the
running test site and then runs `openapi-ts`. `src/hey-api.ts` exports `createClientConfig` that
spreads `umbHttpClient.getConfig()` so calls are authenticated.

### `ReindexRepository` (`src/repository/reindex.repository.ts`)

Extends `UmbRepositoryBase`. Methods `start(indexAlias, rebuildIndex)` and `getStatus(indexAlias)`.
Both use `tryExecute` and map generated types to the domain types in `src/types.ts`:

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

### `ReindexDetailBoxElement` (`src/detailboxes/reindex-detail-box.element.ts`)

Custom element `search-examine-reindex-detail-box`, extends `UmbLitElement`. Replaces the
placeholder `reindex.ts`.

- Consumes `UMB_SEARCH_WORKSPACE_CONTEXT` (from `@umbraco-cms/search/settings`) for the index
  alias via `getUnique()` and for `setState('loading')`. Consumes `UMB_SEARCH_CONTEXT` (from
  `@umbraco-cms/search/global`) for `setUserWaitingForIndexUpdate`.
- Renders a `uui-box` containing: a short explanation (including that reindex does not remove
  deleted items and rebuild does), a `uui-toggle` "Also rebuild the index", and a `uui-button`
  "Reindex" whose `state` shows the waiting spinner. While a job is running the button is disabled
  and a `uui-progress-bar` plus "x of y items" text is shown. A failed status shows the error
  message in danger colour.
- Click flow: `umbConfirmModal` (warning colour; different content when rebuild is on), then
  `repository.start`, then a "Reindex started" toast, then poll `getStatus` every 3 seconds until
  state is not `Running`. Then a positive toast (reindex) or danger toast (failed).
- Rebuild mode: when the user confirms, call `searchContext.setUserWaitingForIndexUpdate(alias, true)`.
  Umbraco Search's own `IndexRebuildCompleted` handling then reloads the workspace and shows the
  completion toast, and the stats box shows the `Rebuilding` health status meanwhile. The element
  does not call `workspaceContext.setState('loading')`: on small sites the completed event can
  arrive before the next poll, which would leave the workspace stuck in the loading view.
- A 409 from `start` is treated as "attach to the running job": begin polling, no error toast.
- Transient polling errors are ignored and retried on the next tick. Polling stops in
  `disconnectedCallback`.
- On first render the element fetches the status once so a page refresh during a run resumes
  showing progress.
- Interactive elements carry `data-mark` attributes for E2E selectors:
  `search-examine-reindex:toggle-rebuild`, `search-examine-reindex:button-reindex`,
  `search-examine-reindex:progress`.

### Manifests

- `searchIndexDetailBox`, alias `Umbraco.Community.Search.Examine.Reindex.DetailBox`,
  `weight: 150`, `meta: { label: '#searchExamineReindex_boxLabel', column: 'right' }`,
  `conditions: [{ alias: 'Umb.Search.Condition.IndexProviderName', match: 'search-examine-provider' }]`.
  Element is imported directly (single bundle). The local `UmbExtensionManifestMap` declaration is
  removed; the type ships in `@umbraco-cms/search/global`.
- `localization` manifest for culture `en` with dictionary key `searchExamineReindex`
  (`boxLabel`, `description`, `rebuildToggle`, `button`, `confirmHeadline`, `confirmMessage`,
  `confirmMessageRebuild`, `confirmLabel`, `startedTitle`, `startedMessage`, `completedTitle`,
  `completedMessage`, `failedTitle`, `progress`).

### Housekeeping

- Remove the `paths` entries in `tsconfig.json` that point at non-existent local Search sources.
- Keep `/^@umbraco/` in `rollupOptions.external`; it already covers `@umbraco-cms/search`.

## Error handling summary

| Situation | Server | Client |
|-----------|--------|--------|
| Unknown / non-content alias | 400 problem details | Danger toast with message |
| Already running | 409 with status | Start polling, no toast |
| Batch exception | Log, `Failed` + message, stop | Danger toast, message shown in box |
| Shutdown / cancellation | `Failed` "cancelled" | Shown after refresh |
| `Rebuild` returns false | `Failed` with message | Danger toast |
| Polling network error | n/a | Ignore, retry next tick |
| Permissions | `SectionAccessSettings` policy | Same policy as Search workspace |

## Testing

### C# unit tests (`Umbraco.Community.Search.Examine.Reindex.Tests`, NUnit + Moq)

- `IndexContentReindexerTests`: unknown alias, non-content registration, already running,
  published vs draft vs custom strategy state resolution, batching across object types, published
  filter for documents, refresh calls in reindex mode, cache flush and rebuild call in rebuild mode,
  `Rebuild` returning false, exception recording, cancellation. The background queue mock runs the
  work item inline.
- `ReindexStatusTrackerTests`: idle default, set/get, replace.
- `ReindexApiControllerTests`: 200, 400 and 409 mapping for POST; 200 for GET.

### E2E tests (`src/tests/Umbraco.Community.Search.Examine.Reindex.E2E`)

Own `package.json` with `@playwright/test`, `@umbraco/playwright-testhelpers`, `dotenv`;
`playwright.config.ts` (`testIdAttribute: 'data-mark'`, `STORAGE_STAGE_PATH`, setup project);
`tests/auth.setup.ts`; `.env.example` with `UMBRACO_URL=https://localhost:44310`,
`UMBRACO_USER_LOGIN=admin@example.com`, `UMBRACO_USER_PASSWORD=1234567890`. Runs against an
already started test site. Not part of CI for now.

Specs:

1. Box is visible on the `Umb_PublishedContent` index workspace.
2. Clicking Reindex opens the confirm modal; cancelling does nothing.
3. Confirming shows the started toast, the status API reports `Running` then `Idle`.
4. With the rebuild toggle on, confirming ends with Umbraco Search's rebuild completed toast.

### Verification before completion

`npm run build` (client), `dotnet build` and `dotnet test` in `src`, E2E run against the test site,
and a manual click-through in the backoffice.

## Repository housekeeping

- Root `CLAUDE.md`: overview, build/test commands for the three projects and the test site,
  architecture summary, coding conventions (StyleCop header, file-scoped namespaces, expression
  bodies, `var` rules), gotchas list maintained during implementation.
- `Directory.Build.props`: fix `PackageProjectUrl` and `RepositoryUrl` (currently point at
  `umbraco-pagespeed-optimization`); add `assets/logo.png` and `assets/readme.md` so packing works.
- `.gitignore`: add `.env`, `.auth/`, `test-results/`, `playwright-report/`, `swagger.json`.

## Out of scope

- Persisted or cross-instance job status (approach C in the brainstorm).
- Reindexing a subset of content (by content type or subtree).
- Showing the box for non-Examine providers.
- Running E2E tests in CI.
