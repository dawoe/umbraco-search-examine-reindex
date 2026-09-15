import { getReindexStatus, reindex } from '../api/index.js';
import type { ReindexStatus as ReindexStatusModel } from '../api/types.gen.js';
import type { ReindexStatus } from '../types.js';
import { UmbRepositoryBase } from '@umbraco-cms/backoffice/repository';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';

/**
 * Talks to the reindex Management API for a single index.
 */
export class ReindexRepository extends UmbRepositoryBase {
  constructor(host: UmbControllerHost) {
    super(host);
  }

  async start(indexAlias: string, rebuildIndex: boolean) {
    const { data, error } = await tryExecute(
      this,
      reindex({ client: umbHttpClient, path: { indexAlias }, body: { rebuildIndex } }),
      { disableNotifications: true },
    );
    return { data: data ? this.#map(data) : undefined, error };
  }

  async getStatus(indexAlias: string) {
    const { data, error } = await tryExecute(
      this,
      getReindexStatus({ client: umbHttpClient, path: { indexAlias } }),
      { disableNotifications: true },
    );
    return { data: data ? this.#map(data) : undefined, error };
  }

  #map(model: ReindexStatusModel): ReindexStatus {
    return {
      indexAlias: model.indexAlias,
      state: model.state,
      rebuildIndex: model.rebuildIndex,
      processedItems: model.processedItems,
      totalItems: model.totalItems ?? undefined,
      startedAt: model.startedAt ?? undefined,
      completedAt: model.completedAt ?? undefined,
      errorMessage: model.errorMessage ?? undefined,
    };
  }
}
