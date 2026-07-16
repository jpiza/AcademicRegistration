import { Injectable } from '@angular/core';

type RuntimeConfig = {
  apiBaseUrl: string;
};

const DEFAULT_CONFIG: RuntimeConfig = {
  apiBaseUrl: '/api'
};

@Injectable({ providedIn: 'root' })
export class AppConfigService {
  private config: RuntimeConfig = DEFAULT_CONFIG;

  get apiBaseUrl(): string {
    return this.config.apiBaseUrl;
  }

  async load(): Promise<void> {
    try {
      const response = await fetch('app-config.json', { cache: 'no-store' });

      if (!response.ok) {
        return;
      }

      const config = (await response.json()) as Partial<RuntimeConfig>;
      this.config = {
        apiBaseUrl: this.normalizeApiBaseUrl(config.apiBaseUrl ?? DEFAULT_CONFIG.apiBaseUrl)
      };
    } catch {
      this.config = DEFAULT_CONFIG;
    }
  }

  private normalizeApiBaseUrl(value: string): string {
    return value.replace(/\/+$/, '') || DEFAULT_CONFIG.apiBaseUrl;
  }
}
