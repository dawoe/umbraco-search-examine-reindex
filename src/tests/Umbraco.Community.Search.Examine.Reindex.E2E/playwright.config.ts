import 'dotenv/config';
import { defineConfig, devices } from '@playwright/test';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));

export const STORAGE_STATE = join(__dirname, '.auth/user.json');

// The Umbraco testhelpers read the auth token from this file.
process.env.STORAGE_STATE_PATH = STORAGE_STATE;
// The testhelpers also read URL from this variable.
process.env.URL = process.env.UMBRACO_URL ?? 'https://localhost:44310';

export default defineConfig({
  testDir: './tests',
  timeout: 60 * 1000,
  expect: { timeout: 10 * 1000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: 'list',
  use: {
    baseURL: process.env.UMBRACO_URL ?? 'https://localhost:44310',
    ignoreHTTPSErrors: true,
    trace: 'retain-on-failure',
    // Umbraco marks elements with data-mark, not data-testid.
    testIdAttribute: 'data-mark',
  },
  projects: [
    {
      name: 'setup',
      testMatch: '**/*.setup.ts',
    },
    {
      name: 'e2e',
      testMatch: '**/*.spec.ts',
      dependencies: ['setup'],
      use: {
        ...devices['Desktop Chrome'],
        ignoreHTTPSErrors: true,
        storageState: STORAGE_STATE,
      },
    },
  ],
});
