import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { applyDocumentLanguage } from './app/core/i18n';

applyDocumentLanguage();

bootstrapApplication(App, appConfig)
  .catch((err) => console.error(err));
