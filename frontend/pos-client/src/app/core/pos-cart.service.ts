import { Injectable, signal, computed } from '@angular/core';

export interface CartItem {
  skuId: string;
  name: string;
  quantity: number;
  unitPrice: number;
  vatRate: number;
  promoCode?: string;
}

export interface ActiveCart {
  items: CartItem[];
  customerId?: string;
  customerName?: string;
  loyaltyPoints?: number;
  loyaltyTier?: string;
  resumedFromChannel?: string;
  resumedMinutesAgo?: number;
}

@Injectable({ providedIn: 'root' })
export class PosCartService {
  cart = signal<ActiveCart>({ items: [] });

  total = computed(() =>
    this.cart().items.reduce((sum, i) => sum + i.quantity * i.unitPrice, 0)
  );

  itemCount = computed(() =>
    this.cart().items.reduce((sum, i) => sum + i.quantity, 0)
  );

  addItem(item: Omit<CartItem, 'quantity'>, qty = 1) {
    this.cart.update(c => {
      const existing = c.items.find(i => i.skuId === item.skuId);
      if (existing) {
        return { ...c, items: c.items.map(i => i.skuId === item.skuId ? { ...i, quantity: i.quantity + qty } : i) };
      }
      return { ...c, items: [...c.items, { ...item, quantity: qty }] };
    });
  }

  removeItem(skuId: string) {
    this.cart.update(c => ({ ...c, items: c.items.filter(i => i.skuId !== skuId) }));
  }

  updateQty(skuId: string, qty: number) {
    if (qty <= 0) { this.removeItem(skuId); return; }
    this.cart.update(c => ({
      ...c, items: c.items.map(i => i.skuId === skuId ? { ...i, quantity: qty } : i)
    }));
  }

  setCustomer(customerId: string, name: string, points: number, tier: string) {
    this.cart.update(c => ({ ...c, customerId, customerName: name, loyaltyPoints: points, loyaltyTier: tier }));
  }

  resumeFromWeb(webItems: CartItem[], minutesAgo: number) {
    this.cart.update(c => ({
      ...c,
      items: webItems,
      resumedFromChannel: 'Web',
      resumedMinutesAgo: minutesAgo
    }));
  }

  clear() { this.cart.set({ items: [] }); }

  park() {
    // In production: persist to IndexedDB so staff can resume
    const snapshot = this.cart();
    localStorage.setItem('parked-cart', JSON.stringify(snapshot));
    this.clear();
  }

  resumeParked(): boolean {
    const raw = localStorage.getItem('parked-cart');
    if (!raw) return false;
    try {
      this.cart.set(JSON.parse(raw));
      localStorage.removeItem('parked-cart');
      return true;
    } catch { return false; }
  }
}
