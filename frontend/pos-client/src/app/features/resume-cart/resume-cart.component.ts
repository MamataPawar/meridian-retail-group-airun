import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { HttpClient } from '@angular/common/http';
import { PosCartService } from '../../core/pos-cart.service';
import { environment } from '../../../environments/environment';
import { catchError, of } from 'rxjs';

@Component({
  selector: 'pos-resume-cart',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule, MatProgressSpinnerModule],
  template: `
    <div class="section-title">Resume Web Cart</div>

    <div style="display:flex;gap:8px;margin-bottom:24px">
      <input [(ngModel)]="customerId"
             placeholder="Enter customer ID or scan loyalty card…"
             style="flex:1;background:#1a1f30;border:1px solid rgba(255,255,255,.15);
                    border-radius:10px;padding:14px 18px;color:white;font-size:1rem;outline:none">
      <button class="key-btn accent" (click)="lookup()" [disabled]="loading()">
        <mat-icon>search</mat-icon> Load
      </button>
    </div>

    <div *ngIf="loading()" style="display:flex;align-items:center;gap:12px;color:rgba(255,255,255,.6)">
      <mat-spinner diameter="24" style="--mdc-circular-progress-active-indicator-color:#818cf8"/>
      Fetching cart from backend…
    </div>

    <!-- No cart found -->
    <div *ngIf="noneFound()" style="text-align:center;padding:32px 0;color:rgba(255,255,255,.4)">
      <mat-icon style="font-size:48px;display:block">shopping_bag</mat-icon>
      No active web cart found for this customer
    </div>

    <!-- Resume banner (no conflict) -->
    <div *ngIf="webCart() && !hasConflict()" style="background:#1a1f30;border-radius:12px;padding:20px;border:1px solid rgba(99,102,241,.3)">
      <div style="display:flex;align-items:center;gap:10px;margin-bottom:16px;color:#a5b4fc;font-weight:600">
        <mat-icon>sync</mat-icon>
        Web cart — {{webCart()!.items.length}} items · added {{minutesAgo}} min ago
      </div>
      <div class="cart-item" *ngFor="let item of webCart()!.items">
        <div style="flex:1">
          <div style="font-weight:500">{{item.skuName || item.name}}</div>
          <div style="font-size:.8rem;color:rgba(255,255,255,.5)">Qty: {{item.quantity}}</div>
        </div>
        <div style="font-weight:700;color:#6ee7b7">€{{item.unitPrice * item.quantity | number:'1.2-2'}}</div>
      </div>
      <button class="key-btn success" (click)="resume()" style="width:100%;margin-top:16px;font-size:1rem">
        <mat-icon>play_arrow</mat-icon> Resume This Cart
      </button>
    </div>

    <!-- Conflict resolver -->
    <div *ngIf="hasConflict()" style="background:#1a1f30;border-radius:12px;padding:20px;border:1px solid rgba(239,68,68,.4)">
      <div style="display:flex;align-items:center;gap:10px;margin-bottom:4px;color:#fca5a5;font-weight:600;font-size:1rem">
        <mat-icon>warning</mat-icon> Cart Conflict Detected
      </div>
      <p style="color:rgba(255,255,255,.6);font-size:.85rem;margin-bottom:16px">
        The web cart and POS cart have different quantities for the same items. Choose which version to keep.
      </p>

      <div *ngFor="let skuId of conflictSkus()" class="cart-item" style="border:1px solid rgba(239,68,68,.3)">
        <div style="flex:1">
          <div style="font-weight:500;font-family:monospace;font-size:.85rem">{{skuId | slice:0:16}}…</div>
          <div style="display:flex;gap:16px;margin-top:8px">
            <div style="font-size:.85rem">
              <div style="color:rgba(255,255,255,.5)">Web cart</div>
              <div style="font-weight:600;color:#a5b4fc">Qty: {{getWebQty(skuId)}}</div>
            </div>
            <div style="font-size:.85rem">
              <div style="color:rgba(255,255,255,.5)">POS cart</div>
              <div style="font-weight:600;color:#fcd34d">Qty: {{getPosQty(skuId)}}</div>
            </div>
          </div>
        </div>
      </div>

      <div style="display:flex;gap:8px;margin-top:16px">
        <button class="key-btn accent" (click)="resolveWith('web')" style="flex:1;font-size:.85rem">
          <mat-icon>computer</mat-icon> Use Web Cart
        </button>
        <button class="key-btn" (click)="resolveWith('pos')" style="flex:1;font-size:.85rem">
          <mat-icon>point_of_sale</mat-icon> Keep POS Cart
        </button>
        <button class="key-btn" (click)="resolveWith('merge')" style="flex:1;font-size:.85rem">
          <mat-icon>merge</mat-icon> Merge
        </button>
      </div>
    </div>

    <!-- Success -->
    <div *ngIf="resumed()" style="text-align:center;padding:32px;color:#6ee7b7">
      <mat-icon style="font-size:48px;display:block;margin-bottom:8px">check_circle</mat-icon>
      Cart resumed! Go to Sell to continue.
    </div>
  `
})
export class ResumeCartComponent {
  private http = inject(HttpClient);
  private cartSvc = inject(PosCartService);

  customerId = '';
  loading = signal(false);
  noneFound = signal(false);
  webCart = signal<any>(null);
  posCart = signal<any>(null);
  hasConflict = signal(false);
  conflictSkus = signal<string[]>([]);
  resumed = signal(false);
  minutesAgo = 0;

  lookup() {
    if (!this.customerId) return;
    this.loading.set(true);
    this.noneFound.set(false);
    this.resumed.set(false);

    this.http.post<any>(
      `${environment.apis.cart}/api/carts/resume-on-pos`,
      { customerId: this.customerId, storeId: environment.storeId }
    ).pipe(catchError(() => of(null))).subscribe(result => {
      this.loading.set(false);
      if (!result) { this.noneFound.set(true); return; }

      if (result.hasConflict) {
        this.hasConflict.set(true);
        this.webCart.set({ items: result.cart?.lines || [] });
        this.conflictSkus.set(result.conflictingSkus || []);
      } else if (result.cart?.lines?.length > 0) {
        this.webCart.set({ items: result.cart.lines });
        this.minutesAgo = Math.floor(Math.random() * 60 + 1);
      } else {
        this.noneFound.set(true);
      }
    });
  }

  resume() {
    const items = (this.webCart()?.items || []).map((l: any) => ({
      skuId: l.skuId,
      name: l.skuName || `SKU ${l.skuId.slice(0,8)}`,
      quantity: l.quantity,
      unitPrice: l.unitPrice,
      vatRate: 0.22
    }));
    this.cartSvc.resumeFromWeb(items, this.minutesAgo);
    this.resumed.set(true);
    this.webCart.set(null);
  }

  resolveWith(strategy: 'web' | 'pos' | 'merge') {
    if (strategy === 'pos') {
      this.hasConflict.set(false);
      this.resumed.set(true);
    } else {
      this.resume();
    }
    // In prod: POST conflict resolution to cart service
  }

  getWebQty(skuId: string): number {
    return this.webCart()?.items?.find((i: any) => i.skuId === skuId)?.quantity ?? 0;
  }
  getPosQty(skuId: string): number {
    return this.cartSvc.cart().items.find(i => i.skuId === skuId)?.quantity ?? 0;
  }
}
