/*
Author       : Dreamguys
Template Name: POS - Bootstrap Admin Template
*/

// ---- Small vanilla helpers (replace jQuery utility usage) ----
function qs(sel, ctx) { return (ctx || document).querySelector(sel); }
function qsa(sel, ctx) { return Array.from((ctx || document).querySelectorAll(sel)); }
function on(el, evt, handler) { if (el) el.addEventListener(evt, handler); }
function delegate(root, evt, selector, handler) {
	root.addEventListener(evt, function (e) {
		var target = e.target.closest(selector);
		if (target && root.contains(target)) handler.call(target, e);
	});
}
// Cancel any pending slideUp/slideDown cleanup timeout already scheduled for
// this element - without this, calling slideDown() right after slideUp() on
// the same element (e.g. opening a submenu that a "close all siblings" sweep
// just closed) lets the stale slideUp timeout fire later and hide it again.
// jQuery's animation queue handles this automatically; setTimeout doesn't.
function cancelPendingSlide(el) {
	if (el._slideTimeout) {
		window.clearTimeout(el._slideTimeout);
		el._slideTimeout = null;
	}
}
function slideUp(el, duration) {
	if (!el) return;
	duration = duration || 300;
	cancelPendingSlide(el);
	el.style.height = el.offsetHeight + 'px';
	el.style.transitionProperty = 'height, margin, padding';
	el.style.transitionDuration = duration + 'ms';
	el.offsetHeight;
	el.style.overflow = 'hidden';
	el.style.height = 0;
	el.style.paddingTop = 0;
	el.style.paddingBottom = 0;
	el.style.marginTop = 0;
	el.style.marginBottom = 0;
	el._slideTimeout = window.setTimeout(function () {
		el._slideTimeout = null;
		el.style.display = 'none';
		el.style.removeProperty('height');
		el.style.removeProperty('padding-top');
		el.style.removeProperty('padding-bottom');
		el.style.removeProperty('margin-top');
		el.style.removeProperty('margin-bottom');
		el.style.removeProperty('overflow');
		el.style.removeProperty('transition-duration');
		el.style.removeProperty('transition-property');
	}, duration);
}
function slideDown(el, duration) {
	if (!el) return;
	duration = duration || 300;
	cancelPendingSlide(el);
	el.style.removeProperty('display');
	var display = window.getComputedStyle(el).display;
	if (display === 'none') display = 'block';
	el.style.display = display;
	var height = el.offsetHeight;
	el.style.overflow = 'hidden';
	el.style.height = 0;
	el.style.paddingTop = 0;
	el.style.paddingBottom = 0;
	el.style.marginTop = 0;
	el.style.marginBottom = 0;
	el.offsetHeight;
	el.style.transitionProperty = 'height, margin, padding';
	el.style.transitionDuration = duration + 'ms';
	el.style.height = height + 'px';
	el.style.removeProperty('padding-top');
	el.style.removeProperty('padding-bottom');
	el.style.removeProperty('margin-top');
	el.style.removeProperty('margin-bottom');
	el._slideTimeout = window.setTimeout(function () {
		el._slideTimeout = null;
		el.style.removeProperty('height');
		el.style.removeProperty('overflow');
		el.style.removeProperty('transition-duration');
		el.style.removeProperty('transition-property');
	}, duration);
}
function slideToggle(el, duration) {
	if (!el) return;
	if (window.getComputedStyle(el).display === 'none') slideDown(el, duration);
	else slideUp(el, duration);
}
function initTomSelects(selector) {
	if (!window.TomSelect) return;
	document.querySelectorAll(selector).forEach(function (el) {
		if (el.tomselect) return;
		try {
			new TomSelect(el, { controlInput: null });
		} catch (err) {
			console.error('TomSelect init failed for', el, err);
		}
	});
}

// Convert an Owl Carousel-style `responsive` map ({0:{items:1},768:{items:2}})
// into Slick's `responsive` array ({breakpoint,settings}) - the two libraries
// apply breakpoints in opposite directions (Owl: width >= key; Slick: width <= breakpoint).
function owlToSlickResponsive(responsive) {
	if (!responsive) return undefined;
	var keys = Object.keys(responsive).map(Number).sort(function (a, b) { return a - b; });
	var out = [];
	for (var i = 0; i < keys.length - 1; i++) {
		out.push({
			breakpoint: keys[i + 1] - 1,
			settings: { slidesToShow: responsive[keys[i]].items, slidesToScroll: 1 }
		});
	}
	return out;
}

// Builds a Slick config from the same option shape script.js used for Owl Carousel,
// including a `.slick-arrow-wrap` container around the arrows so the existing
// theme CSS (ported from .owl-nav) keeps applying unchanged.
function initSlick(selector, options) {
	if (!window.jQuery || !jQuery.fn.slick) return null;
	var $el = jQuery(selector);
	if (!$el.length) return null;
	// snapshot so a mid-init failure (Slick partially wraps children, then
	// throws) can be undone instead of leaving the container empty
	var originalHtml = $el.html();
	try {
		return initSlickUnsafe($el, options || {});
	} catch (err) {
		console.error('Slick init failed for', selector, err);
		try { $el.slick('unslick'); } catch (e2) { /* wasn't far enough along to need unslick */ }
		$el.html(originalHtml);
		return null;
	}
}

function initSlickUnsafe($el, options) {
	// variableWidth sizes each slide to its own content, so a responsive
	// array driving slidesToShow doesn't apply the same way and can make
	// Slick misbehave (or throw) on breakpoint changes - skip it in that mode,
	// matching how Owl's autoWidth also effectively ignored items/responsive counts
	var responsive = options.autoWidth ? undefined : owlToSlickResponsive(options.responsive);
	var keys = options.responsive ? Object.keys(options.responsive).map(Number).sort(function (a, b) { return a - b; }) : [];
	var baseItems = keys.length ? options.responsive[keys[keys.length - 1]].items : options.items;

	var navText = options.navText || ['<i class="fas fa-chevron-left"></i>', '<i class="fas fa-chevron-right"></i>'];
	var slickOptions = {
		// Slick only supports variableWidth combined with slidesToShow:1 -
		// with a higher slidesToShow it miscalculates and breaks rendering
		slidesToShow: options.autoWidth ? 1 : (baseItems || 1),
		slidesToScroll: 1,
		infinite: !!options.loop,
		dots: !!options.dots,
		arrows: !!options.nav,
		autoplay: !!options.autoplay,
		autoplaySpeed: options.autoplayTimeout || 5000,
		speed: options.smartSpeed || 300,
		variableWidth: !!options.autoWidth,
		prevArrow: '<button type="button" class="slick-prev">' + navText[0] + '</button>',
		nextArrow: '<button type="button" class="slick-next">' + navText[1] + '</button>',
		responsive: responsive
	};

	var navWrap = null;
	var navContainerEl = null;
	if (options.nav) {
		// kept detached until after .slick() runs, otherwise Slick would count
		// it as an extra slide (it treats every child of $el as a slide)
		navWrap = jQuery('<div class="slick-arrow-wrap"></div>');
		if (options.navContainer) {
			navContainerEl = jQuery(options.navContainer);
			if (!navContainerEl.length) navContainerEl = null;
		}
		slickOptions.appendArrows = navWrap;
	}

	// Slick has no native per-slide gutter option (Owl's `margin`) - apply the
	// standard negative-margin-on-list / margin-on-slide technique instead.
	// Must be set on the raw children BEFORE .slick() runs: Slick measures
	// each slide's margin-inclusive width during init to size the track, so
	// applying the margin afterward invalidates that math and makes the last
	// slide(s) overflow the track and wrap onto a new line.
	if (options.margin) {
		var half = options.margin / 2;
		$el.children().css('margin', '0 ' + half + 'px');
	}

	$el.slick(slickOptions);

	if (options.margin) {
		// cosmetic edge-alignment only (doesn't feed into Slick's own slide-width
		// math), safe to apply to .slick-list after init
		$el.find('.slick-list').css('margin', '0 -' + half + 'px');
	}

	if (navWrap) {
		// child of the slider container (matches Owl's .owl-carousel > .owl-nav
		// structure) so the ported position:absolute CSS stays relative to it
		if (navContainerEl) navContainerEl.append(navWrap);
		else $el.append(navWrap);
	}

	return $el;
}

