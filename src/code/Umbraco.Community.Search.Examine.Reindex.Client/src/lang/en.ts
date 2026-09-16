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
