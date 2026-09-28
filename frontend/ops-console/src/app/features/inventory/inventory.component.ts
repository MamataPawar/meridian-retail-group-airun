import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatTabsModule } from '@angular/material/tabs';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { forkJoin } from 'rxjs';
import { InventoryApiService } from '../../core/api.services';

@Component({
  selector: 'app-inventory',
  standalone: true,
  imports: [CommonModule, FormsModule, MatTableModule, MatButtonModule, MatIconModule,
    MatProgressSpinnerModule, MatPaginatorModule, MatTabsModule, MatInputModule, MatFormFieldModule],
  template: `
    <div class="page-header">
      <h1>Inventory</h1>
      <span class="badge red">7.1% phantom-stock rate</span>
    </div>

    <div class="stat-grid">
      <div class="stat-card danger"><div class="label">Phantom Stock Stores</div><div class="value">12</div></div>
      <div class="stat-card warn"><div class="label">Low Confidence SKUs</div><div class="value">{{lowConfidenceCount | number}}</div></div>
      <div class="stat-card danger"><div class="label">Active Drift Alerts</div><div class="value">{{driftAlerts.length}}</div></div>
      <div class="stat-card gray"><div class="label">Hidden from C&C</div><div class="value">{{hiddenFromCnC | number}}</div></div>
    </div>

    <mat-tab-group>
      <!-- Stock Positions tab -->
      <mat-tab label="Stock Positions">
        <div class="card" style="margin-top:16px">
          <div style="display:flex; gap:12px; margin-bottom:16px; align-items:center">
            <mat-form-field appearance="outline" style="width:200px">
              <mat-label>Store ID</mat-label>
              <input matInput [(ngModel)]="storeFilter" placeholder="STORE-0001" (keyup.enter)="loadPositions()">
            </mat-form-field>
            <label style="display:flex; align-items:center; gap:6px; font-size:.9rem">
              <input type="checkbox" [(ngModel)]="lowConfidenceOnly" (change)="loadPositions()">
              Low confidence only
            </label>
          </div>

          <div *ngIf="loading" class="loading-spinner"><mat-spinner diameter="36"/></div>
          <table mat-table [dataSource]="positions" *ngIf="!loading">
            <ng-container matColumnDef="skuId">
              <th mat-header-cell *matHeaderCellDef>SKU</th>
              <td mat-cell *matCellDef="let p">{{p.skuId | slice:0:8}}…</td>
            </ng-container>
            <ng-container matColumnDef="storeId">
              <th mat-header-cell *matHeaderCellDef>Store</th>
              <td mat-cell *matCellDef="let p">{{p.storeId}}</td>
            </ng-container>
            <ng-container matColumnDef="onHand">
              <th mat-header-cell *matHeaderCellDef>On Hand</th>
              <td mat-cell *matCellDef="let p">{{p.onHand}}</td>
            </ng-container>
            <ng-container matColumnDef="reserved">
              <th mat-header-cell *matHeaderCellDef>Reserved</th>
              <td mat-cell *matCellDef="let p">{{p.reserved}}</td>
            </ng-container>
            <ng-container matColumnDef="available">
              <th mat-header-cell *matHeaderCellDef>Available (ATP)</th>
              <td mat-cell *matCellDef="let p" [style.color]="p.available <= 0 ? '#ef4444' : 'inherit'">
                <strong>{{p.available}}</strong>
              </td>
            </ng-container>
            <ng-container matColumnDef="confidence">
              <th mat-header-cell *matHeaderCellDef>Confidence</th>
              <td mat-cell *matCellDef="let p">
                <div style="display:flex; align-items:center; gap:8px">
                  <div class="confidence-bar" style="width:60px">
                    <div class="fill"
                         [class.high]="p.confidenceScore>=.7"
                         [class.medium]="p.confidenceScore>=.5&&p.confidenceScore<.7"
                         [class.low]="p.confidenceScore<.5"
                         [style.width]="(p.confidenceScore*100)+'%'"></div>
                  </div>
                  <span style="font-size:.8rem">{{p.confidenceScore | percent:'1.0-0'}}</span>
                </div>
              </td>
            </ng-container>
            <ng-container matColumnDef="cnc">
              <th mat-header-cell *matHeaderCellDef>C&amp;C</th>
              <td mat-cell *matCellDef="let p">
                <span class="badge" [class.red]="p.hiddenFromClickAndCollect" [class.green]="!p.hiddenFromClickAndCollect">
                  {{p.hiddenFromClickAndCollect ? 'Hidden' : 'Visible'}}
                </span>
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="posColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: posColumns;"></tr>
          </table>
          <mat-paginator [length]="posTotal" [pageSize]="pageSize" [pageSizeOptions]="[25,50,100]" (page)="onPage($event)"/>
        </div>
      </mat-tab>

      <!-- Drift Alerts tab -->
      <mat-tab label="Drift Alerts ({{driftAlerts.length}})">
        <div class="card" style="margin-top:16px">
          <table mat-table [dataSource]="driftAlerts">
            <ng-container matColumnDef="store">
              <th mat-header-cell *matHeaderCellDef>Store</th>
              <td mat-cell *matCellDef="let d">{{d.storeId}}</td>
            </ng-container>
            <ng-container matColumnDef="sku">
              <th mat-header-cell *matHeaderCellDef>SKU</th>
              <td mat-cell *matCellDef="let d">{{d.skuId | slice:0:8}}…</td>
            </ng-container>
            <ng-container matColumnDef="sap">
              <th mat-header-cell *matHeaderCellDef>SAP Qty</th>
              <td mat-cell *matCellDef="let d">{{d.sapQuantity}}</td>
            </ng-container>
            <ng-container matColumnDef="local">
              <th mat-header-cell *matHeaderCellDef>Local Qty</th>
              <td mat-cell *matCellDef="let d">{{d.localQuantity}}</td>
            </ng-container>
            <ng-container matColumnDef="drift">
              <th mat-header-cell *matHeaderCellDef>Drift</th>
              <td mat-cell *matCellDef="let d" style="font-weight:600;color:#ef4444">
                {{(d.sapQuantity - d.localQuantity) | number}}
              </td>
            </ng-container>
            <ng-container matColumnDef="severity">
              <th mat-header-cell *matHeaderCellDef>Severity</th>
              <td mat-cell *matCellDef="let d">
                <span class="badge" [class.red]="d.severity==='critical'" [class.yellow]="d.severity==='warning'">
                  {{d.severity}}
                </span>
              </td>
            </ng-container>
            <ng-container matColumnDef="detected">
              <th mat-header-cell *matHeaderCellDef>Detected</th>
              <td mat-cell *matCellDef="let d">{{d.detectedAt | date:'short'}}</td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="driftColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: driftColumns;"></tr>
          </table>
        </div>
      </mat-tab>
    </mat-tab-group>
  `
})
export class InventoryComponent implements OnInit {
  private api = inject(InventoryApiService);

