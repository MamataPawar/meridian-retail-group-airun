import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: 'sell', pathMatch: 'full' },
  { path: 'sell',          loadComponent: () => import('./features/sell/sell.component').then(m => m.SellComponent) },
  { path: 'customer',      loadComponent: () => import('./features/customer-lookup/customer-lookup.component').then(m => m.CustomerLookupComponent) },
  { path: 'resume-cart',   loadComponent: () => import('./features/resume-cart/resume-cart.component').then(m => m.ResumeCartComponent) },
  { path: 'payment',       loadComponent: () => import('./features/payment/payment.component').then(m => m.PaymentComponent) },
  { path: 'click-collect', loadComponent: () => import('./features/click-collect/click-collect.component').then(m => m.ClickCollectComponent) },
  { path: 'offline',       loadComponent: () => import('./features/offline-mode/offline-mode.component').then(m => m.OfflineModeComponent) },
  { path: '**', redirectTo: 'sell' }
];
