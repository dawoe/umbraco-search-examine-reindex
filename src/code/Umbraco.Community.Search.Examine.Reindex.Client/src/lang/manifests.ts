import english from './en.js';

export const manifests: Array<UmbExtensionManifest> = [
  {
    type: 'localization',
    name: 'Umbraco Search Examine Reindex Localization - English',
    alias: 'Umbraco.Community.Search.Examine.Reindex.Localization.En',
    meta: { culture: 'en', localizations: english },
  },
];
