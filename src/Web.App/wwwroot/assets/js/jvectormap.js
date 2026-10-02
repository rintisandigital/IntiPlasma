(function () {
	"use strict";
	if (!document.getElementById('sales_db_world_map') || !window.jsVectorMap) return;

	var salesData = {
		"Africa": 3455,
		"AL": 11.58,
		"DZ": 158.97,
		"US": 1000000,
		"RU": 5488,
		"IN": 98765,
		"UK": 5467,
		'CN': 7777,
		'UAE': 98654,
		'SA': 54678
	};

	new jsVectorMap({
		selector: '#sales_db_world_map',
		map: 'world',
		series: {
			regions: [{
				values: salesData,
				scale: ['#ECECEC', '#ECECEC'],
				normalizeFunction: 'polynomial',
			}]
		},
		onRegionTooltipShow: function (event, tooltip, code) {
			if (salesData[code] === undefined) {
				tooltip.text('<h6>' + tooltip.text() + '</h6><p>No Branch Here</p>', true);
			} else {
				tooltip.text('<h6>' + tooltip.text() + '</h6><p>' + salesData[code] + ' Sales</p>', true);
			}
			tooltip.css({ "font-size": "15px", "color": "#5B6670" });
		},
		regionStyle: {
			initial: {
				fill: '#ECECEC'
			},
			hover: {
				"fill-opacity": 1,
				"fill": '#FF9F43',
				"stroke": '#FF9F43'
			},
			selected: {
				fill: '#F4A582'
			},
		},
		backgroundColor: 'transparent'
	});
}());
