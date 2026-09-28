import { Component } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatButtonModule } from '@angular/material/button';
import { CommonModule } from '@angular/common';

interface NavItem { icon: string; label: string; route: string; badge?: number; }

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule, RouterOutlet, RouterLink, RouterLinkActive,
    MatSidenavModule, MatToolbarModule, MatIconModule, MatListModule, MatButtonModule
  ],
  template: `
    <mat-sidenav-container class="sidenav-container">
      <mat-sidenav mode="side" opened class="sidenav">
        <div class="brand">
          <mat-icon>store</mat-icon>
          <span>Retail Ops</span>
        </div>
        <mat-nav-list>
          <a mat-list-item *ngFor="let item of navItems"
             [routerLink]="item.route"
             routerLinkActive="active-link"
             [routerLinkActiveOptions]="{exact: item.route === '/'}">
            <mat-icon matListItemIcon>{{item.icon}}</mat-icon>
            <span matListItemTitle>{{item.label}}</span>
            <span *ngIf="item.badge" class="nav-badge">{{item.badge}}</span>
          </a>
        </mat-nav-list>
      </mat-sidenav>

      <mat-sidenav-content class="main-content">
        <mat-toolbar color="primary" class="top-bar">
          <span class="spacer"></span>
          <mat-icon class="toolbar-icon">notifications</mat-icon>
          <mat-icon class="toolbar-icon" style="margin-left:12px">account_circle</mat-icon>
        </mat-toolbar>
        <div class="content-area">
          <router-outlet />
        </div>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: [`
    .sidenav-container { height: 100vh; }
    .sidenav {
      width: 220px;
      background: #1e2536;
      color: white;
      border: none;
      .brand {
        display: flex; align-items: center; gap: 10px;
        padding: 20px 16px 16px;
        font-size: 1.1rem; font-weight: 700; color: white;
        border-bottom: 1px solid rgba(255,255,255,.1);
        mat-icon { color: #818cf8; }
      }
    }
    mat-nav-list { padding-top: 8px; }
    a[mat-list-item] {
      color: rgba(255,255,255,.7);
      border-radius: 6px; margin: 2px 8px;
      &:hover { background: rgba(255,255,255,.07); color: white; }
    }
    .active-link { background: rgba(129,140,248,.2) !important; color: #818cf8 !important;
      mat-icon { color: #818cf8; } }
    .nav-badge {
      background: #ef4444; color: white; border-radius: 9999px;
      padding: 1px 6px; font-size: 0.7rem; font-weight: 600; margin-left: auto;
    }
    .main-content { display: flex; flex-direction: column; }
    .top-bar { background: white; color: #1a1a2e; box-shadow: 0 1px 3px rgba(0,0,0,.08); }
    .spacer { flex: 1; }
    .toolbar-icon { cursor: pointer; color: #6b7280; }
    .content-area { flex: 1; padding: 24px; overflow-y: auto; }
  `]
})
export class AppComponent {
  navItems: NavItem[] = [
    { icon: 'dashboard',    label: 'Dashboard',      route: '/dashboard' },
    { icon: 'people',       label: 'Customers',      route: '/customers' },
    { icon: 'merge_type',   label: 'Merge Queue',    route: '/merge-queue', badge: 40_000 },
    { icon: 'inventory_2',  label: 'Inventory',      route: '/inventory' },
    { icon: 'receipt_long', label: 'Orders',         route: '/orders' },
    { icon: 'loyalty',      label: 'Loyalty',        route: '/loyalty' },
    { icon: 'store',        label: 'Stores & POS',   route: '/stores' },
    { icon: 'local_offer',  label: 'Promotions',     route: '/promotions' },
  ];
}
