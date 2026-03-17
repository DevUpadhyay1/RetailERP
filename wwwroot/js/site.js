// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener('DOMContentLoaded', function () {
	var shell = document.getElementById('appShell');
	var toggle = document.getElementById('sidebarToggle');
	if (!shell || !toggle) return;

	var storageKey = 'retailerpsidebar:collapsed';
	var isCollapsed = localStorage.getItem(storageKey) === '1';
	if (isCollapsed) shell.classList.add('sidebar-collapsed');

	toggle.addEventListener('click', function () {
		shell.classList.toggle('sidebar-collapsed');
		localStorage.setItem(storageKey, shell.classList.contains('sidebar-collapsed') ? '1' : '0');
	});
});
