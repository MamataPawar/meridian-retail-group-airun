import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDividerModule } from '@angular/material/divider';
import { MatTableModule } from '@angular/material/table';
import { switchMap } from 'rxjs';
import { OrderApiService } from '../../core/api.services';

@Component({
  selector: 'app-order-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, MatButtonModule, MatIconModule,
    MatProgressSpinnerModule, MatDividerModule, MatTableModule],
  template: `
    <div class="page-header">
      <div style="display:flex;align-items:center;gap:12px">
        <a routerLink="/orders" mat-icon-button><mat-icon>arrow_back</mat-icon></a>
        <h1>Order <span style="font-family:monospace;font-size:1rem">{{orderId | slice:0:8}}…</span></h1>
      </div>
      <span *ngIf="order" class="badge"
        [class.green]="order.status==='confirmed'||order.status==='delivered'"
        [class.yellow]="order.status==='pending'"
        [class.red]="order.status==='cancelled'"
        [class.blue]="order.status==='shipped'">{{order?.status}}</span>
    </div>

    <div *ngIf="loading" class="loading-spinner"><mat-spinner/></div>

    <div *ngIf="order && !loading" style="display:grid;grid-template-columns:2fr 1fr;gap:16px">
      <!-- Left column -->
      <div>
        <!-- Saga Inspector -->
        <div class="card" style="margin-bottom:16px">
          <h2>Saga Inspector</h2>
          <div *ngIf="saga" class="saga-states">
            <div *ngFor="let state of saga.states; let i = index" class="saga-step"
                 [class.active]="state === saga.currentState"
                 [class.done]="isStateBefore(state, saga.currentState, saga.states)"
                 [class.error]="state === 'Cancelled' && saga.currentState === 'Cancelled'">
              <div class="step-dot">
                <mat-icon *ngIf="isStateBefore(state, saga.currentState, saga.states)" style="font-size:14px;color:white">check</mat-icon>
                <mat-icon *ngIf="state==='Cancelled'&&saga.currentState==='Cancelled'" style="font-size:14px;color:white">close</mat-icon>
              </div>
              <div class="step-label">{{state | titlecase}}</div>
              <div *ngIf="i < saga.states.length - 1" class="step-connector"></div>
            </div>
          </div>

          <div *ngIf="saga?.failureReason" style="background:#fff5f5;border:1px solid #fed7d7;border-radius:6px;padding:12px;margin-top:12px">
            <strong style="color:#c53030">Failure reason:</strong> {{saga.failureReason}}
          </div>

          <div *ngIf="saga?.compensationPath" style="margin-top:12px">
            <div style="font-weight:600;font-size:.85rem;margin-bottom:8px">Compensation path:</div>
            <div *ngFor="let step of saga.compensationPath" class="badge yellow" style="margin:2px;display:inline-block">
              {{step}}
            </div>
          </div>

          <!-- Transition history -->
          <div style="margin-top:16px" *ngIf="saga?.transitions?.length">
            <div style="font-weight:600;font-size:.85rem;margin-bottom:8px">Transition History</div>
            <div *ngFor="let t of saga.transitions" style="display:flex;gap:12px;font-size:.8rem;padding:4px 0;border-bottom:1px solid #f3f4f6">
              <span style="color:#6b7280;white-space:nowrap">{{t.occurredAt | date:'HH:mm:ss'}}</span>
              <span class="badge blue">{{t.type}}</span>
              <span style="color:#374151">{{t.details}}</span>
            </div>
          </div>
        </div>

        <!-- Order Lines -->
        <div class="card">
          <h2>Order Lines</h2>
          <table mat-table [dataSource]="order.lines || []">
            <ng-container matColumnDef="sku"><th mat-header-cell *matHeaderCellDef>SKU</th>
              <td mat-cell *matCellDef="let l">{{l.skuName}}</td></ng-container>
            <ng-container matColumnDef="qty"><th mat-header-cell *matHeaderCellDef>Qty</th>
              <td mat-cell *matCellDef="let l">{{l.quantity}}</td></ng-container>
            <ng-container matColumnDef="price"><th mat-header-cell *matHeaderCellDef>Unit Price</th>
              <td mat-cell *matCellDef="let l">{{l.unitPrice | number:'1.2-2'}}</td></ng-container>
            <ng-container matColumnDef="vat"><th mat-header-cell *matHeaderCellDef>VAT</th>
              <td mat-cell *matCellDef="let l">{{l.vatRate | percent}}</td></ng-container>
            <ng-container matColumnDef="total"><th mat-header-cell *matHeaderCellDef>Line Total</th>
              <td mat-cell *matCellDef="let l"><strong>{{l.lineTotal | number:'1.2-2'}}</strong></td></ng-container>
            <tr mat-header-row *matHeaderRowDef="lineColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: lineColumns;"></tr>
          </table>
          <div style="text-align:right;margin-top:16px;font-size:1.1rem;font-weight:700">
            Total: {{order.currency}} {{order.totalAmount | number:'1.2-2'}}
          </div>
        </div>
      </div>

      <!-- Right column -->
      <div>
        <div class="card" style="margin-bottom:16px">
          <h2>Details</h2>
          <div class="detail-row"><span>Channel</span><span class="badge blue">{{order.channel}}</span></div>
          <div class="detail-row"><span>Country</span><span>{{order.country}}</span></div>
          <div class="detail-row"><span>Store</span><span>{{order.storeId || '—'}}</span></div>
          <div class="detail-row"><span>Customer</span><span style="font-family:monospace;font-size:.8rem">{{order.customerId | slice:0:8}}…</span></div>
          <div class="detail-row"><span>Created</span><span>{{order.createdAt | date:'medium'}}</span></div>
          <div class="detail-row" *ngIf="order.paymentTransactionRef">
            <span>Payment Ref</span><span style="font-family:monospace;font-size:.75rem">{{order.paymentTransactionRef}}</span>
          </div>
        </div>

        <div class="card" *ngIf="order.isClickAndCollect">
          <h2>Click &amp; Collect</h2>
          <div class="detail-row"><span>Pickup Status</span>
            <span class="badge" [class.green]="order.pickupStatus==='collected'" [class.yellow]="order.pickupStatus==='reserved'" [class.red]="order.pickupStatus==='expired'">
              {{order.pickupStatus}}
            </span>
          </div>
          <div class="detail-row"><span>Expires</span><span>{{order.pickupExpiresAt | date:'short'}}</span></div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    h2 { margin: 0 0 16px; font-size: 1rem; font-weight: 600; }
    .saga-states {
      display: flex; align-items: center; flex-wrap: wrap; gap: 0;
      padding: 16px 0;
    }
    .saga-step {
      display: flex; flex-direction: column; align-items: center; position: relative;
    }
    .step-dot {
      width: 28px; height: 28px; border-radius: 50%;
      background: #e5e7eb; border: 2px solid #d1d5db;
      display: flex; align-items: center; justify-content: center;
      transition: all .2s;
    }
    .saga-step.done .step-dot { background: #10b981; border-color: #10b981; }
    .saga-step.active .step-dot { background: #4f46e5; border-color: #4f46e5; box-shadow: 0 0 0 3px rgba(79,70,229,.2); }
    .saga-step.error .step-dot { background: #ef4444; border-color: #ef4444; }
    .step-label { font-size: .7rem; margin-top: 4px; text-align: center; max-width: 80px; color: #6b7280; }
    .saga-step.active .step-label { color: #4f46e5; font-weight: 600; }
    .step-connector { width: 40px; height: 2px; background: #e5e7eb; position: absolute; left: 28px; top: 13px; }
    .saga-step.done .step-connector { background: #10b981; }
    .detail-row {
      display: flex; justify-content: space-between; align-items: center;
      padding: 8px 0; border-bottom: 1px solid #f3f4f6; font-size: .9rem;
      &:last-child { border: none; }
    }
    a { color: inherit; }
  `]
})
export class OrderDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private api = inject(OrderApiService);

  orderId = '';
  order: any = null;
  saga: any = null;
  loading = true;
  lineColumns = ['sku', 'qty', 'price', 'vat', 'total'];

  ngOnInit() {
    this.route.paramMap.pipe(
      switchMap(params => {
        this.orderId = params.get('id')!;
        return this.api.getOrder(this.orderId);
      })
    ).subscribe({
      next: order => {
        this.order = order;
        this.loading = false;
        this.api.getSagaState(this.orderId).subscribe(s => this.saga = s);
      },
      error: () => { this.loading = false; }
    });
  }

  isStateBefore(state: string, current: string, states: string[]): boolean {
    const si = states.indexOf(state);
    const ci = states.indexOf(current);
    return si < ci && state !== 'Cancelled';
  }
}
