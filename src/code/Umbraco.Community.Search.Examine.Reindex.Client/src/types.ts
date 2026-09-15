export type ReindexState = 'Idle' | 'Running' | 'Failed';

export interface ReindexStatus {
  indexAlias: string;
  state: ReindexState;
  rebuildIndex: boolean;
  processedItems: number;
  totalItems?: number;
  startedAt?: string;
  completedAt?: string;
  errorMessage?: string;
}
