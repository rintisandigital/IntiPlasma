/*
 * Minimal year-only picker, replacing bootstrap-datetimepicker's
 * viewMode:'years' mode - a small dropdown list of years next to the input,
 * no calendar grid needed since only the year is selected.
 */
(function (global) {
	'use strict';

	function vanillaYearPicker(input, options) {
		if (!input || input._yearPicker) return;
		options = options || {};
		var span = options.range || 15;
		var currentYear = new Date().getFullYear();

		var dropdown = document.createElement('div');
		dropdown.className = 'yearpicker-dropdown';
		dropdown.style.position = 'absolute';
		dropdown.style.zIndex = '1060';
		dropdown.style.display = 'none';
		dropdown.style.maxHeight = '200px';
		dropdown.style.overflowY = 'auto';
		dropdown.style.background = '#fff';
		dropdown.style.border = '1px solid #e9ecef';
		dropdown.style.borderRadius = '5px';
		dropdown.style.boxShadow = '0 4px 12px rgba(0,0,0,0.08)';
		dropdown.style.minWidth = '100px';

		for (var y = currentYear + 5; y >= currentYear - span; y--) {
			(function (year) {
				var item = document.createElement('div');
				item.textContent = year;
				item.className = 'yearpicker-item';
				item.style.padding = '6px 12px';
				item.style.cursor = 'pointer';
				item.addEventListener('mouseenter', function () { item.style.background = '#f5f5f5'; });
				item.addEventListener('mouseleave', function () { item.style.background = ''; });
				item.addEventListener('click', function () {
					input.value = String(year);
					input.dispatchEvent(new Event('change'));
					dropdown.style.display = 'none';
				});
				dropdown.appendChild(item);
			})(y);
		}

		document.body.appendChild(dropdown);

		function position() {
			var rect = input.getBoundingClientRect();
			dropdown.style.top = (window.scrollY + rect.bottom + 2) + 'px';
			dropdown.style.left = (window.scrollX + rect.left) + 'px';
			dropdown.style.width = rect.width + 'px';
		}

		input.addEventListener('focus', function () { position(); dropdown.style.display = 'block'; });
		document.addEventListener('click', function (e) {
			if (e.target !== input && !dropdown.contains(e.target)) dropdown.style.display = 'none';
		});
		window.addEventListener('resize', position);

		input._yearPicker = dropdown;
	}

	global.vanillaYearPicker = vanillaYearPicker;
})(window);
