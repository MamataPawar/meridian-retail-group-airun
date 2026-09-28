import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: 'dashboard',    loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent) },
  { path: 'customers',    loadComponent: () => import('./features/customers/customers.component').then(m => m.CustomersComponent) },
  { path: 'customers/:id',loadComponent: () => import('./features/customers/customer-detail.component').then(m => m.CustomerDetailComponent) },
  { path: 'merge-queue',  loadComponent: () => import('./features/merge-queue/merge-queue.component').then(m => m.MergeQueueComponent) },
  { path: 'inventory',    loadComponent: () => import('./features/inventory/inventory.component').then(m => m.InventoryComponent) },
  { path: 'orders',       loadComponent: () => import('./features/orders/orders.component').then(m => m.OrdersComponent) },
  { path: 'orders/:id',   loadComponent: () => import('./features/orders/order-detail.component').then(m => m.OrderDetailComponent) },
  { path: 'loyalty',      loadComponent: () => import('./features/loyalty/loyalty.component').then(m => m.LoyaltyComponent) },
  { path: 'stores',       loadComponent: () => import('./features/stores/stores.component').then(m => m.StoresComponent) },
  { path: 'promotions',   loadComponent: () => import('./features/promotions/promotions.component').then(m => m.PromotionsComponent) },
  { path: '**', redirectTo: 'dashboard' }
];
