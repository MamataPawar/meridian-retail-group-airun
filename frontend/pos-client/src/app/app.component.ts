import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { PosCartService } from './core/pos-cart.service';
import { OfflineQueueService } from './core/offline-queue.service';
import { environment } from '../environments/environment';

interface NavItem { icon: string; label: string; route: string; }

@Component({
  selector: 'pos-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, MatIconModule],
  template: `
    <div class="pos-layout">
      <!-- Top bar -->
      <div class="pos-topbar">
        <mat-icon style="color:#818cf8">point_of_sale</mat-icon>
        <span style="font-weight:700;font-size:1rem">Retail POS</span>
        <span class="store-id">{{storeId}}</span>
        <div class="spacer"></div>

        <!-- Cart summary -->
        <div style="display:flex;align-items:center;gap:8px;margin-right:16px"
             *ngIf="cart.itemCount() > 0">
          <mat-icon style="color:#a5b4fc">shopping_cart</mat-icon>
          <span style="font-weight:600">{{cart.itemCount()}} items</span>
          <span style="color:#6ee7b7;font-weight:700">€{{cart.total() | number:'1.2-2'}}</span>
        </div>

        <!-- Offline badge -->
        <div *ngIf="!offline.isOnline()" class="badge red" style="display:flex;align-items:center;gap:4px">
          <mat-icon style="font-size:14px">wifi_off</mat-icon> OFFLINE
          <span *ngIf="offline.queueDepth() > 0">({{offline.queueDepth()}} queued)</span>
        </div>
        <div *ngIf="offline.isOnline()" class="badge green" style="display:flex;align-items:center;gap:4px">
          <mat-icon style="font-size:14px">wifi</mat-icon> Online
        </div>

        <mat-icon style="margin-left:12px;color:rgba(255,255,255,.5)">account_circle</mat-icon>
      </div>

      <div class="pos-content">
        <!-- Side nav -->
        <nav class="pos-nav">
          <button *ngFor="let item of navItems" class="pos-nav-btn"
                  [routerLink]="item.route" routerLinkActive="active"
                  [routerLinkActiveOptions]="{exact: false}">
            <mat-icon>{{item.icon}}</mat-icon>
            <span>{{item.label}}</span>
          </button>
        </nav>

        <!-- Main area -->
        <main class="pos-main">
          <!-- Offline banner -->
          <div class="offline-banner" *ngIf="!offline.isOnline()">
            <mat-icon>wifi_off</mat-icon>
            Offline mode — transactions are queued locally ({{offline.queueDepth()}} pending)
          </div>
          <!-- Resume banner -->
          <div *ngIf="cart.cart().resumedFromChannel" class="badge blue"
               style="display:flex;align-items:center;gap:8px;padding:10px 14px;border-radius:8px;margin-bottom:12px;font-size:.9rem">
            <mat-icon>sync</mat-icon>
            Cart resumed from {{cart.cart().resumedFromChannel}},
            {{cart.cart().resumedMinutesAgo}} min ago —
            {{cart.cart().items.length}} items
          </div>
          <router-outlet />
        </main>
      </div>
    </div>
  `
})
export class PosRootComponent {
  cart = inject(PosCartService);
  offline = inject(OfflineQueueService);
  storeId = environment.storeId;

  navItems: NavItem[] = [
    { icon: 'point_of_sale', label: 'Sell',      route: '/sell' },
    { icon: 'person_search', label: 'Customer',  route: '/customer' },
    { icon: 'shopping_bag',  label: 'Resume',    route: '/resume-cart' },
    { icon: 'payments',      label: 'Payment',   route: '/payment' },
    { icon: 'storefront',    label: 'C&C',       route: '/click-collect' },
    { icon: 'cloud_off',     label: 'Offline',   route: '/offline' },
  ];
}
