import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { HttpClient } from '@angular/common/http';
import { PosCartService } from '../../core/pos-cart.service';
import { OfflineQueueService } from '../../core/offline-queue.service';
import { environment } from '../../../environments/environment';
import { catchError, of } from 'rxjs';

type PaymentMethod = 'card' | 'cash' | 'postepay' | 'satispay' | 'klarna' | 'paypay' | 'split';
type PaymentState = 'idle' | 'processing' | 'sca' | 'done' | 'failed';

@Component({
  selector: 'pos-payment',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatProgressSpinnerModule],
  template: `
    <div style="max-width:540px;margin:0 auto">
      <div class="section-title">Payment</div>

      <!-- Amount -->
      <div style="text-align:center;padding:24px;background:#1a1f30;border-radius:12px;margin-bottom:24px">
        <div style="font-size:.8rem;color:rgba(255,255,255,.5);text-transform:uppercase;letter-spacing:.08em">Amount Due</div>
        <div style="font-size:2.8rem;font-weight:800;color:#6ee7b7">
          €{{cartSvc.total() | number:'1.2-2'}}
        </div>
        <div style="font-size:.85rem;color:rgba(255,255,255,.4)">{{cartSvc.itemCount()}} items</div>
      </div>

      <!-- Method selection (idle state) -->
      <div *ngIf="state() === 'idle'">
        <div class="section-title">Payment Method</div>
        <div style="display:grid;grid-template-columns:repeat(3,1fr);gap:10px;margin-bottom:20px">
          <button *ngFor="let m of methods" class="key-btn"
                  [class.accent]="selectedMethod() === m.key"
                  (click)="selectedMethod.set(m.key)"
                  style="flex-direction:column;gap:4px;height:72px;font-size:.8rem">
            <mat-icon>{{m.icon}}</mat-icon>
            {{m.label}}
          </button>
        </div>

        <button class="key-btn success" (click)="processPayment()"
                style="width:100%;font-size:1.1rem;font-weight:700;margin-bottom:8px">
          <mat-icon>check_circle</mat-icon> Confirm Payment
        </button>
        <button class="key-btn danger" (click)="cancel()" style="width:100%;font-size:.9rem">
          <mat-icon>cancel</mat-icon> Cancel
        </button>
      </div>

      <!-- Processing -->
      <div *ngIf="state() === 'processing'" style="text-align:center;padding:40px">
        <mat-spinner diameter="56" style="margin:0 auto 20px;--mdc-circular-progress-active-indicator-color:#818cf8"/>
        <div style="font-size:1rem;font-weight:600">Processing payment…</div>
        <div style="color:rgba(255,255,255,.5);font-size:.85rem;margin-top:8px">{{selectedMethod() | titlecase}}</div>
      </div>

      <!-- SCA Challenge -->
      <div *ngIf="state() === 'sca'" style="text-align:center;padding:32px;background:#1a1f30;border-radius:12px;border:1px solid rgba(245,158,11,.3)">
        <mat-icon style="font-size:48px;color:#fcd34d;display:block;margin-bottom:12px">security</mat-icon>
        <div style="font-size:1.1rem;font-weight:700;margin-bottom:8px">Strong Customer Authentication</div>
        <div style="color:rgba(255,255,255,.5);font-size:.85rem;margin-bottom:24px">
          PSD2 SCA challenge required. Ask customer to authenticate on their device.
        </div>
        <div style="font-family:monospace;font-size:.75rem;color:rgba(255,255,255,.3);margin-bottom:20px;word-break:break-all">
          {{scaUrl}}
        </div>
        <div style="display:flex;gap:8px;justify-content:center">
          <button class="key-btn success" (click)="scaComplete(true)" style="padding:0 24px">
            <mat-icon>check</mat-icon> Authenticated
          </button>
          <button class="key-btn danger" (click)="scaComplete(false)" style="padding:0 24px">
            <mat-icon>close</mat-icon> Failed
          </button>
        </div>
      </div>

      <!-- Done -->
      <div *ngIf="state() === 'done'" style="text-align:center;padding:40px">
        <mat-icon style="font-size:72px;color:#6ee7b7;display:block;margin-bottom:16px">check_circle</mat-icon>
        <div style="font-size:1.4rem;font-weight:800;color:#6ee7b7;margin-bottom:8px">Payment Successful!</div>
        <div style="color:rgba(255,255,255,.5);font-size:.85rem;margin-bottom:4px">{{selectedMethod() | titlecase}}</div>
        <div style="font-family:monospace;font-size:.75rem;color:rgba(255,255,255,.3);margin-bottom:24px">{{txnRef}}</div>
        <div style="display:flex;gap:8px;justify-content:center">
          <button class="key-btn" (click)="printReceipt()" style="padding:0 20px;font-size:.9rem">
            <mat-icon>receipt</mat-icon> Receipt
          </button>
          <button class="key-btn success" (click)="newSale()" style="padding:0 24px;font-size:.9rem">
            <mat-icon>add</mat-icon> New Sale
          </button>
        </div>
      </div>

      <!-- Failed -->
      <div *ngIf="state() === 'failed'" style="text-align:center;padding:40px">
        <mat-icon style="font-size:72px;color:#fca5a5;display:block;margin-bottom:16px">error</mat-icon>
        <div style="font-size:1.2rem;font-weight:700;color:#fca5a5;margin-bottom:8px">Payment Failed</div>
        <div style="color:rgba(255,255,255,.5);font-size:.85rem;margin-bottom:24px">{{failureReason}}</div>
        <button class="key-btn accent" (click)="state.set('idle')" style="padding:0 24px">
          <mat-icon>refresh</mat-icon> Try Again
        </button>
      </div>
    </div>
  `
})
export class PaymentComponent {
  cartSvc = inject(PosCartService);
  private http = inject(HttpClient);
  private offline = inject(OfflineQueueService);

