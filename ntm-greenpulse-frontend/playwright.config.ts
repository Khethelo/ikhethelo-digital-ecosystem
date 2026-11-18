import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "./e2e",
  timeout: 60000,
  expect: { timeout: 10000 },
  workers: 1,
  reporter: [["list"]],
  use: {
    baseURL: "http://127.0.0.1:4301",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    launchOptions: {
      executablePath:
        process.env["CHROME_PATH"] ??
        "C:/Program Files/Google/Chrome/Application/chrome.exe",
    },
  },
  webServer: {
    command: "node scripts/preview.mjs --port 4301",
    url: "http://127.0.0.1:4301/login",
    reuseExistingServer: false,
    timeout: 30000,
  },
});
