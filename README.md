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

Requires Umbraco 17 with Umbraco Search Core and the Examine provider configured.

## Development

See [CLAUDE.md](CLAUDE.md) for build, test and architecture notes, and
`docs/superpowers/specs` for the design.
