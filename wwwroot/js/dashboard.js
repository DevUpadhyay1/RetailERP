(function () {
    'use strict';

    let grid = null;
    let catalog = [];
    let currentLayout = [];
    let saveTimer = null;
    let isLocked = true;
    const chartInstances = {};

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
        applyLockState();
        initSignalR();
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
                        ${skeletonHtml(def.type)}
                    </div>
                </div>
            </div>`;

        grid.addWidget(el, { x: p.x, y: p.y, w: p.w, h: p.h, id: p.widgetId });

        el.querySelector('.btn-remove').addEventListener('click', () => {
            grid.removeWidget(el);
            scheduleSave();
        });

        el.querySelector('.btn-refresh').addEventListener('click', () => loadWidgetData(p.widgetId));

        loadWidgetData(p.widgetId);
    }

    function skeletonHtml(type) {
        if (type === 'Kpi') {
            return `<div style="text-align:center;width:100%;padding:1rem;">
                <div class="skeleton" style="width:40px;height:40px;border-radius:50%;margin:0 auto .5rem;"></div>
                <div class="skeleton skeleton-title" style="width:50%;margin:0 auto .4rem;"></div>
                <div class="skeleton skeleton-text" style="width:35%;margin:0 auto;"></div>
            </div>`;
        }
        if (type === 'Chart') {
            return '<div class="skeleton skeleton-chart" style="width:95%;margin:auto;"></div>';
        }
        return `<div style="width:100%;padding:.5rem;">
            <div class="skeleton skeleton-text" style="width:100%;"></div>
            <div class="skeleton skeleton-text" style="width:90%;"></div>
            <div class="skeleton skeleton-text" style="width:80%;"></div>
            <div class="skeleton skeleton-text" style="width:95%;"></div>
        </div>`;
    }

    async function loadWidgetData(widgetId) {
        const container = document.getElementById('wb-' + widgetId);
        if (!container) return;

        const def = catalog.find(c => c.id === widgetId);
        container.innerHTML = skeletonHtml(def ? def.type : 'Table');

        try {
            const resp = await fetch('/Home/WidgetData?id=' + encodeURIComponent(widgetId));
            if (!resp.ok) throw new Error('HTTP ' + resp.status);
            const data = await resp.json();
            if (!def) return;

            if (def.type === 'Kpi') renderKpi(container, data, def);
            else if (def.type === 'Chart') renderChart(container, data, widgetId);
            else if (def.type === 'Table') renderTable(container, data, widgetId);
        } catch (e) {
            container.innerHTML = '<span class="text-danger small"><i class="bi bi-exclamation-triangle me-1"></i>Failed to load</span>';
            console.error('Widget error:', widgetId, e);
        }
    }

    // ── KPI with count-up animation ──
    function renderKpi(container, data, def) {
        const isMoney = typeof data.value === 'number' && data.label &&
            (data.label.toLowerCase().includes('sales') || data.label.toLowerCase().includes('purchase'));

        const rawValue = typeof data.value === 'number' ? data.value : 0;
        const display = isMoney
            ? '\u20B9' + Number(rawValue).toLocaleString('en-IN', { maximumFractionDigits: 0 })
            : (typeof data.value === 'number' ? rawValue.toLocaleString('en-IN') : data.value);

        container.innerHTML = `
            <div class="widget-kpi">
                <div class="kpi-icon"><i class="bi ${def.icon}"></i></div>
                <div class="kpi-value" data-target="${rawValue}" data-money="${isMoney ? '1' : '0'}">0</div>
                <div class="kpi-label">${data.label || def.title}</div>
            </div>`;

        const valueEl = container.querySelector('.kpi-value');
        if (typeof data.value === 'number' && rawValue > 0) {
            animateCount(valueEl, rawValue, isMoney);
        } else {
            valueEl.textContent = display;
        }
    }

    function animateCount(el, target, isMoney) {
        const duration = 800;
        const start = performance.now();
        const step = (now) => {
            const progress = Math.min((now - start) / duration, 1);
            const eased = 1 - Math.pow(1 - progress, 3);
            const current = Math.round(target * eased);
            el.textContent = isMoney
                ? '\u20B9' + current.toLocaleString('en-IN', { maximumFractionDigits: 0 })
                : current.toLocaleString('en-IN');
            if (progress < 1) requestAnimationFrame(step);
        };
        requestAnimationFrame(step);
    }

    // ── Chart renderer using ApexCharts ──
    function renderChart(container, data, widgetId) {
        if (typeof ApexCharts === 'undefined') {
            container.innerHTML = '<span class="text-muted small"><i class="bi bi-bar-chart me-1"></i>Chart library loading...</span>';
            setTimeout(() => renderChart(container, data, widgetId), 1000);
            return;
        }

        if (chartInstances[widgetId]) {
            chartInstances[widgetId].destroy();
            delete chartInstances[widgetId];
        }

        container.innerHTML = '<div class="widget-apex-chart"></div>';
        const chartEl = container.querySelector('.widget-apex-chart');
        chartEl.style.width = '100%';
        chartEl.style.height = '100%';
        chartEl.style.minHeight = '180px';

        const isDark = document.body.classList.contains('dark-mode');
        const textColor = isDark ? '#94a3b8' : '#64748b';
        const gridColor = isDark ? '#1e293b' : '#f1f5f9';
        const bgColor = 'transparent';

        let options;

        if (widgetId === 'sales-purchases-chart') {
            options = {
                chart: {
                    type: 'area', height: '100%',
                    background: bgColor,
                    toolbar: { show: true, tools: { download: true, zoom: true, zoomin: true, zoomout: true, pan: true, reset: true } },
                    zoom: { enabled: true, type: 'x' },
                    animations: { enabled: true, easing: 'easeinout', speed: 600 }
                },
                series: [
                    { name: 'Invoice Sales', data: data.invoiceSales || [] },
                    { name: 'POS Sales', data: data.posSales || [] },
                    { name: 'Purchases', data: data.purchases || [] }
                ],
                xaxis: { categories: data.labels || [], labels: { style: { colors: textColor, fontSize: '11px' }, rotateAlways: false, rotate: -45 } },
                yaxis: { labels: { style: { colors: textColor }, formatter: v => '\u20B9' + Number(v || 0).toLocaleString('en-IN', { maximumFractionDigits: 0 }) } },
                colors: ['#3b82f6', '#10b981', '#ef4444'],
                fill: { type: 'gradient', gradient: { shadeIntensity: 1, opacityFrom: 0.4, opacityTo: 0.05, stops: [0, 95, 100] } },
                stroke: { curve: 'smooth', width: 2.5 },
                dataLabels: { enabled: false },
                grid: { borderColor: gridColor, strokeDashArray: 3 },
                tooltip: { theme: isDark ? 'dark' : 'light', y: { formatter: v => '\u20B9' + Number(v || 0).toLocaleString('en-IN') } },
                legend: { labels: { colors: textColor }, fontSize: '12px' },
                theme: { mode: isDark ? 'dark' : 'light' }
            };
        } else if (widgetId === 'pos-hourly-chart') {
            options = {
                chart: {
                    type: 'bar', height: '100%', background: bgColor,
                    toolbar: { show: true, tools: { download: true, zoom: true, zoomin: true, zoomout: true, reset: true } },
                    zoom: { enabled: true },
                    animations: { enabled: true, speed: 500 }
                },
                series: [{ name: 'POS Sales', data: data.data || [] }],
                xaxis: { categories: data.labels || [], labels: { style: { colors: textColor, fontSize: '11px' } } },
                yaxis: { labels: { style: { colors: textColor }, formatter: v => '\u20B9' + Number(v || 0).toLocaleString('en-IN', { maximumFractionDigits: 0 }) } },
                colors: ['#3b82f6'],
                fill: { type: 'gradient', gradient: { shade: 'light', type: 'vertical', shadeIntensity: 0.2, opacityFrom: 0.9, opacityTo: 0.7 } },
                plotOptions: { bar: { borderRadius: 4, columnWidth: '60%' } },
                dataLabels: { enabled: false },
                grid: { borderColor: gridColor, strokeDashArray: 3 },
                tooltip: { theme: isDark ? 'dark' : 'light', y: { formatter: v => '\u20B9' + Number(v || 0).toLocaleString('en-IN') } },
                theme: { mode: isDark ? 'dark' : 'light' }
            };
        } else if (widgetId === 'category-pie') {
            options = {
                chart: { type: 'donut', height: '100%', background: bgColor, animations: { enabled: true, speed: 500 } },
                series: data.data || [],
                labels: data.labels || [],
                colors: apexPalette(),
                legend: { position: 'right', labels: { colors: textColor }, fontSize: '11px' },
                stroke: { show: true, width: 2, colors: [isDark ? '#1e293b' : '#fff'] },
                plotOptions: { pie: { donut: { size: '55%', labels: { show: true, total: { show: true, label: 'Total', color: textColor, formatter: w => '\u20B9' + w.globals.seriesTotals.reduce((a, b) => a + b, 0).toLocaleString('en-IN') } } } } },
                dataLabels: { enabled: false },
                tooltip: { theme: isDark ? 'dark' : 'light' },
                theme: { mode: isDark ? 'dark' : 'light' }
            };
        } else if (widgetId === 'payment-method-pie') {
            options = {
                chart: { type: 'pie', height: '100%', background: bgColor, animations: { enabled: true } },
                series: data.data || [],
                labels: data.labels || [],
                colors: apexPalette(),
                legend: { position: 'right', labels: { colors: textColor }, fontSize: '11px' },
                stroke: { show: true, width: 2, colors: [isDark ? '#1e293b' : '#fff'] },
                dataLabels: { enabled: false },
                tooltip: { theme: isDark ? 'dark' : 'light' },
                theme: { mode: isDark ? 'dark' : 'light' }
            };
        } else if (widgetId === 'top-items-bar') {
            options = {
                chart: { type: 'bar', height: '100%', background: bgColor, toolbar: { show: false }, animations: { enabled: true, speed: 500 } },
                series: [{ name: 'Qty Sold', data: data.data || [] }],
                xaxis: { categories: data.labels || [], labels: { style: { colors: textColor, fontSize: '11px' } } },
                yaxis: { labels: { style: { colors: textColor } } },
                colors: ['#10b981'],
                plotOptions: { bar: { horizontal: true, borderRadius: 4, barHeight: '60%' } },
                fill: { type: 'gradient', gradient: { shade: 'light', type: 'horizontal', shadeIntensity: 0.1, opacityFrom: 0.9, opacityTo: 0.7 } },
                dataLabels: { enabled: false },
                grid: { borderColor: gridColor, strokeDashArray: 3 },
                tooltip: { theme: isDark ? 'dark' : 'light' },
                theme: { mode: isDark ? 'dark' : 'light' }
            };
        } else {
            container.innerHTML = '<span class="text-muted small">Chart not configured</span>';
            return;
        }

        const chart = new ApexCharts(chartEl, options);
        chart.render();
        chartInstances[widgetId] = chart;
    }

    function apexPalette() {
        return ['#3b82f6', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', '#f97316', '#14b8a6', '#06b6d4', '#ec4899', '#64748b'];
    }

    // ── Table renderer ──
    function renderTable(container, data, widgetId) {
        if (!data.rows || data.rows.length === 0) {
            container.innerHTML = `<div class="text-center py-3" style="width:100%;">
                <i class="bi bi-inbox text-muted" style="font-size:2rem;opacity:.3;"></i>
                <div class="text-muted small mt-1">No data available</div>
            </div>`;
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
                else if (typeof v === 'number' && c !== 'reorder' && c !== 'qty') v = '\u20B9' + v.toLocaleString('en-IN');
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
                ? '<span class="badge bg-success">Posted</span>'
                : '<span class="badge bg-warning">Draft</span>';
        }
        if (widgetId === 'recent-pos-bills') {
            if (status === 1) return '<span class="badge bg-warning">Open</span>';
            if (status === 2) return '<span class="badge bg-success">Completed</span>';
            return '<span class="badge bg-secondary">Void</span>';
        }
        if (widgetId === 'eod-summary') {
            return status === 2
                ? '<span class="badge bg-success">Closed</span>'
                : '<span class="badge bg-warning">Pending</span>';
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
                x: node.x, y: node.y, w: node.w, h: node.h, visible: true
            };
        });

        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value
            || document.cookie.split('; ').find(r => r.startsWith('XSRF-TOKEN='))?.split('=')[1];

        await fetch('/Home/SaveLayout', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'X-XSRF-TOKEN': token || '', 'RequestVerificationToken': token || '' },
            body: JSON.stringify(placements)
        });
    }

    // ── Toolbar ──
    function wireToolbar() {
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

        window._closeDashboardPicker = closePicker;

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
                        <span class="picker-type">${w.type} \u00B7 ${w.defaultW}\u00D7${w.defaultH}</span>
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

                const resp = await fetch('/Home/GetLayout');
                const data = await resp.json();
                catalog = data.catalog;
                renderWidgets(data.layout);
            });
        }

        const refreshBtn = document.getElementById('btn-refresh-all');
        if (refreshBtn) {
            refreshBtn.addEventListener('click', () => {
                grid.getGridItems().forEach(el => {
                    const id = el.getAttribute('gs-id');
                    if (id) loadWidgetData(id);
                });
            });
        }

        const lockBtn = document.getElementById('btn-lock-toggle');
        if (lockBtn) {
            lockBtn.addEventListener('click', () => {
                isLocked = !isLocked;
                applyLockState();
            });
        }
    }

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

    // ── SignalR real-time dashboard updates ──
    function initSignalR() {
        if (typeof signalR === 'undefined') return;

        const connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/retail')
            .withAutomaticReconnect()
            .build();

        connection.on('BillCompleted', () => {
            ['total-sales', 'pos-sales', 'open-pos-bills', 'completed-bills-7d',
             'low-stock-count', 'recent-pos-bills', 'sales-purchases-chart',
             'pos-hourly-chart', 'payment-method-pie', 'top-items-bar'].forEach(id => loadWidgetData(id));
        });

        connection.on('InvoicePosted', () => {
            ['total-sales', 'draft-invoices', 'recent-invoices',
             'sales-purchases-chart', 'category-pie'].forEach(id => loadWidgetData(id));
        });

        connection.on('StockAlert', (data) => {
            ['low-stock-count', 'low-stock-list'].forEach(id => loadWidgetData(id));
            showToast('\u26A0\uFE0F ' + data.count + ' items below reorder level', 'warning');
        });

        connection.on('EodReportGenerated', () => {
            ['eod-summary'].forEach(id => loadWidgetData(id));
            showToast('\uD83D\uDCCA EOD Report auto-generated', 'info');
        });

        connection.start().catch(err => console.warn('SignalR:', err));
    }

    function showToast(message, type) {
        const container = document.getElementById('signalr-toasts') || createToastContainer();
        const toast = document.createElement('div');
        toast.className = 'erp-toast';
        toast.innerHTML = message + '<button class="btn-close btn-close-sm ms-auto" onclick="this.parentElement.remove()"></button>';
        container.prepend(toast);
        setTimeout(() => { if (toast.parentElement) toast.remove(); }, 8000);
    }

    function createToastContainer() {
        const c = document.createElement('div');
        c.id = 'signalr-toasts';
        c.className = 'erp-toast-container';
        document.body.appendChild(c);
        return c;
    }
})();
