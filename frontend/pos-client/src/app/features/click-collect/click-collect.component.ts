import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { HttpClient } from '@angular/common/http';
import { OfflineQueueService } from '../../core/offline-queue.service';
import { environment } from '../../../environments/environment';
import { catchError, of } from 'rxjs';

// ── Click & Collect ──────────────────────────────────────────────────────────

@Component({
  selector: 'pos-click-collect',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule, MatProgressSpinnerModule],
  template: `
    <div class="section-title">Click &amp; Collect Pickup Queue — {{storeId}}</div>

    <div *ngIf="loading()" style="display:flex;align-items:center;gap:12px;color:rgba(255,255,255,.6);padding:16px">
      <mat-spinner diameter="24" style="--mdc-circular-progress-active-indicator-color:#818cf8"/>
      Loading pickup queue…
    </div>

    <div *ngIf="!loading() && orders().length === 0" style="text-align:center;padding:40px;color:rgba(255,255,255,.3)">
      <mat-icon style="font-size:48px;display:block;margin-bottom:8px">storefront</mat-icon>
      No pending pickups for this store
    </div>

    <div *ngFor="let order of orders()" style="background:#1a1f30;border-radius:10px;padding:16px;margin-bottom:10px;border:1px solid rgba(255,255,255,.07)">
      <div style="display:flex;align-items:flex-start;justify-content:space-between;margin-bottom:12px">
        <div>
          <div style="font-family:monospace;font-size:.85rem;color:rgba(255,255,255,.5)">{{order.id | slice:0:12}}…</div>
          <div style="font-weight:700;font-size:1rem;margin-top:2px">€{{order.totalAmount | number:'1.2-2'}}</div>
        </div>
        <div style="text-align:right">
          <span class="badge" [class.yellow]="ttlMinutes(order) > 0" [class.red]="ttlMinutes(order) <= 0">
            {{ttlMinutes(order) > 0 ? ttlMinutes(order) + ' min left' : 'EXPIRED'}}
          </span>
          <div style="font-size:.75rem;color:rgba(255,255,255,.4);margin-top:4px">
            Expires {{order.pickupExpiresAt | date:'shortTime'}}
          </div>
        </div>
      </div>

      <!-- Identity verification -->
      <div style="margin-bottom:12px">
        <div style="font-size:.75rem;color:rgba(255,255,255,.4);margin-bottom:6px;text-transform:uppercase">Verify Identity</div>
        <input [(ngModel)]="verifyInputs[order.id]"
               placeholder="Scan ID or enter last 4 of card…"
               style="width:100%;background:rgba(255,255,255,.05);border:1px solid rgba(255,255,255,.12);
                      border-radius:8px;padding:10px 14px;color:white;font-size:.9rem;outline:none">
      </div>

      <div style="display:flex;gap:8px">
        <button class="key-btn success" (click)="handOver(order)"
                [disabled]="processing[order.id]"
                style="flex:1;font-size:.85rem">
          <mat-icon>check_circle</mat-icon> Hand Over
        </button>
        <button class="key-btn danger" (click)="cancelPickup(order, 'customer_no_show')"
                style="flex:1;font-size:.85rem">
          <mat-icon>cancel</mat-icon> No Show
        </button>
        <button class="key-btn" (click)="cancelPickup(order, 'stock_not_available')"
                style="flex:1;font-size:.85rem">
          <mat-icon>inventory_2</mat-icon> No Stock
        </button>
      </div>
    </div>
  `
})
export class ClickCollectComponent implements OnInit {
  private http = inject(HttpClient);
  storeId = environment.storeId;
  orders = signal<any[]>([]);
  loading = signal(true);
  verifyInputs: Record<string, string> = {};
  processing: Record<string, boolean> = {};

  ngOnInit() { this.loadQueue(); }

  loadQueue() {
    this.http.get<any[]>(`${environment.apis.orders}/api/orders/click-and-collect?storeId=${this.storeId}`)
      .pipe(catchError(() => of([])))
      .subscribe(o => { this.orders.set(o); this.loading.set(false); });
  }

  ttlMinutes(order: any): number {
    if (!order.pickupExpiresAt) return 0;
    return Math.max(0, Math.floor((new Date(order.pickupExpiresAt).getTime() - Date.now()) / 60000));
  }

  handOver(order: any) {
    this.processing[order.id] = true;
    this.http.post(`${environment.apis.orders}/api/orders/${order.id}/pickup`, {
      staffId: 'STAFF-001', storeId: this.storeId
    }).pipe(catchError(() => of(null))).subscribe(() => {
      this.orders.update(os => os.filter(o => o.id !== order.id));
      delete this.processing[order.id];
    });
  }

  cancelPickup(order: any, reason: string) {
    this.http.post(`${environment.apis.orders}/api/orders/${order.id}/cancel`, { reason })
      .pipe(catchError(() => of(null))).subscribe(() => {
        this.orders.update(os => os.filter(o => o.id !== order.id));
      });
  }
}

