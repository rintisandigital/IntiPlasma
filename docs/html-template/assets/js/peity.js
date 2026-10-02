/*
 * Minimal vanilla-JS re-implementation of Peity's pie/donut/line/bar mini
 * charts, replacing the jQuery-dependent jquery.peity.min.js. Renders an
 * inline SVG in place of the target element's text content - the same
 * approach Peity itself uses - so surrounding layout/CSS is unaffected.
 */
(function (global) {
	'use strict';

	var SVG_NS = 'http://www.w3.org/2000/svg';

	function svgEl(tag, attrs) {
		var el = document.createElementNS(SVG_NS, tag);
		for (var key in attrs) el.setAttribute(key, attrs[key]);
		return el;
	}

	function parseData(el) {
		var raw = el.textContent.trim();
		if (raw.indexOf('/') > -1) {
			var parts = raw.split('/').map(Number);
			return { value: parts[0], total: parts[1] };
		}
		return { series: raw.split(',').map(Number) };
	}

	function dataAttrOptions(el) {
		var attr = el.getAttribute('data-peity');
		if (!attr) return {};
		try { return JSON.parse(attr.replace(/'/g, '"')); } catch (e) { return {}; }
	}

	function polar(cx, cy, r, angleDeg) {
		var rad = (angleDeg - 90) * Math.PI / 180;
		return { x: cx + r * Math.cos(rad), y: cy + r * Math.sin(rad) };
	}

	function pieSlicePath(cx, cy, r, innerR, startAngle, endAngle) {
		var large = (endAngle - startAngle) > 180 ? 1 : 0;
		var startOuter = polar(cx, cy, r, startAngle);
		var endOuter = polar(cx, cy, r, endAngle);
		if (!innerR) {
			return ['M', cx, cy, 'L', startOuter.x, startOuter.y,
				'A', r, r, 0, large, 1, endOuter.x, endOuter.y, 'Z'].join(' ');
		}
		var startInner = polar(cx, cy, innerR, endAngle);
		var endInner = polar(cx, cy, innerR, startAngle);
		return ['M', startOuter.x, startOuter.y,
			'A', r, r, 0, large, 1, endOuter.x, endOuter.y,
			'L', startInner.x, startInner.y,
			'A', innerR, innerR, 0, large, 0, endInner.x, endInner.y, 'Z'].join(' ');
	}

	function renderPie(el, options, donut) {
		var data = parseData(el);
		var width = options.width || 32, height = options.height || 32;
		var fill = options.fill || ['#4D89F9', '#C9D9FC', '#67AE3F', '#D8464E', '#EEB2B4'];
		var size = Math.min(width, height);
		var cx = size / 2, cy = size / 2, r = size / 2;
		var innerR = donut ? r * 0.6 : 0;

		var slices;
		if (data.value !== undefined) {
			slices = [data.value, Math.max(data.total - data.value, 0)];
		} else {
			slices = data.series;
		}
		var total = slices.reduce(function (a, b) { return a + Math.abs(b); }, 0) || 1;

		var svg = svgEl('svg', { width: width, height: height, viewBox: '0 0 ' + size + ' ' + size });
		var angle = 0;
		slices.forEach(function (value, i) {
			var slice = (Math.abs(value) / total) * 360;
			if (slice > 0) {
				var color = typeof fill === 'function' ? fill(value, i) : (fill[i % fill.length]);
				svg.appendChild(svgEl('path', { d: pieSlicePath(cx, cy, r, innerR, angle, angle + slice), fill: color }));
			}
			angle += slice;
		});

		el.innerHTML = '';
		el.appendChild(svg);
	}

	function renderLine(el, options) {
		var data = parseData(el).series || [];
		var width = options.width === '100%' ? (el.parentElement ? el.parentElement.clientWidth || 200 : 200) : (options.width || 200);
		var height = options.height || 40;
		var fillColor = (options.fill && options.fill[0]) || 'rgba(77,137,249,0.15)';
		var strokeColor = (options.stroke && options.stroke[0]) || (options.fill && options.fill[0]) || '#4D89F9';

		var max = Math.max.apply(null, data.concat([0]));
		var min = Math.min.apply(null, data.concat([0]));
		var range = (max - min) || 1;
		var stepX = data.length > 1 ? width / (data.length - 1) : width;

		var points = data.map(function (v, i) {
			var x = i * stepX;
			var y = height - ((v - min) / range) * height;
			return [x, y];
		});

		var linePath = points.map(function (p, i) { return (i === 0 ? 'M' : 'L') + p[0] + ',' + p[1]; }).join(' ');
		var areaPath = linePath + ' L' + width + ',' + height + ' L0,' + height + ' Z';

		var svg = svgEl('svg', { width: options.width === '100%' ? '100%' : width, height: height, viewBox: '0 0 ' + width + ' ' + height, preserveAspectRatio: 'none' });
		svg.appendChild(svgEl('path', { d: areaPath, fill: fillColor, stroke: 'none' }));
		svg.appendChild(svgEl('path', { d: linePath, fill: 'none', stroke: strokeColor, 'stroke-width': 1.5 }));

		el.innerHTML = '';
		el.appendChild(svg);
	}

	function renderBar(el, options) {
		var data = parseData(el).series || [];
		var width = options.width === '100%' ? (el.parentElement ? el.parentElement.clientWidth || 200 : 200) : (options.width || 200);
		var height = options.height || 40;
		var fill = options.fill || ['#4D89F9'];

		var max = Math.max.apply(null, data.map(Math.abs).concat([1]));
		var barGap = 2;
		var barWidth = (width / data.length) - barGap;
		var zeroY = height / 2;
		var hasNegative = data.some(function (v) { return v < 0; });

		var svg = svgEl('svg', { width: options.width === '100%' ? '100%' : width, height: height, viewBox: '0 0 ' + width + ' ' + height, preserveAspectRatio: 'none' });
		data.forEach(function (v, i) {
			var color = typeof fill === 'function' ? fill(v) : fill[i % fill.length];
			var barHeight = (Math.abs(v) / max) * (hasNegative ? height / 2 : height);
			var x = i * (barWidth + barGap);
			var y = hasNegative ? (v >= 0 ? zeroY - barHeight : zeroY) : height - barHeight;
			svg.appendChild(svgEl('rect', { x: x, y: y, width: Math.max(barWidth, 1), height: Math.max(barHeight, 1), fill: color }));
		});

		el.innerHTML = '';
		el.appendChild(svg);
	}

	function peity(elOrSelector, type, options) {
		var els = typeof elOrSelector === 'string'
			? Array.prototype.slice.call(document.querySelectorAll(elOrSelector))
			: [elOrSelector];

		els.forEach(function (el) {
			var merged = Object.assign({}, options || {}, dataAttrOptions(el));
			function redraw() {
				if (type === 'pie') renderPie(el, merged, false);
				else if (type === 'donut') renderPie(el, merged, true);
				else if (type === 'line') renderLine(el, merged);
				else if (type === 'bar') renderBar(el, merged);
			}
			redraw();
			el._peityRedraw = redraw;
		});
	}

	peity.update = function (el) {
		if (el._peityRedraw) el._peityRedraw();
	};

	global.vanillaPeity = peity;
})(window);
