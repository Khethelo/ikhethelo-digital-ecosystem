import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: './e2e', timeout: 60_000, workers: 1,
  expect: { timeout: 30_000 },
  reporter: [['list']],
  use: { baseURL: process.env['ESHL_BASE_URL'] ?? 'http://localhost:4200', trace: 'off', screenshot: 'off', video: 'off', launchOptions: { executablePath: process.env['CHROME_PATH'] ?? 'C:/Program Files/Google/Chrome/Application/chrome.exe' } },
});
