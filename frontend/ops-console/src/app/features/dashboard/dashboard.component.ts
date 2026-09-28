import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { OrderApiService } from '../../core/api.services';
import { InventoryApiService } from '../../core/api.services';
import { StoreApiService } from '../../core/api.services';
import { IdentityApiService } from '../../core/api.services';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatIconModule, MatProgressSpinnerModule, MatTableModule, MatChipsModule, RouterLink],
  template: `
    <div class="page-header">
      <h1>Dashboard</h1>
      <span class="badge green">Live</span>
    </div>

    <div class="stat-grid">
      <div class="stat-card ok">
        <div class="label">Total Orders (90d)</div>
        <div class="value">{{stats.totalOrders | number}}</div>
        <div class="sub">across all channels</div>
      </div>
      <div class="stat-card warn">
        <div class="label">Phantom Stock Stores</div>
        <div class="value">12</div>
        <div class="sub">7.1% phantom-stock rate</div>
      </div>
      <div class="stat-card danger">
        <div class="label">Merge Queue</div>
        <div class="value">{{stats.pendingMerges | number}}</div>
        <div class="sub"><a routerLink="/merge-queue">Review candidates →</a></div>
      </div>
      <div class="stat-card warn">
        <div class="label">Overdue DSARs</div>
        <div class="value">90</div>
        <div class="sub">require immediate action</div>
      </div>
      <div class="stat-card ok">
        <div class="label">Stores Online</div>
        <div class="value">{{storeHealth?.online ?? '—'}}</div>
        <div class="sub">of {{storeHealth?.total ?? '—'}} stores</div>
      </div>
      <div class="stat-card danger">
        <div class="label">Chronically Offline</div>
        <div class="value">{{storeHealth?.chronicallyOffline ?? '—'}}</div>
        <div class="sub">stores need attention</div>
      </div>
    </div>

    <div style="display:grid; grid-template-columns: 1fr 1fr; gap:16px;">
      <!-- Recent Orders -->
      <div class="card">
        <div class="page-header" style="margin-bottom:16px">
          <h2 style="margin:0;font-size:1rem;font-weight:600">Recent Orders</h2>
          <a routerLink="/orders" style="font-size:.85rem">View all →</a>
        </div>
        <div *ngIf="loading" class="loading-spinner"><mat-spinner diameter="32"/></div>
        <table mat-table [dataSource]="recentOrders" *ngIf="!loading">
          <ng-container matColumnDef="id">
            <th mat-header-cell *matHeaderCellDef>ID</th>
            <td mat-cell *matCellDef="let o">{{o.id | slice:0:8}}…</td>
          </ng-container>
          <ng-container matColumnDef="channel">
            <th mat-header-cell *matHeaderCellDef>Channel</th>
            <td mat-cell *matCellDef="let o"><span class="badge blue">{{o.channel}}</span></td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let o">
              <span class="badge" [class.green]="o.status==='confirmed'||o.status==='delivered'"
                [class.yellow]="o.status==='pending'" [class.red]="o.status==='cancelled'"
                [class.blue]="o.status==='shipped'">{{o.status}}</span>
            </td>
          </ng-container>
          <ng-container matColumnDef="amount">
            <th mat-header-cell *matHeaderCellDef>Amount</th>
            <td mat-cell *matCellDef="let o">{{o.currency}} {{o.totalAmount | number:'1.2-2'}}</td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="['id','channel','status','amount']"></tr>
          <tr mat-row *matRowDef="let row; columns: ['id','channel','status','amount'];" class="clickable-row"></tr>
        </table>
      </div>

      <!-- Drift Alerts -->
      <div class="card">
        <div class="page-header" style="margin-bottom:16px">
          <h2 style="margin:0;font-size:1rem;font-weight:600">Active Drift Alerts</h2>
          <a routerLink="/inventory" style="font-size:.85rem">Investigate →</a>
        </div>
        <div *ngIf="driftAlerts.length === 0 && !loading" style="color:#6b7280;padding:16px 0">
          No active drift alerts
        </div>
        <div *ngFor="let alert of driftAlerts.slice(0,6)" class="drift-row"
             [class.critical]="alert.severity === 'critical'">
          <div>
            <div style="font-weight:500;font-size:.85rem">{{alert.storeId}}</div>
            <div style="font-size:.75rem;color:#6b7280">SKU {{alert.skuId | slice:0:8}}…</div>
          </div>
          <div style="text-align:right">
            <span class="badge" [class.red]="alert.severity==='critical'" [class.yellow]="alert.severity==='warning'">
              {{alert.severity}}
            </span>
            <div style="font-size:.75rem;color:#6b7280;margin-top:2px">
              SAP: {{alert.sapQuantity}} vs Local: {{alert.localQuantity}}
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    h2 { margin: 0 0 16px; font-size: 1rem; }
    .drift-row {
      display: flex; justify-content: space-between; align-items: center;
      padding: 8px 0; border-bottom: 1px solid #f3f4f6;
      &:last-child { border-bottom: none; }
      &.critical { background: #fff5f5; margin: 0 -24px; padding: 8px 24px; }
    }
    a { color: #4f46e5; text-decoration: none; }
    a:hover { text-decoration: underline; }
  `]
})
export class DashboardComponent implements OnInit {
  private ordersApi = inject(OrderApiService);
  private inventoryApi = inject(InventoryApiService);
  private storeApi = inject(StoreApiService);
  private identityApi = inject(IdentityApiService);

  loading = true;
  recentOrders: any[] = [];
  driftAlerts: any[] = [];
  storeHealth: any = null;
  stats = { totalOrders: 0, pendingMerges: 0 };

  ngOnInit() {
    forkJoin({
      orders: this.ordersApi.getOrders({}, 1, 10),
      drift: this.inventoryApi.getDriftAlerts(),
      health: this.storeApi.getHealthSummary(),
      mergeQueue: this.identityApi.getMergeQueue('pending', 0, 1, 1),
    }).subscribe({
      next: ({ orders, drift, health, mergeQueue }) => {
        this.recentOrders = orders.items;
        this.stats.totalOrders = orders.total;
        this.driftAlerts = drift;
        this.storeHealth = health;
        this.stats.pendingMerges = mergeQueue.total;
        this.loading = false;
      },
      error: () => { this.loading = false; }
    });
  }
}
