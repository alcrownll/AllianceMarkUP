(() => {
    document.addEventListener('DOMContentLoaded', () => {
        // ===== Donut: Cumulative GWA (1 is best) =====
        const ring = document.getElementById('gpaRing');
        const gwa = parseFloat(ring.dataset.gwa || '0');   // 1.00–5.00
        const fill = parseFloat(ring.dataset.fill || '0'); // 0–1 (more fill = better)

        new Chart(ring, {
            type: 'doughnut',
            data: {
                datasets: [{
                    data: [fill, 1 - fill],
                    borderWidth: 0,
                    backgroundColor: ['#2563eb', '#e8eef7'],
                    cutout: '78%'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: true,
                aspectRatio: 1,
                plugins: { legend: { display: false }, tooltip: { enabled: false } }
            },
            plugins: [{
                id: 'centerText',
                afterDraw(chart) {
                    const { ctx, chartArea } = chart;
                    const cx = (chartArea.left + chartArea.right) / 2;
                    const cy = (chartArea.top + chartArea.bottom) / 2;
                    ctx.save();
                    ctx.font = '700 44px Inter, system-ui';
                    ctx.fillStyle = '#2563eb';
                    ctx.textAlign = 'center';
                    ctx.textBaseline = 'middle';
                    ctx.fillText(isNaN(gwa) || gwa === 0 ? '—' : gwa.toFixed(2), cx, cy - 12);
                    ctx.font = '12px Inter, system-ui';
                    ctx.fillStyle = '#6b7280';
                    ctx.fillText('Cumulative GWA', cx, cy + 18);
                    ctx.restore();
                }
            }]
        });

        // ---------- Line chart (dynamic from server) ----------
        const ctxLine = document.getElementById('termLine');
        let line;
        const sem1 = JSON.parse(ctxLine.dataset.sem1 || '[]');
        const sem2 = JSON.parse(ctxLine.dataset.sem2 || '[]');

        function drawLine(arr) {
            if (line) line.destroy();
            const data = [...Array(4)].map((_, i) => (arr && arr[i] != null) ? arr[i] : null);

            line = new Chart(ctxLine, {
                type: 'line',
                data: {
                    labels: ['Prelims', 'Midterms', 'SemiFinals', 'Finals'],
                    datasets: [{
                        data,
                        tension: .35,
                        pointRadius: 4,
                        pointHoverRadius: 5,
                        borderWidth: 2,
                        fill: false,
                        borderColor: '#2563eb',
                        backgroundColor: '#2563eb'
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    layout: { padding: { right: 12 } },
                    scales: {
                        y: {
                            min: 1, max: 5, reverse: true,
                            grid: { color: '#eef2f7' },
                            ticks: { stepSize: .5, color: '#64748b', font: { size: 11 } }
                        },
                        x: {
                            grid: { display: false },
                            ticks: { color: '#64748b', font: { size: 11 } }
                        }
                    },
                    plugins: { legend: { display: false } }
                }
            });
        }
        drawLine(sem1);

        const t1 = document.getElementById('tabSem1');
        const t2 = document.getElementById('tabSem2');

        function activate(el, other, data) {
            el.classList.add('active');
            other.classList.remove('active');
            drawLine(data);
        }

        t1.addEventListener('click', () => activate(t1, t2, sem1));
        t2.addEventListener('click', () => activate(t2, t1, sem2));
    });
})();
