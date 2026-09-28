import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { RouterLink } from '@angular/router';
import { OrderApiService } from '../../core/api.services';

@Component({
  selector: 'app-orders',
  standalone: true,
  imports: [CommonModule, FormsModule, MatTableModule, MatButtonModule, MatIconModule,
    MatProgressSpinnerModule, MatPaginatorModule, MatSelectModule, MatFormFieldModule, RouterLink],
  template: `
    <div class="page-header">
      <h1>Orders</h1>
      <span class="badge blue">{{total | number}} total</span>
    </div>

    <div class="card">
      <div style="display:flex;gap:12px;margin-bottom:16px;flex-wrap:wrap">
        <mat-form-field appearance="outline">
          <mat-label>Channel</mat-label>
          <mat-select [(ngModel)]="channelFilter" (ngModelChange)="loadOrders()">
            <mat-option value="">All</mat-option>
            <mat-option value="web">Web</mat-option>
            <mat-option value="pos">POS</mat-option>
            <mat-option value="app">App</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Status</mat-label>
          <mat-select [(ngModel)]="statusFilter" (ngModelChange)="loadOrders()">
            <mat-option value="">All</mat-option>
            <mat-option value="pending">Pending</mat-option>
            <mat-option value="confirmed">Confirmed</mat-option>
            <mat-option value="shipped">Shipped</mat-option>
            <mat-option value="delivered">Delivered</mat-option>
            <mat-option value="cancelled">Cancelled</mat-option>
          </mat-select>
        </mat-form-field>
      </div>

      <div *ngIf="loading" class="loading-spinner"><mat-spinner diameter="36"/></div>
      <table mat-table [dataSource]="orders" *ngIf="!loading">
        <ng-container matColumnDef="id">
          <th mat-header-cell *matHeaderCellDef>Order ID</th>
          <td mat-cell *matCellDef="let o">
            <a [routerLink]="['/orders', o.id]" style="color:#4f46e5;text-decoration:none;font-family:monospace">
              {{o.id | slice:0:8}}…
            </a>
          </td>
        </ng-container>
        <ng-container matColumnDef="channel">
          <th mat-header-cell *matHeaderCellDef>Channel</th>
          <td mat-cell *matCellDef="let o"><span class="badge blue">{{o.channel}}</span></td>
        </ng-container>
        <ng-container matColumnDef="country">
          <th mat-header-cell *matHeaderCellDef>Country</th>
          <td mat-cell *matCellDef="let o">{{o.country}}</td>
        </ng-container>
        <ng-container matColumnDef="amount">
          <th mat-header-cell *matHeaderCellDef>Amount</th>
          <td mat-cell *matCellDef="let o">{{o.currency}} {{o.totalAmount | number:'1.2-2'}}</td>
        </ng-container>
        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef>Status</th>
          <td mat-cell *matCellDef="let o">
            <span class="badge"
              [class.green]="o.status==='confirmed'||o.status==='delivered'"
              [class.yellow]="o.status==='pending'"
              [class.red]="o.status==='cancelled'"
              [class.blue]="o.status==='shipped'">{{o.status}}</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="cnc">
          <th mat-header-cell *matHeaderCellDef>C&amp;C</th>
          <td mat-cell *matCellDef="let o">
            <span *ngIf="o.isClickAndCollect" class="badge blue">{{o.pickupStatus || 'reserved'}}</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="created">
          <th mat-header-cell *matHeaderCellDef>Created</th>
          <td mat-cell *matCellDef="let o">{{o.createdAt | date:'short'}}</td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns;" class="clickable-row"></tr>
      </table>
      <mat-paginator [length]="total" [pageSize]="pageSize" [pageSizeOptions]="[25,50,100]" (page)="onPage($event)"/>
    </div>
  `
})
export class OrdersComponent implements OnInit {
  private api = inject(OrderApiService);
  orders: any[] = [];
  loading = false;
  total = 0;
  page = 1;
  pageSize = 25;
  channelFilter = '';
  statusFilter = '';
  columns = ['id', 'channel', 'country', 'amount', 'status', 'cnc', 'created'];

  ngOnInit() { this.loadOrders(); }

  loadOrders() {
    this.loading = true;
    const filters: any = {};
    if (this.channelFilter) filters['channel'] = this.channelFilter;
    if (this.statusFilter) filters['status'] = this.statusFilter;
    this.api.getOrders(filters, this.page, this.pageSize).subscribe({
      next: r => { this.orders = r.items; this.total = r.total; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  onPage(e: PageEvent) { this.page = e.pageIndex + 1; this.pageSize = e.pageSize; this.loadOrders(); }
}
