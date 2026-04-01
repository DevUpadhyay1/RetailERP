document.addEventListener('DOMContentLoaded', function () {
	// ── Sidebar toggle ──
	var shell = document.getElementById('appShell');
	var toggle = document.getElementById('sidebarToggle');
	if (shell && toggle) {
		var storageKey = 'retailerpsidebar:collapsed';
		var isCollapsed = localStorage.getItem(storageKey) === '1';
		if (isCollapsed) shell.classList.add('sidebar-collapsed');

		toggle.addEventListener('click', function () {
			shell.classList.toggle('sidebar-collapsed');
			localStorage.setItem(storageKey, shell.classList.contains('sidebar-collapsed') ? '1' : '0');
		});
	}

	// ── Dark mode toggle ──
	var themeToggle = document.getElementById('themeToggle');
	var themeIcon = document.getElementById('themeIcon');
	var themeKey = 'retailerp:theme';

	function applyTheme(dark) {
		document.body.classList.toggle('dark-mode', dark);
		if (themeIcon) {
			themeIcon.className = dark ? 'bi bi-sun' : 'bi bi-moon-stars';
		}
	}

	var savedTheme = localStorage.getItem(themeKey);
	var prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
	var isDark = savedTheme === 'dark' || (savedTheme === null && prefersDark);
	if (themeIcon) {
		themeIcon.className = isDark ? 'bi bi-sun' : 'bi bi-moon-stars';
	}

	if (themeToggle) {
		themeToggle.addEventListener('click', function () {
			isDark = !isDark;
			applyTheme(isDark);
			localStorage.setItem(themeKey, isDark ? 'dark' : 'light');
		});
	}

	window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', function (e) {
		if (localStorage.getItem(themeKey) === null) {
			applyTheme(e.matches);
		}
	});

	// ── Fast-navigation helpers (no SPA rewrite) ──
	initNavProgress();
	initSmartPrefetch();
});

function initNavProgress() {
	var bar = document.getElementById('navProgressBar');
	if (!bar) {
		bar = document.createElement('div');
		bar.id = 'navProgressBar';
		bar.className = 'nav-progress-bar';
		document.body.appendChild(bar);
	}

	var start = function () {
		document.body.classList.add('nav-loading');
	};

	var stop = function () {
		document.body.classList.remove('nav-loading');
	};

	document.addEventListener('click', function (e) {
		var link = e.target && e.target.closest ? e.target.closest('a[href]') : null;
		if (!link) return;
		if (!shouldHandleLink(link, e)) return;
		start();
	}, true);

	window.addEventListener('pageshow', stop);
	window.addEventListener('load', stop);
}

function initSmartPrefetch() {
	var supportsPrefetch = !!document.createElement('link').relList && document.createElement('link').relList.supports('prefetch');
	if (!supportsPrefetch) return;

	var prefetched = new Set();
	var maxPrefetch = 30;

	var tryPrefetch = function (url) {
		if (!url || prefetched.has(url) || prefetched.size >= maxPrefetch) return;
		prefetched.add(url);

		var l = document.createElement('link');
		l.rel = 'prefetch';
		l.href = url;
		l.as = 'document';
		document.head.appendChild(l);
	};

	var likelyLinks = document.querySelectorAll('.app-sidebar a[href], .app-topbar a[href], .dashboard-toolbar a[href]');
	likelyLinks.forEach(function (a) {
		if (isPrefetchCandidate(a)) {
			tryPrefetch(a.href);
		}

		a.addEventListener('mouseenter', function () {
			if (isPrefetchCandidate(a)) tryPrefetch(a.href);
		}, { passive: true });

		a.addEventListener('focus', function () {
			if (isPrefetchCandidate(a)) tryPrefetch(a.href);
		}, { passive: true });
	});
}

function isPrefetchCandidate(link) {
	if (!link || !link.href) return false;
	if (link.dataset && link.dataset.noPrefetch === 'true') return false;
	if (link.target && link.target !== '' && link.target !== '_self') return false;
	if (link.hasAttribute('download')) return false;

	var url;
	try {
		url = new URL(link.href, window.location.href);
	} catch {
		return false;
	}

	if (url.origin !== window.location.origin) return false;
	if (url.pathname === window.location.pathname && (!url.search || url.search === window.location.search)) return false;
	if (url.hash && url.pathname === window.location.pathname) return false;

	return true;
}

function shouldHandleLink(link, e) {
	if (!link || !link.href) return false;
	if (e.defaultPrevented) return false;
	if (e.button !== 0) return false;
	if (e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return false;
	if (link.target && link.target !== '' && link.target !== '_self') return false;
	if (link.hasAttribute('download')) return false;

	var url;
	try {
		url = new URL(link.href, window.location.href);
	} catch {
		return false;
	}

	if (url.origin !== window.location.origin) return false;
	if (url.hash && url.pathname === window.location.pathname && url.search === window.location.search) return false;

	return true;
}
