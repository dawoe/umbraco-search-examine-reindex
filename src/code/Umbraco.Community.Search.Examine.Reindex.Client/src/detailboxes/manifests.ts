import ReindexDetailBox from './reindex.ts';
import type { ManifestElement } from '@umbraco-cms/backoffice/extension-api';

// Make TypeScript recognize the searchIndexDetailBox type in this package
declare global {
  interface UmbExtensionManifestMap {
    mySearchIndexDetailBox: ManifestElement & { type: 'searchIndexDetailBox' };
  }
}

const detailBox : UmbExtensionManifest  = {
  type: 'searchIndexDetailBox',
  name: 'Umbraco Search Examine Reindex',
  alias: 'Umbraco.Community.Search.Examine.Reindex.Detailbox',
  element: ReindexDetailBox,
  weight: 150,
  meta: {
    label: 'Reindex',
    column: 'right',
  },
};

export const manifests = [detailBox];