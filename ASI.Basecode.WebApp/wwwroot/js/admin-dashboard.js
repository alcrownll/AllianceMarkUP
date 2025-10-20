(function () {
    const root = document.getElementById('admin-dashboard');
    if (!root || typeof Chart === 'undefined') {
        return;
    }

    // Dashboard charts: renders enrollment, GPA, and pass/fail visual summaries
    const payload = window.AdminDashboardInitial || {};
    const yearSelector = document.getElementById('yearSelector');
    const termSelector = document.getElementById('termSelector');
    const yearChip = root.querySelector('[data-year-display]');
    const termChip = root.querySelector('[data-term-display]');
    const termDescriptor = document.getElementById('termDescriptor');

    const charts = {
        trend: null,
        gpa: null,
        passFail: null
    };


    const colorPalette = ['#2563eb', '#22c55e', '#f97316', '#a855f7', '#0ea5e9', '#f59e0b', '#ec4899'];

    let currentDetail = payload.detail || {};
    let currentSchoolYear = payload.selectedSchoolYear || currentDetail.SchoolYear || currentDetail.schoolYear || '';
    let currentTermKey = payload.selectedTermKey || currentDetail.SelectedTermKey || currentDetail.selectedTermKey || '';

    function updateSubjectEnrollmentChart(enrollments) {
        const ctx = document.getElementById('enrollmentTrendChart');
        if (!ctx) {
            return;
        }

        charts.trend?.destroy();

        const items = Array.isArray(enrollments) ? enrollments : [];
        if (!items.length) {
            charts.trend = createEmptyChart(ctx, 'bar', 'No data');
            return;
        }

        const labels = items.map(item => {
            const code = item.courseCode || item.CourseCode || 'N/A';
            const name = item.courseName || item.CourseName || '';
            return name && name !== code ? `${code} • ${name}` : code;
        });
        const counts = items.map(item => toNumber(item.studentCount ?? item.StudentCount, 0));
        const barThickness = Math.max(12, Math.floor(260 / items.length));

        charts.trend = new Chart(ctx, {
            type: 'bar',
            data: {
                labels,
                datasets: [
                    {
                        label: 'Students Enrolled',
                        data: counts,
                        backgroundColor: '#2563eb',
                        borderRadius: 6,
                        barThickness,
                        maxBarThickness: 48
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
                                const value = context.parsed.y ?? context.parsed;
                                return `Students: ${Number(value || 0).toLocaleString()}`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        ticks: {
                            color: '#475569',
                            maxRotation: 45,
                            minRotation: 0,
                            autoSkip: false,
                            callback(value, index) {
                                const label = labels[index] || '';
                                return label.length > 28 ? `${label.slice(0, 25)}…` : label;
                            }
                        },
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
            totals.passed += toNumber(r.passed ?? r.Passed, 0);
            totals.failed += toNumber(r.failed ?? r.Failed, 0);
            totals.incomplete += toNumber(r.incomplete ?? r.Incomplete, 0);
            return totals;
        }, { passed: 0, failed: 0, incomplete: 0 });
    }

    function toNumber(value) {
        const n = Number(value);
        return Number.isFinite(n) ? n : 0;
    }

    function clampGpa(value) {
        const numeric = toNumber(value);
        if (!Number.isFinite(numeric) || numeric === 0) {
            return null;
        }

        return Math.min(Math.max(numeric, 1), 5);
    }

    // Render Data
    function applyYearDetail(detail) {
        const safeDetail = detail || {};

        currentDetail = safeDetail;
        currentSchoolYear = safeDetail.schoolYear || safeDetail.SchoolYear || currentSchoolYear;
        currentTermKey = safeDetail.selectedTermKey || safeDetail.SelectedTermKey || '';
        payload.detail = safeDetail;
        payload.selectedTermKey = currentTermKey;

        // Handle both PascalCase (C#) and camelCase property names
        const subjectEnrollments = safeDetail.subjectEnrollments || safeDetail.SubjectEnrollments || [];
        const subjectAverageGpa = safeDetail.subjectAverageGpa || safeDetail.SubjectAverageGpa || [];
        const passFailRates = safeDetail.passFailRates || safeDetail.PassFailRates || [];

        console.log('Applying year detail:', {
            enrollments: subjectEnrollments,
            gpa: subjectAverageGpa,
            passFail: passFailRates
        });

        updateSubjectEnrollmentChart(subjectEnrollments);
        updateAverageGpaChart(subjectAverageGpa);
        updatePassFailChart(passFailRates);
        syncSelectors(safeDetail);
        syncChips(safeDetail);
        syncYearSelector(safeDetail);
    }

    function updateAverageGpaChart(points) {
        const ctx = document.getElementById('averageGpaChart');
        if (!ctx) {
            return;
        }

        charts.gpa?.destroy();

        const items = Array.isArray(points) ? points : [];
        const entries = items.map(item => {
            const label = (() => {
                const code = item.courseCode || item.CourseCode || 'N/A';
                const name = item.courseName || item.CourseName || '';
                return name && name !== code ? `${code} • ${name}` : code;
            })();

            return {
                label,
                value: clampGpa(item.averageGpa ?? item.AverageGpa)
            };
        }).filter(entry => entry.value !== null);

        if (!entries.length) {
            charts.gpa = createEmptyChart(ctx, 'bar', 'No data');
            return;
        }

        const labels = entries.map(entry => entry.label);
        const data = entries.map(entry => 6 - entry.value);

        charts.gpa = new Chart(ctx, {
            type: 'bar',
            options: {  
                responsive: true,
                maintainAspectRatio: false,
                resizeDelay: 200,
                devicePixelRatio: Math.min(window.devicePixelRatio, 2),
                indexAxis: 'y',
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label(context) {
                                const index = context.dataIndex ?? context.parsed.y;
                                const original = entries[index]?.value ?? 0;
                                return `Average GPA: ${Number(original || 0).toFixed(2)}`;
                            }
                        }
                    }
                },
                layout: {
                    padding: {
                        top: 16,
                        bottom: 16,
                        left: 12,
                        right: 12
                    }
                },
                scales: {
                    x: {
                        min: 1,
                        max: 5,
                        ticks: {
                            color: '#475569',
                            stepSize: 1,
                            callback(value) {
                                return Number(6 - value).toFixed(1);
                            }
                        },
                        grid: {
                            color: 'rgba(148, 163, 184, 0.25)',
                            drawBorder: false,
                            tickLength: 0
                        }
                    },
                    y: {
                        ticks: {
                            color: '#475569',
                            callback(value, index) {
                                const label = labels[index] || '';
                                return label.length > 32 ? `${label.slice(0, 29)}…` : label;
                            }
                        },
                        grid: { display: false },
                        afterFit(axis) {
                            axis.width += 20;
                        }
                    }
                }
            },
            data: {
                labels,
                datasets: [
                    {
                        label: 'Average GPA',
                        data,
                        backgroundColor: labels.map((_, idx) => colorPalette[idx % colorPalette.length]),
                        borderRadius: 8,
                        barThickness: Math.max(24, Math.floor(260 / Math.max(entries.length, 1))),
                        maxBarThickness: 48,
                        borderSkipped: false,
                        categoryPercentage: 0.6,
                        barPercentage: 0.9
                    }
                ]
            }
        });
    }
    // if no Chart Values (Pass vs Fail)
    function updatePassFailChart(rates) {
        const ctx = document.getElementById('passFailChart');
        if (!ctx) {
            return;
        }

        charts.passFail?.destroy();

        const items = Array.isArray(rates) ? rates : [];
        if (!items.length) {
            charts.passFail = createEmptyChart(ctx, 'doughnut', 'No data');
            return;
        }   

        const totals = aggregatePassFail(items);
        const data = [totals.passed, totals.failed];
        const totalStudents = data.reduce((sum, current) => sum + current, 0);
        if (totalStudents <= 0) {
            charts.passFail = createEmptyChart(ctx, 'doughnut', 'No data');
            return;
        }
        const labels = ['Pass', 'Fail'];

        charts.passFail = new Chart(ctx, {
            type: 'doughnut',
            options: {
                responsive: true,
                maintainAspectRatio: false,
                resizeDelay: 200,
                devicePixelRatio: Math.min(window.devicePixelRatio, 2),
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
                layout: {
                    padding: {
                        top: 8,
                        bottom: 8,
                        left: 8,
                        right: 8
                    }
                },
                cutout: '55%'
            },
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
                        backgroundColor: ['rgba(148, 163, 184, 0.2)'],
                        borderWidth: 2,
                        borderColor: ['rgba(148, 163, 184, 0.3)']
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: true,
                        position: 'top',
                        labels: {
                            color: '#94a3b8',
                            font: { size: 14, weight: '500' },
                            padding: 20,
                            generateLabels: () => [{
                                text: message,
                                fillStyle: 'transparent',
                                strokeStyle: 'transparent',
                                fontColor: '#64748b'
                            }]
                        }
                    },
                    tooltip: { enabled: false }
                },
                scales: type === 'bar' ? {
                    x: { display: false },
                    y: { display: false }
                } : {}
            }
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
        const termOptions = Array.isArray(safeDetail.termOptions) ? safeDetail.termOptions : (safeDetail.TermOptions || []);
        const selected = safeDetail.selectedTermKey || safeDetail.SelectedTermKey || '';

        if (termSelector) {
            const existingOptions = Array.from(termSelector.options).map(opt => opt.value);
            if (!arraysEqual(existingOptions, termOptions.map(opt => opt.termKey ?? opt.TermKey ?? ''))) {
                termSelector.innerHTML = '';

                if (!termOptions.length) {
                    const wholeOption = document.createElement('option');
                    wholeOption.value = '';
                    wholeOption.textContent = 'Whole Year';
                    termSelector.appendChild(wholeOption);
                } else {
                    termOptions.forEach(opt => {
                        const optionEl = document.createElement('option');
                        optionEl.value = opt.termKey ?? opt.TermKey ?? '';
                        optionEl.textContent = opt.label ?? opt.Label ?? 'Term';
                        termSelector.appendChild(optionEl);
                    });
                }
            }

            termSelector.value = selected || '';
        }
    }

    function syncChips(detail) {
        const safeDetail = detail || {};
        const label = safeDetail.termOptions?.find?.(opt => (opt.termKey ?? opt.TermKey) === currentTermKey)?.label
            || safeDetail.TermOptions?.find?.(opt => (opt.termKey ?? opt.TermKey) === currentTermKey)?.Label
            || (safeDetail.schoolYear || safeDetail.SchoolYear ? `${safeDetail.schoolYear || safeDetail.SchoolYear} - Whole Year` : 'Whole Year');

        if (yearChip) {
            yearChip.textContent = safeDetail.schoolYear || safeDetail.SchoolYear || currentSchoolYear || '--';
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
        const year = safeDetail.schoolYear || safeDetail.SchoolYear || currentSchoolYear;
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
        console.log('Admin Dashboard Initialized');
        applyYearDetail(payload.detail);
        bindEvents();
    }

    init();
})();