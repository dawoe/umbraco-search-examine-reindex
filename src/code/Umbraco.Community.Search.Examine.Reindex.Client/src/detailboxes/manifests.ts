
const detailBox : UmbExtensionManifest  = {
  type: 'searchIndexDetailBox',
  name: 'Umbraco Search Examine Reindex',
  alias: 'Umbraco.Community.Search.Examine.Reindex.Detailbox',
  element: () => import('./reindex.ts'),
  weight: 150,
  meta: {
    label: 'Reindex',
    column: 'right',
  }
}

export const manifests = [detailBox];