document.addEventListener('DOMContentLoaded', function () {
	'use strict';
	if (!window.ApexCharts) return;

	var morrisData = [
		{ y: '2006', a: 12, b: 18 },
		{ y: '2007', a: 18, b: 22 },
		{ y: '2008', a: 15, b: 18 },
		{ y: '2009', a: 25, b: 28 },
		{ y: '2010', a: 30, b: 35 },
		{ y: '2011', a: 18, b: 28 },
		{ y: '2012', a: 12, b: 18 }
	];

	function renderChart(elementId, options) {
		var el = document.getElementById(elementId);
		if (!el) return null;
		var chart = new ApexCharts(el, options);
		chart.render();
		return chart;
	}

	renderChart('morrisBar1', {
		chart: { type: 'bar', height: 300, toolbar: { show: false } },
		series: [
			{ name: 'Series A', data: morrisData.map(function (d) { return d.a; }) },
			{ name: 'Series B', data: morrisData.map(function (d) { return d.b; }) }
		],
		xaxis: { categories: morrisData.map(function (d) { return d.y; }) },
		colors: ['#664dc9', '#44c4fa']
	});

	renderChart('morrisBar3', {
		chart: { type: 'bar', height: 300, stacked: true, toolbar: { show: false } },
		series: [
			{ name: 'Series A', data: morrisData.map(function (d) { return d.a; }) },
			{ name: 'Series B', data: morrisData.map(function (d) { return d.b; }) }
		],
		xaxis: { categories: morrisData.map(function (d) { return d.y; }) },
		colors: ['#664dc9', '#44c4fa']
	});

	renderChart('morrisLine1', {
		chart: { type: 'line', height: 300, toolbar: { show: false } },
		series: [
			{ name: 'Series A', data: morrisData.map(function (d) { return d.a; }) },
			{ name: 'Series B', data: morrisData.map(function (d) { return d.b; }) }
		],
		xaxis: { categories: morrisData.map(function (d) { return d.y; }) },
		yaxis: { max: 50 },
		colors: ['#664dc9', '#44c4fa'],
		stroke: { width: 1 }
	});

	renderChart('morrisArea1', {
		chart: { type: 'area', height: 300, toolbar: { show: false } },
		series: [
			{ name: 'Series A', data: [10, 25, 80, 25, 30, 18, 12] },
			{ name: 'Series B', data: [15, 22, 60, 28, 35, 28, 18] }
		],
		xaxis: { categories: morrisData.map(function (d) { return d.y; }) },
		yaxis: { max: 100 },
		colors: ['#664dc9', '#44c4fa'],
		stroke: { width: 1 },
		fill: { opacity: 0.9 }
	});

	// morrisBar6 - live-updating sine/cosine wave
	var nReloads = 0;
	function waveData(offset) {
		var x = [], y = [], z = [];
		for (var v = 0; v <= 360; v += 10) {
			var deg = (offset + v) % 360;
			x.push(v);
			y.push(parseFloat(Math.sin(Math.PI * deg / 180).toFixed(4)));
			z.push(parseFloat(Math.cos(Math.PI * deg / 180).toFixed(4)));
		}
		return { x: x, y: y, z: z };
	}
	var initialWave = waveData(0);
	var waveChart = renderChart('morrisBar6', {
		chart: { type: 'line', height: 300, toolbar: { show: false }, animations: { enabled: false } },
		series: [
			{ name: 'data1', data: initialWave.y },
			{ name: 'data2', data: initialWave.z }
		],
		xaxis: { categories: initialWave.x },
		yaxis: { min: -1.0, max: 1.0 },
		colors: ['#664dc9', '#44c4fa']
	});
	if (waveChart) {
		window.setInterval(function () {
			nReloads++;
			var wave = waveData(5 * nReloads);
			waveChart.updateSeries([
				{ name: 'data1', data: wave.y },
				{ name: 'data2', data: wave.z }
			]);
			var reloadStatus = document.getElementById('reloadStatus');
			if (reloadStatus) reloadStatus.textContent = nReloads + ' reloads';
		}, 1000);
	}

	// morrisBar7 - licensed/SORN by date
	var dayData = [
		{ period: '2012-10-01', licensed: 3407, sorned: 660 },
		{ period: '2012-09-30', licensed: 3351, sorned: 629 },
		{ period: '2012-09-29', licensed: 3269, sorned: 618 },
		{ period: '2012-09-20', licensed: 3246, sorned: 661 },
		{ period: '2012-09-19', licensed: 3257, sorned: 667 },
		{ period: '2012-09-18', licensed: 3248, sorned: 627 },
		{ period: '2012-09-17', licensed: 3171, sorned: 660 },
		{ period: '2012-09-16', licensed: 3171, sorned: 676 },
		{ period: '2012-09-15', licensed: 3201, sorned: 656 },
		{ period: '2012-09-10', licensed: 3215, sorned: 622 }
	];
	renderChart('morrisBar7', {
		chart: { type: 'line', height: 300, toolbar: { show: false } },
		series: [
			{ name: 'Licensed', data: dayData.map(function (d) { return d.licensed; }) },
			{ name: 'SORN', data: dayData.map(function (d) { return d.sorned; }) }
		],
		xaxis: { categories: dayData.map(function (d) { return d.period; }) },
		colors: ['#664dc9', '#44c4fa']
	});

	renderChart('morrisDonut1', {
		chart: { type: 'donut', height: 300 },
		series: [50, 30, 20],
		labels: ['Sales', 'Pending', 'Process'],
		colors: ['#664dc9', '#44c4fa', '#38cb89']
	});
});
