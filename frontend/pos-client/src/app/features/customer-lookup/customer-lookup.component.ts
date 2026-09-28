import { Component, inject, signal, ElementRef, ViewChild, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { HttpClient } from '@angular/common/http';
import { PosCartService } from '../../core/pos-cart.service';
import { environment } from '../../../environments/environment';
import { debounceTime, distinctUntilChanged, Subject, switchMap, catchError, of } from 'rxjs';

@Component({
  selector: 'pos-customer-lookup',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule, MatProgressSpinnerModule],
  template: `
    <div class="section-title">Customer Lookup</div>

    <div style="display:flex;gap:8px;margin-bottom:24px">
      <input #lookupInput
             [(ngModel)]="query"
             (input)="search$.next(query)"
             placeholder="Scan loyalty card, NFC, or type email…"
             style="flex:1;background:#1a1f30;border:2px solid rgba(255,255,255,.15);
                    border-radius:10px;padding:14px 18px;color:white;font-size:1rem;outline:none;
                    transition:border-color .2s"
             [style.border-color]="customer() ? '#6ee7b7' : 'rgba(255,255,255,.15)'">
      <button class="key-btn" (click)="simulateNfc()" style="padding:0 16px;font-size:.85rem">
        <mat-icon>nfc</mat-icon> NFC
      </button>
      <button class="key-btn" (click)="simulateScan()" style="padding:0 16px;font-size:.85rem">
        <mat-icon>qr_code</mat-icon> Scan
      </button>
    </div>

    <!-- Loading -->
    <div *ngIf="loading()" style="display:flex;align-items:center;gap:12px;color:rgba(255,255,255,.6);padding:16px 0">
      <mat-spinner diameter="24" style="--mdc-circular-progress-active-indicator-color:#818cf8"/>
      Looking up customer…
    </div>

    <!-- Not found -->
    <div *ngIf="notFound()" style="text-align:center;padding:32px 0;color:rgba(255,255,255,.4)">
      <mat-icon style="font-size:48px;display:block;margin-bottom:8px">person_off</mat-icon>
      No customer found for "{{query}}"
    </div>

    <!-- Customer card -->
    <div *ngIf="customer()" style="background:#1a1f30;border-radius:12px;padding:24px;border:1px solid rgba(99,102,241,.4)">
      <div style="display:flex;align-items:center;gap:16px;margin-bottom:20px">
        <div style="width:56px;height:56px;border-radius:50%;background:#312e81;display:flex;align-items:center;justify-content:center;font-size:1.4rem;font-weight:700;color:#a5b4fc">
          {{customer()!.firstName?.charAt(0)}}{{customer()!.lastName?.charAt(0)}}
        </div>
        <div>
          <div style="font-size:1.2rem;font-weight:700">{{customer()!.firstName}} {{customer()!.lastName}}</div>
          <div style="color:rgba(255,255,255,.5);font-size:.85rem">{{customer()!.email}}</div>
          <div style="color:rgba(255,255,255,.5);font-size:.85rem">{{customer()!.phone}}</div>
        </div>
      </div>

      <!-- Loyalty -->
      <div style="display:grid;grid-template-columns:repeat(3,1fr);gap:12px;margin-bottom:20px">
        <div style="background:rgba(255,255,255,.05);border-radius:8px;padding:12px;text-align:center">
          <div style="font-size:1.5rem;font-weight:700;color:#a5b4fc">{{loyalty()?.points | number:'1.0-0'}}</div>
          <div style="font-size:.7rem;color:rgba(255,255,255,.4);text-transform:uppercase">Points</div>
        </div>
        <div style="background:rgba(255,255,255,.05);border-radius:8px;padding:12px;text-align:center">
          <div style="font-size:1.1rem;font-weight:600;color:#fcd34d">{{loyalty()?.tier | titlecase}}</div>
          <div style="font-size:.7rem;color:rgba(255,255,255,.4);text-transform:uppercase">Tier</div>
        </div>
        <div style="background:rgba(255,255,255,.05);border-radius:8px;padding:12px;text-align:center">
          <div style="font-size:1.1rem;font-weight:600;color:#6ee7b7">{{customer()!.country}}</div>
          <div style="font-size:.7rem;color:rgba(255,255,255,.4);text-transform:uppercase">Country</div>
        </div>
      </div>

      <!-- Linked identities -->
      <div *ngIf="customer()!.identityLinks?.length" style="margin-bottom:20px">
        <div class="section-title">Linked Systems</div>
        <div style="display:flex;gap:8px;flex-wrap:wrap">
          <span *ngFor="let link of customer()!.identityLinks" class="badge gray">
            {{link.system}}
          </span>
        </div>
      </div>

      <!-- Consent flags -->
      <div style="margin-bottom:20px">
        <div class="section-title">Consents</div>
        <div style="display:flex;gap:8px;flex-wrap:wrap">
          <span *ngFor="let c of (customer()!.consents || []).slice(0,4)" class="badge"
                [class.green]="c.granted" [class.red]="!c.granted">
            {{c.consentType}}: {{c.granted ? '✓' : '✗'}}
          </span>
        </div>
      </div>

      <!-- Actions -->
      <div style="display:flex;gap:8px">
        <button class="key-btn accent" (click)="attachToCart()" style="flex:1;font-size:.9rem">
          <mat-icon>person_add</mat-icon> Attach to Sale
        </button>
        <button class="key-btn" (click)="clear()" style="padding:0 16px;font-size:.85rem">
          <mat-icon>close</mat-icon>
        </button>
      </div>
    </div>
  `
})
export class CustomerLookupComponent implements AfterViewInit {
  @ViewChild('lookupInput') inputEl!: ElementRef<HTMLInputElement>;

