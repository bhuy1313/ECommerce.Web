import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  timeout: 30_000,
  fullyParallel: false,
  retries: 0,
  workers: 1,
  use: {
    baseURL: 'http://localhost:5135',
    trace: 'on-first-retry',
  },
  webServer: {
    command: 'dotnet run --project ECommerce.Web --urls http://localhost:5135',
    url: 'http://localhost:5135',
    reuseExistingServer: true,
    timeout: 120_000,
  },
});