document.addEventListener('DOMContentLoaded', function () {

	var wrapper = qs('.main-wrapper');
	var slimScrolls = qsa('.slimscroll');
	if (window.feather) feather.replace();

	// Page Content Height Resize
	on(window, 'resize', function () {
		var pageWrapper = qs('.page-wrapper');
		if (pageWrapper) {
			pageWrapper.style.minHeight = window.innerHeight + 'px';
		}
	});

	// Mobile menu sidebar overlay
	var overlay = document.createElement('div');
	overlay.className = 'sidebar-overlay';
	document.body.appendChild(overlay);

	on(document.getElementById('mobile_btn'), 'click', function () {
		wrapper.classList.toggle('slide-nav');
		overlay.classList.toggle('opened');
		document.documentElement.classList.add('menu-opened');
		var taskWindow = document.getElementById('task_window');
		if (taskWindow) taskWindow.classList.remove('opened');
		return false;
	});

	on(overlay, 'click', function () {
		document.documentElement.classList.remove('menu-opened');
		overlay.classList.remove('opened');
		wrapper.classList.remove('slide-nav');
		var taskWindow = document.getElementById('task_window');
		if (taskWindow) taskWindow.classList.remove('opened');
	});

	// Logo Hide Btn
	delegate(document, 'click', '.hideset', function () {
		var target = this.parentElement && this.parentElement.parentElement && this.parentElement.parentElement.parentElement;
		if (target) target.style.display = 'none';
	});

	delegate(document, 'click', '.delete-set', function () {
		var target = this.parentElement && this.parentElement.parentElement;
		if (target) target.style.display = 'none';
	});

	// Stick Sidebar - now pure CSS (position: sticky) in _common.scss, no JS needed

	// Slick Slider
	if (qs('.product-slide')) {
		var $owl = initSlick('.product-slide', {
			items: 1, margin: 30, dots: false, nav: false, loop: true,
			responsive: { 0: { items: 1 }, 800: { items: 1 }, 1170: { items: 1 } }
		});
		on(qs('.product-prev'), 'click', function () { if ($owl) $owl.slick('slickPrev'); });
		on(qs('.product-next'), 'click', function () { if ($owl) $owl.slick('slickNext'); });
	}

	if (qs('.notes-slider')) {
		new Swiper('.notes-slider', {
			loop: true,
			spaceBetween: 24,
			slidesPerView: 1,
			autoplay: {
				delay: 2000,
				disableOnInteraction: false,
			},
			navigation: {
				nextEl: '.notes-slider-next',
				prevEl: '.notes-slider-prev',
			},
			breakpoints: {
				0: {
					slidesPerView: 1,
				},
				768: {
					slidesPerView: 2,
				},
				1300: {
					slidesPerView: 3,
				}
			}
		});
	}

	// Table Responsive
	window.setTimeout(function () {
		qsa('.table').forEach(function (table) {
			if (table.parentElement) table.parentElement.classList.add('table-responsive');
		});
	}, 1000);

	// Datatable (now datatable.js, no jQuery)
	if (qs('.datatable') && window.VanillaDataTable) {
		qsa('.datatable').forEach(function (table) {
			new VanillaDataTable(table, {
				ordering: true,
				language: {
					searchPlaceholder: "Search",
					sLengthMenu: 'Row Per Page _MENU_ Entries',
					info: "_START_ - _END_ of _TOTAL_ items",
					paginate: {
						next: ' <i class=" fa fa-angle-right"></i>',
						previous: '<i class="fa fa-angle-left"></i> '
					},
				},
				initComplete: function (dt) {
					var instance = dt || this;
					var filter = (instance && instance.filterWrap) ? instance.filterWrap : qs('.dataTables_filter');
					var tableEl = instance && instance.table;
					var container = tableEl ? (tableEl.closest('.card') || tableEl.closest('.tab-pane') || tableEl.closest('.page-wrapper') || document) : document;
					var tableSearch = container.querySelector('#tableSearch') || (container !== document ? document.getElementById('tableSearch') : null);
					var searchInput = container.querySelector('.search-input');
					if (filter && tableSearch) tableSearch.appendChild(filter);
					else if (filter && searchInput) searchInput.appendChild(filter);
				},
			});
		});
	}

	// image file upload image
	function readURL(input) {
		if (input.files && input.files[0]) {
			var reader = new FileReader();
			reader.onload = function (e) {
				var blah = document.getElementById('blah');
				if (blah) blah.setAttribute('src', e.target.result);
			};
			reader.readAsDataURL(input.files[0]);
		}
	}
	on(document.getElementById('imgInp'), 'change', function () { readURL(this); });

	// Loader
	window.setTimeout(function () {
		window.setTimeout(function () {
			var loader = document.getElementById('global-loader');
			if (loader) {
				loader.style.transition = 'opacity 400ms';
				loader.style.opacity = 0;
				window.setTimeout(function () { loader.style.display = 'none'; }, 400);
			}
		}, 100);
	}, 500);

	// Datetimepicker (now flatpickr, no jQuery)
	if (qs('.datetimepicker') && window.flatpickr) {
		qsa('.datetimepicker').forEach(function (el) { flatpickr(el, { dateFormat: 'd-m-Y' }); });
	}

	// toggle-password
	function bindTogglePassword(triggerSelector, inputSelector, classA, classB) {
		if (!qs(triggerSelector)) return;
		delegate(document, 'click', triggerSelector, function () {
			this.classList.toggle(classA);
			this.classList.toggle(classB);
			var input = qs(inputSelector);
			if (!input) return;
			input.setAttribute('type', input.getAttribute('type') === 'password' ? 'text' : 'password');
		});
	}
	bindTogglePassword('.toggle-password', '.pass-input', 'fa-eye', 'fa-eye-slash');
	bindTogglePassword('.toggle-passwords', '.pass-inputs', 'fa-eye', 'fa-eye-slash');
	bindTogglePassword('.toggle-passworda', '.pass-inputa', 'fa-eye', 'fa-eye-slash');

	// Settings Toggle Password
	bindTogglePassword('.toggle-password', '.settings-pass-input', 'ti-eye', 'ti-eye-off');
	bindTogglePassword('.toggle-passwords', '.settings-pass-inputs', 'ti-eye', 'ti-eye-off');
	bindTogglePassword('.toggle-passworda', '.settings-pass-inputa', 'ti-eye', 'ti-eye-off');

	// Coming Soon
	if (qs('.comming-soon-pg')) {
		var day = qs('.days');
		var hour = qs('.hours');
		var minute = qs('.minutes');
		var second = qs('.seconds');

		(function setCountdown() {
			var countdownDate = new Date('oct 22, 2026 16:00:00').getTime();
			var updateCount = window.setInterval(function () {
				var todayDate = new Date().getTime();
				var distance = countdownDate - todayDate;
				var days = Math.floor(distance / (1000 * 60 * 60 * 24));
				var hours = Math.floor(distance % (1000 * 60 * 60 * 24) / (1000 * 60 * 60));
				var minutes = Math.floor(distance % (1000 * 60 * 60) / (1000 * 60));
				var seconds = Math.floor(distance % (1000 * 60) / 1000);
				if (day) day.textContent = days;
				if (hour) hour.textContent = hours;
				if (minute) minute.textContent = minutes;
				if (second) second.textContent = seconds;
				if (distance < 0) {
					window.clearInterval(updateCount);
					var pg = qs('.comming-soon-pg');
					if (pg) pg.innerHTML = '<h1>EXPIRED</h1>';
				}
			}, 1000);
		})();
	}

	// Select (Tom Select)
	initTomSelects('.select');

	// editor
	if (qs('.editor') && window.Quill) {
		qsa('.editor').forEach(function (editor) { new Quill(editor, { theme: 'snow' }); });
	}
	if (qs('.editor2') && window.Quill) {
		qsa('.editor2').forEach(function (editor) { new Quill(editor, { theme: 'snow' }); });
	}

	// Sidebar Slimscroll (now SimpleBar, no jQuery)
	if (slimScrolls.length > 0 && window.SimpleBar) {
		var slimInstances = slimScrolls.map(function (el) {
			// keep the slimScrollDiv class so the existing layout-variant CSS
			// (_theme.scss, _darktheme.scss, etc.) that targets .sidebar
			// .slimScrollDiv for height/position keeps applying unchanged
			el.classList.add('slimScrollDiv');
			return new SimpleBar(el);
		});
		var setSlimHeight = function () {
			var h = (window.innerHeight - 60) + 'px';
			slimInstances.forEach(function (inst) {
				inst.el.style.height = h;
				inst.recalculate();
			});
		};
		setSlimHeight();
		on(window, 'resize', setSlimHeight);
	}

	// This template marks the current page's sidebar entry two different ways
	// depending on which page was exported: some put class="active" directly
	// on the <a>, others put it on the parent <li> instead (the <a> itself is
	// plain). Resolve either convention to "the active <a>", preferring the
	// most deeply nested match if a page happens to have candidates at
	// multiple levels (e.g. a stray "active" on a section wrapper).
	function findActiveLink(container) {
		var candidates = qsa('ul a.active, ul li.active', container).map(function (el) {
			return el.tagName === 'A' ? el : el.querySelector(':scope > a');
		}).filter(Boolean);
		if (!candidates.length) return null;
		var depth = function (el) {
			var d = 0;
			for (var p = el; p; p = p.parentElement) d++;
			return d;
		};
		return candidates.reduce(function (deepest, el) {
			return depth(el) > depth(deepest) ? el : deepest;
		});
	}

	// Smooth-scrolls the nearest scrollable ancestor (SimpleBar's internal
	// viewport, or a plain overflow container) so linkEl lands centered in
	// it, but only when it isn't already fully visible - block:'center'
	// alone would re-center on every call even when nothing is hidden.
	function scrollSidebarToItem(linkEl) {
		if (!linkEl || typeof linkEl.scrollIntoView !== 'function') return;
		try {
			var scrollEl = linkEl.closest('.slimscroll, .sidebar-right');
			if (scrollEl) {
				var containerRect = scrollEl.getBoundingClientRect();
				var itemRect = linkEl.getBoundingClientRect();
				var alreadyVisible = itemRect.top >= containerRect.top && itemRect.bottom <= containerRect.bottom;
				if (alreadyVisible) return;
			}
			linkEl.scrollIntoView({ behavior: 'smooth', block: 'center', inline: 'nearest' });
		} catch (err) {
			// no-op: never let sidebar scroll-into-view break menu interaction
		}
	}

	// Sidebar submenu toggle
	function initSidebarMenu() {
		delegate(document, 'click', '.sidebar-menu a', function (e) {
			var nextEl = this.nextElementSibling;
			if (nextEl && nextEl.tagName === 'UL') e.preventDefault();

			if (!this.classList.contains('subdrop')) {
				var parentUl = this.closest('ul');
				var nextUl = this.nextElementSibling;
				if (parentUl) {
					qsa('ul', parentUl).forEach(function (ul) { if (ul !== nextUl) slideUp(ul, 250); });
					qsa('a', parentUl).forEach(function (a) { if (a !== this) a.classList.remove('subdrop'); }, this);
				}
				if (nextUl && nextUl.tagName === 'UL') slideDown(nextUl, 350);
				this.classList.add('subdrop');
				var clickedLink = this;
				window.setTimeout(function () { scrollSidebarToItem(clickedLink); }, 350);
			} else {
				this.classList.remove('subdrop');
				var nextUl2 = this.nextElementSibling;
				if (nextUl2 && nextUl2.tagName === 'UL') slideUp(nextUl2, 350);
			}
		});
		// A page can contain more than one .sidebar-menu block (this vertical
		// layout, the horizontal layout, an in-page settings nav, ...) with
		// only one actually shown at a time via CSS. Only act on the one
		// that's really on screen, and expand every ancestor submenu level
		// (not just the top one) so items nested 2+ levels deep are reachable.
		qsa('.sidebar-menu').forEach(function (container) {
			if (container.offsetParent === null) return;
			var activeSub = findActiveLink(container);
			if (!activeSub) return;

			var li = activeSub.closest('li');
			while (li) {
				// direct-child <a> only (matches jQuery's .children('a:first')) -
				// section header <li> elements (e.g. "Main") have no direct <a>
				// child, only an <h6> + nested <ul>, so this intentionally no-ops there
				var a = li.querySelector(':scope > a');
				if (a) a.classList.add('active', 'subdrop');
				var ul = li.closest('ul');
				if (ul) slideDown(ul, 350);
				li = ul ? ul.closest('li') : null;
			}
			window.setTimeout(function () { scrollSidebarToItem(activeSub); }, 400);
		});
	}

	// Sidebar right (collapsible column sidebar)
	function initColSidebarMenu() {
		var root = qs('.sidebar-right');
		if (!root) return;
		delegate(root, 'click', 'ul a', function (e) {
			var li = this.closest('li');
			if (li && li.classList.contains('submenu')) e.preventDefault();

			if (!this.classList.contains('subdrop')) {
				var ul = this.closest('ul');
				var next = this.nextElementSibling;
				if (ul) {
					qsa('ul', ul).forEach(function (u) { if (u !== next) slideUp(u, 250); });
					qsa('a', ul).forEach(function (a) { if (a !== this) a.classList.remove('subdrop'); }, this);
				}
				if (next && next.tagName === 'UL') slideDown(next, 350);
				this.classList.add('subdrop');
				var clickedLink = this;
				window.setTimeout(function () { scrollSidebarToItem(clickedLink); }, 350);
			} else {
				this.classList.remove('subdrop');
				var next2 = this.nextElementSibling;
				if (next2 && next2.tagName === 'UL') slideUp(next2, 350);
			}
		});

		var activeItem = findActiveLink(root);
		var activeItems = activeItem ? [activeItem] : [];
		activeItems.forEach(function (item) {
			window.setTimeout(function () { scrollSidebarToItem(item); }, 400);
			var li = item.closest('li');
			while (li) {
				var a = li.querySelector(':scope > a');
				if (a) a.classList.add('active', 'subdrop');
				var ul = li.closest('ul');
				if (ul) slideDown(ul, 350);
				li = ul ? ul.closest('li') : null;
			}
		});
	}

	initColSidebarMenu();
	initSidebarMenu();

	on(document, 'mouseover', function (e) {
		e.stopPropagation();
		var body = document.body;
		var toggleBtn = document.getElementById('toggle_btn');
		var isMini = body.classList.contains('mini-sidebar');
		if (!isMini) return;
		var toggleVisible = toggleBtn && toggleBtn.offsetParent !== null;

		if (toggleVisible) {
			var targ = e.target.closest('.sidebar, .header-left');
			if (targ) {
				body.classList.add('expand-menu');
				qsa('.subdrop').forEach(function (el) { if (el.nextElementSibling) slideDown(el.nextElementSibling); });
			} else {
				body.classList.remove('expand-menu');
				qsa('.subdrop').forEach(function (el) { if (el.nextElementSibling) slideUp(el.nextElementSibling); });
			}
			return false;
		}

		var targ2 = e.target.closest('.sidebar, .header-left');
		if (targ2) {
			var boxBody = qs('body.layout-box-mode');
			if (boxBody) boxBody.classList.add('expand-menu');
			qsa('.subdrop').forEach(function (el) { if (el.nextElementSibling) slideDown(el.nextElementSibling); });
		} else {
			body.classList.remove('expand-menu');
			qsa('.subdrop').forEach(function (el) { if (el.nextElementSibling) slideUp(el.nextElementSibling); });
		}
		return false;
	});

	// Date Range Picker (now daterangepicker.js, built on flatpickr, no jQuery)
	if (qs('.bookingrange') && window.vanillaDateRangePicker) {
		qsa('.bookingrange').forEach(function (el) { vanillaDateRangePicker(el); });
	}

	// toggle_btn
	on(document.getElementById('toggle_btn'), 'click', function (e) {
		var body = document.body;
		var sidebarLogo = qs('.sidebar-logo');
		if (body.classList.contains('mini-sidebar')) {
			body.classList.remove('mini-sidebar');
			this.classList.add('active');
			localStorage.setItem('screenModeNightTokenState', 'night');
			window.setTimeout(function () {
				body.classList.remove('mini-sidebar');
				if (sidebarLogo) sidebarLogo.classList.add('active');
			}, 100);
		} else {
			body.classList.add('mini-sidebar');
			this.classList.remove('active');
			localStorage.removeItem('screenModeNightTokenState');
			window.setTimeout(function () {
				body.classList.add('mini-sidebar');
				if (sidebarLogo) sidebarLogo.classList.remove('active');
			}, 100);
		}
		return false;
	});

	on(qs('.submenus'), 'click', function () { document.body.classList.add('sidebarrightmenu'); });
	on(document.getElementById('searchdiv'), 'click', function () { var el = qs('.searchinputs'); if (el) el.classList.add('show'); });
	on(qs('.search-addon span'), 'click', function () { var el = qs('.searchinputs'); if (el) el.classList.remove('show'); });

	delegate(document, 'click', '.productset', function () { this.classList.toggle('active'); });

	delegate(document, 'click', '.product-info', function () {
		this.classList.toggle('active');
		var emptyCart = qs('.product-wrap .empty-cart');
		var productList = qs('.product-wrap .product-list');
		if (qs('.product-info.active')) {
			if (emptyCart) emptyCart.style.display = 'none';
			if (productList) productList.style.display = '';
		} else {
			if (emptyCart) emptyCart.style.display = 'flex';
			if (productList) productList.style.display = 'none';
		}
	});

	delegate(document, 'click', '.layout-box', function () {
		var el = qs('.layout-hide-box');
		if (el) el.classList.toggle('layout-show-box');
	});

	delegate(document, 'click', '.select-option1', function () {
		var el = qs('.select-color-add');
		if (el) el.classList.add('selected-color-add');
	});

	function bindActiveGroup(selector) {
		qsa(selector).forEach(function (el) {
			on(el, 'click', function () {
				qsa(selector).forEach(function (e) { e.classList.remove('active'); });
				el.classList.add('active');
			});
		});
	}
	bindActiveGroup('.bank-box');
	bindActiveGroup('.theme-image');
	bindActiveGroup('.themecolorset');

	// Increment Decrement value
	delegate(document, 'click', '.inc.button', function () {
		var input = this.previousElementSibling;
		if (!input) return;
		var newValue = parseInt(input.value) + 1;
		var parent = input.closest('div');
		if (parent) {
			var incs = parent.querySelectorAll('.inc');
			incs.forEach(function (i) { i.classList.add('a' + newValue); });
		}
		input.value = newValue;
	});
	delegate(document, 'click', '.dec.button', function () {
		var input = this.nextElementSibling;
		if (!input) return;
		var newValue = parseInt(input.value) - 1;
		var parent = input.closest('div');
		if (parent) {
			var incs = parent.querySelectorAll('.inc');
			incs.forEach(function (i) { i.classList.add('a' + newValue); });
		}
		input.value = newValue;
	});

	if (qs('.custom-file-container') && window.FileUploadWithPreview) {
		new FileUploadWithPreview('myFirstImage');
		new FileUploadWithPreview('mySecondImage');
	}

	qsa('.counters').forEach(function (counterEl) {
		var countTo = parseFloat(counterEl.getAttribute('data-count'));
		var countFrom = parseFloat(counterEl.textContent) || 0;
		var duration = 2000;
		var startTime = null;
		function step(timestamp) {
			if (!startTime) startTime = timestamp;
			var progress = Math.min((timestamp - startTime) / duration, 1);
			counterEl.textContent = Math.floor(countFrom + (countTo - countFrom) * progress);
			if (progress < 1) window.requestAnimationFrame(step);
			else counterEl.textContent = countTo;
		}
		window.requestAnimationFrame(step);
	});

	if (qs('.select-color-add')) {
		var colorSelect = document.getElementById('colorSelect');
		var inputBox = document.getElementById('inputBox');
		var inputShow = document.getElementById('input-show');
		var variantTable = document.getElementById('variant-table');
		if (colorSelect) {
			colorSelect.addEventListener('change', function () {
				var selectedValue = colorSelect.value;
				if (inputShow) inputShow.style.display = 'block';
				if (variantTable) variantTable.style.display = 'block';
				if (inputBox) inputBox.value = selectedValue;
			});
		}
	}

	on(qs('.win-maximize'), 'click', function (e) {
		if (!document.fullscreenElement) {
			document.documentElement.requestFullscreen();
		} else if (document.exitFullscreen) {
			document.exitFullscreen();
		}
	});

	on(document.getElementById('check_all'), 'click', function () {
		qsa('.checkmail').forEach(function (el) { el.click(); });
		return false;
	});
	qsa('.checkmail').forEach(function (el) {
		on(el, 'click', function () {
			var tr = this.closest('tr');
			if (tr) tr.classList.toggle('checked');
		});
	});

	// Popover
	if (window.bootstrap) {
		qsa('[data-bs-toggle="popover"]').forEach(function (el) { new bootstrap.Popover(el); });
	}
	
	// Clipboard
	if (qs('.clipboard') && typeof ClipboardJS !== 'undefined') {
		var clipboard = new ClipboardJS('.btn, .clip-btn, .btn-clipboard');
		clipboard.on('success', function (e) {
			console.log('Copied text:', e.text);
			e.clearSelection();
		});
	}


	// Chat
	var chatAppTarget = qs('.chat-window');
	if (chatAppTarget) {
		if (window.innerWidth > 991) chatAppTarget.classList.remove('chat-slide');

		delegate(document, 'click', '.chat-window .chat-users-list a.media', function () {
			if (window.innerWidth <= 991) chatAppTarget.classList.add('chat-slide');
			return false;
		});
		delegate(document, 'click', '#back_user_list', function () {
			if (window.innerWidth <= 991) chatAppTarget.classList.remove('chat-slide');
			return false;
		});
	}

	// Mail important
	delegate(document, 'click', '.mail-important', function () {
		var icon = this.querySelector('i.fa');
		if (icon) { icon.classList.toggle('fa-star'); icon.classList.toggle('fa-star-o'); }
	});

	function bindSelectAll(selector) {
		var el = qs(selector);
		on(el, 'click', function () {
			qsa(':checkbox').forEach(function (cb) { cb.checked = this.checked; }, this);
		});
	}
	bindSelectAll('#select-all');
	bindSelectAll('#select-all2');
	bindSelectAll('.select-all');

	// Tooltip
	if (window.bootstrap) {
		qsa('[data-bs-toggle="tooltip"]').forEach(function (el) { new bootstrap.Tooltip(el); });
	}

	// Sidebar Visible
	on(qs('.open-layout'), 'click', function (s) {
		s.preventDefault();
		var layout = qs('.sidebar-layout');
		var settings = qs('.sidebar-settings');
		if (layout) layout.classList.add('show-layout');
		if (settings) settings.classList.remove('show-settings');
	});
	on(qs('.open-settings'), 'click', function (s) {
		s.preventDefault();
		var settings = qs('.sidebar-settings');
		var layout = qs('.sidebar-layout');
		if (settings) settings.classList.add('show-settings');
		if (layout) layout.classList.remove('show-layout');
	});
	qsa('.btn-closed').forEach(function (btn) {
		on(btn, 'click', function (s) {
			s.preventDefault();
			var layout = qs('.sidebar-layout');
			var settings = qs('.sidebar-settings');
			var sidebar = qs('.siderbar-view');
			if (layout) layout.classList.remove('show-layout');
			if (settings) settings.classList.remove('show-settings');
			if (sidebar) sidebar.classList.remove('show-sidebar');
		});
	});
	on(qs('.open-siderbar'), 'click', function (s) {
		s.preventDefault();
		var el = qs('.siderbar-view');
		if (el) el.classList.add('show-sidebar');
	});

	if (qs('.toggle-switch')) {
		var toggleSwitch = qs('.toggle-switch input[type="checkbox"]');
		var currentTheme = localStorage.getItem('theme');
		var app = document.body;

		if (currentTheme) {
			app.setAttribute('data-theme', currentTheme);
			if (currentTheme === 'dark' && toggleSwitch) toggleSwitch.checked = true;
		}

		function switchTheme(e) {
			if (e.target.checked) {
				app.setAttribute('data-theme', 'dark');
				localStorage.setItem('theme', 'dark');
			} else {
				app.setAttribute('data-theme', 'light');
				localStorage.setItem('theme', 'light');
			}
		}

		on(toggleSwitch, 'change', switchTheme);
	}

	if (window.location.hash === '#LightMode') {
		localStorage.setItem('theme', 'dark');
	} else if (window.location.hash === '#DarkMode') {
		localStorage.setItem('theme', 'light');
	}

	qsa('ul.tabs li').forEach(function (li) {
		on(li, 'click', function () {
			var theTab = this.id;
			if (this.classList.contains('active')) return;
			var wrapper = this.closest('.tabs_wrapper');
			if (wrapper) {
				qsa('ul.tabs li, .tabs_container .tab_content', wrapper).forEach(function (el) { el.classList.remove('active'); });
			}
			qsa('.tabs_container .tab_content[data-tab="' + theTab + '"], ul.tabs li[id="' + theTab + '"]').forEach(function (el) {
				el.classList.add('active');
			});
		});
	});

	// Otp Verification
	qsa('.digit-group input').forEach(function (input) {
		input.setAttribute('maxlength', 1);
		on(input, 'keyup', function (e) {
			var parent = this.parentElement;
			if (e.keyCode === 8 || e.keyCode === 37) {
				var prevId = this.getAttribute('data-previous');
				var prev = prevId && parent ? parent.querySelector('#' + prevId) : null;
				if (prev) prev.select();
			} else if ((e.keyCode >= 48 && e.keyCode <= 57) || (e.keyCode >= 65 && e.keyCode <= 90) || (e.keyCode >= 96 && e.keyCode <= 105) || e.keyCode === 39) {
				var nextId = this.getAttribute('data-next');
				var next = nextId && parent ? parent.querySelector('#' + nextId) : null;
				if (next) {
					next.select();
				} else if (parent && parent.getAttribute('data-autosubmit') !== null && parent.tagName === 'FORM') {
					parent.submit();
				}
			}
			if (this.value !== '') this.classList.add('active');
			else this.classList.remove('active');
		});
	});

	function closeProfile() {
		var rightSide = qs('.right-side-contact');
		if (rightSide) { rightSide.classList.add('hide-right-sidebar'); rightSide.classList.remove('show-right-sidebar'); }
		if (window.innerWidth > 991 && window.innerWidth < 1201) {
			var chat = qs('.chat');
			if (chat) chat.style.marginLeft = '0';
		}
		if (window.innerWidth < 992) {
			var chat2 = qs('.chat');
			if (chat2) chat2.classList.remove('hide-chatbar');
		}
	}
	qsa('.close_profile').forEach(function (el) { on(el, 'click', closeProfile); });

	if (qs('.emoj-action')) {
		delegate(document, 'click', '.emoj-action', function () {
			var el = qs('.emoj-group-list');
			if (el) el.style.display = (el.style.display === 'none' || !el.style.display) ? 'block' : 'none';
		});
	}
	if (qs('.emoj-action-foot')) {
		delegate(document, 'click', '.emoj-action-foot', function () {
			var el = qs('.emoj-group-list-foot');
			if (el) el.style.display = (el.style.display === 'none' || !el.style.display) ? 'block' : 'none';
		});
	}

	if (qs('.custom-input')) {
		var inputRange = qs('.custom-input');
		on(inputRange, 'input', function () {
			var progress = (inputRange.value - inputRange.min) / (inputRange.max - inputRange.min) * 100;
			inputRange.style.background = 'linear-gradient(to top, var(--md-sys-color-on-surface-variant) 0%, var(--md-sys-color-on-surface-variant) ' + progress + '%, var(--md-sys-color-surface-variant) ' + progress + '%, var(--md-sys-color-surface-variant) 100%)';
		});
	}

	// Chat Resize
	qsa('.close_profile').forEach(function (el) {
		on(el, 'click', function () {
			var rus = qs('.right-user-side');
			var comman = qs('.chat-center-blk .card-comman');
			if (rus) rus.classList.remove('open-message');
			if (comman) comman.classList.add('chat-center-space');
		});
	});
	on(qs('.profile-open'), 'click', function () {
		var rus = qs('.right-user-side');
		var comman = qs('.chat-center-blk .card-comman');
		if (rus) rus.classList.remove('add-setting');
		if (comman) comman.classList.remove('chat-center-space');
	});

	// Call Resize
	qsa('.close_profile').forEach(function (el) {
		on(el, 'click', function () {
			var rus = qs('.right-user-side');
			var videoInner = qs('.video-screen-inner');
			var rightParty = qs('.right-side-party');
			var meetingList = qs('.meeting-list');
			var chatRoom = document.getElementById('chat-room');
			var callUserSide = qs('.call-user-side');
			if (rus) rus.classList.remove('open-message');
			if (videoInner) videoInner.classList.remove('video-space');
			if (rightParty) rightParty.classList.remove('open-message');
			if (meetingList) meetingList.classList.remove('add-meeting');
			if (chatRoom) chatRoom.classList.remove('open-chats');
			if (callUserSide) callUserSide.classList.add('add-setting');
		});
	});
	on(document.getElementById('add-partispant'), 'click', function () {
		var rightParty = qs('.right-side-party');
		var chatRoom = document.getElementById('chat-room');
		var meetingList = qs('.meeting-list');
		if (rightParty) rightParty.classList.add('open-message');
		if (chatRoom) chatRoom.classList.remove('open-chats');
		if (meetingList) meetingList.classList.add('add-meeting');
	});
	on(document.getElementById('show-message'), 'click', function () {
		var chatRoom = document.getElementById('chat-room');
		var rightParty = qs('.right-side-party');
		var meetingList = qs('.meeting-list');
		if (chatRoom) chatRoom.classList.add('open-chats');
		if (rightParty) rightParty.classList.remove('open-message');
		if (meetingList) meetingList.classList.add('add-meeting');
	});

	// Chat Search Visible
	on(qs('.chat-search-btn'), 'click', function () { var el = qs('.chat-search'); if (el) el.classList.add('visible-chat'); });
	on(qs('.close-btn-chat'), 'click', function () { var el = qs('.chat-search'); if (el) el.classList.remove('visible-chat'); });
	var chatSearchInput = qs('.chat-search .form-control');
	on(chatSearchInput, 'keyup', function () {
		var value = this.value.toLowerCase();
		qsa('.chat .chat-body .messages .chats').forEach(function (el) {
			el.style.display = el.textContent.toLowerCase().indexOf(value) > -1 ? '' : 'none';
		});
	});

	if (document.getElementById('collapse-header')) {
		document.getElementById('collapse-header').onclick = function () {
			this.classList.toggle('active');
			document.body.classList.toggle('header-collapse');
		};
	}

	on(document.getElementById('file-delete'), 'click', function () {
		var deletedTable = qs('.deleted-table');
		var deletedInfo = qs('.deleted-info');
		if (deletedTable) deletedTable.classList.add('d-none');
		if (deletedInfo) deletedInfo.classList.add('d-block');
	});

	if (qs('.pos-category')) {
		new Swiper('.pos-category', {
			loop: false,
			spaceBetween: 8,
			slidesPerView: 2,
			breakpoints: {
				0: { slidesPerView: 2 },
				500: { slidesPerView: 3 },
				768: { slidesPerView: 4 },
				991: { slidesPerView: 5 },
				1200: { slidesPerView: 6 }
			}
		});
	}
	if (qs('.product-slide')) {
		new Swiper('.product-slide', {
			loop: true,
			spaceBetween: 15,
			slidesPerView: 1,
			navigation: {
				nextEl: '.product-next',
				prevEl: '.product-prev',
			}
		});
	}
	if (qs('.channels-slider')) {
		new Swiper('.channels-slider', {
			loop: true,
			spaceBetween: 10,
			slidesPerView: 'auto',
			breakpoints: {
				0: { slidesPerView: 3 },
				576: { slidesPerView: 5 },
				768: { slidesPerView: 6 },
				992: { slidesPerView: 8 }
			}
		});
	}
	if (qs('.social-gallery-slider')) {
		new Swiper('.social-gallery-slider', {
			loop: true,
			spaceBetween: 10,
			slidesPerView: 2,
			breakpoints: {
				0: { slidesPerView: 2 },
				576: { slidesPerView: 3 },
				768: { slidesPerView: 4 }
			}
		});
	}
	if (qs('.folders-carousel')) {
		new Swiper('.folders-carousel', {
			loop: true,
			spaceBetween: 15,
			slidesPerView: 1,			
			navigation: {
				nextEl: '.folder-slider-next',
				prevEl: '.folder-slider-prev',
			},
			breakpoints: {
				0: { slidesPerView: 1 },
				768: { slidesPerView: 2 },
				1400: { slidesPerView: 3 }
			}
		});
	}

	if (qs('.files-carousel')) {
		new Swiper('.files-carousel', {
			loop: true,
			spaceBetween: 15,
			slidesPerView: 1,
			autoplay: {
				delay: 2000,
				disableOnInteraction: false
			},
			breakpoints: {
				0: { slidesPerView: 1 },
				768: { slidesPerView: 2 },
				1200: { slidesPerView: 3 }
			}
		});
	}

	if (qs('.video-section')) {
		new Swiper('.video-section', {
			loop: true,
			spaceBetween: 15,
			slidesPerView: 1,
			breakpoints: {
				0: { slidesPerView: 1 },
				768: { slidesPerView: 2 },
				1200: { slidesPerView: 3 }
			}
		});

		if (window.Plyr) {
			var playerSettings = {
				controls: ['play-large'], fullscreen: { enabled: false }, resetOnEnd: true,
				hideControls: true, clickToPlay: true, keyboard: false,
			};
			var players = Plyr.setup('.js-player', playerSettings);
			players.forEach(function (instance) {
				instance.on('play', function () {
					players.forEach(function (instance1) { if (instance !== instance1) instance1.pause(); });
				});
			});
		}
	}

	if (window.bootstrap) {
		qsa('.video-section, .files-carousel, .folders-carousel').forEach(function (carousel) {
			delegate(carousel, 'show.bs.dropdown', '[data-bs-toggle=dropdown]', function () {
				var dropdown = bootstrap.Dropdown.getInstance(this);
				var menu = this.nextElementSibling;
				if (menu && menu.classList.contains('dropdown-menu')) {
					carousel.insertAdjacentElement('afterend', menu);
				}
			});
		});
	}

	// Increment Decrement
	function updateValue(obj, delta) {
		var item = obj.parentElement ? obj.parentElement.querySelector('input') : null;
		if (!item) return;
		var newValue = parseInt(item.value, 10) + delta;
		item.value = Math.max(newValue, 0);
	}
	delegate(document, 'click', '.inc', function () { updateValue(this, 1); });
	delegate(document, 'click', '.dec', function () { updateValue(this, -1); });

	if (qs('.popup-toggle')) {
		delegate(document, 'click', '.popup-toggle', function () {
			var el = qs('.toggle-sidebar');
			if (el) el.classList.add('open-sidebar');
		});
		delegate(document, 'click', '.sidebar-closes', function () {
			var el = qs('.toggle-sidebar');
			if (el) el.classList.remove('open-sidebar');
		});
	}

	function hideModal(id) {
		var el = document.getElementById(id);
		if (!el || !window.bootstrap) return;
		var instance = bootstrap.Modal.getOrCreateInstance(el);
		instance.hide();
	}
	function showModal(el) {
		if (!el || !window.bootstrap) return;
		var instance = bootstrap.Modal.getOrCreateInstance(el);
		instance.show();
	}
	window.setTimeout(function () { hideModal('upload-file'); }, 4000);
	window.setTimeout(function () { hideModal('upload-folder'); }, 4000);

	qsa('.upload-modal').forEach(function (modalEl) {
		on(modalEl, 'hidden.bs.modal', function () {
			var msgModal = qs('.upload-message');
			showModal(msgModal);
			window.setTimeout(function () { hideModal('upload-message'); if (msgModal && !msgModal.id) { var inst = bootstrap.Modal.getOrCreateInstance(msgModal); inst.hide(); } }, 3000);
		});
	});

	/* card with fullscreen */
	qsa('[data-bs-toggle="card-fullscreen"]').forEach(function (ele) {
		on(ele, 'click', function (e) {
			var card = this.closest('.card');
			if (card) { card.classList.toggle('card-fullscreen'); card.classList.remove('card-collapsed'); }
			e.preventDefault();
			return false;
		});
	});

	/* card with close button */
	qsa('[data-bs-toggle="card-remove"]').forEach(function (ele) {
		on(ele, 'click', function (e) {
			e.preventDefault();
			var card = this.closest('.card');
			if (card) card.remove();
			return false;
		});
	});

	// View all Show hide
	function initViewAll(menuSelector, buttonSelector) {
		var menu = qs(menuSelector);
		if (!menu) return;
		menu.style.display = 'none';
		delegate(document, 'click', buttonSelector, function () {
			this.textContent = this.textContent === 'Less' ? 'Show More' : 'Less';
			slideToggle(menu, 900);
		});
	}
	initViewAll('.more-menu', '.viewall-button');
	initViewAll('.more-menu-2', '.viewall-button-2');
	initViewAll('.more-menu-3', '.viewall-button-3');

	initSlick('.channels-slider', {
		loop: true,
			spaceBetween: 24,
			slidesPerView: 1,
			autoplay: {
				delay: 2000,
				disableOnInteraction: false,
			},
			navigation: {
				nextEl: '.notes-slider-next',
				prevEl: '.notes-slider-prev',
			},
			breakpoints: {
				0: {
					slidesPerView: 1,
				},
				768: {
					slidesPerView: 2,
				},
				1300: {
					slidesPerView: 3,
				}
			}
	});
	initSlick('.social-gallery-slider', {
		loop: true, margin: 8, dots: false, nav: false, smartSpeed: 2000,
		responsive: { 0: { items: 2 }, 768: { items: 3 }, 1300: { items: 4 } }
	});

	// Kanban Drag (now sortable.js, no jQuery UI)
	if (qs('.kanban-drag-wrap') && window.makeSortable) {
		makeSortable(qsa('.kanban-drag-wrap'), { handle: '.kanban-card', placeholder: 'drag-placeholder' });
	}

	// Datetimepicker (now flatpickr, no jQuery)
	if (qs('.datepic') && window.flatpickr) {
		qsa('.datepic').forEach(function (el) { flatpickr(el, { dateFormat: 'd-m-Y', inline: true }); });
	}

	// Compose Mail Popup
	on(document.getElementById('compose_mail'), 'click', function () {
		var backdrop = document.createElement('div');
		backdrop.className = 'modal-backdrop fade show';
		document.body.appendChild(backdrop);
		var view = document.getElementById('compose-view');
		if (view) view.classList.add('show');
	});
	on(document.getElementById('compose-close'), 'click', function () {
		var backdrop = qs('.modal-backdrop');
		if (backdrop) backdrop.remove();
		var view = document.getElementById('compose-view');
		if (view) view.classList.remove('show');
	});

	if (qs('.pos-category3')) {
		new Swiper('.pos-category3', {
			loop: false,
			spaceBetween: 8,
			slidesPerView: 'auto',
			breakpoints: {
				0: { slidesPerView: 2 },
				500: { slidesPerView: 3 },
				768: { slidesPerView: 4 },
				991: { slidesPerView: 5 },
				1200: { slidesPerView: 6 }
			}
		});
	}
	if (qs('.pos-category4')) {
		new Swiper('.pos-category4', {
			loop: false,
			spaceBetween: 8,
			slidesPerView: 'auto',
			breakpoints: {
				0: { slidesPerView: 2 },
				500: { slidesPerView: 3 },
				768: { slidesPerView: 4 },
				991: { slidesPerView: 5 },
				1200: { slidesPerView: 6 }
			}
		});
	}
	if (qs('.pos-category5')) {
		new Swiper('.pos-category5', {
			loop: false,
			spaceBetween: 0,
			slidesPerView: 1,
			breakpoints: {
				0: { slidesPerView: 1 },
				500: { slidesPerView: 3 },
				768: { slidesPerView: 4 },
				991: { slidesPerView: 5 },
				1200: { slidesPerView: 6 }
			}
		});
	}

	// Select Payment
	if (qs('.select-payment')) {
		var optionMap = { cash: 'quick-cash', points: 'point-wrap', card: 'card-wrap' };
		qsa('.modal').forEach(function (modal) {
			function updatePaymentContent() {
				var select = modal.querySelector('.select-payment');
				var selectedValue = select ? select.value : null;
				qsa('.payment-content', modal).forEach(function (el) { el.style.display = 'none'; });
				qsa('.point-item', modal).forEach(function (el) { el.style.display = 'none'; });

				if (selectedValue && optionMap[selectedValue]) {
					var target = modal.querySelector('.' + optionMap[selectedValue]);
					if (target) target.style.display = '';
				}
				if (selectedValue === 'points') {
					qsa('.change-item', modal).forEach(function (el) { el.style.display = 'none'; });
					qsa('.point-item', modal).forEach(function (el) { el.style.display = ''; });
				}
			}
			on(modal, 'shown.bs.modal', updatePaymentContent);
			var select = modal.querySelector('.select-payment');
			on(select, 'change', updatePaymentContent);
			updatePaymentContent();
		});
	}

	// Active Payment
	delegate(document, 'click', '.payment-item', function (s) {
		qsa('.payment-item').forEach(function (el) { el.classList.remove('active'); });
		this.classList.add('active');
	});

	// YearPicker (now yearpicker.js, no jQuery)
	if (qs('.yearpicker') && window.vanillaYearPicker) {
		qsa('.yearpicker').forEach(function (el) { vanillaYearPicker(el); });
	}

	// Attach keydown event only when calculator modal is open
	var calculatorModal = document.getElementById('calculator');
	if (calculatorModal && typeof myFunction === 'function') {
		on(calculatorModal, 'shown.bs.modal', function () { document.addEventListener('keydown', myFunction); });
		on(calculatorModal, 'hidden.bs.modal', function () { document.removeEventListener('keydown', myFunction); });
	}

	// Circle Progress
	function percentageToDegrees(percentage) { return percentage / 100 * 360; }
	qsa('.circle-progress').forEach(function (el) {
		var value = parseFloat(el.getAttribute('data-value'));
		var left = el.querySelector('.progress-left .progress-bar');
		var right = el.querySelector('.progress-right .progress-bar');
		if (value > 0) {
			if (value <= 50) {
				if (right) right.style.transform = 'rotate(' + percentageToDegrees(value) + 'deg)';
			} else {
				if (right) right.style.transform = 'rotate(180deg)';
				if (left) left.style.transform = 'rotate(' + percentageToDegrees(value - 50) + 'deg)';
			}
		}
	});

	if (window.bootstrap) {
		var bottomcenterToast2 = qsa('.delete-toast-btn');
		var bottomcentertoastExample2 = qsa('.delete-toast');
		bottomcenterToast2.forEach(function (btn, index) {
			on(btn, 'click', function () {
				var toast = new bootstrap.Toast(bottomcentertoastExample2[index]);
				toast.show();
			});
		});
	}

	// Active Payment - remove customer item
	delegate(document, 'click', '.customer-item .close-icon', function (s) {
		var item = this.closest('.customer-item');
		if (item) item.remove();
	});

	delegate(qs('.additem-info') || document, 'click', '.trash-icon', function () {
		var item = this.closest('.add-info');
		if (item) item.remove();
	});

	on(qs('.add-item'), 'click', function () {
		var addcontent = '<div class="bg-light p-3 add-info">' +
			'<div class="row align-items-center g-2">' +
			'<div class="col-lg-2">' +
			'<h6 class="fs-14 fw-semibold">Payment</h6>' +
			'</div>' +
			'<div class="col-lg-4">' +
			'<select class="select">' +
			'<option>Cash</option>' +
			'<option>Card</option>' +
			'</select>' +
			'</div>' +
			'<div class="col-lg-4">' +
			'<input type="text" class="form-control" placeholder="Enter Amount">' +
			'</div>' +
			'<div class="col-lg-2">' +
			'<div class="d-flex align-items-center gap-2">' +
			'<button class="btn btn-dark w-100">Charge</button>' +
			'<a href="#" class="trash-icon"><i class="ti ti-trash"></i></a>' +
			'</div>' +
			'</div>' +
			'</div>' +
			'</div>';

		window.setTimeout(function () { initTomSelects('.select'); }, 100);

		var container = qs('.additem-info');
		if (container) container.insertAdjacentHTML('beforeend', addcontent);
		return false;
	});

	// Todo Strike Content
	function bindTodoStrike(selector, className) {
		qsa(selector).forEach(function (input) {
			on(input, 'click', function () {
				var target = this.parentElement && this.parentElement.parentElement;
				if (target) target.classList.toggle(className);
			});
		});
	}
	bindTodoStrike('.todo-item input', 'todo-strike');
	bindTodoStrike('.todo-inbox-check input', 'todo-strike-content');
	bindTodoStrike('.todo-list input', 'todo-strike-content');

	// Fancybox (jQuery-independent plugin - migrated in a later pass)
	if (qs('[data-fancybox]') && window.Fancybox) {
		Fancybox.bind("[data-fancybox]", {});
	}

	// Collapse Header
	on(document.getElementById('btnFullscreen'), 'click', function () { toggleFullscreen(); });

	// Quantity buttons
	delegate(document, 'click', '.quantity-btn', function () {
		var wrap = this.closest('.product-quantity');
		var input = wrap ? wrap.querySelector('input.quntity-input') : null;
		if (!input) return;
		var oldValue = input.value;
		var newVal;
		if (this.textContent === '+') {
			newVal = parseFloat(oldValue) + 1;
		} else {
			newVal = oldValue > 0 ? parseFloat(oldValue) - 1 : 0;
		}
		input.value = newVal;
	});

	// Custom Country Code Selector
	['#phone', '#phone2', '#phone3'].forEach(function (sel) {
		var input = qs(sel);
		if (input && window.intlTelInput) {
			window.intlTelInput(input, { utilsScript: "assets/plugins/intltelinput/js/utils.js" });
		}
	});

	// Remove Product
	delegate(document, 'click', '.remove-product', function () {
		var target = this.parentElement && this.parentElement.parentElement;
		if (target) target.style.display = 'none';
	});

	// Datetimepicker time (now flatpickr, no jQuery)
	if (qs('.timepicker') && window.flatpickr) {
		qsa('.timepicker').forEach(function (el) {
			flatpickr(el, { enableTime: true, noCalendar: true, dateFormat: 'h:i K' });
		});
	}

	function initSelect2OnSelects() {
		window.setTimeout(function () { initTomSelects('.select'); }, 100);
	}

	on(qs('.add-extra'), 'click', function () {
		var servicecontent = '<div class="row">' +
			'<div class="col-lg-4 col-sm-6 col-12">' +
			'<div class="form-group add-product">' +
			'<div class="add-newplus">' +
			'<label>Category</label>' +
			'</div>' +
			'<select class="select">' +
			'<option>Select</option>' +
			'<option>Computers</option>' +
			'</select>' +
			'</div>' +
			'</div>' +
			'<div class="col-lg-4 col-sm-6 col-12">' +
			'<div class="form-group add-product">' +
			'<label>Choose Category</label>' +
			'<select class="select">' +
			'<option>Select</option>' +
			'<option>Computers</option>' +
			'</select>' +
			'</div>' +
			'</div>' +
			'<div class="col-lg-4 col-sm-6 col-12">' +
			'<div class="d-flex align-items-center">' +
			'<div class="form-group w-100 add-product">' +
			'<label>Sub Category</label>' +
			'<select class="select">' +
			'<option>Select</option>' +
			'<option>Computers</option>' +
			'</select>' +
			'</div>' +
			'<div class="input-blocks">' +
			'<a href="#" class="btn btn-danger-outline trash"><i class="far fa-trash-alt"></i></a>' +
			'</div>' +
			'</div>' +
			'</div>';

		initSelect2OnSelects();
		var container = qs('.addservice-info');
		if (container) container.insertAdjacentHTML('beforeend', servicecontent);
		return false;
	});

	on(qs('.add-extra-item-two'), 'click', function () {
		var servicecontent = '<div class="row">' +
			'<div class="col-lg-4 col-sm-6 col-12">' +
			'<div class="form-group add-product">' +
			'<div class="add-newplus">' +
			'<label>Brand</label>' +
			'</div>' +
			'<select class="select">' +
			'<option>Select</option>' +
			'<option>Computers</option>' +
			'</select>' +
			'</div>' +
			'</div>' +
			'<div class="col-lg-4 col-sm-6 col-12">' +
			'<div class="form-group add-product">' +
			'<label>Unit</label>' +
			'<select class="select">' +
			'<option>Select</option>' +
			'<option>Computers</option>' +
			'</select>' +
			'</div>' +
			'</div>' +
			'<div class="col-lg-4 col-sm-6 col-12">' +
			'<div class="d-flex align-items-center">' +
			'<div class="form-group w-100 add-product">' +
			'<label>Selling Type</label>' +
			'<select class="select">' +
			'<option>Select</option>' +
			'<option>Computers</option>' +
			'</select>' +
			'</div>' +
			'<div class="input-blocks">' +
			'<a href="#" class="btn btn-danger-outline trash"><i class="far fa-trash-alt"></i></a>' +
			'</div>' +
			'</div>' +
			'</div>';

		initSelect2OnSelects();
		var container = qs('.add-product-new');
		if (container) container.insertAdjacentHTML('beforeend', servicecontent);
		return false;
	});

	// Remove Gallery
	delegate(document, 'click', '.remove-color', function () {
		var target = this.parentElement && this.parentElement.parentElement && this.parentElement.parentElement.parentElement;
		if (target) target.style.display = 'none';
	});

});

function toggleFullscreen(elem) {
	elem = elem || document.documentElement;
	if (!document.fullscreenElement && !document.mozFullScreenElement &&
		!document.webkitFullscreenElement && !document.msFullscreenElement) {
		if (elem.requestFullscreen) {
			elem.requestFullscreen();
		} else if (elem.msRequestFullscreen) {
			elem.msRequestFullscreen();
		} else if (elem.mozRequestFullScreen) {
			elem.mozRequestFullScreen();
		} else if (elem.webkitRequestFullscreen) {
			elem.webkitRequestFullscreen(Element.ALLOW_KEYBOARD_INPUT);
		}
	} else {
		if (document.exitFullscreen) {
			document.exitFullscreen();
		} else if (document.msExitFullscreen) {
			document.msExitFullscreen();
		} else if (document.mozCancelFullScreen) {
			document.mozCancelFullScreen();
		} else if (document.webkitExitFullscreen) {
			document.webkitExitFullscreen();
		}
	}
}
