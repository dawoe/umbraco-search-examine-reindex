import { ReindexRepository } from '../repository/reindex.repository.js';
import type { ReindexStatus } from '../types.js';
import { css, customElement, html, nothing, state } from '@umbraco-cms/backoffice/external/lit';
import type { UUIButtonState, UUIToggleElement } from '@umbraco-cms/backoffice/external/uui';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { umbConfirmModal } from '@umbraco-cms/backoffice/modal';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UmbApiError } from '@umbraco-cms/backoffice/resources';
import { UmbTextStyles } from '@umbraco-cms/backoffice/style';
import { UMB_SEARCH_CONTEXT } from '@umbraco-cms/search/global';
import { UMB_SEARCH_WORKSPACE_CONTEXT } from '@umbraco-cms/search/settings';

const POLL_INTERVAL_MS = 3000;

@customElement('search-examine-reindex-detail-box')
export class ReindexDetailBoxElement extends UmbLitElement {
  #repository = new ReindexRepository(this);
  #searchContext?: typeof UMB_SEARCH_CONTEXT.TYPE;
  #notificationContext?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;
  #pollTimer?: ReturnType<typeof setTimeout>;

  @state()
  private _indexAlias?: string;

  @state()
  private _rebuildIndex = false;

  @state()
  private _status?: ReindexStatus;

  @state()
  private _buttonState?: UUIButtonState;

  constructor() {
    super();

    this.consumeContext(UMB_SEARCH_WORKSPACE_CONTEXT, (context) => {
      this.observe(
        context?.unique,
        (unique) => {
          this._indexAlias = unique ?? undefined;
          this._status = undefined;
          this.#stopPolling();
          if (this._indexAlias) void this.#refreshStatus();
        },
        '_observeUnique',
      );
    });

    this.consumeContext(UMB_SEARCH_CONTEXT, (context) => (this.#searchContext = context));
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (context) => (this.#notificationContext = context));
  }

  override disconnectedCallback() {
    super.disconnectedCallback();
    this.#stopPolling();
  }

  async #refreshStatus() {
    const alias = this._indexAlias;
    if (!alias) return;

    const { data } = await this.#repository.getStatus(alias);
    // The element may have been removed or switched to another index while the request was in flight.
    if (!this.isConnected || this._indexAlias !== alias) return;

    if (!data) {
      // transient error: keep polling while we believe a job is running
      if (this._status?.state === 'Running') this.#schedulePoll();
      return;
    }

    this.#applyStatus(data);
  }

  #applyStatus(status: ReindexStatus) {
    const wasRunning = this._status?.state === 'Running';
    this._status = status;

    if (status.state === 'Running') {
      this.#schedulePoll();
      return;
    }

    this.#stopPolling();
    if (!wasRunning) return;

    if (status.state === 'Failed') {
      this.#notificationContext?.peek('danger', {
        data: {
          headline: this.localize.term('searchExamineReindex_failedTitle'),
          message: status.errorMessage ?? '',
        },
      });
      return;
    }

