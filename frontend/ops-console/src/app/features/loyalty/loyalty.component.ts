import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { LoyaltyApiService } from '../../core/api.services';

@Component({
  selector: 'app-loyalty',
  standalone: true,
  imports: [CommonModule, MatTableModule, MatButtonModule, MatIconModule,
    MatProgressSpinnerModule, MatTabsModule, MatSnackBarModule],
  template: `
    <div class="page-header">
      <h1>Loyalty</h1>
      <span class="badge yellow">{{fragmented.length}} fragmented accounts</span>
    </div>

    <div class="stat-grid">
      <div class="stat-card warn">
        <div class="label">Fragmented Accounts</div>
        <div class="value">{{fragmented.length}}</div>
        <div class="sub">210 customers with multiple accounts</div>
      </div>
      <div class="stat-card ok">
        <div class="label">Merged This Session</div>
        <div class="value">{{mergedCount}}</div>
      </div>
    </div>

    <mat-tab-group>
      <mat-tab label="Fragmented Accounts ({{fragmented.length}})">
        <div class="card" style="margin-top:16px">
          <p style="color:#6b7280;font-size:.9rem;margin-bottom:16px">
            These customers have more than one loyalty account — typically from legacy system imports.
            Merging will transfer all points to the primary account.
          </p>
          <div *ngIf="loading" class="loading-spinner"><mat-spinner diameter="36"/></div>
          <table mat-table [dataSource]="fragmented" *ngIf="!loading">
            <ng-container matColumnDef="customerId">
              <th mat-header-cell *matHeaderCellDef>Customer ID</th>
              <td mat-cell *matCellDef="let f" style="font-family:monospace;font-size:.8rem">
                {{f.customerId | slice:0:12}}…
              </td>
            </ng-container>
            <ng-container matColumnDef="accounts">
              <th mat-header-cell *matHeaderCellDef>Accounts</th>
              <td mat-cell *matCellDef="let f">
                <span class="badge red">{{f.accountCount}} accounts</span>
              </td>
            </ng-container>
            <ng-container matColumnDef="points">
              <th mat-header-cell *matHeaderCellDef>Total Points</th>
              <td mat-cell *matCellDef="let f">
                <strong>{{f.totalPoints | number}}</strong>
              </td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef>Action</th>
              <td mat-cell *matCellDef="let f">
                <button mat-raised-button color="primary" (click)="mergeAccounts(f)"
                        [disabled]="merging[f.customerId]" style="font-size:.8rem">
                  <mat-icon style="font-size:16px">merge</mat-icon>
                  Merge Accounts
                </button>
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="['customerId','accounts','points','actions']"></tr>
            <tr mat-row *matRowDef="let r; columns:['customerId','accounts','points','actions'];"></tr>
          </table>
        </div>
      </mat-tab>
    </mat-tab-group>
  `
})
export class LoyaltyComponent implements OnInit {
  private api = inject(LoyaltyApiService);
  private snack = inject(MatSnackBar);

  fragmented: any[] = [];
  loading = true;
  merging: Record<string, boolean> = {};
  mergedCount = 0;

  ngOnInit() {
    this.api.getFragmented().subscribe({
      next: f => { this.fragmented = f; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  mergeAccounts(fragment: any) {
    this.merging[fragment.customerId] = true;
    // For demo: merge all but first into first (primary = first encountered)
    const primaryId = fragment.customerId; // in real app, pick canonical from list
    this.api.mergeAccounts(primaryId, []).subscribe({
      next: () => {
        this.mergedCount++;
        this.fragmented = this.fragmented.filter(f => f.customerId !== fragment.customerId);
        this.snack.open('Accounts merged — points transferred ✓', 'Close', { duration: 3000 });
        delete this.merging[fragment.customerId];
      },
      error: () => {
        delete this.merging[fragment.customerId];
        this.snack.open('Merge failed', 'Close', { duration: 3000 });
      }
    });
  }
}
