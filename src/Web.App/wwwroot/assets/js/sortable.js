/*
 * Minimal HTML5 drag & drop sortable, replacing jQuery UI's .sortable().
 * Supports the subset used by the Kanban board: dragging items (the direct
 * children of each container) between any container passed in, restricting
 * the drag-initiation point to a handle element (mirrors jQuery UI's
 * "handle" option), and showing a placeholder at the drop position while
 * dragging (mirrors jQuery UI's "placeholder" option).
 */
(function (global) {
	'use strict';

	function makeSortable(containers, options) {
		options = options || {};
		var handleSelector = options.handle;
		var placeholderClass = options.placeholder || 'drag-placeholder';
		var dragEl = null;
		var placeholder = document.createElement('div');
		placeholder.className = placeholderClass;

		containers.forEach(function (container) {
			Array.prototype.forEach.call(container.children, function (item) {
				bindItem(item);
			});

			container.addEventListener('dragover', function (e) {
				if (!dragEl) return;
				e.preventDefault();
				var afterEl = getDragAfterElement(container, e.clientY);
				if (afterEl == null) {
					container.appendChild(placeholder);
				} else {
					container.insertBefore(placeholder, afterEl);
				}
			});

			container.addEventListener('drop', function (e) {
				if (!dragEl) return;
				e.preventDefault();
				if (placeholder.parentNode) placeholder.parentNode.insertBefore(dragEl, placeholder);
				placeholder.remove();
			});
		});

		function bindItem(item) {
			item.setAttribute('draggable', 'true');

			item.addEventListener('dragstart', function (e) {
				if (handleSelector && !e.target.closest(handleSelector)) {
					e.preventDefault();
					return;
				}
				dragEl = item;
				e.dataTransfer.effectAllowed = 'move';
				window.setTimeout(function () { item.classList.add('dragging'); }, 0);
			});

			item.addEventListener('dragend', function () {
				item.classList.remove('dragging');
				placeholder.remove();
				dragEl = null;
			});
		}

		function getDragAfterElement(container, y) {
			var items = Array.prototype.filter.call(container.children, function (child) {
				return child !== placeholder && !child.classList.contains('dragging');
			});
			return items.reduce(function (closest, child) {
				var box = child.getBoundingClientRect();
				var offset = y - box.top - box.height / 2;
				if (offset < 0 && offset > closest.offset) {
					return { offset: offset, element: child };
				}
				return closest;
			}, { offset: Number.NEGATIVE_INFINITY, element: null }).element;
		}
	}

	global.makeSortable = makeSortable;
})(window);