    if (!status.rebuildIndex) {
      this.#notificationContext?.peek('positive', {
        data: {
          headline: this.localize.term('searchExamineReindex_completedTitle'),
          message: this.localize.term('searchExamineReindex_completedMessage', status.indexAlias),
        },
      });
    }
    // In rebuild mode Umbraco Search shows its own "rebuild completed" toast and reloads the workspace.
  }

  #schedulePoll() {
    if (!this.isConnected) return;
    this.#stopPolling();
    this.#pollTimer = setTimeout(() => void this.#refreshStatus(), POLL_INTERVAL_MS);
  }

  #stopPolling() {
    if (this.#pollTimer) {
      clearTimeout(this.#pollTimer);
      this.#pollTimer = undefined;
    }
  }

  #onToggleChange(event: Event) {
    this._rebuildIndex = (event.target as UUIToggleElement).checked;
  }

  async #onReindexClick() {
    const alias = this._indexAlias;
    const rebuildIndex = this._rebuildIndex;
    if (!alias) return;

    try {
      await umbConfirmModal(this, {
        color: 'warning',
        headline: this.localize.term('searchExamineReindex_confirmHeadline'),
        content: this.localize.term(
          rebuildIndex
            ? 'searchExamineReindex_confirmMessageRebuild'
            : 'searchExamineReindex_confirmMessage',
          alias,
        ),
        confirmLabel: this.localize.term('searchExamineReindex_confirmLabel'),
      });
    } catch {
      return; // cancelled
    }

    this._buttonState = 'waiting';
    if (rebuildIndex) {
      this.#searchContext?.setUserWaitingForIndexUpdate(alias, true);
    }

    const { data, error } = await this.#repository.start(alias, rebuildIndex);
    // The element may have been removed or switched to another index while the request was in flight.
    if (!this.isConnected || this._indexAlias !== alias) return;

    this._buttonState = undefined;

    if (data) {
      this.#notificationContext?.peek('warning', {
        data: {
          headline: this.localize.term('searchExamineReindex_startedTitle'),
          message: this.localize.term('searchExamineReindex_startedMessage', alias),
        },
      });
      this._status = undefined;
      this.#applyStatus(data);
      return;
    }

    if (UmbApiError.isUmbApiError(error) && error.status === 409) {
      // already running: attach to the running job
      await this.#refreshStatus();
      if (rebuildIndex && this._status && !this._status.rebuildIndex) {
        this.#searchContext?.setUserWaitingForIndexUpdate(alias, false);
      }
      return;
    }

    if (rebuildIndex) {
      this.#searchContext?.setUserWaitingForIndexUpdate(alias, false);
    }

    this.#notificationContext?.peek('danger', {
      data: {
        headline: this.localize.term('searchExamineReindex_failedTitle'),
        message: error?.message ?? '',
      },
    });
  }

  override render() {
    const running = this._status?.state === 'Running';

    return html`
      <uui-box headline=${this.localize.term('searchExamineReindex_boxLabel')}>
        <p>${this.localize.term('searchExamineReindex_description')}</p>

        <uui-toggle
          data-mark="search-examine-reindex:toggle-rebuild"
          label=${this.localize.term('searchExamineReindex_rebuildToggle')}
          .checked=${this._rebuildIndex}
          ?disabled=${running}
          @change=${this.#onToggleChange}></uui-toggle>

        ${running ? this.#renderProgress() : nothing}
        ${this._status?.state === 'Failed'
          ? html`<p class="error">${this._status.errorMessage}</p>`
          : nothing}

        <uui-button
          data-mark="search-examine-reindex:button-reindex"
          look="primary"
          color="warning"
          label=${this.localize.term('searchExamineReindex_button')}
          .state=${this._buttonState}
          ?disabled=${running || !this._indexAlias}
          @click=${this.#onReindexClick}></uui-button>
      </uui-box>
    `;
  }

  #renderProgress() {
    const status = this._status!;
    const percentage = status.totalItems
      ? Math.min(100, Math.round((status.processedItems / status.totalItems) * 100))
      : 0;

    return html`
      <div class="progress" data-mark="search-examine-reindex:progress">
        <uui-progress-bar .progress=${percentage}></uui-progress-bar>
        <small>
          ${this.localize.term(
            'searchExamineReindex_progress',
            status.processedItems,
            status.totalItems ?? '?',
          )}
        </small>
      </div>
    `;
  }

  static override styles = [
    UmbTextStyles,
    css`
      :host {
        display: block;
      }

      uui-toggle,
      .progress {
        display: block;
        margin-bottom: var(--uui-size-space-4);
      }

      .error {
        color: var(--uui-color-danger);
      }
    `,
  ];
}

export default ReindexDetailBoxElement;

declare global {
  interface HTMLElementTagNameMap {
    'search-examine-reindex-detail-box': ReindexDetailBoxElement;
  }
}
