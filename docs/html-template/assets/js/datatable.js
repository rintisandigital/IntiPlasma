/*
 * Lightweight vanilla-JS DataTable engine that outputs the exact same class
 * names DataTables (+ dataTables.bootstrap5) always has here - table.dataTable,
 * .sorting/.sorting_asc/.sorting_desc, .dataTables_wrapper, .dataTables_filter,
 * .dataTables_length, .dataTables_info, .dataTables_paginate .pagination -
 * so assets/scss/vendors/_datatables.scss keeps applying unmodified. Supports
 * sorting, text search, and pagination - the only features this template uses.
 */
(function (global) {
	'use strict';

	function VanillaDataTable(table, options) {
		if (!table || table._vanillaDataTable) return table && table._vanillaDataTable;
		this.table = table;
		this.options = options || {};
		this.language = Object.assign({
			searchPlaceholder: 'Search',
			sLengthMenu: 'Row Per Page _MENU_ Entries',
			info: '_START_ - _END_ of _TOTAL_ items',
			paginate: { next: 'Next', previous: 'Previous' }
		}, this.options.language || {});
		this.pageLength = this.options.pageLength || 10;
		this.page = 0;
		this.sortColumn = -1;
		this.sortDir = 'asc';
		this.searchTerm = '';

		table.classList.add('dataTable');
		this._readRows();
		this._buildControls();
		this._bindSorting();
		this.render();

		table._vanillaDataTable = this;
		if (typeof this.options.initComplete === 'function') {
			this.options.initComplete.call(this, this);
		}
		return this;
	}

	VanillaDataTable.prototype._readRows = function () {
		var tbody = this.table.querySelector('tbody');
		this.tbody = tbody;
		this.rows = tbody ? Array.prototype.slice.call(tbody.querySelectorAll('tr')) : [];
	};

	VanillaDataTable.prototype._outer = function () {
		return this.table.closest('.table-responsive') || this.table;
	};

	VanillaDataTable.prototype._buildControls = function () {
		var self = this;
		var outer = this._outer();

		// Filter
		this.filterWrap = document.createElement('div');
		this.filterWrap.className = 'dataTables_filter';
		var filterInput = document.createElement('input');
		filterInput.type = 'search';
		filterInput.className = 'form-control';
		filterInput.placeholder = this.language.searchPlaceholder;
		this.filterWrap.appendChild(filterInput);
		this.filterInput = filterInput;
		function onSearch() {
			self.searchTerm = filterInput.value.toLowerCase();
			self.page = 0;
			self.render();
		}
		filterInput.addEventListener('keyup', onSearch);
		filterInput.addEventListener('input', onSearch);

		// Length
		this.lengthWrap = document.createElement('div');
		this.lengthWrap.className = 'dataTables_length';
		var lengthLabel = document.createElement('label');
		var parts = this.language.sLengthMenu.split('_MENU_');
		var select = document.createElement('select');
		select.className = 'form-select form-select-sm';
		[10, 25, 50, 100].forEach(function (n) {
			var opt = document.createElement('option');
			opt.value = n;
			opt.textContent = n;
			if (n === self.pageLength) opt.selected = true;
			select.appendChild(opt);
		});
		lengthLabel.appendChild(document.createTextNode(parts[0] || ''));
		lengthLabel.appendChild(select);
		lengthLabel.appendChild(document.createTextNode(parts[1] || ''));
		this.lengthWrap.appendChild(lengthLabel);
		this.lengthSelect = select;
		select.addEventListener('change', function () {
			self.pageLength = parseInt(select.value, 10);
			self.page = 0;
			self.render();
		});

		// Info
		this.infoWrap = document.createElement('div');
		this.infoWrap.className = 'dataTables_info';

		// Paginate
		this.paginateWrap = document.createElement('div');
		this.paginateWrap.className = 'dataTables_paginate';
		this.paginateList = document.createElement('ul');
		this.paginateList.className = 'pagination';
		this.paginateWrap.appendChild(this.paginateList);

		outer.parentNode.insertBefore(this.filterWrap, outer);

		var afterWrap = document.createElement('div');
		afterWrap.className = 'dataTables_wrapper';
		afterWrap.appendChild(this.lengthWrap);
		afterWrap.appendChild(this.infoWrap);
		afterWrap.appendChild(this.paginateWrap);
		if (outer.nextSibling) {
			outer.parentNode.insertBefore(afterWrap, outer.nextSibling);
		} else {
			outer.parentNode.appendChild(afterWrap);
		}
	};

	VanillaDataTable.prototype._bindSorting = function () {
		var self = this;
		var ths = Array.prototype.slice.call(this.table.querySelectorAll('thead th'));
		ths.forEach(function (th, index) {
			if (th.classList.contains('no-sort') || self.options.ordering === false) return;
			th.classList.add('sorting');
			th.style.cursor = 'pointer';
			th.addEventListener('click', function () {
				if (self.sortColumn === index) {
					self.sortDir = self.sortDir === 'asc' ? 'desc' : 'asc';
				} else {
					self.sortColumn = index;
					self.sortDir = 'asc';
				}
				ths.forEach(function (t) { t.classList.remove('sorting_asc', 'sorting_desc'); t.classList.add('sorting'); });
				th.classList.remove('sorting');
				th.classList.add(self.sortDir === 'asc' ? 'sorting_asc' : 'sorting_desc');
				self.render();
			});
		});
	};

	VanillaDataTable.prototype._cellText = function (row, index) {
		var cell = row.children[index];
		return cell ? cell.textContent.trim() : '';
	};

	VanillaDataTable.prototype.render = function () {
		var self = this;
		var rows = this.rows;

		// Filter
		var visible = this.searchTerm
			? rows.filter(function (row) { return row.textContent.toLowerCase().indexOf(self.searchTerm) > -1; })
			: rows.slice();

		// Sort
		if (this.sortColumn > -1) {
			var col = this.sortColumn;
			var dir = this.sortDir === 'asc' ? 1 : -1;
			visible.sort(function (a, b) {
				var av = self._cellText(a, col);
				var bv = self._cellText(b, col);
				var an = parseFloat(av.replace(/[^0-9.\-]/g, ''));
				var bn = parseFloat(bv.replace(/[^0-9.\-]/g, ''));
				if (!isNaN(an) && !isNaN(bn) && av.match(/[0-9]/) && bv.match(/[0-9]/)) {
					return (an - bn) * dir;
				}
				return av.localeCompare(bv) * dir;
			});
		}

		// Hide all rows first
		rows.forEach(function (row) { row.style.display = 'none'; });

		// Paginate
		var total = visible.length;
		var pageCount = Math.max(1, Math.ceil(total / this.pageLength));
		if (this.page >= pageCount) this.page = pageCount - 1;
		if (this.page < 0) this.page = 0;
		var start = this.page * this.pageLength;
		var end = Math.min(start + this.pageLength, total);
		var pageRows = visible.slice(start, end);

		// Re-append in sorted order, show current page
		pageRows.forEach(function (row) {
			self.tbody.appendChild(row);
			row.style.display = '';
		});
		visible.slice(0, start).concat(visible.slice(end)).forEach(function (row) {
			self.tbody.appendChild(row);
		});

		// Info
		this.infoWrap.textContent = total === 0
			? '0 - 0 of 0 items'
			: this.language.info
				.replace('_START_', String(start + 1))
				.replace('_END_', String(end))
				.replace('_TOTAL_', String(total));

		this._renderPagination(pageCount);
	};

	VanillaDataTable.prototype._renderPagination = function (pageCount) {
		var self = this;
		this.paginateList.innerHTML = '';

		function makeItem(label, page, opts) {
			opts = opts || {};
			var li = document.createElement('li');
			li.className = 'page-item' + (opts.className ? ' ' + opts.className : '');
			if (opts.disabled) li.classList.add('disabled');
			if (opts.active) li.classList.add('active');
			var a = document.createElement('a');
			a.className = 'page-link';
			a.href = 'javascript:void(0);';
			a.innerHTML = label;
			if (!opts.disabled && !opts.active) {
				a.addEventListener('click', function () { self.page = page; self.render(); });
			}
			li.appendChild(a);
			return li;
		}

		this.paginateList.appendChild(makeItem(this.language.paginate.previous, this.page - 1, {
			className: 'previous', disabled: this.page <= 0
		}));

		for (var i = 0; i < pageCount; i++) {
			this.paginateList.appendChild(makeItem(String(i + 1), i, { active: i === this.page }));
		}

		this.paginateList.appendChild(makeItem(this.language.paginate.next, this.page + 1, {
			className: 'next', disabled: this.page >= pageCount - 1
		}));
	};

	global.VanillaDataTable = VanillaDataTable;
})(window);
