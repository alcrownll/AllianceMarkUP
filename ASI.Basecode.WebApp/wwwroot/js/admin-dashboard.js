(function () {
    const root = document.getElementById('admin-dashboard');
    if (!root || typeof Chart === 'undefined') {
        return;
    }

    const payload = window.AdminDashboardInitial || {};
    const programHighlight = document.getElementById('programShareHighlight');
    const programLegendList = document.getElementById('programLegendList');
    const yearSelector = document.getElementById('yearSelector');
    const termSelector = document.getElementById('termSelector');
    const yearChip = root.querySelector('[data-year-display]');
    const termChip = root.querySelector('[data-term-display]');
    const termDescriptor = document.getElementById('termDescriptor');

    const charts = {
        trend: null,
        program: null,
        yearLevel: null,
        gpa: null,
        passFail: null
    };


    const colorPalette = ['#2563eb', '#22c55e', '#f97316', '#a855f7', '#0ea5e9', '#f59e0b', '#ec4899'];

    let currentDetail = payload.detail || {};
    let currentSchoolYear = payload.selectedSchoolYear || currentDetail.SchoolYear || currentDetail.schoolYear || '';
    let currentTermKey = payload.selectedTermKey || currentDetail.SelectedTermKey || currentDetail.selectedTermKey || '';

    function buildTrendChart() {
        const ctx = document.getElementById('enrollmentTrendChart');
        if (!ctx) {
            return;
        }

        charts.trend?.destroy();
        const trendPoints = Array.isArray(payload.trend) ? payload.trend : [];
        if (!trendPoints.length) {
            charts.trend = createEmptyChart(ctx, 'line', 'No data');
            return;
        }
        const labels = trendPoints.map(p => p.label || p.Label || p.TermKey);
        const data = trendPoints.map(p => p.studentCount ?? p.StudentCount ?? 0);

        charts.trend = new Chart(ctx, {
            type: 'line',
            data: {
                labels,
                datasets: [
                    {
                        label: 'Students',
                        data,
                        fill: false,
                        borderColor: '#2563eb',
                        backgroundColor: 'rgba(37, 99, 235, 0.15)',
                        tension: 0.35,
                        pointRadius: 6,
                        pointHoverRadius: 8,
                        pointBackgroundColor: '#ffffff',
                        pointBorderColor: '#2563eb',
                        pointBorderWidth: 2
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label(context) {
                                const value = context.parsed.y;
                                return `Students: ${value.toLocaleString()}`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        ticks: { color: '#475569' },
                        grid: { display: false }
                    },
                    y: {
                        ticks: {
                            color: '#475569',
                            precision: 0
                        },
                        grid: {
                            color: 'rgba(15, 23, 42, 0.08)',
                            drawBorder: false
                        }
                    }
                }
            }
        });
    }

    function aggregatePassFail(rates) {
        return rates.reduce((totals, r) => {
            totals.passed += r.passed ?? r.Passed ?? 0;
            totals.failed += r.failed ?? r.Failed ?? 0;
            return totals;
        }, { passed: 0, failed: 0 });
    }

    function toNumber(value) {
        const n = Number(value);
        return Number.isFinite(n) ? n : 0;
    }

    function applyYearDetail(detail) {
        const safeDetail = detail || {};

        currentDetail = safeDetail;
        currentSchoolYear = safeDetail.SchoolYear || safeDetail.schoolYear || currentSchoolYear;
        currentTermKey = safeDetail.SelectedTermKey || safeDetail.selectedTermKey || '';
        payload.detail = safeDetail;
        payload.selectedTermKey = currentTermKey;

        updateProgramChart(safeDetail.ProgramShares || safeDetail.programShares || []);
        updateYearLevelChart(safeDetail.YearLevelSeries || safeDetail.yearLevelSeries || []);
        updateAverageGpaChart(safeDetail.AverageGpa || safeDetail.averageGpa || []);
        updatePassFailChart(safeDetail.PassFailRates || safeDetail.passFailRates || []);

        syncSelectors(safeDetail);
        syncChips(safeDetail);
        syncYearSelector(safeDetail);
    }

    function updateProgramChart(shares) {
        const ctx = document.getElementById('programDistributionChart');
        if (!ctx) {
            return;
        }

        charts.program?.destroy();

        if (!shares.length) {
            resetProgramHighlight();
            renderProgramLegend([]);
            charts.program = createEmptyChart(ctx, 'doughnut', 'No data');
            return;
        }

        const entries = shares.map((share, index) => ({
            label: share.Program || share.program || 'N/A',
            value: toNumber(share.SharePercent ?? share.sharePercent),
            color: colorPalette[index % colorPalette.length]
        }));

        const labels = entries.map(e => e.label);
        const data = entries.map(e => e.value);
        const colors = entries.map(e => e.color);

        const topEntry = entries.reduce((best, current) => current.value > best.value ? current : best, entries[0]);

        charts.program = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels,
                datasets: [
                    {
                        data,
                        backgroundColor: colors,
                        borderWidth: 0,
                        hoverOffset: 8
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label(context) {
                                const label = context.label || 'Program';
                                const value = context.parsed ?? 0;
                                return `${label}: ${value.toFixed(1)}%`;
                            }
                        }
                    }
                },
                cutout: '62%',
                onHover(event, elements) {
                    const canvas = event?.native?.target;
                    if (canvas) {
                        canvas.style.cursor = elements.length ? 'pointer' : 'default';
                    }

                    if (!elements.length) {
                        setProgramHighlight(topEntry.label, topEntry.value);
                        return;
                    }

                    const item = entries[elements[0].index];
                    setProgramHighlight(item.label, item.value);
                }
            }
        });

        renderProgramLegend(entries);
        setProgramHighlight(topEntry.label, topEntry.value);
    }

    function updateYearLevelChart(series) {
        const ctx = document.getElementById('yearLevelHorizontalChart');
        if (!ctx) {
            return;
        }

        charts.yearLevel?.destroy();

        if (!series.length) {
            charts.yearLevel = createEmptyChart(ctx, 'bar', 'No data');
            return;
        }

        const labels = Array.from(new Set(series.map(s => s.YearLevel || s.yearLevel))).sort();
        const programs = Array.from(new Set(series.map(s => s.Program || s.program)));

        const datasets = programs.map((program, index) => {
            const data = labels.map(label => {
                const entry = series.find(s => (s.YearLevel || s.yearLevel) === label && (s.Program || s.program) === program);
                return entry ? toNumber(entry.Count ?? entry.count) : 0;
            });
            return {
                label: program,
                data,
                backgroundColor: colorPalette[index % colorPalette.length],
                borderRadius: 8,
                barThickness: 18
            };
        });

        charts.yearLevel = new Chart(ctx, {
            type: 'bar',
            data: { labels, datasets },
            options: {
                indexAxis: 'y',
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            usePointStyle: true,
                            padding: 16
                        }
                    },
                    tooltip: {
                        callbacks: {
                            label(context) {
                                const label = context.dataset.label || 'Program';
                                return `${label}: ${context.parsed.x.toLocaleString()} students`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        stacked: true,
                        ticks: { color: '#475569', precision: 0 },
                        grid: { color: 'rgba(15, 23, 42, 0.06)', drawBorder: false }
                    },
                    y: {
                        stacked: true,
                        ticks: { color: '#475569' },
                        grid: { display: false }
                    }
                }
            }
        });
    }

    function updateAverageGpaChart(points) {
        const ctx = document.getElementById('averageGpaChart');
        if (!ctx) {
            return;
        }

        charts.gpa?.destroy();

        if (!points.length) {
            charts.gpa = createEmptyChart(ctx, 'bar', 'No data');
            return;
        }

        const labels = points.map(p => p.Label || p.label || p.TermKey);
        const data = points.map(p => Number((p.AverageGpa ?? p.averageGpa ?? 0).toFixed(2)));

        charts.gpa = new Chart(ctx, {
            type: 'bar',
            data: {
                labels,
                datasets: [
                    {
                        label: 'Average GPA',
                        data,
                        backgroundColor: 'rgba(37, 99, 235, 0.8)',
                        borderRadius: 10,
                        maxBarThickness: 36
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label(context) {
                                return `Average GPA: ${context.parsed.y.toFixed(2)}`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false },
                        ticks: { color: '#475569' }
                    },
                    y: {
                        beginAtZero: true,
                        suggestedMax: 4,
                        ticks: {
                            color: '#475569',
                            callback(value) {
                                return value.toFixed(1);
                            }
                        },
                        grid: { color: 'rgba(15, 23, 42, 0.06)', drawBorder: false }
                    }
                }
            }
        });
    }

    function updatePassFailChart(rates) {
        const ctx = document.getElementById('passFailChart');
        if (!ctx) {
            return;
        }

        charts.passFail?.destroy();

        if (!rates.length) {
            charts.passFail = createEmptyChart(ctx, 'doughnut', 'No data');
            return;
        }

        const totals = aggregatePassFail(rates);
        const data = [totals.passed, totals.failed];
        const labels = ['Pass', 'Fail'];

        charts.passFail = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels,
                datasets: [
                    {
                        data,
                        backgroundColor: ['#22c55e', '#ef4444'],
                        borderWidth: 0,
                        hoverOffset: 8
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: { usePointStyle: true, padding: 16 }
                    },
                    tooltip: {
                        callbacks: {
                            label(context) {
                                const label = context.label || '';
                                const value = context.parsed ?? 0;
                                const total = data.reduce((sum, current) => sum + current, 0) || 1;
                                const percent = (value / total) * 100;
                                return `${label}: ${value.toLocaleString()} (${percent.toFixed(1)}%)`;
                            }
                        }
                    }
                },
                cutout: '55%'
            }
        });
    }

    function createEmptyChart(ctx, type, message) {
        return new Chart(ctx, {
            type,
            data: {
                labels: [message],
                datasets: [
                    {
                        data: [1],
                        backgroundColor: ['rgba(148, 163, 184, 0.25)'],
                        borderWidth: 0
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: { enabled: false }
                },
                scales: type === 'bar' ? {
                    x: { display: false },
                    y: { display: false }
                } : {}
            }
        });
    }

    function setProgramHighlight(label, value) {
        if (!programHighlight) {
            return;
        }

        const valueNode = programHighlight.querySelector('.donut-value');
        const labelNode = programHighlight.querySelector('.donut-label');

        if (valueNode) {
            valueNode.textContent = `${value.toFixed(1)}%`;
        }

        if (labelNode) {
            labelNode.textContent = label;
        }
    }

    function resetProgramHighlight() {
        if (!programHighlight) {
            return;
        }

        const valueNode = programHighlight.querySelector('.donut-value');
        const labelNode = programHighlight.querySelector('.donut-label');

        if (valueNode) {
            valueNode.textContent = '--';
        }

        if (labelNode) {
            labelNode.textContent = 'Top Share';
        }
    }

    function renderProgramLegend(entries) {
        if (!programLegendList) {
            return;
        }

        programLegendList.innerHTML = '';

        entries.forEach(entry => {
            const li = document.createElement('li');
            const left = document.createElement('span');
            left.className = 'legend-left';

            const dot = document.createElement('span');
            dot.className = 'legend-dot';
            dot.style.backgroundColor = entry.color;

            const title = document.createElement('span');
            title.textContent = entry.label;

            left.appendChild(dot);
            left.appendChild(title);

            const valueSpan = document.createElement('span');
            valueSpan.className = 'legend-right';
            valueSpan.textContent = `${entry.value.toFixed(1)}%`;

            li.appendChild(left);
            li.appendChild(valueSpan);
            programLegendList.appendChild(li);
        });
    }

    async function handleSchoolYearChange(event) {
        const year = event.target.value;
        if (!year || year === '--') {
            return;
        }

        currentSchoolYear = year;
        root.classList.add('is-loading');
        try {
            const params = new URLSearchParams({ schoolYear: year });
            if (currentTermKey) {
                params.append('termKey', currentTermKey);
            }

            const response = await fetch(`/Admin/DashboardYearDetail?${params.toString()}`, {
                headers: { Accept: 'application/json' }
            });

            if (!response.ok) {
                throw new Error('Failed to fetch year detail');
            }

            const json = await response.json();
            applyYearDetail(json);
        } catch (error) {
            console.error(error);
        } finally {
            root.classList.remove('is-loading');
        }
    }

    async function handleTermChange(event) {
        const termKey = event.target.value;
        currentTermKey = termKey || '';

        if (!currentSchoolYear) {
            return;
        }

        root.classList.add('is-loading');
        try {
            const params = new URLSearchParams({ schoolYear: currentSchoolYear });
            if (currentTermKey) {
                params.append('termKey', currentTermKey);
            }

            const response = await fetch(`/Admin/DashboardYearDetail?${params.toString()}`, {
                headers: { Accept: 'application/json' }
            });

            if (!response.ok) {
                throw new Error('Failed to fetch term detail');
            }

            const json = await response.json();
            applyYearDetail(json);
        } catch (error) {
            console.error(error);
        } finally {
            root.classList.remove('is-loading');
        }
    }

    function bindEvents() {
        if (yearSelector) {
            yearSelector.addEventListener('change', handleSchoolYearChange);
        }

        if (termSelector) {
            termSelector.addEventListener('change', handleTermChange);
        }
    }

    function syncSelectors(detail) {
        const safeDetail = detail || {};
        const termOptions = Array.isArray(safeDetail.TermOptions) ? safeDetail.TermOptions : (safeDetail.termOptions || []);
        const selected = safeDetail.SelectedTermKey || safeDetail.selectedTermKey || '';

        if (termSelector) {
            const existingOptions = Array.from(termSelector.options).map(opt => opt.value);
            if (!arraysEqual(existingOptions, termOptions.map(opt => opt.TermKey ?? opt.termKey ?? ''))) {
                termSelector.innerHTML = '';

                if (!termOptions.length) {
                    const wholeOption = document.createElement('option');
                    wholeOption.value = '';
                    wholeOption.textContent = 'Whole Year';
                    termSelector.appendChild(wholeOption);
                } else {
                    termOptions.forEach(opt => {
                        const optionEl = document.createElement('option');
                        optionEl.value = opt.TermKey ?? opt.termKey ?? '';
                        optionEl.textContent = opt.Label ?? opt.label ?? 'Term';
                        termSelector.appendChild(optionEl);
                    });
                }
            }

            termSelector.value = selected || '';
        }
    }

    function syncChips(detail) {
        const safeDetail = detail || {};
        const label = safeDetail.TermOptions?.find?.(opt => (opt.TermKey ?? opt.termKey) === currentTermKey)?.Label
            || safeDetail.termOptions?.find?.(opt => (opt.TermKey ?? opt.termKey) === currentTermKey)?.label
            || (safeDetail.SchoolYear || safeDetail.schoolYear ? `${safeDetail.SchoolYear || safeDetail.schoolYear} - Whole Year` : 'Whole Year');

        if (yearChip) {
            yearChip.textContent = safeDetail.SchoolYear || safeDetail.schoolYear || currentSchoolYear || '--';
        }

        if (termChip) {
            termChip.textContent = label;
        }

        if (termDescriptor) {
            termDescriptor.textContent = label;
        }
    }

    function syncYearSelector(detail) {
        if (!yearSelector) {
            return;
        }

        const safeDetail = detail || {};
        const year = safeDetail.SchoolYear || safeDetail.schoolYear || currentSchoolYear;
        if (year) {
            yearSelector.value = year;
        }
    }

    function arraysEqual(a, b) {
        if (a.length !== b.length) {
            return false;
        }
        return a.every((val, index) => val === b[index]);
    }

    function init() {
        buildTrendChart();
        applyYearDetail(payload.detail);
        bindEvents();
    }

    init();
})();
