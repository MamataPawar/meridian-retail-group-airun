import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { debounceTime, distinctUntilChanged, Subject, switchMap } from 'rxjs';
import { IdentityApiService } from '../../core/api.services';

@Component({
  selector: 'app-customers',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, MatTableModule, MatButtonModule, MatIconModule,
    MatInputModule, MatFormFieldModule, MatProgressSpinnerModule, MatPaginatorModule, MatSelectModule],
  template: `
    <div class="page-header">
      <h1>Customers</h1>
      <span class="badge blue">{{total | number}} total</span>
    </div>

    <div class="card">
      <div style="display:flex;gap:12px;margin-bottom:16px;flex-wrap:wrap;align-items:center">
        <mat-form-field appearance="outline" style="width:280px">
          <mat-label>Search name, email, phone…</mat-label>
          <input matInput [(ngModel)]="searchQuery" (input)="onSearch()" placeholder="Search…">
          <mat-icon matSuffix>search</mat-icon>
        </mat-form-field>
        <mat-form-field appearance="outline" style="width:140px">
          <mat-label>Country</mat-label>
          <mat-select [(ngModel)]="countryFilter" (ngModelChange)="loadCustomers()">
            <mat-option value="">All</mat-option>
            <mat-option *ngFor="let c of countries" [value]="c">{{c}}</mat-option>
          </mat-select>
        </mat-form-field>
      </div>

      <div *ngIf="loading" class="loading-spinner"><mat-spinner diameter="36"/></div>

      <table mat-table [dataSource]="customers" *ngIf="!loading">
        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef>Name</th>
          <td mat-cell *matCellDef="let c">
            <a [routerLink]="['/customers', c.id]" style="color:#4f46e5;text-decoration:none;font-weight:500">
              {{c.firstName}} {{c.lastName}}
            </a>
          </td>
        </ng-container>
        <ng-container matColumnDef="email">
          <th mat-header-cell *matHeaderCellDef>Email</th>
          <td mat-cell *matCellDef="let c" style="font-size:.85rem">{{c.email}}</td>
        </ng-container>
        <ng-container matColumnDef="phone">
          <th mat-header-cell *matHeaderCellDef>Phone</th>
          <td mat-cell *matCellDef="let c" style="font-size:.85rem">{{c.phone || '—'}}</td>
        </ng-container>
        <ng-container matColumnDef="country">
          <th mat-header-cell *matHeaderCellDef>Country</th>
          <td mat-cell *matCellDef="let c"><span class="badge gray">{{c.country}}</span></td>
        </ng-container>
        <ng-container matColumnDef="created">
          <th mat-header-cell *matHeaderCellDef>Member Since</th>
          <td mat-cell *matCellDef="let c" style="font-size:.85rem">{{c.createdAt | date:'mediumDate'}}</td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let c">
            <a mat-icon-button [routerLink]="['/customers', c.id]"><mat-icon>open_in_new</mat-icon></a>
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns;" class="clickable-row"></tr>
      </table>
      <mat-paginator [length]="total" [pageSize]="pageSize" [pageSizeOptions]="[25,50,100]" (page)="onPage($event)"/>
    </div>
  `
})
export class CustomersComponent implements OnInit {
  private api = inject(IdentityApiService);
  private search$ = new Subject<string>();

  customers: any[] = [];
  loading = false;
  total = 0;
  page = 1;
  pageSize = 25;
  searchQuery = '';
  countryFilter = '';
  countries = ['IT', 'JP', 'US', 'FR', 'DE', 'GB'];
  columns = ['name', 'email', 'phone', 'country', 'created', 'actions'];

  ngOnInit() {
    this.search$.pipe(debounceTime(300), distinctUntilChanged()).subscribe(() => this.loadCustomers());
    this.loadCustomers();
  }

  onSearch() { this.search$.next(this.searchQuery); }

  loadCustomers() {
    this.loading = true;
    this.api.searchCustomers(this.searchQuery || undefined, this.countryFilter || undefined, this.page, this.pageSize)
      .subscribe({
        next: r => { this.customers = r.items; this.total = r.total; this.loading = false; },
        error: () => { this.loading = false; }
      });
  }

  onPage(e: PageEvent) { this.page = e.pageIndex + 1; this.pageSize = e.pageSize; this.loadCustomers(); }
}
