// ═══════════════════════════════════════════════════
// Sprint 3 – Customisable Widget Dashboard (Gridstack.js)
// ═══════════════════════════════════════════════════

(function () {
    'use strict';

    let grid = null;
    let catalog = [];
    let currentLayout = [];
    let saveTimer = null;
    let isLocked = true; // Start locked by default
    const chartInstances = {};

    // ── Init ──
    document.addEventListener('DOMContentLoaded', init);

    async function init() {
        const resp = await fetch('/Home/GetLayout');
        if (!resp.ok) { console.error('Failed to load layout'); return; }
        const data = await resp.json();
        catalog = data.catalog;
        currentLayout = data.layout;

        initGrid();
        renderWidgets(currentLayout);
        wireToolbar();
        applyLockState(); // Start in locked mode
        initSignalR();    // Sprint 9: real-time updates
    }

    function initGrid() {
        grid = GridStack.init({
            column: 12,
            cellHeight: 80,
            margin: 8,
            animate: true,
            float: false,
            handle: '.widget-header',
            removable: false,
            resizable: { handles: 'se,sw' }
        }, '#dashboard-grid');

        grid.on('change', () => scheduleSave());
    }

    // ── Render all widgets ──
    function renderWidgets(placements) {
        grid.removeAll();
        placements.forEach(p => addWidgetToGrid(p));
    }

    function addWidgetToGrid(p) {
        const def = catalog.find(c => c.id === p.widgetId);
        if (!def) return;

        const el = document.createElement('div');
        el.className = 'grid-stack-item';
        el.setAttribute('gs-id', p.widgetId);

        el.innerHTML = `
            <div class="grid-stack-item-content">
                <div class="widget-card">
                    <div class="widget-header">
                        <span class="widget-title"><i class="bi ${def.icon}"></i> ${def.title}</span>
                        <span class="widget-actions">
                            <button class="btn btn-sm text-muted btn-refresh" title="Refresh"><i class="bi bi-arrow-clockwise"></i></button>
                            <button class="btn btn-sm text-muted btn-remove" title="Remove"><i class="bi bi-x-lg"></i></button>
                        </span>
                    </div>
                    <div class="widget-body" id="wb-${p.widgetId}">
                        <div class="widget-loading"><div class="spinner-border spinner-border-sm text-primary"></div></div>
                    </div>
                </div>
            </div>`;

        grid.addWidget(el, { x: p.x, y: p.y, w: p.w, h: p.h, id: p.widgetId });

        // Wire remove
        el.querySelector('.btn-remove').addEventListener('click', () => {
            grid.removeWidget(el);
            scheduleSave();
        });

        // Wire refresh
        el.querySelector('.btn-refresh').addEventListener('click', () => loadWidgetData(p.widgetId));

        // Load data
        loadWidgetData(p.widgetId);
    }

    // ── Load widget data via AJAX ──
    async function loadWidgetData(widgetId) {
        const container = document.getElementById('wb-' + widgetId);
        if (!container) return;
        container.innerHTML = '<div class="widget-loading"><div class="spinner-border spinner-border-sm text-primary"></div></div>';

        try {
            const resp = await fetch('/Home/WidgetData?id=' + encodeURIComponent(widgetId));
            if (!resp.ok) throw new Error('HTTP ' + resp.status);
            const data = await resp.json();
            const def = catalog.find(c => c.id === widgetId);
            if (!def) return;

            if (def.type === 'Kpi') renderKpi(container, data, def);
            else if (def.type === 'Chart') renderChart(container, data, widgetId);
            else if (def.type === 'Table') renderTable(container, data, widgetId);
        } catch (e) {
            container.innerHTML = '<span class="text-danger small">Failed to load</span>';
            console.error('Widget error:', widgetId, e);
        }
    }

    // ── KPI renderer ──
    function renderKpi(container, data, def) {
        const val = typeof data.value === 'number'
            ? (data.value >= 1000 ? '₹' + data.value.toLocaleString('en-IN') : data.value.toLocaleString('en-IN'))
            : data.value;

        // Determine if the value looks monetary (> 100 and decimal)
        const isMoney = typeof data.value === 'number' && data.label &&
            (data.label.toLowerCase().includes('sales') || data.label.toLowerCase().includes('purchase'));

        const display = isMoney ? '₹' + Number(data.value).toLocaleString('en-IN', { maximumFractionDigits: 0 }) : data.value.toLocaleString('en-IN');

        container.innerHTML = `
            <div class="widget-kpi">
                <div class="kpi-icon"><i class="bi ${def.icon}"></i></div>
                <div class="kpi-value">${display}</div>
                <div class="kpi-label">${data.label || def.title}</div>
            </div>`;
    }

    // ── Chart renderer ──
    function renderChart(container, data, widgetId) {
        // Destroy previous instance
        if (chartInstances[widgetId]) {
            chartInstances[widgetId].destroy();
            delete chartInstances[widgetId];
        }

        container.innerHTML = '<canvas class="widget-chart"></canvas>';
        const canvas = container.querySelector('canvas');
        const ctx = canvas.getContext('2d');

        let config;

        if (widgetId === 'sales-purchases-chart') {
            config = {
                type: 'line',
                data: {
                    labels: data.labels,
                    datasets: [
                        { label: 'Invoice Sales', data: data.invoiceSales, borderColor: '#0d6efd', backgroundColor: 'rgba(13,110,253,.1)', fill: true, tension: .3 },
                        { label: 'POS Sales', data: data.posSales, borderColor: '#198754', backgroundColor: 'rgba(25,135,84,.1)', fill: true, tension: .3 },
                        { label: 'Purchases', data: data.purchases, borderColor: '#dc3545', backgroundColor: 'rgba(220,53,69,.1)', fill: true, tension: .3 }
                    ]
                },
                options: chartOpts('₹')
            };
        } else if (widgetId === 'pos-hourly-chart') {
            config = {
                type: 'bar',
                data: {
                    labels: data.labels,
                    datasets: [{ label: 'POS Sales', data: data.data, backgroundColor: '#0d6efd' }]
                },
                options: chartOpts('₹')
            };
        } else if (widgetId === 'category-pie') {
            config = {
                type: 'doughnut',
                data: {
                    labels: data.labels,
                    datasets: [{ data: data.data, backgroundColor: palette() }]
                },
                options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'right', labels: { boxWidth: 12, font: { size: 11 } } } } }
            };
        } else if (widgetId === 'payment-method-pie') {
            config = {
                type: 'pie',
                data: {
                    labels: data.labels,
                    datasets: [{ data: data.data, backgroundColor: palette() }]
                },
                options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'right', labels: { boxWidth: 12, font: { size: 11 } } } } }
            };
        } else if (widgetId === 'top-items-bar') {
            config = {
                type: 'bar',
                data: {
                    labels: data.labels,
                    datasets: [{ label: 'Qty Sold', data: data.data, backgroundColor: '#198754' }]
                },
                options: { ...chartOpts(''), indexAxis: 'y' }
            };
        } else {
            container.innerHTML = '<span class="text-muted small">Chart not configured</span>';
            return;
        }

        chartInstances[widgetId] = new Chart(ctx, config);
    }

    function chartOpts(prefix) {
        return {
            responsive: true,
            maintainAspectRatio: false,
            scales: {
                y: { beginAtZero: true, ticks: { callback: v => prefix + v.toLocaleString('en-IN') } },
                x: { ticks: { maxTicksToShow: 15, maxRotation: 45 } }
            },
            plugins: { legend: { labels: { boxWidth: 12, font: { size: 11 } } } }
        };
    }

    function palette() {
        return ['#0d6efd', '#198754', '#ffc107', '#dc3545', '#6f42c1', '#fd7e14', '#20c997', '#0dcaf0', '#d63384', '#6c757d'];
    }

    // ── Table renderer ──
    function renderTable(container, data, widgetId) {
        if (!data.rows || data.rows.length === 0) {
            container.innerHTML = '<span class="text-muted small">No data</span>';
            return;
        }

        const cols = Object.keys(data.rows[0]);
        let html = '<div class="widget-table"><table class="table table-sm table-hover mb-0"><thead><tr>';
        cols.forEach(c => { html += '<th>' + formatHeader(c) + '</th>'; });
        html += '</tr></thead><tbody>';
        data.rows.forEach(row => {
            html += '<tr>';
            cols.forEach(c => {
                let v = row[c];
                if (c === 'status') v = statusBadge(v, widgetId);
                else if (typeof v === 'number' && c !== 'reorder' && c !== 'qty') v = '₹' + v.toLocaleString('en-IN');
                else if (typeof v === 'number') v = v.toLocaleString('en-IN');
                html += '<td>' + v + '</td>';
            });
            html += '</tr>';
        });
        html += '</tbody></table></div>';
        container.innerHTML = html;
    }

    function formatHeader(key) {
        return key.replace(/([A-Z])/g, ' $1').replace(/^./, s => s.toUpperCase());
    }

    function statusBadge(status, widgetId) {
        if (widgetId === 'recent-invoices') {
            return status === 2
                ? '<span class="badge bg-success text-white">Posted</span>'
                : '<span class="badge bg-warning text-dark">Draft</span>';
        }
        if (widgetId === 'recent-pos-bills') {
            if (status === 1) return '<span class="badge bg-warning text-dark">Open</span>';
            if (status === 2) return '<span class="badge bg-success text-white">Completed</span>';
            return '<span class="badge bg-secondary text-white">Void</span>';
        }
        if (widgetId === 'eod-summary') {
            return status === 2
                ? '<span class="badge bg-success text-white">Closed</span>'
                : '<span class="badge bg-warning text-dark">Pending</span>';
        }
        return status;
    }

    // ── Auto-save layout ──
    function scheduleSave() {
        clearTimeout(saveTimer);
        saveTimer = setTimeout(saveLayout, 1500);
    }

    async function saveLayout() {
        const items = grid.getGridItems();
        const placements = items.map(el => {
            const node = el.gridstackNode;
            return {
                widgetId: node.id || el.getAttribute('gs-id'),
                x: node.x,
                y: node.y,
                w: node.w,
                h: node.h,
                visible: true
            };
        });

        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value
            || document.cookie.split('; ').find(r => r.startsWith('XSRF-TOKEN='))?.split('=')[1];

        await fetch('/Home/SaveLayout', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-XSRF-TOKEN': token || '',
                'RequestVerificationToken': token || ''
            },
            body: JSON.stringify(placements)
        });
    }

    // ── Toolbar ──
    function wireToolbar() {
        // Widget picker toggle
        const pickerBtn = document.getElementById('btn-add-widget');
        const pickerOverlay = document.getElementById('widget-picker-overlay');
        const pickerPanel = document.getElementById('widget-picker');
        const pickerClose = document.getElementById('picker-close');
        const pickerBody = document.getElementById('picker-body');

        if (pickerBtn) {
            pickerBtn.addEventListener('click', () => {
                renderPickerList();
                pickerOverlay?.classList.add('show');
                pickerPanel?.classList.add('show');
            });
        }

        if (pickerClose) {
            pickerClose.addEventListener('click', closePicker);
        }
        if (pickerOverlay) {
            pickerOverlay.addEventListener('click', closePicker);
        }

        function closePicker() {
            pickerOverlay?.classList.remove('show');
            pickerPanel?.classList.remove('show');
        }

        function renderPickerList() {
            if (!pickerBody) return;
            const existing = new Set(grid.getGridItems().map(el => el.getAttribute('gs-id')));

            pickerBody.innerHTML = '';
            catalog.forEach(w => {
                const isActive = existing.has(w.id);
                const item = document.createElement('div');
                item.className = 'widget-picker-item' + (isActive ? ' active' : '');
                item.innerHTML = `
                    <span class="picker-icon"><i class="bi ${w.icon}"></i></span>
                    <span>
                        <span class="picker-text">${w.title}</span><br>
                        <span class="picker-type">${w.type} · ${w.defaultW}×${w.defaultH}</span>
                    </span>`;

                item.addEventListener('click', () => {
                    if (isActive) return;
                    addWidgetToGrid({ widgetId: w.id, x: 0, y: 0, w: w.defaultW, h: w.defaultH });
                    scheduleSave();
                    closePicker();
                });

                pickerBody.appendChild(item);
            });
        }

        // Reset layout
        const resetBtn = document.getElementById('btn-reset-layout');
        if (resetBtn) {
            resetBtn.addEventListener('click', async () => {
                if (!confirm('Reset dashboard to default layout?')) return;

                const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value
                    || document.cookie.split('; ').find(r => r.startsWith('XSRF-TOKEN='))?.split('=')[1];

                await fetch('/Home/ResetLayout', {
                    method: 'POST',
                    headers: { 'X-XSRF-TOKEN': token || '', 'RequestVerificationToken': token || '' }
                });

                // Reload
                const resp = await fetch('/Home/GetLayout');
                const data = await resp.json();
                catalog = data.catalog;
                renderWidgets(data.layout);
            });
        }

        // Refresh all
        const refreshBtn = document.getElementById('btn-refresh-all');
        if (refreshBtn) {
            refreshBtn.addEventListener('click', () => {
                grid.getGridItems().forEach(el => {
                    const id = el.getAttribute('gs-id');
                    if (id) loadWidgetData(id);
                });
            });
        }

        // Lock / Unlock toggle
        const lockBtn = document.getElementById('btn-lock-toggle');
        if (lockBtn) {
            lockBtn.addEventListener('click', () => {
                isLocked = !isLocked;
                applyLockState();
            });
        }
    }

    // ── Lock / Unlock helpers ──
    function applyLockState() {
        const lockBtn = document.getElementById('btn-lock-toggle');
        const gridEl = document.getElementById('dashboard-grid');

        if (isLocked) {
            grid.enableMove(false);
            grid.enableResize(false);
            gridEl?.classList.add('dashboard-locked');
            if (lockBtn) {
                lockBtn.innerHTML = '<i class="bi bi-lock me-1"></i><span>Unlock</span>';
                lockBtn.classList.add('btn-lock-locked');
                lockBtn.classList.remove('btn-outline-dark');
            }
        } else {
            grid.enableMove(true);
            grid.enableResize(true);
            gridEl?.classList.remove('dashboard-locked');
            if (lockBtn) {
                lockBtn.innerHTML = '<i class="bi bi-unlock me-1"></i><span>Lock</span>';
                lockBtn.classList.remove('btn-lock-locked');
                lockBtn.classList.add('btn-outline-dark');
            }
        }
    }

    // ── Sprint 9: SignalR real-time dashboard updates ──
    function initSignalR() {
        if (typeof signalR === 'undefined') return; // CDN not loaded

        const connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/retail')
            .withAutomaticReconnect()
            .build();

        // When a POS bill is completed, refresh sales-related KPI widgets
        connection.on('BillCompleted', () => {
            ['total-sales', 'pos-sales', 'open-pos-bills', 'completed-bills-7d',
             'low-stock-count', 'recent-pos-bills', 'sales-purchases-chart',
             'pos-hourly-chart', 'payment-method-pie', 'top-items-bar'].forEach(id => loadWidgetData(id));
        });

        // When an invoice is posted, refresh invoice-related widgets
        connection.on('InvoicePosted', () => {
            ['total-sales', 'draft-invoices', 'recent-invoices',
             'sales-purchases-chart', 'category-pie'].forEach(id => loadWidgetData(id));
        });

        // When stock alert fires, refresh low-stock widgets
        connection.on('StockAlert', (data) => {
            ['low-stock-count', 'low-stock-list'].forEach(id => loadWidgetData(id));
            // Show a toast notification
            showToast(`⚠️ ${data.count} items below reorder level`, 'warning');
        });

        // EOD report generated
        connection.on('EodReportGenerated', () => {
            ['eod-summary'].forEach(id => loadWidgetData(id));
            showToast('📊 EOD Report auto-generated', 'info');
        });

        connection.start().catch(err => console.warn('SignalR:', err));
    }

    function showToast(message, type) {
        const container = document.getElementById('signalr-toasts') || createToastContainer();
        const toast = document.createElement('div');
        toast.className = `alert alert-${type || 'info'} alert-dismissible fade show small shadow-sm`;
        toast.style.cssText = 'min-width:280px;margin-bottom:8px;';
        toast.innerHTML = `${message}<button class="btn-close btn-close-sm" data-bs-dismiss="alert"></button>`;
        container.prepend(toast);
        setTimeout(() => toast.remove(), 8000);
    }

    function createToastContainer() {
        const c = document.createElement('div');
        c.id = 'signalr-toasts';
        c.style.cssText = 'position:fixed;top:70px;right:16px;z-index:9999;max-width:350px;';
        document.body.appendChild(c);
        return c;
    }
})();
