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
