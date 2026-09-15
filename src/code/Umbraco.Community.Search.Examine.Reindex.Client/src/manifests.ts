import type { UmbBackofficeExtensionRegistry } from "@umbraco-cms/backoffice/extension-registry";
import { manifests as DetailBoxesManifests } from "./detailboxes/manifests";

export function registerManifest(registry: UmbBackofficeExtensionRegistry) {
  registry.registerMany([...DetailBoxesManifests]);
}