  state = signal<PaymentState>('idle');
  selectedMethod = signal<PaymentMethod>('card');
  txnRef = '';
  failureReason = '';
  scaUrl = '';

  methods = [
    { key: 'card' as PaymentMethod,      icon: 'credit_card',   label: 'Card' },
    { key: 'cash' as PaymentMethod,       icon: 'payments',      label: 'Cash' },
    { key: 'postepay' as PaymentMethod,   icon: 'account_balance',label: 'Postepay' },
    { key: 'satispay' as PaymentMethod,   icon: 'smartphone',    label: 'Satispay' },
    { key: 'klarna' as PaymentMethod,     icon: 'shopping_bag',  label: 'Klarna' },
    { key: 'paypay' as PaymentMethod,     icon: 'qr_code',       label: 'PayPay' },
  ];

  processPayment() {
    if (this.selectedMethod() === 'cash') { this.handleCash(); return; }

    if (!this.offline.isOnline()) {
      // Offline: queue the transaction
      this.offline.enqueue({
        id: crypto.randomUUID(),
        type: 'sale',
        storeId: environment.storeId,
        timestamp: Date.now(),
        payload: {
          customerId: this.cartSvc.cart().customerId || crypto.randomUUID(),
          channel: 'pos',
          storeId: environment.storeId,
          country: 'IT',
          totalAmount: this.cartSvc.total(),
          currency: 'EUR',
          lines: this.cartSvc.cart().items.map(i => ({ skuId: i.skuId, skuName: i.name, quantity: i.quantity, unitPrice: i.unitPrice, vatRate: i.vatRate }))
        }
      }).then(() => {
        this.txnRef = `OFFLINE-${Date.now()}`;
        this.state.set('done');
        this.cartSvc.clear();
      });
      return;
    }

    this.state.set('processing');
    this.http.post<any>(`${environment.apis.payment}/api/payments/process`, {
      orderId: crypto.randomUUID(),
      paymentMethodId: crypto.randomUUID(),
      amount: this.cartSvc.total(),
      currency: 'EUR',
      requiresSca: this.selectedMethod() === 'card' && this.cartSvc.total() > 30
    }).pipe(catchError(err => of({ status: 'failed', failureReason: 'Network error' })))
      .subscribe(result => {
        if (result.status === 'sca_required') {
          this.scaUrl = result.scaChallengeUrl || '';
          this.state.set('sca');
        } else if (result.status === 'completed') {
          this.txnRef = result.transactionRef;
          this.state.set('done');
          this.cartSvc.clear();
        } else {
          this.failureReason = result.failureReason || 'Unknown error';
          this.state.set('failed');
        }
      });
  }

  handleCash() {
    // Cash: always succeeds instantly
    this.state.set('processing');
    setTimeout(() => {
      this.txnRef = `CASH-${Date.now()}`;
      this.state.set('done');
      this.cartSvc.clear();
    }, 800);
  }

  scaComplete(succeeded: boolean) {
    if (succeeded) {
      this.txnRef = `SCA-CARD-${Date.now()}`;
      this.state.set('done');
      this.cartSvc.clear();
    } else {
      this.failureReason = 'SCA challenge failed by customer';
      this.state.set('failed');
    }
  }

  printReceipt() { window.print(); }

  newSale() { this.state.set('idle'); }

  cancel() { this.state.set('idle'); }
}
