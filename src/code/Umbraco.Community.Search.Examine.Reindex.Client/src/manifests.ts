import type { UmbBackofficeExtensionRegistry } from '@umbraco-cms/backoffice/extension-registry';
import { manifests as detailBoxManifests } from './detailboxes/manifests.js';
import { manifests as localizationManifests } from './lang/manifests.js';

export function registerManifest(registry: UmbBackofficeExtensionRegistry) {
  registry.registerMany([...localizationManifests, ...detailBoxManifests]);
}
