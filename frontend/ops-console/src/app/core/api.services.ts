import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { Observable } from 'rxjs';

const api = environment.apis;

export interface PagedResult<T> { total: number; page: number; pageSize: number; items: T[]; }

// ── Identity ──────────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class IdentityApiService {
  private http = inject(HttpClient);
  private base = api.identity;

  searchCustomers(q?: string, country?: string, page = 1, pageSize = 25): Observable<PagedResult<any>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (q) params = params.set('q', q);
    if (country) params = params.set('country', country);
    return this.http.get<PagedResult<any>>(`${this.base}/api/customers`, { params });
  }

  getCustomer(id: string): Observable<any> {
    return this.http.get<any>(`${this.base}/api/customers/${id}`);
  }

  getMergeQueue(status = 'pending', minScore = 0, page = 1, pageSize = 25): Observable<PagedResult<any>> {
    const params = new HttpParams()
      .set('status', status).set('minScore', minScore).set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<any>>(`${this.base}/api/merge-queue`, { params });
  }

  approveMerge(id: string, reviewerId: string): Observable<any> {
    return this.http.post<any>(`${this.base}/api/merge-queue/${id}/approve`, { reviewerId });
  }

  rejectMerge(id: string, reviewerId: string, reason: string): Observable<any> {
    return this.http.post<any>(`${this.base}/api/merge-queue/${id}/reject`, { reviewerId, reason });
  }

  undoMerge(auditId: string): Observable<any> {
    return this.http.post<any>(`${this.base}/api/merge-queue/undo/${auditId}`, {});
  }

  getDsarRequests(status?: string, overdueOnly = false): Observable<any[]> {
    let params = new HttpParams();
    if (status) params = params.set('status', status);
    if (overdueOnly) params = params.set('overdueOnly', 'true');
    return this.http.get<any[]>(`${this.base}/api/dsar`, { params });
  }
}

// ── Inventory ─────────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class InventoryApiService {
  private http = inject(HttpClient);
  private base = api.inventory;

  getPositions(storeId?: string, lowConfidence?: boolean, page = 1, pageSize = 50): Observable<PagedResult<any>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (storeId) params = params.set('storeId', storeId);
    if (lowConfidence != null) params = params.set('lowConfidence', String(lowConfidence));
    return this.http.get<PagedResult<any>>(`${this.base}/api/inventory/positions`, { params });
  }

  getDriftAlerts(storeId?: string): Observable<any[]> {
    let params = new HttpParams();
    if (storeId) params = params.set('storeId', storeId);
    return this.http.get<any[]>(`${this.base}/api/inventory/drift-alerts`, { params });
  }

  getSyncLog(storeId?: string): Observable<any[]> {
    let params = new HttpParams();
    if (storeId) params = params.set('storeId', storeId);
    return this.http.get<any[]>(`${this.base}/api/inventory/sync-log`, { params });
  }
}

// ── Orders ────────────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class OrderApiService {
  private http = inject(HttpClient);
  private base = api.orders;

  getOrders(filters: Record<string, any> = {}, page = 1, pageSize = 25): Observable<PagedResult<any>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    Object.entries(filters).forEach(([k, v]) => { if (v != null) params = params.set(k, String(v)); });
    return this.http.get<PagedResult<any>>(`${this.base}/api/orders`, { params });
  }

  getOrder(id: string): Observable<any> {
    return this.http.get<any>(`${this.base}/api/orders/${id}`);
  }

  getSagaState(id: string): Observable<any> {
    return this.http.get<any>(`${this.base}/api/orders/${id}/saga`);
  }

  getClickAndCollectQueue(storeId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/api/orders/click-and-collect?storeId=${storeId}`);
  }
}

// ── Loyalty ───────────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class LoyaltyApiService {
  private http = inject(HttpClient);
  private base = api.loyalty;

  getAccount(customerId: string): Observable<any> {
    return this.http.get<any>(`${this.base}/api/loyalty/customer/${customerId}`);
  }

  getFragmented(): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/api/loyalty/fragmented`);
  }

  mergeAccounts(primaryAccountId: string, subordinateIds: string[]): Observable<any> {
    return this.http.post<any>(`${this.base}/api/loyalty/merge`, { primaryAccountId, subordinateIds });
  }

  adjust(customerId: string, delta: number, staffId: string, note: string): Observable<any> {
    return this.http.post<any>(`${this.base}/api/loyalty/${customerId}/adjust`, { delta, staffId, note });
  }
}

// ── Stores ────────────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class StoreApiService {
  private http = inject(HttpClient);
  private base = api.stores;

  getStores(country?: string, page = 1, pageSize = 50): Observable<PagedResult<any>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (country) params = params.set('country', country);
    return this.http.get<PagedResult<any>>(`${this.base}/api/stores`, { params });
  }

  getHealthSummary(): Observable<any> {
    return this.http.get<any>(`${this.base}/api/stores/health-summary`);
  }
}

// ── Promotions ────────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class PromotionApiService {
  private http = inject(HttpClient);
  private base = api.promotions;

  getPromotions(country?: string): Observable<any[]> {
    let params = new HttpParams();
    if (country) params = params.set('country', country);
    return this.http.get<any[]>(`${this.base}/api/promotions`, { params });
  }

  getConflicts(): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/api/promotions/conflicts`);
  }

  detectConflicts(): Observable<any> {
    return this.http.post<any>(`${this.base}/api/promotions/detect-conflicts`, {});
  }
}
