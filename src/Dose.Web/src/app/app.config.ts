import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, isDevMode, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideServiceWorker } from '@angular/service-worker';
import { TitleStrategy, provideRouter, withComponentInputBinding } from '@angular/router';
import { TranslatedTitleStrategy, languageInterceptor } from './core/i18n';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withFetch(), withInterceptors([languageInterceptor])),
    { provide: TitleStrategy, useClass: TranslatedTitleStrategy },
    // PWA: installable, fast repeat visits, offline fallback. Off in development so code changes are never stale.
    provideServiceWorker('ngsw-worker.js', { enabled: !isDevMode(), registrationStrategy: 'registerWhenStable:30000' }),
  ],
};
