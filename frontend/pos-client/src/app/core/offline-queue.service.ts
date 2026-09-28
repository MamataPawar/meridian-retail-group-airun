import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';

export interface OfflineTransaction {
  id: string;
  type: 'sale' | 'return' | 'cnc_pickup';
  payload: any;
  timestamp: number;
  synced: boolean;
  storeId: string;
}

export interface DriftEvent {
  skuId: string;
  storeId: string;
  localQty: number;
  timestamp: number;
}

const DB_NAME = 'pos-offline';
const TX_STORE = 'transactions';
const DB_VERSION = 1;

@Injectable({ providedIn: 'root' })
export class OfflineQueueService {
  private http = inject(HttpClient);
  isOnline = signal(navigator.onLine);
  queueDepth = signal(0);

  private db: IDBDatabase | null = null;

  constructor() {
    window.addEventListener('online',  () => { this.isOnline.set(true);  this.reconcile(); });
    window.addEventListener('offline', () => { this.isOnline.set(false); });
    this.initDb();
  }

  private async initDb(): Promise<void> {
    return new Promise((resolve, reject) => {
      const req = indexedDB.open(DB_NAME, DB_VERSION);
      req.onupgradeneeded = (e) => {
        const db = (e.target as IDBOpenDBRequest).result;
        if (!db.objectStoreNames.contains(TX_STORE)) {
          const store = db.createObjectStore(TX_STORE, { keyPath: 'id' });
          store.createIndex('synced', 'synced', { unique: false });
        }
      };
      req.onsuccess = (e) => {
        this.db = (e.target as IDBOpenDBRequest).result;
        this.refreshQueueDepth();
        resolve();
      };
      req.onerror = () => reject(req.error);
    });
  }

  async enqueue(tx: Omit<OfflineTransaction, 'synced'>): Promise<void> {
    if (!this.db) await this.initDb();
    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([TX_STORE], 'readwrite');
      const store = transaction.objectStore(TX_STORE);
      store.add({ ...tx, synced: false });
      transaction.oncomplete = () => { this.refreshQueueDepth(); resolve(); };
      transaction.onerror = () => reject(transaction.error);
    });
  }

  async getPending(): Promise<OfflineTransaction[]> {
    if (!this.db) await this.initDb();
    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([TX_STORE], 'readonly');
      const store = transaction.objectStore(TX_STORE);
      const index = store.index('synced');
      const req = index.getAll(IDBKeyRange.only(false));
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });
  }

  async markSynced(id: string): Promise<void> {
    if (!this.db) return;
    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([TX_STORE], 'readwrite');
      const store = transaction.objectStore(TX_STORE);
      const getReq = store.get(id);
      getReq.onsuccess = () => {
        const record = getReq.result;
        if (record) { record.synced = true; store.put(record); }
        transaction.oncomplete = () => { this.refreshQueueDepth(); resolve(); };
      };
      getReq.onerror = () => reject(getReq.error);
    });
  }

  /** On reconnect: push pending transactions to backend and emit drift events */
  async reconcile(): Promise<void> {
    const pending = await this.getPending();
    if (!pending.length) return;

    console.log(`[OfflineQueue] Reconciling ${pending.length} pending transactions`);
    const driftEvents: DriftEvent[] = [];

    for (const tx of pending) {
      try {
        if (tx.type === 'sale') {
          // Push sale to order service
          await fetch(`${environment.apis.orders}/api/orders`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(tx.payload)
          });
          // Inventory drift: local sales happened while offline
          for (const line of tx.payload.lines ?? []) {
            driftEvents.push({
              skuId: line.skuId,
              storeId: tx.storeId,
              localQty: -(line.quantity),
              timestamp: tx.timestamp
            });
          }
        }
        await this.markSynced(tx.id);
      } catch (e) {
        console.error(`[OfflineQueue] Failed to sync tx ${tx.id}`, e);
      }
    }

    // Report drift events to inventory service
    for (const drift of driftEvents) {
      try {
        await fetch(`${environment.apis.inventory}/api/inventory/sap-sync`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ skuId: drift.skuId, storeId: drift.storeId, quantity: 0 })
        });
      } catch { /* best-effort */ }
    }

    console.log(`[OfflineQueue] Reconciled. ${driftEvents.length} drift events reported.`);
    this.refreshQueueDepth();
  }

  private async refreshQueueDepth(): Promise<void> {
    const pending = await this.getPending();
    this.queueDepth.set(pending.length);
  }
}
