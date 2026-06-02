import { defineConfig, devices } from '@playwright/test';

const adminWebUrl = process.env.ADMIN_WEB_URL ?? 'http://localhost:4200';
const boothUiUrl = process.env.BOOTH_UI_URL ?? 'http://localhost:4201';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: [['list'], ['html', { open: 'never' }]],
  timeout: 120_000,
  expect: {
    timeout: 15_000,
  },
  use: {
    ...devices['Desktop Chrome'],
    baseURL: adminWebUrl,
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
    video: 'retain-on-failure',
  },
  webServer: [
    {
      command: 'npm run start:admin',
      url: adminWebUrl,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
    },
    {
      command: 'npm run start:booth',
      url: boothUiUrl,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
    },
  ],
});
