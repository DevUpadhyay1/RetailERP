/**
 * Sprint 10: PWA Offline Mode
 * - Service Worker registration
 * - IndexedDB for offline POS bill storage
 * - Online/offline detection with UI indicator
 * - Auto-sync on reconnect
 */

// ═══════════════════════════════════════════════════════════
// 1. SERVICE WORKER REGISTRATION
// ═══════════════════════════════════════════════════════════

if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => {
        navigator.serviceWorker.register('/sw.js')
            .then(reg => {
                console.log('[PWA] Service Worker registered, scope:', reg.scope);
                reg.addEventListener('updatefound', () => {
                    const newWorker = reg.installing;
                    newWorker.addEventListener('statechange', () => {
                        if (newWorker.state === 'activated' && navigator.serviceWorker.controller) {
                            showUpdateBanner();
                        }
                    });
                });
            })
            .catch(err => console.warn('[PWA] SW registration failed:', err));
    });

    navigator.serviceWorker.addEventListener('message', event => {
        if (event.data?.type === 'TRIGGER_SYNC') {
            OfflineSync.syncAll();
        }
    });
}

function showUpdateBanner() {
    const banner = document.createElement('div');
    banner.id = 'pwa-update-banner';
    banner.style.cssText = 'position:fixed;bottom:0;left:0;right:0;background:#1e3a5f;color:#fff;padding:12px 20px;display:flex;align-items:center;justify-content:space-between;z-index:9999;font-size:14px;';
    banner.innerHTML = '<span>A new version of RetailERP is available.</span><button onclick="location.reload()" style="background:#4fc3f7;color:#1e3a5f;border:none;padding:6px 16px;border-radius:4px;font-weight:600;cursor:pointer;">Update Now</button>';
    document.body.appendChild(banner);
}

// ═══════════════════════════════════════════════════════════
// 2. ONLINE / OFFLINE DETECTION
// ═══════════════════════════════════════════════════════════

const NetworkStatus = {
    _indicator: null,

    init() {
        this._createIndicator();
        window.addEventListener('online', () => this._setOnline());
        window.addEventListener('offline', () => this._setOffline());
        if (!navigator.onLine) this._setOffline();
    },

    _createIndicator() {
        const el = document.createElement('div');
        el.id = 'network-status';
        el.style.cssText = 'position:fixed;top:0;left:0;right:0;z-index:10000;text-align:center;font-size:13px;font-weight:600;padding:4px 0;transition:transform 0.3s;transform:translateY(-100%);';
        document.body.appendChild(el);
        this._indicator = el;
    },

    _setOnline() {
        const el = this._indicator;
        if (!el) return;
        el.style.background = '#43a047';
        el.style.color = '#fff';
        el.textContent = '✓ Back Online — syncing offline data...';
        el.style.transform = 'translateY(0)';

        OfflineSync.syncAll().then(result => {
            if (result.synced > 0) {
                el.textContent = `✓ Online — ${result.synced} offline bill(s) synced successfully`;
            } else {
                el.textContent = '✓ Back Online';
            }
            setTimeout(() => { el.style.transform = 'translateY(-100%)'; }, 4000);
        });
    },

    _setOffline() {
        const el = this._indicator;
        if (!el) return;
        el.style.background = '#e53935';
        el.style.color = '#fff';
        el.textContent = '⚠ No Internet — POS bills will be saved offline and synced when reconnected';
        el.style.transform = 'translateY(0)';
    },

    get isOnline() { return navigator.onLine; }
};

// ═══════════════════════════════════════════════════════════
// 3. INDEXEDDB — OFFLINE POS BILL STORAGE
// ═══════════════════════════════════════════════════════════

