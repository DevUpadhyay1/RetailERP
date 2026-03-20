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
	applyTheme(isDark);

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
});
