document.addEventListener('DOMContentLoaded', function () {
	'use strict';
	if (!window.ApexCharts) return;

	var axisFont = { colors: '#8e9cad', fontSize: '10px' };
	var gridColor = 'rgba(67, 87, 133, .09)';

	function render(id, options) {
		var el = document.getElementById(id);
		if (!el) return;
		new ApexCharts(el, options).render();
	}

	render('flotBar1', {
		chart: { type: 'bar', height: 250, toolbar: { show: false } },
		series: [{ name: 'Value', data: [20, 35, 25, 22, 18, 27, 34, 35, 48, 30] }],
		colors: ['#44c4fa'],
		plotOptions: { bar: { columnWidth: '40%' } },
		dataLabels: { enabled: false },
		grid: { borderColor: gridColor },
		xaxis: { categories: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9], labels: { style: axisFont } },
		yaxis: { labels: { style: axisFont } }
	});

	render('flotBar2', {
		chart: { type: 'bar', height: 250, toolbar: { show: false } },
		series: [
			{ name: 'Series 1', data: [30, null, 15, null, 45, null, 22, null, 18] },
			{ name: 'Series 2', data: [null, 80, null, 20, null, 24, null, 17] }
		],
		colors: ['#664dc9', '#44c4fa'],
		plotOptions: { bar: { columnWidth: '80%' } },
		dataLabels: { enabled: false },
		grid: { borderColor: gridColor },
		xaxis: { categories: [0, 1, 2, 3, 4, 5, 6, 7, 8], labels: { style: axisFont } },
		yaxis: { labels: { style: axisFont } }
	});

	var categories = [0, 1, 2, 3, 4, 5, 6];
	var newCust = [10, 15, 25, 22, 18, 27, 34];
	var retCust = [8, 17, 28, 20, 16, 24, 36];

	render('flotLine1', {
		chart: { type: 'line', height: 250, toolbar: { show: false } },
		series: [{ name: 'Sales', data: newCust }, { name: 'Customer', data: retCust }],
		colors: ['#664dc9', '#44c4fa'],
		stroke: { width: 2, curve: 'straight' },
		markers: { size: 0 },
		legend: { position: 'top', horizontalAlign: 'left' },
		grid: { borderColor: gridColor },
		xaxis: { categories: categories, labels: { style: axisFont } },
		yaxis: { min: 0, max: 40, labels: { style: axisFont } }
	});

	render('flotLine2', {
		chart: { type: 'line', height: 250, toolbar: { show: false } },
		series: [{ name: 'Sales', data: newCust }, { name: 'Customer', data: retCust }],
		colors: ['#664dc9', '#44c4fa'],
		stroke: { width: 2, curve: 'straight' },
		markers: { size: 4 },
		legend: { position: 'top', horizontalAlign: 'right' },
		grid: { borderColor: gridColor },
		xaxis: { categories: categories, labels: { style: axisFont } },
		yaxis: { min: 0, max: 50, labels: { style: axisFont } }
	});

	render('flotArea1', {
		chart: { type: 'area', height: 250, toolbar: { show: false } },
		series: [{ name: 'Sales', data: newCust }, { name: 'Customer', data: retCust }],
		colors: ['#664dc9', '#44c4fa'],
		stroke: { width: 1, curve: 'straight' },
		fill: { type: 'gradient', gradient: { opacityFrom: 0.8, opacityTo: 0 } },
		markers: { size: 0 },
		legend: { position: 'top', horizontalAlign: 'left' },
		grid: { borderColor: gridColor },
		xaxis: { categories: categories, labels: { style: axisFont } },
		yaxis: { min: 0, max: 50, labels: { style: axisFont } }
	});

	render('flotArea2', {
		chart: { type: 'area', height: 250, toolbar: { show: false } },
		series: [{ name: 'Sales', data: newCust }, { name: 'Customer', data: retCust }],
		colors: ['#664dc9', '#44c4fa'],
		stroke: { width: 1, curve: 'straight' },
		fill: { type: 'gradient', gradient: { opacityFrom: 0.3, opacityTo: 0 } },
		markers: { size: 4 },
		legend: { position: 'top', horizontalAlign: 'left' },
		grid: { borderColor: gridColor },
		xaxis: { categories: categories, labels: { style: axisFont } },
		yaxis: { min: 0, max: 50, labels: { style: axisFont } }
	});

	var pieLabels = ['Series 1', 'Series 2', 'Series 3', 'Series 4', 'Series 5'];
	var pieData = [10, 50, 30, 30, 60];
	var pieColors = ['#664dc9', '#44c4fa', '#38cb89', '#ef4b4b', '#ffab00'];

	render('flotPie1', {
		chart: { type: 'pie', height: 250 },
		series: pieData,
		labels: pieLabels,
		colors: pieColors,
		dataLabels: { formatter: function (val) { return Math.round(val) + '%'; } }
	});

	render('flotPie2', {
		chart: { type: 'donut', height: 250 },
		series: pieData,
		labels: pieLabels,
		colors: pieColors,
		dataLabels: { formatter: function (val) { return Math.round(val) + '%'; } }
	});
});
