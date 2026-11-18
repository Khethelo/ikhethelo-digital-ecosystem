import { bootstrapApplication } from "@angular/platform-browser";
import { provideBrowserGlobalErrorListeners } from "@angular/core";
import { provideHttpClient, withInterceptors } from "@angular/common/http";
import { provideRouter, withComponentInputBinding } from "@angular/router";
import { App, routes } from "./app/app";
import { CONFIG, validateConfig } from "./app/core/config";
import { sessionInterceptor } from "./app/core/auth";

fetch("/config.json", { cache: "no-store" })
  .then((response) => {
    if (!response.ok) throw new Error("Configuration unavailable.");
    return response.json();
  })
  .then((value) =>
    bootstrapApplication(App, {
      providers: [
        { provide: CONFIG, useValue: validateConfig(value) },
        provideBrowserGlobalErrorListeners(),
        provideRouter(routes, withComponentInputBinding()),
        provideHttpClient(withInterceptors([sessionInterceptor])),
      ],
    }),
  )
  .catch(() => {
    document.body.textContent =
      "NTM GreenPulse could not start. Check the application configuration and reload.";
  });
