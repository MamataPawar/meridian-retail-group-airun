import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatTabsModule } from '@angular/material/tabs';
import { StoreApiService } from '../../core/api.services';

@Component({
  selector: 'app-stores',
  standalone: true,
  imports: [CommonModule, FormsModule, MatTableModule, MatIconModule,
    MatProgressSpinnerModule, MatPaginatorModule, MatSelectModule, MatFormFieldModule, MatTabsModule],
  template: `
    <div class="page-header">
      <h1>Stores &amp; POS</h1>
      <span class="badge blue">{{total}} stores</span>
    </div>

    <!-- Health summary -->
    <div class="stat-grid" *ngIf="health">
      <div class="stat-card ok">
        <div class="label">Stores Online</div>
        <div class="value">{{health.online}}</div>
        <div class="sub">of {{health.total}} total</div>
      </div>
      <div class="stat-card danger">
        <div class="label">Chronically Offline</div>
        <div class="value">{{health.chronicallyOffline}}</div>
      </div>
      <div class="stat-card gray">
        <div class="label">Total Terminals</div>
        <div class="value">{{health.totalTerminals}}</div>
      </div>
      <div class="stat-card warn">
        <div class="label">Offline Terminals</div>
        <div class="value">{{health.offlineTerminals}}</div>
      </div>
      <div class="stat-card warn">
        <div class="label">Offline Queue Depth</div>
        <div class="value">{{health.totalOfflineQueueDepth | number}}</div>
        <div class="sub">pending transactions</div>
      </div>
    </div>

    <div class="card">
      <div style="display:flex;gap:12px;margin-bottom:16px">
        <mat-form-field appearance="outline" style="width:160px">
          <mat-label>Country</mat-label>
          <mat-select [(ngModel)]="countryFilter" (ngModelChange)="loadStores()">
            <mat-option value="">All</mat-option>
            <mat-option *ngFor="let c of countries" [value]="c">{{c}}</mat-option>
          </mat-select>
        </mat-form-field>
      </div>

      <div *ngIf="loading" class="loading-spinner"><mat-spinner diameter="36"/></div>

      <table mat-table [dataSource]="stores" *ngIf="!loading">
        <ng-container matColumnDef="id">
          <th mat-header-cell *matHeaderCellDef>Store ID</th>
          <td mat-cell *matCellDef="let s" style="font-family:monospace;font-weight:500">{{s.id}}</td>
        </ng-container>
        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef>Name</th>
          <td mat-cell *matCellDef="let s">{{s.name}}</td>
        </ng-container>
        <ng-container matColumnDef="country">
          <th mat-header-cell *matHeaderCellDef>Country</th>
          <td mat-cell *matCellDef="let s"><span class="badge gray">{{s.country}}</span></td>
        </ng-container>
        <ng-container matColumnDef="city">
          <th mat-header-cell *matHeaderCellDef>City</th>
          <td mat-cell *matCellDef="let s">{{s.city}}</td>
        </ng-container>
        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef>Status</th>
          <td mat-cell *matCellDef="let s">
            <span class="badge" [class.red]="s.isChronicallyOffline" [class.green]="!s.isChronicallyOffline">
              {{s.isChronicallyOffline ? 'Chronically Offline' : 'Online'}}
            </span>
          </td>
        </ng-container>
        <ng-container matColumnDef="cnc">
          <th mat-header-cell *matHeaderCellDef>C&amp;C</th>
          <td mat-cell *matCellDef="let s">
            <mat-icon [style.color]="s.clickAndCollectEnabled ? '#10b981' : '#ef4444'">
              {{s.clickAndCollectEnabled ? 'check_circle' : 'cancel'}}
            </mat-icon>
          </td>
        </ng-container>
        <ng-container matColumnDef="terminals">
          <th mat-header-cell *matHeaderCellDef>Terminals</th>
          <td mat-cell *matCellDef="let s">
            <div style="display:flex;gap:4px;flex-wrap:wrap">
              <span *ngFor="let t of s.terminals" class="badge"
                [class.green]="t.status==='online'"
                [class.red]="t.status==='offline'"
                [class.yellow]="t.status==='maintenance'"
                style="font-size:.65rem">
                {{t.terminalCode.split('-').pop()}}
                <span *ngIf="t.offlineQueueDepth > 0">({{t.offlineQueueDepth}})</span>
              </span>
            </div>
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let r; columns:columns;" class="clickable-row"></tr>
      </table>
      <mat-paginator [length]="total" [pageSize]="pageSize" [pageSizeOptions]="[25,50,100]" (page)="onPage($event)"/>
    </div>
  `
})
export class StoresComponent implements OnInit {
  private api = inject(StoreApiService);

  stores: any[] = [];
  health: any = null;
  loading = true;
  total = 0;
  page = 1;
  pageSize = 50;
  countryFilter = '';
  countries = ['IT', 'JP', 'US', 'FR', 'DE', 'GB'];
  columns = ['id', 'name', 'country', 'city', 'status', 'cnc', 'terminals'];

  ngOnInit() {
    this.api.getHealthSummary().subscribe(h => this.health = h);
    this.loadStores();
  }

  loadStores() {
    this.loading = true;
    this.api.getStores(this.countryFilter || undefined, this.page, this.pageSize).subscribe({
      next: r => { this.stores = r.items; this.total = r.total; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  onPage(e: PageEvent) { this.page = e.pageIndex + 1; this.pageSize = e.pageSize; this.loadStores(); }
}
