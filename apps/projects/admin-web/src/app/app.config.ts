import { FullscreenOverlayContainer, OverlayContainer } from '@angular/cdk/overlay';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withFetch()),
    provideAnimationsAsync(),
    provideRouter(routes),
    { provide: OverlayContainer, useClass: FullscreenOverlayContainer },
  ],
};