const OfflineDB = {
    DB_NAME: 'RetailERP_Offline',
    DB_VERSION: 1,
    _db: null,

    async open() {
        if (this._db) return this._db;
        return new Promise((resolve, reject) => {
            const req = indexedDB.open(this.DB_NAME, this.DB_VERSION);
            req.onupgradeneeded = e => {
                const db = e.target.result;
                if (!db.objectStoreNames.contains('offlineBills')) {
                    const store = db.createObjectStore('offlineBills', { keyPath: 'localId' });
                    store.createIndex('status', 'status', { unique: false });
                    store.createIndex('createdAt', 'createdAt', { unique: false });
                }
                if (!db.objectStoreNames.contains('offlineItems')) {
                    db.createObjectStore('offlineItems', { keyPath: 'itemId' });
                }
                if (!db.objectStoreNames.contains('syncQueue')) {
                    const sq = db.createObjectStore('syncQueue', { keyPath: 'id', autoIncrement: true });
                    sq.createIndex('status', 'status', { unique: false });
                }
            };
            req.onsuccess = e => { this._db = e.target.result; resolve(this._db); };
            req.onerror = e => reject(e.target.error);
        });
    },

    async _tx(storeName, mode, fn) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction(storeName, mode);
            const store = tx.objectStore(storeName);
            const result = fn(store);
            tx.oncomplete = () => resolve(result._result ?? result);
            tx.onerror = e => reject(e.target.error);
        });
    },

    // ── Offline Bills ──

    async saveBill(bill) {
        bill.localId = bill.localId || crypto.randomUUID();
        bill.createdAt = bill.createdAt || new Date().toISOString();
        bill.status = bill.status || 'pending';
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('offlineBills', 'readwrite');
            tx.objectStore('offlineBills').put(bill);
            tx.oncomplete = () => resolve(bill.localId);
            tx.onerror = e => reject(e.target.error);
        });
    },

    async getBill(localId) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('offlineBills', 'readonly');
            const req = tx.objectStore('offlineBills').get(localId);
            req.onsuccess = () => resolve(req.result);
            req.onerror = e => reject(e.target.error);
        });
    },

    async getAllBills(statusFilter) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('offlineBills', 'readonly');
            const store = tx.objectStore('offlineBills');
            const req = statusFilter
                ? store.index('status').getAll(statusFilter)
                : store.getAll();
            req.onsuccess = () => resolve(req.result || []);
            req.onerror = e => reject(e.target.error);
        });
    },

    async updateBillStatus(localId, status, serverId) {
        const bill = await this.getBill(localId);
        if (!bill) return;
        bill.status = status;
        if (serverId) bill.serverId = serverId;
        bill.syncedAt = new Date().toISOString();
        await this.saveBill(bill);
    },

    async deleteBill(localId) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('offlineBills', 'readwrite');
            tx.objectStore('offlineBills').delete(localId);
            tx.oncomplete = () => resolve();
            tx.onerror = e => reject(e.target.error);
        });
    },

    async getPendingBillCount() {
        const bills = await this.getAllBills('pending');
        return bills.length;
    },

    // ── Item Cache (for offline barcode lookup) ──

    async cacheItems(items) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('offlineItems', 'readwrite');
            const store = tx.objectStore('offlineItems');
            items.forEach(item => store.put(item));
            tx.oncomplete = () => resolve(items.length);
            tx.onerror = e => reject(e.target.error);
        });
    },

    async lookupItem(barcodeOrSku) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('offlineItems', 'readonly');
            const req = tx.objectStore('offlineItems').getAll();
            req.onsuccess = () => {
                const items = req.result || [];
                const code = barcodeOrSku.trim().toLowerCase();
                const match = items.find(i =>
                    (i.barcode && i.barcode.toLowerCase() === code) ||
                    (i.sku && i.sku.toLowerCase() === code)
                );
                resolve(match || null);
            };
            req.onerror = e => reject(e.target.error);
        });
    },

    async getItemCount() {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('offlineItems', 'readonly');
            const req = tx.objectStore('offlineItems').count();
            req.onsuccess = () => resolve(req.result);
            req.onerror = e => reject(e.target.error);
        });
    },

    // ── Sync Queue ──

    async addToSyncQueue(entry) {
        entry.status = 'pending';
        entry.createdAt = new Date().toISOString();
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('syncQueue', 'readwrite');
            tx.objectStore('syncQueue').add(entry);
            tx.oncomplete = () => resolve();
            tx.onerror = e => reject(e.target.error);
        });
    },

    async getPendingSyncEntries() {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('syncQueue', 'readonly');
            const req = tx.objectStore('syncQueue').index('status').getAll('pending');
            req.onsuccess = () => resolve(req.result || []);
            req.onerror = e => reject(e.target.error);
        });
    },

    async markSyncEntryDone(id) {
        const db = await this.open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('syncQueue', 'readwrite');
            const store = tx.objectStore('syncQueue');
            const req = store.get(id);
            req.onsuccess = () => {
                const entry = req.result;
                if (entry) {
                    entry.status = 'synced';
                    entry.syncedAt = new Date().toISOString();
                    store.put(entry);
                }
            };
            tx.oncomplete = () => resolve();
            tx.onerror = e => reject(e.target.error);
        });
    }
};

