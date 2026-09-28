import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { PosCartService } from '../../core/pos-cart.service';
import { OfflineQueueService } from '../../core/offline-queue.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'pos-sell',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule],
  template: `
    <div style="display:grid;grid-template-columns:1fr 340px;gap:16px;height:100%">
      <!-- Left: item list + scan -->
      <div>
        <div class="section-title">Current Sale</div>

        <!-- Scan / barcode entry -->
        <div style="display:flex;gap:8px;margin-bottom:16px">
          <input #scanInput
                 [(ngModel)]="scanValue"
                 (keyup.enter)="scanItem()"
                 placeholder="Scan barcode or enter SKU…"
                 style="flex:1;background:#1a1f30;border:1px solid rgba(255,255,255,.15);border-radius:8px;
                        padding:12px 16px;color:white;font-size:1rem;outline:none">
          <button class="key-btn accent" (click)="scanItem()" style="padding:0 20px">
            <mat-icon>qr_code_scanner</mat-icon>
          </button>
        </div>

        <!-- Cart items -->
        <div *ngIf="cart.cart().items.length === 0" style="text-align:center;padding:48px 0;color:rgba(255,255,255,.3)">
          <mat-icon style="font-size:48px;display:block;margin-bottom:8px">shopping_cart</mat-icon>
          Cart is empty — scan an item to begin
        </div>

        <div class="cart-item" *ngFor="let item of cart.cart().items">
          <div style="flex:1">
            <div style="font-weight:500">{{item.name}}</div>
            <div style="font-size:.8rem;color:rgba(255,255,255,.5)">
              {{item.skuId | slice:0:12}}… · €{{item.unitPrice | number:'1.2-2'}} each
              <span *ngIf="item.promoCode" class="badge green" style="margin-left:6px">{{item.promoCode}}</span>
            </div>
          </div>
          <!-- Qty stepper -->
          <div style="display:flex;align-items:center;gap:8px">
            <button class="key-btn" (click)="cart.updateQty(item.skuId, item.quantity - 1)"
                    style="width:36px;height:36px;font-size:1rem">−</button>
            <span style="min-width:24px;text-align:center;font-weight:600">{{item.quantity}}</span>
            <button class="key-btn" (click)="cart.updateQty(item.skuId, item.quantity + 1)"
                    style="width:36px;height:36px;font-size:1rem">+</button>
          </div>
          <div style="min-width:80px;text-align:right;font-weight:700">
            €{{item.quantity * item.unitPrice | number:'1.2-2'}}
          </div>
          <button class="key-btn danger" (click)="cart.removeItem(item.skuId)"
                  style="width:36px;height:36px;font-size:.8rem;padding:0">
            <mat-icon style="font-size:16px">delete</mat-icon>
          </button>
        </div>

        <!-- Promo code -->
        <div style="margin-top:16px;display:flex;gap:8px" *ngIf="cart.cart().items.length > 0">
          <input [(ngModel)]="promoCode" placeholder="Promo code…"
                 style="flex:1;background:#1a1f30;border:1px solid rgba(255,255,255,.15);border-radius:8px;
                        padding:10px 14px;color:white;font-size:.9rem;outline:none">
          <button class="key-btn" (click)="applyPromo()" style="padding:0 16px;font-size:.9rem">Apply</button>
        </div>
        <div *ngIf="promoResult" style="margin-top:8px;font-size:.85rem"
             [style.color]="promoResult.success ? '#6ee7b7' : '#fca5a5'">
          {{promoResult.message}}
        </div>
      </div>

      <!-- Right: totals + actions -->
      <div style="display:flex;flex-direction:column;gap:12px">
        <!-- Customer info -->
        <div *ngIf="cart.cart().customerName" class="cart-item" style="flex-direction:column;align-items:flex-start">
          <div class="section-title" style="margin-bottom:4px">Customer</div>
          <div style="font-weight:600">{{cart.cart().customerName}}</div>
          <div style="font-size:.8rem;color:rgba(255,255,255,.5)">
            <span class="badge blue">{{cart.cart().loyaltyTier}}</span>
            {{cart.cart().loyaltyPoints | number}} pts
          </div>
        </div>

        <!-- Totals -->
        <div style="background:#1a1f30;border-radius:8px;padding:16px;border:1px solid rgba(255,255,255,.06)">
          <div class="section-title">Summary</div>
          <div style="display:flex;justify-content:space-between;margin-bottom:8px;font-size:.9rem">
            <span>Items</span><span>{{cart.itemCount()}}</span>
          </div>
          <div style="display:flex;justify-content:space-between;margin-bottom:8px;font-size:.9rem">
            <span>Subtotal</span><span>€{{cart.total() | number:'1.2-2'}}</span>
          </div>
          <div *ngIf="discount > 0" style="display:flex;justify-content:space-between;margin-bottom:8px;font-size:.9rem;color:#6ee7b7">
            <span>Discount</span><span>−€{{discount | number:'1.2-2'}}</span>
          </div>
          <div style="display:flex;justify-content:space-between;padding-top:8px;border-top:1px solid rgba(255,255,255,.1);font-size:1.2rem;font-weight:700">
            <span>TOTAL</span><span style="color:#6ee7b7">€{{(cart.total() - discount) | number:'1.2-2'}}</span>
          </div>
        </div>

        <!-- Action buttons -->
        <button class="key-btn success" (click)="goToPayment()" [disabled]="cart.cart().items.length === 0"
                style="font-size:1rem;font-weight:700">
          <mat-icon>payment</mat-icon> Pay Now
        </button>
        <button class="key-btn" (click)="parkSale()" style="font-size:.9rem">
          <mat-icon>pause</mat-icon> Park Sale
        </button>
        <button class="key-btn danger" (click)="voidSale()" [disabled]="cart.cart().items.length === 0"
                style="font-size:.9rem">
          <mat-icon>delete_sweep</mat-icon> Void Sale
        </button>

        <!-- Quick items (demo) -->
        <div class="section-title" style="margin-top:8px">Quick Add</div>
        <div style="display:grid;grid-template-columns:1fr 1fr;gap:8px">
          <button *ngFor="let demo of demoItems" class="key-btn"
                  (click)="addDemo(demo)" style="font-size:.75rem;padding:8px">
            {{demo.name}}<br><span style="color:#a5b4fc">€{{demo.price}}</span>
          </button>
        </div>
      </div>
    </div>
  `
})
export class SellComponent {
  cart = inject(PosCartService);
  private offline = inject(OfflineQueueService);
  private router = inject(Router);
  private http = inject(HttpClient);

