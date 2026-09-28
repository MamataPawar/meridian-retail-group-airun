import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatSliderModule } from '@angular/material/slider';
import { MatCardModule } from '@angular/material/card';
import { MatDividerModule } from '@angular/material/divider';
import { MatChipsModule } from '@angular/material/chips';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { IdentityApiService } from '../../core/api.services';

@Component({
  selector: 'app-merge-queue',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatTableModule, MatButtonModule, MatIconModule,
    MatProgressSpinnerModule, MatPaginatorModule, MatDialogModule, MatSliderModule,
    MatCardModule, MatDividerModule, MatChipsModule, MatSnackBarModule
  ],
  template: `
    <div class="page-header">
      <h1>Identity Merge Queue</h1>
      <span class="badge yellow">{{total | number}} candidates</span>
    </div>

    <div class="stat-grid" style="grid-template-columns: repeat(3, 1fr);">
      <div class="stat-card danger"><div class="label">Pending Review</div><div class="value">{{total | number}}</div></div>
      <div class="stat-card ok"><div class="label">Approved (session)</div><div class="value">{{sessionApproved}}</div></div>
      <div class="stat-card gray"><div class="label">Rejected (session)</div><div class="value">{{sessionRejected}}</div></div>
    </div>

    <div class="card">
      <div style="display:flex; gap:16px; margin-bottom:16px; align-items:center; flex-wrap:wrap">
        <label style="font-weight:500; font-size:.9rem">Min confidence: {{minScore | percent}}</label>
        <input type="range" min="0" max="1" step="0.05" [(ngModel)]="minScore" (change)="loadQueue()" style="width:180px">
        <span style="font-size:.85rem; color:#6b7280">Higher = more certain match</span>
      </div>

      <div *ngIf="loading" class="loading-spinner"><mat-spinner diameter="36"/></div>

      <table mat-table [dataSource]="candidates" *ngIf="!loading">
        <ng-container matColumnDef="score">
          <th mat-header-cell *matHeaderCellDef>Confidence</th>
          <td mat-cell *matCellDef="let c">
            <div style="display:flex; align-items:center; gap:8px">
              <div class="confidence-bar" style="width:80px">
                <div class="fill" [class.high]="c.confidenceScore>=.8" [class.medium]="c.confidenceScore>=.6&&c.confidenceScore<.8" [class.low]="c.confidenceScore<.6" [style.width]="(c.confidenceScore*100)+'%'"></div>
              </div>
              <span style="font-size:.8rem; font-weight:600">{{c.confidenceScore | percent:'1.0-0'}}</span>
            </div>
          </td>
        </ng-container>
        <ng-container matColumnDef="customerA">
          <th mat-header-cell *matHeaderCellDef>Customer A</th>
          <td mat-cell *matCellDef="let c">
            <div style="font-weight:500">{{c.customerA?.firstName}} {{c.customerA?.lastName}}</div>
            <div style="font-size:.75rem;color:#6b7280">{{c.customerA?.email}}</div>
          </td>
        </ng-container>
        <ng-container matColumnDef="customerB">
          <th mat-header-cell *matHeaderCellDef>Customer B</th>
          <td mat-cell *matCellDef="let c">
            <div style="font-weight:500">{{c.customerB?.firstName}} {{c.customerB?.lastName}}</div>
            <div style="font-size:.75rem;color:#6b7280">{{c.customerB?.email}}</div>
          </td>
        </ng-container>
        <ng-container matColumnDef="reason">
          <th mat-header-cell *matHeaderCellDef>Match Fields</th>
          <td mat-cell *matCellDef="let c">
            <span *ngFor="let r of parseReason(c.matchReason)" class="badge blue" style="margin:2px">{{r}}</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef>Actions</th>
          <td mat-cell *matCellDef="let c">
            <button mat-raised-button color="primary" style="margin-right:8px"
                    (click)="approve(c)" [disabled]="processing[c.id]">
              <mat-icon>check</mat-icon> Approve
            </button>
            <button mat-stroked-button color="warn" (click)="reject(c)" [disabled]="processing[c.id]">
              <mat-icon>close</mat-icon> Reject
            </button>
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns;" class="clickable-row"></tr>
      </table>

      <mat-paginator [length]="total" [pageSize]="pageSize" [pageSizeOptions]="[10,25,50]"
                     (page)="onPage($event)" />
    </div>
  `
})
export class MergeQueueComponent implements OnInit {
  private api = inject(IdentityApiService);
  private snack = inject(MatSnackBar);

  candidates: any[] = [];
  loading = false;
  total = 0;
  page = 1;
  pageSize = 25;
  minScore = 0.6;
  processing: Record<string, boolean> = {};
  sessionApproved = 0;
  sessionRejected = 0;
  columns = ['score', 'customerA', 'customerB', 'reason', 'actions'];

  ngOnInit() { this.loadQueue(); }

  loadQueue() {
    this.loading = true;
    this.api.getMergeQueue('pending', this.minScore, this.page, this.pageSize).subscribe({
      next: r => { this.candidates = r.items; this.total = r.total; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  onPage(e: PageEvent) { this.page = e.pageIndex + 1; this.pageSize = e.pageSize; this.loadQueue(); }

  parseReason(raw: string): string[] {
    try { return JSON.parse(raw); } catch { return []; }
  }

  approve(candidate: any) {
    this.processing[candidate.id] = true;
    this.api.approveMerge(candidate.id, 'reviewer-ops').subscribe({
      next: () => {
        this.sessionApproved++;
        this.snack.open('Merge approved ✓', 'Close', { duration: 2000 });
        this.candidates = this.candidates.filter(c => c.id !== candidate.id);
        delete this.processing[candidate.id];
      },
      error: () => { delete this.processing[candidate.id]; }
    });
  }

  reject(candidate: any) {
    this.processing[candidate.id] = true;
    this.api.rejectMerge(candidate.id, 'reviewer-ops', 'Manual rejection').subscribe({
      next: () => {
        this.sessionRejected++;
        this.snack.open('Rejected', 'Close', { duration: 2000 });
        this.candidates = this.candidates.filter(c => c.id !== candidate.id);
        delete this.processing[candidate.id];
      },
      error: () => { delete this.processing[candidate.id]; }
    });
  }
}
