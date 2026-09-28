import { bootstrapApplication } from '@angular/platform-browser';
import { PosRootComponent } from './app/app.component';
import { appConfig } from './app/app.config';

bootstrapApplication(PosRootComponent, appConfig).catch(err => console.error(err));