  private http = inject(HttpClient);
  private cartSvc = inject(PosCartService);

  query = '';
  search$ = new Subject<string>();
  customer = signal<any>(null);
  loyalty = signal<any>(null);
  loading = signal(false);
  notFound = signal(false);

  ngAfterViewInit() {
    this.inputEl.nativeElement.focus();

    this.search$.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(q => {
        if (!q.trim()) { this.customer.set(null); this.notFound.set(false); return of(null); }
        this.loading.set(true);
        this.notFound.set(false);
        return this.http.get<any>(`${environment.apis.identity}/api/customers?q=${encodeURIComponent(q)}&pageSize=1`)
          .pipe(catchError(() => of({ items: [] })));
      })
    ).subscribe(result => {
      this.loading.set(false);
      if (!result) return;
      const found = result.items?.[0];
      if (found) {
        this.loadFullCustomer(found.id);
      } else {
        this.customer.set(null);
        this.notFound.set(this.query.length > 2);
      }
    });
  }

  private loadFullCustomer(id: string) {
    this.http.get<any>(`${environment.apis.identity}/api/customers/${id}`).subscribe(c => {
      this.customer.set(c);
      this.http.get<any>(`${environment.apis.loyalty}/api/loyalty/customer/${id}`)
        .pipe(catchError(() => of(null)))
        .subscribe(l => this.loyalty.set(l));
    });
  }

  simulateNfc() {
    // Simulate NFC tap: in prod, read loyalty card ID from hardware
    this.query = 'nfc-' + Math.random().toString(36).slice(2, 8);
    this.search$.next(this.query);
  }

  simulateScan() {
    // Simulate barcode scan
    this.query = 'test@example.com';
    this.search$.next(this.query);
  }

  attachToCart() {
    const c = this.customer();
    const l = this.loyalty();
    if (c) this.cartSvc.setCustomer(c.id, `${c.firstName} ${c.lastName}`, l?.points ?? 0, l?.tier ?? 'bronze');
  }

  clear() { this.customer.set(null); this.loyalty.set(null); this.query = ''; this.notFound.set(false); }
}
