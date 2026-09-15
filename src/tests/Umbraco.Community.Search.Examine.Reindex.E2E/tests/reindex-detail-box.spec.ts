import { expect, type Page } from '@playwright/test';
import { ApiHelpers, test } from '@umbraco/playwright-testhelpers';

const INDEX_ALIAS = 'Umb_PublishedContent';
const WORKSPACE_PATH = `/umbraco/section/settings/workspace/search-index/edit/${INDEX_ALIAS}`;
const STATUS_PATH = `/umbraco/search-examine-reindex/api/v1/index/${INDEX_ALIAS}/reindex/status`;

// The confirm modal (Umbraco's own `umbConfirmModal`) is a custom element whose content is
// projected into a native <dialog> through several layers of shadow-DOM slots. Locators chained
// off `page.getByRole('dialog')` (e.g. `dialog.getByText(...)`, `dialog.getByRole('button', ...)`)
// never resolve against that slotted content and hang until timeout, even though the same content
// shows up fine in an aria snapshot. Querying the page directly for the confirm/cancel buttons
// (disambiguated from our own "Reindex" button by the absence of a `data-mark` attribute) avoids
// that dead end entirely.
const confirmModalHeadline = (page: Page) => page.getByRole('heading', { name: 'Reindex Search Index' });
const confirmModalButton = (page: Page, name: 'Reindex' | 'Cancel') =>
  page.locator(`uui-button[label="${name}" i]:not([data-mark])`);

// Immediately after the workspace loads, the very first click on the Reindex button is
// occasionally swallowed (the backoffice's web components are still upgrading/hydrating), so the
// confirm modal does not open. A second click always works. Retry once, bounded by a short
// explicit wait for the modal headline rather than a fixed sleep, to keep the tests deterministic.
async function openReindexConfirmModal(page: Page) {
  const reindexButton = page.getByTestId('search-examine-reindex:button-reindex');
  const headline = confirmModalHeadline(page);

  await reindexButton.click();
  try {
    await headline.waitFor({ state: 'visible', timeout: 5_000 });
  } catch {
    await reindexButton.click();
    await headline.waitFor({ state: 'visible', timeout: 15_000 });
  }
}

interface StatusBody {
  state: 'Idle' | 'Running' | 'Failed';
  rebuildIndex: boolean;
}

// The plain Playwright `request` fixture does not attach the backoffice bearer token, so the
// status endpoint returns 401. `umbracoApi.get` (from the testhelpers fixture) attaches it from
// STORAGE_STAGE_PATH, so we use that for authenticated API calls instead.
async function readStatus(umbracoApi: ApiHelpers, baseURL: string): Promise<StatusBody> {
  const response = await umbracoApi.get(baseURL + STATUS_PATH);
  expect(response.ok()).toBeTruthy();
  return (await response.json()) as StatusBody;
}

async function waitForIdle(umbracoApi: ApiHelpers, baseURL: string) {
  await expect
    .poll(async () => (await readStatus(umbracoApi, baseURL)).state, { timeout: 60_000, intervals: [1000] })
    .toBe('Idle');
}

test.describe('Reindex detail box', () => {
  test.beforeEach(async ({ umbracoUi, umbracoApi, baseURL }) => {
    await waitForIdle(umbracoApi, baseURL!);
    await umbracoUi.goToBackOffice();
    await umbracoUi.page.goto(WORKSPACE_PATH);
    await umbracoUi.page.locator('search-examine-reindex-detail-box').waitFor({ timeout: 30_000 });
  });

  test('shows the box on an Examine index', async ({ umbracoUi }) => {
    const box = umbracoUi.page.locator('search-examine-reindex-detail-box');
    await expect(box.getByText('Reindex', { exact: true }).first()).toBeVisible();
    await expect(umbracoUi.page.getByTestId('search-examine-reindex:button-reindex')).toBeEnabled();
    await expect(umbracoUi.page.getByTestId('search-examine-reindex:toggle-rebuild')).toBeVisible();
  });

  test('cancelling the confirm modal does not start a reindex', async ({ umbracoUi, umbracoApi, baseURL }) => {
    await openReindexConfirmModal(umbracoUi.page);
    await expect(confirmModalHeadline(umbracoUi.page)).toBeVisible();
    await confirmModalButton(umbracoUi.page, 'Cancel').click();

    await expect(confirmModalHeadline(umbracoUi.page)).toBeHidden();
    expect((await readStatus(umbracoApi, baseURL!)).state).toBe('Idle');
  });

  test('reindex runs and completes', async ({ umbracoUi, umbracoApi, baseURL }) => {
    await openReindexConfirmModal(umbracoUi.page);
    await confirmModalButton(umbracoUi.page, 'Reindex').click();

    // Umbraco's default toast layout only renders the notification's `message`, not its `title`
    // ("Reindex started" / "Reindex completed" never appear on screen), so we assert on the
    // (localized, alias-interpolated) message text instead. The same text is duplicated into a
    // visually-hidden `#sr-live` region for screen readers, so `.first()` disambiguates.
    await expect(umbracoUi.page.getByText(/has started\. You can continue working/i).first()).toBeVisible();
    await waitForIdle(umbracoApi, baseURL!);
    await expect(umbracoUi.page.getByText(/has been queued for reindexing/i).first()).toBeVisible({
      timeout: 15_000,
    });
    expect((await readStatus(umbracoApi, baseURL!)).rebuildIndex).toBe(false);
  });

  test('reindex with rebuild triggers the search index rebuild', async ({ umbracoUi, umbracoApi, baseURL }) => {
    await umbracoUi.page.getByTestId('search-examine-reindex:toggle-rebuild').click();
    await openReindexConfirmModal(umbracoUi.page);
    await confirmModalButton(umbracoUi.page, 'Reindex').click();

    await expect(umbracoUi.page.getByText(/has started\. You can continue working/i).first()).toBeVisible();
    await waitForIdle(umbracoApi, baseURL!);
    expect((await readStatus(umbracoApi, baseURL!)).rebuildIndex).toBe(true);
    // Umbraco Search's own "rebuild completed" toast has the same title-not-rendered behavior;
    // its message is "The rebuild of search index "{0}" has completed successfully."
    await expect(umbracoUi.page.getByText(/has completed successfully/i).first()).toBeVisible({ timeout: 60_000 });
  });
});
