/*
 * Lightweight date-range picker with quick-range presets, built on flatpickr
 * (already vendored, vanilla JS - no jQuery). Replaces the jQuery
 * bootstrap-daterangepicker plugin previously used for `.bookingrange`
 * inputs, including its "Today / Yesterday / Last 7 Days / ..." presets
 * panel.
 */
(function (global) {
	'use strict';

	function pad(n) { return n < 10 ? '0' + n : n; }
	function fmt(date) { return pad(date.getDate()) + '/' + pad(date.getMonth() + 1) + '/' + date.getFullYear(); }

	function daysAgo(n) {
		var d = new Date();
		d.setHours(0, 0, 0, 0);
		d.setDate(d.getDate() - n);
		return d;
	}

	function defaultRanges() {
		var today = daysAgo(0);
		var yesterday = daysAgo(1);
		var startOfMonth = new Date(today.getFullYear(), today.getMonth(), 1);
		var endOfMonth = new Date(today.getFullYear(), today.getMonth() + 1, 0);
		var lastMonthStart = new Date(today.getFullYear(), today.getMonth() - 1, 1);
		var lastMonthEnd = new Date(today.getFullYear(), today.getMonth(), 0);
		return {
			'Today': [today, today],
			'Yesterday': [yesterday, yesterday],
			'Last 7 Days': [daysAgo(6), today],
			'Last 30 Days': [daysAgo(29), today],
			'This Month': [startOfMonth, endOfMonth],
			'Last Month': [lastMonthStart, lastMonthEnd]
		};
	}

	function vanillaDateRangePicker(el, options) {
		if (!el || el._dateRangePicker || !window.flatpickr) return;
		options = options || {};
		var ranges = options.ranges || defaultRanges();
		var onSelect = options.onSelect;
		var startDate = options.startDate || defaultRanges()['Last 7 Days'][0];
		var endDate = options.endDate || defaultRanges()['Last 7 Days'][1];

		function apply(start, end) {
			el.value = fmt(start) + ' - ' + fmt(end);
			if (onSelect) onSelect(start, end);
		}

		var fp = flatpickr(el, {
			mode: 'range',
			dateFormat: 'd/m/Y',
			defaultDate: [startDate, endDate],
			onReady: function (selectedDates, dateStr, instance) {
				var panel = document.createElement('div');
				panel.className = 'flatpickr-presets';
				panel.style.display = 'flex';
				panel.style.flexWrap = 'wrap';
				panel.style.gap = '4px';
				panel.style.padding = '8px';
				panel.style.borderBottom = '1px solid #e9ecef';

				Object.keys(ranges).forEach(function (label) {
					var btn = document.createElement('button');
					btn.type = 'button';
					btn.className = 'btn btn-sm btn-light flatpickr-preset-btn';
					btn.textContent = label;
					btn.addEventListener('click', function () {
						var range = ranges[label];
						instance.setDate(range, false);
						apply(range[0], range[1]);
						instance.close();
					});
					panel.appendChild(btn);
				});

				instance.calendarContainer.insertBefore(panel, instance.calendarContainer.firstChild);
			},
			onClose: function (selectedDates) {
				if (selectedDates.length === 2) {
					apply(selectedDates[0], selectedDates[1]);
				}
			}
		});

		apply(startDate, endDate);
		el._dateRangePicker = fp;
		return fp;
	}

	global.vanillaDateRangePicker = vanillaDateRangePicker;
})(window);