// ── Offline Mode ─────────────────────────────────────────────────────────────

@Component({
  selector: 'pos-offline-mode',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatProgressSpinnerModule],
  template: `
    <div class="section-title">Offline Mode Status</div>

    <div style="background:#1a1f30;border-radius:12px;padding:24px;margin-bottom:16px">
      <div style="display:flex;align-items:center;gap:16px;margin-bottom:20px">
        <div style="width:56px;height:56px;border-radius:50%;display:flex;align-items:center;justify-content:center"
             [style.background]="offline.isOnline() ? 'rgba(16,185,129,.2)' : 'rgba(239,68,68,.2)'">
          <mat-icon [style.color]="offline.isOnline() ? '#6ee7b7' : '#fca5a5'" style="font-size:28px">
            {{offline.isOnline() ? 'wifi' : 'wifi_off'}}
          </mat-icon>
        </div>
        <div>
          <div style="font-weight:700;font-size:1.1rem">
            {{offline.isOnline() ? 'Online' : 'Offline — Queuing mode active'}}
          </div>
          <div style="color:rgba(255,255,255,.5);font-size:.85rem">
            {{offline.isOnline() ? 'All transactions syncing normally' : 'Transactions are saved locally and will sync on reconnect'}}
          </div>
        </div>
      </div>

      <div style="display:grid;grid-template-columns:1fr 1fr;gap:12px">
        <div style="background:rgba(255,255,255,.05);border-radius:8px;padding:16px;text-align:center">
          <div style="font-size:2rem;font-weight:700" [style.color]="offline.queueDepth() > 0 ? '#fcd34d' : '#6ee7b7'">
            {{offline.queueDepth()}}
          </div>
          <div style="font-size:.75rem;color:rgba(255,255,255,.4);text-transform:uppercase">Queued Transactions</div>
        </div>
        <div style="background:rgba(255,255,255,.05);border-radius:8px;padding:16px;text-align:center">
          <div style="font-size:1.1rem;font-weight:700;color:#a5b4fc">{{storeId}}</div>
          <div style="font-size:.75rem;color:rgba(255,255,255,.4);text-transform:uppercase">Store ID</div>
        </div>
      </div>
    </div>

    <div *ngIf="pendingTxns().length > 0" style="background:#1a1f30;border-radius:10px;padding:16px">
      <div class="section-title">Pending Queue</div>
      <div *ngFor="let tx of pendingTxns()" style="display:flex;justify-content:space-between;padding:8px 0;border-bottom:1px solid rgba(255,255,255,.06)">
        <div>
          <span class="badge gray">{{tx.type}}</span>
          <span style="font-size:.8rem;margin-left:8px;color:rgba(255,255,255,.5)">{{tx.id | slice:0:12}}…</span>
        </div>
        <div style="font-size:.8rem;color:rgba(255,255,255,.4)">{{tx.timestamp | date:'HH:mm:ss'}}</div>
      </div>
    </div>

    <div style="margin-top:16px;display:flex;gap:8px" *ngIf="offline.isOnline() && offline.queueDepth() > 0">
      <button class="key-btn accent" (click)="forceSync()" [disabled]="syncing()" style="flex:1;font-size:.9rem">
        <mat-spinner *ngIf="syncing()" diameter="20" style="--mdc-circular-progress-active-indicator-color:white"/>
        <mat-icon *ngIf="!syncing()">sync</mat-icon>
        {{syncing() ? 'Syncing…' : 'Sync Now'}}
      </button>
    </div>

    <!-- Simulate offline for demo -->
    <div style="margin-top:24px;padding-top:16px;border-top:1px solid rgba(255,255,255,.06)">
      <div class="section-title">Demo Controls</div>
      <div style="display:flex;gap:8px">
        <button class="key-btn danger" (click)="simulateOffline()" style="flex:1;font-size:.8rem">
          <mat-icon>wifi_off</mat-icon> Go Offline
        </button>
        <button class="key-btn success" (click)="simulateOnline()" style="flex:1;font-size:.8rem">
          <mat-icon>wifi</mat-icon> Go Online
        </button>
      </div>
    </div>
  `
})
export class OfflineModeComponent implements OnInit {
  offline = inject(OfflineQueueService);
  storeId = environment.storeId;
  pendingTxns = signal<any[]>([]);
  syncing = signal(false);

  ngOnInit() { this.loadPending(); }

  loadPending() {
    this.offline.getPending().then(txns => this.pendingTxns.set(txns));
  }

  forceSync() {
    this.syncing.set(true);
    this.offline.reconcile().then(() => {
      this.syncing.set(false);
      this.loadPending();
    });
  }

  simulateOffline() {
    window.dispatchEvent(new Event('offline'));
  }

  simulateOnline() {
    window.dispatchEvent(new Event('online'));
    this.loadPending();
  }
}