// ═══════════════════════════════════════════════════════════
// 4. AUTO-SYNC ON RECONNECT
// ═══════════════════════════════════════════════════════════

const OfflineSync = {
    _syncing: false,

    async syncAll() {
        if (this._syncing || !navigator.onLine) return { synced: 0, failed: 0 };
        this._syncing = true;

        let synced = 0, failed = 0;
        try {
            const pendingBills = await OfflineDB.getAllBills('pending');

            for (const bill of pendingBills) {
                try {
                    const response = await fetch('/Sync/QueueChange', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            deviceId: this._getDeviceId(),
                            entityType: 'PosBill',
                            entityId: bill.localId,
                            action: 'Create',
                            payload: bill
                        })
                    });

                    const result = await response.json();
                    if (result.success) {
                        await OfflineDB.updateBillStatus(bill.localId, 'synced', result.syncLogId);
                        synced++;
                    } else {
                        failed++;
                    }
                } catch {
                    failed++;
                }
            }
        } finally {
            this._syncing = false;
        }

        this._updateBadge();
        return { synced, failed };
    },

    _getDeviceId() {
        let id = localStorage.getItem('retailerp_device_id');
        if (!id) {
            id = 'device-' + crypto.randomUUID();
            localStorage.setItem('retailerp_device_id', id);
        }
        return id;
    },

    async _updateBadge() {
        const count = await OfflineDB.getPendingBillCount();
        const badge = document.getElementById('offline-bill-badge');
        if (badge) {
            badge.textContent = count;
            badge.style.display = count > 0 ? 'inline-block' : 'none';
        }
    }
};

// ═══════════════════════════════════════════════════════════
// 5. ITEM CACHE PRE-LOADER
// ═══════════════════════════════════════════════════════════

const ItemCacheLoader = {
    async refresh() {
        if (!navigator.onLine) return;

        // Item cache priming is only needed on POS routes.
        const path = (window.location.pathname || '').toLowerCase();
        if (!path.startsWith('/pos')) return;

        try {
            const xsrf = document.cookie.match(/XSRF-TOKEN=([^;]+)/)?.[1] || '';
            const resp = await fetch('/Pos/AllItems', {
                headers: { 'X-XSRF-TOKEN': xsrf },
                credentials: 'same-origin'
            });

            if (resp.redirected || resp.type === 'opaqueredirect' || !resp.ok) return;

            const contentType = resp.headers.get('content-type') || '';
            if (!contentType.includes('application/json')) return;

            const items = await resp.json();
            if (Array.isArray(items) && items.length > 0) {
                await OfflineDB.cacheItems(items);
                console.log(`[PWA] Cached ${items.length} items for offline lookup`);
            }
        } catch (e) {
            console.warn('[PWA] Item cache refresh failed:', e);
        }
    }
};

// ═══════════════════════════════════════════════════════════
// 6. INIT
// ═══════════════════════════════════════════════════════════

document.addEventListener('DOMContentLoaded', () => {
    NetworkStatus.init();
    OfflineDB.open().then(() => {
        console.log('[PWA] IndexedDB ready');
        OfflineSync._updateBadge();
    });

    // Pre-cache items only when user is on POS pages.
    if (navigator.onLine) ItemCacheLoader.refresh();
});

// Expose for use by POS billing page
window.RetailERP = window.RetailERP || {};
window.RetailERP.OfflineDB = OfflineDB;
window.RetailERP.OfflineSync = OfflineSync;
window.RetailERP.NetworkStatus = NetworkStatus;
window.RetailERP.ItemCacheLoader = ItemCacheLoader;