  loading = false;
  positions: any[] = [];
  posTotal = 0;
  driftAlerts: any[] = [];
  lowConfidenceCount = 0;
  hiddenFromCnC = 0;
  storeFilter = '';
  lowConfidenceOnly = false;
  page = 1;
  pageSize = 50;
  posColumns = ['skuId', 'storeId', 'onHand', 'reserved', 'available', 'confidence', 'cnc'];
  driftColumns = ['store', 'sku', 'sap', 'local', 'drift', 'severity', 'detected'];

  ngOnInit() {
    this.loadPositions();
    this.api.getDriftAlerts().subscribe(d => this.driftAlerts = d);
  }

  loadPositions() {
    this.loading = true;
    forkJoin({
      all: this.api.getPositions(this.storeFilter || undefined, this.lowConfidenceOnly || undefined, this.page, this.pageSize),
      lowConf: this.api.getPositions(undefined, true, 1, 1),
    }).subscribe({
      next: ({ all, lowConf }) => {
        this.positions = all.items;
        this.posTotal = all.total;
        this.lowConfidenceCount = lowConf.total;
        this.hiddenFromCnC = all.items.filter((p: any) => p.hiddenFromClickAndCollect).length;
        this.loading = false;
      },
      error: () => { this.loading = false; }
    });
  }

  onPage(e: PageEvent) { this.page = e.pageIndex + 1; this.pageSize = e.pageSize; this.loadPositions(); }
}