  scanValue = '';
  promoCode = '';
  discount = 0;
  promoResult: { success: boolean; message: string } | null = null;

  demoItems = [
    { skuId: 'SKU-DEMO-001', name: 'T-Shirt S', price: 29.99, vatRate: 0.22 },
    { skuId: 'SKU-DEMO-002', name: 'Jeans 32', price: 79.99, vatRate: 0.22 },
    { skuId: 'SKU-DEMO-003', name: 'Sneakers 42', price: 129.99, vatRate: 0.22 },
    { skuId: 'SKU-DEMO-004', name: 'Cap Uni', price: 24.99, vatRate: 0.22 },
  ];

  addDemo(item: any) {
    this.cart.addItem({ skuId: item.skuId, name: item.name, unitPrice: item.price, vatRate: item.vatRate });
  }

  scanItem() {
    if (!this.scanValue.trim()) return;
    // In production: hit catalog API to resolve barcode → product
    const mockProduct = {
      skuId: this.scanValue,
      name: `Product [${this.scanValue.slice(-6)}]`,
      unitPrice: Math.round(Math.random() * 200 + 10) / 10,
      vatRate: 0.22
    };
    this.cart.addItem(mockProduct);
    this.scanValue = '';
  }

  applyPromo() {
    if (!this.promoCode) return;
    this.http.post<any>(`${environment.apis.promotions}/api/promotions/apply`, {
      code: this.promoCode,
      country: 'IT',
      channel: 'pos',
      orderTotal: this.cart.total()
    }).subscribe({
      next: result => {
        if (result.applied?.length > 0) {
          this.discount = result.originalTotal - result.finalTotal;
          this.promoResult = { success: true, message: `Applied: ${result.applied[0].name} — saving €${this.discount.toFixed(2)}` };
          this.cart.cart().items.forEach(i => i.promoCode = this.promoCode);
        } else {
          this.promoResult = { success: false, message: 'Promo code not valid or not applicable' };
        }
      },
      error: () => { this.promoResult = { success: false, message: 'Could not validate promo code' }; }
    });
  }

  goToPayment() { this.router.navigate(['/payment']); }

  parkSale() { this.cart.park(); }

  voidSale() { if (confirm('Void this sale?')) this.cart.clear(); }
}
