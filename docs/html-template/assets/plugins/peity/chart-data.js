(function () {
	"use strict";
	if (!window.vanillaPeity) return;

	document.addEventListener('DOMContentLoaded', function () {
		vanillaPeity("span.pie", "pie", { width: '80', height: '80' });
		vanillaPeity("span.donut", "donut", { width: '50', height: '50' });
		vanillaPeity(".peity-line", "line", { width: '100%', height: '65', fill: ['#705ec8'] });
		vanillaPeity(".bar", "bar", { width: '100%', height: '50' });

		vanillaPeity(".bar-colours-1", "bar", { fill: ["#664dc9", "#e3dff5"], width: '100', height: '100' });
		vanillaPeity(".bar-colours-2", "bar", {
			fill: function (value) { return value > 0 ? "#38cb89" : "#aeeacf"; },
			width: '100', height: '100'
		});
		vanillaPeity(".bar-colours-3", "bar", { fill: ["#ffab00", "#ffdd99"], width: '100', height: '100' });
		vanillaPeity(".bar-colours-4", "bar", {
			fill: function (value) { return value > 0 ? "#ef4b4b" : "#f7a1a1"; },
			width: '100', height: '100'
		});

		vanillaPeity(".pie-colours-1", "pie", { fill: ["#705ec8", "#fa057a", "#2dce89", "#ff5b51"], width: '100', height: '100' });
		vanillaPeity(".pie-colours-2", "pie", { fill: ["#705ec8", "#fa057a", "#2dce89", "#ff5b51", "#fcbf09"], width: '100', height: '100' });

		// Using data attributes
		document.querySelectorAll(".data-attributes span").forEach(function (el) {
			vanillaPeity(el, "donut");
		});

		// Evented example
		document.querySelectorAll('select').forEach(function (select) {
			function updateGraph() {
				var text = select.value + "/5";
				var graph = select.parentElement ? select.parentElement.querySelector("span.graph") : null;
				if (graph) {
					graph.textContent = text;
					vanillaPeity.update(graph);
				}
				var notice = document.getElementById('notice');
				if (notice) notice.textContent = "Chart updated: " + text;
			}
			select.addEventListener('change', updateGraph);
			updateGraph();
		});

		document.querySelectorAll("span.graph").forEach(function (el) { vanillaPeity(el, "pie"); });

		// Updating charts
		var updatingCharts = document.querySelectorAll(".updating-chart");
		vanillaPeity(".updating-chart", "line", { width: "100%", height: 65 });

		window.setInterval(function () {
			updatingCharts.forEach(function (chart) {
				var random = Math.round(Math.random() * 20);
				var values = chart.textContent.split(",");
				values.shift();
				values.push(random);
				chart.textContent = values.join(",");
				vanillaPeity.update(chart);
			});
		}, 2500);

		vanillaPeity(".company-bar1", "bar", { fill: ["#FF6F28"], width: '52', height: '40' });
		vanillaPeity(".company-bar2", "bar", { fill: ["#4B3088"], width: '52', height: '40' });
		vanillaPeity(".company-bar3", "bar", { fill: ["#177DBC"], width: '52', height: '40' });
		vanillaPeity(".company-bar4", "bar", { fill: ["#2DCB73"], width: '52', height: '40' });

		vanillaPeity(".subscription-line-1", "line", { width: '100%', height: '35', fill: ['#F7A37A'], stroke: ['#F7A37A'] });
		vanillaPeity(".subscription-line-2", "line", { width: '100%', height: '25', fill: ['#70B1FF'], stroke: ['#70B1FF'] });
		vanillaPeity(".subscription-line-3", "line", { width: '100%', height: '25', fill: ['#60DD97'], stroke: ['#60DD97'] });
		vanillaPeity(".subscription-line-4", "line", { width: '100%', height: '25', fill: ['#DE5555'], stroke: ['#DE5555'] });
	});
})();
