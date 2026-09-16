# Umbraco.Community.Search.Examine.Reindex

Adds a **Reindex** box to the index details page of Umbraco Search (Examine provider) in the
Umbraco backoffice.

- **Reindex**: flushes the cached index values for every item in the index and re-collects them
  from the content, so changes to property value handlers and custom content indexers are applied.
  The index stays searchable while this runs.
- **Also rebuild the index**: flushes the cache and then triggers Umbraco Search's index rebuild,
  which wipes and repopulates the Examine index.

## Good to know

- "Completed" means every item has been handed off to Umbraco's background indexing queue, not
  that the index has necessarily caught up yet. On large sites the index may keep processing for a
  while after the box reports completion.
- Reindexing runs through Umbraco Search's distributed refresher, which applies to every index
  that shares the same content change strategy (for example all published-content indexes) — not
  only the index whose page you triggered the reindex from.

## Installation

```
dotnet add package Umbraco.Community.Search.Examine.Reindex
```

The package registers itself through a composer. It requires Umbraco Search Core and the Examine
provider to be configured (`AddSearchCore()` and `AddExamineSearchProvider()`).

## Usage

Settings → Advanced → Search → pick an index → the **Reindex** box on the right.
