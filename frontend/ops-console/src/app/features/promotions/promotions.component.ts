import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { PromotionApiService } from '../../core/api.services';

@Component({
  selector: 'app-promotions',
  standalone: true,
  imports: [CommonModule, MatTableModule, MatButtonModule, MatIconModule,
    MatProgressSpinnerModule, MatTabsModule, MatSnackBarModule],
  template: `
    <div class="page-header">
      <h1>Promotions</h1>
      <button mat-raised-button color="warn" (click)="detectConflicts()" [disabled]="detecting">
        <mat-icon>warning</mat-icon> Detect Conflicts
      </button>
    </div>

    <div class="stat-grid">
      <div class="stat-card ok">
        <div class="label">Active Promotions</div>
        <div class="value">{{activeCount}}</div>
      </div>
      <div class="stat-card danger" *ngIf="conflicts.length">
        <div class="label">Conflicts Detected</div>
        <div class="value">{{conflicts.length}}</div>
        <div class="sub">regions running conflicting promos</div>
      </div>
    </div>

    <mat-tab-group>
      <mat-tab label="All Promotions">
        <div class="card" style="margin-top:16px">
          <div *ngIf="loading" class="loading-spinner"><mat-spinner diameter="36"/></div>
          <table mat-table [dataSource]="promotions" *ngIf="!loading">
            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef>Name</th>
              <td mat-cell *matCellDef="let p">
                <div style="font-weight:500">{{p.name}}</div>
                <div style="font-family:monospace;font-size:.75rem;color:#6b7280">{{p.code}}</div>
              </td>
            </ng-container>
            <ng-container matColumnDef="type">
              <th mat-header-cell *matHeaderCellDef>Type</th>
              <td mat-cell *matCellDef="let p"><span class="badge blue">{{p.type}}</span></td>
            </ng-container>
            <ng-container matColumnDef="value">
              <th mat-header-cell *matHeaderCellDef>Discount</th>
              <td mat-cell *matCellDef="let p">
                <strong>{{p.type === 'percentage' ? p.discountValue + '%' : '€' + p.discountValue}}</strong>
              </td>
            </ng-container>
            <ng-container matColumnDef="scope">
              <th mat-header-cell *matHeaderCellDef>Scope</th>
              <td mat-cell *matCellDef="let p">
                <span class="badge gray">{{p.country || 'ALL'}}</span>
                <span class="badge gray" style="margin-left:4px">{{p.channel || 'ALL'}}</span>
              </td>
            </ng-container>
            <ng-container matColumnDef="stacking">
              <th mat-header-cell *matHeaderCellDef>Stacks?</th>
              <td mat-cell *matCellDef="let p">
                <mat-icon [style.color]="p.stackingAllowed ? '#10b981' : '#ef4444'">
                  {{p.stackingAllowed ? 'check_circle' : 'cancel'}}
                </mat-icon>
              </td>
            </ng-container>
            <ng-container matColumnDef="dates">
              <th mat-header-cell *matHeaderCellDef>Period</th>
              <td mat-cell *matCellDef="let p" style="font-size:.8rem">
                {{p.startsAt | date:'shortDate'}} → {{p.endsAt | date:'shortDate'}}
              </td>
            </ng-container>
            <ng-container matColumnDef="usage">
              <th mat-header-cell *matHeaderCellDef>Used</th>
              <td mat-cell *matCellDef="let p">
                {{p.usageCount}}{{p.usageLimit ? ' / ' + p.usageLimit : ''}}
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="['name','type','value','scope','stacking','dates','usage']"></tr>
            <tr mat-row *matRowDef="let r; columns:['name','type','value','scope','stacking','dates','usage'];"></tr>
          </table>
        </div>
      </mat-tab>

      <mat-tab label="Conflicts ({{conflicts.length}})">
        <div class="card" style="margin-top:16px">
          <div *ngIf="!conflicts.length" style="color:#9ca3af;padding:24px 0;text-align:center">
            <mat-icon style="font-size:48px;display:block;margin-bottom:8px">check_circle</mat-icon>
            No conflicts detected. Click "Detect Conflicts" to run the analysis.
          </div>
          <table mat-table [dataSource]="conflicts" *ngIf="conflicts.length">
            <ng-container matColumnDef="promoA">
              <th mat-header-cell *matHeaderCellDef>Promotion A</th>
              <td mat-cell *matCellDef="let c" style="font-family:monospace;font-size:.8rem">{{c.promotionAId | slice:0:8}}…</td>
            </ng-container>
            <ng-container matColumnDef="promoB">
              <th mat-header-cell *matHeaderCellDef>Promotion B</th>
              <td mat-cell *matCellDef="let c" style="font-family:monospace;font-size:.8rem">{{c.promotionBId | slice:0:8}}…</td>
            </ng-container>
            <ng-container matColumnDef="type">
              <th mat-header-cell *matHeaderCellDef>Conflict Type</th>
              <td mat-cell *matCellDef="let c"><span class="badge red">{{c.conflictType}}</span></td>
            </ng-container>
            <ng-container matColumnDef="detected">
              <th mat-header-cell *matHeaderCellDef>Detected</th>
              <td mat-cell *matCellDef="let c" style="font-size:.8rem">{{c.detectedAt | date:'short'}}</td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="['promoA','promoB','type','detected']"></tr>
            <tr mat-row *matRowDef="let r; columns:['promoA','promoB','type','detected'];"></tr>
          </table>
        </div>
      </mat-tab>
    </mat-tab-group>
  `
})
export class PromotionsComponent implements OnInit {
  private api = inject(PromotionApiService);
  private snack = inject(MatSnackBar);

  promotions: any[] = [];
  conflicts: any[] = [];
  loading = true;
  detecting = false;
  activeCount = 0;

  ngOnInit() {
    this.api.getPromotions().subscribe({
      next: p => {
        this.promotions = p;
        this.activeCount = p.filter((x: any) => x.isActive).length;
        this.loading = false;
      },
      error: () => { this.loading = false; }
    });
    this.api.getConflicts().subscribe({ next: c => this.conflicts = c, error: () => {} });
  }

  detectConflicts() {
    this.detecting = true;
    this.api.detectConflicts().subscribe({
      next: r => {
        this.snack.open(`Found ${r.detected} conflicts`, 'Close', { duration: 3000 });
        this.api.getConflicts().subscribe(c => this.conflicts = c);
        this.detecting = false;
      },
      error: () => { this.detecting = false; }
    });
  }
}
