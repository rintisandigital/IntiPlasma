// Chart.js bridge for the production charts (PLAN-MOBILE M-67). The chart spec is built and tested in
// MobileApp.Core (PerformanceCharts); this file only draws it. Values from recordings that are not sent yet
// (dataset.localFrom onwards) are drawn dashed with hollow points.
(function () {
    const charts = new WeakMap();

    Chart.defaults.font.family = "Inter, sans-serif";
    Chart.defaults.color = "#6c757d";

    function dataset(d) {
        const isLine = d.type === "line";
        const localFrom = d.localFrom;
        const isLocal = (index) => localFrom !== null && localFrom !== undefined && index >= localFrom;

        return {
            label: d.label,
            data: d.data,
            type: d.type,
            yAxisID: d.axis,
            stack: d.stack || undefined,
            order: isLine ? 0 : 1,
            borderColor: d.color,
            backgroundColor: isLine ? d.color : d.color + "CC",
            borderWidth: isLine ? 2 : 0,
            pointRadius: isLine ? 2 : 0,
            pointHoverRadius: isLine ? 4 : 0,
            pointBackgroundColor: (ctx) => (isLocal(ctx.dataIndex) ? "#ffffff" : d.color),
            spanGaps: true,
            tension: 0.2,
            segment: {
                borderDash: (ctx) => (isLocal(ctx.p1DataIndex) ? [6, 4] : undefined)
            }
        };
    }

    window.intiplasmaCharts = {
        render(canvas, spec) {
            const existing = charts.get(canvas);
            if (existing) {
                existing.destroy();
            }

            const stacked = spec.datasets.some((d) => d.stack);
            const hasY1 = !!spec.y1Title;

            charts.set(canvas, new Chart(canvas, {
                type: "bar",
                data: { labels: spec.labels, datasets: spec.datasets.map(dataset) },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    animation: false,
                    locale: "id-ID",
                    interaction: { mode: "index", intersect: false },
                    plugins: {
                        legend: { position: "bottom", labels: { boxWidth: 12, font: { size: 11 } } }
                    },
                    scales: {
                        x: {
                            stacked: stacked,
                            title: { display: true, text: spec.xTitle },
                            ticks: { maxRotation: 0, autoSkip: true }
                        },
                        y: {
                            stacked: stacked,
                            beginAtZero: true,
                            title: { display: true, text: spec.yTitle }
                        },
                        y1: hasY1
                            ? {
                                position: "right",
                                beginAtZero: true,
                                grid: { drawOnChartArea: false },
                                title: { display: true, text: spec.y1Title }
                            }
                            : { display: false }
                    }
                }
            }));
        },

        destroy(canvas) {
            const chart = charts.get(canvas);
            if (chart) {
                chart.destroy();
                charts.delete(canvas);
            }
        }
    };
})();
