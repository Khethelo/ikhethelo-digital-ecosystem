import { InjectionToken } from "@angular/core";
export interface Config {
  apiBaseUrl: string;
  tenantCode: string;
}
export const CONFIG = new InjectionToken<Config>("NTM_CONFIG");
export function validateConfig(value: unknown): Config {
  const config = value as Partial<Config> | null;
  // Same-origin API configuration keeps administrator tokens away from third-party origins.
  if (
    !config ||
    typeof config.apiBaseUrl !== "string" ||
    !/^\/(?!\/)[A-Za-z0-9/_-]+$/.test(config.apiBaseUrl) ||
    typeof config.tenantCode !== "string" ||
    !/^[A-Za-z][A-Za-z0-9-]{0,49}$/.test(config.tenantCode)
  )
    throw new Error("Invalid configuration.");
  return {
    apiBaseUrl: config.apiBaseUrl.replace(/\/$/, ""),
    tenantCode: config.tenantCode,
  };
}
