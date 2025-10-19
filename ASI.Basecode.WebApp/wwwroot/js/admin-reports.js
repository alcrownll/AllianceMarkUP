/* global Chart */
(function () {
    const root = document.getElementById('reports-page');
    if (!root) {
        return;
    }

    const state = {
        dashboard: window.AdminReportsInitial || null,
        selectedSchoolYear: null,
        selectedTermKey: '',
        selectedTeacherId: null,
        selectedStudentId: null,
        teacherSearch: '',
        teacherProgram: '',
        teacherDirectory: [],
        studentBaseOptions: []
    };

    const chartRegistry = new Map();

    const els = {
        tabs: Array.from(root.querySelectorAll('.reports-tab')),
        panels: Array.from(root.querySelectorAll('.tab-panel')),
        schoolYear: root.querySelector('[data-selector="school-year"]'),
        term: root.querySelector('[data-selector="term"]'),
        studentSelector: root.querySelector('[data-selector="student"]'),
        teacherSearch: root.querySelector('[data-filter="teacher-search"]'),
        teacherProgram: root.querySelector('[data-filter="teacher-department"]'),
        teacherTableBody: root.querySelector('[data-table="teacher-directory"] tbody'),
        teacherAssignmentsBody: root.querySelector('[data-table="teacher-assignments"] tbody'),
        teacherSubmissionList: root.querySelector('[data-list="teacher-submissions"]'),
        teacherPrint: root.querySelector('[data-action="print-teacher"]'),
        programLeaderboard: (() => {
            const node = root.querySelector('[data-list="program-leaderboard"]');
            if (!node) {
                return null;
            }
            return node.tagName === 'UL' ? node : node.querySelector('ul');
        })(),
        courseFailure: root.querySelector('[data-list="course-failure"]'),
        courseSuccess: root.querySelector('[data-list="course-success"]'),
        riskList: root.querySelector('[data-list="risk-indicators"]'),
        strengthContainer: root.querySelector('.chip-list.strength'),
        riskContainer: root.querySelector('.chip-list.risk'),
        studentGradesBody: root.querySelector('[data-table="student-grades"] tbody')
    };

    const numberFormatter = new Intl.NumberFormat();
    const percentFormatter = new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 });

    const field = (name) => root.querySelector(`[data-field="${name}"]`);

    async function fetchJson(url, params) {
        const query = params && Object.keys(params).length
            ? `?${new URLSearchParams(Object.entries(params).reduce((acc, [key, value]) => {
                if (value == null || value === '') {
                    return acc;
                }
                acc[key] = value;
                return acc;
            }, {})).toString()}`
            : '';

        const response = await fetch(`${url}${query}`, {
            headers: { Accept: 'application/json' }
        });
        if (!response.ok) {
            throw new Error(`Request failed: ${response.status}`);
        }

        return response.json();
    }

    function formatNumber(value) {
        const numeric = typeof value === 'number' && !Number.isNaN(value) ? value : 0;
        return numberFormatter.format(numeric);
    }

    function formatPercent(value, opts = {}) {
        const numeric = typeof value === 'number' && !Number.isNaN(value) ? value : 0;
        const prefix = opts.showSign && numeric > 0 ? '+' : '';
        return `${prefix}${percentFormatter.format(numeric)}%`;
    }

    function setField(name, value, fallback = '--') {
        const el = field(name);
        if (!el) {
            return;
        }
        el.textContent = value ?? fallback;
    }
    
    function activateTab(id) {
        els.tabs.forEach(tab => {
            const active = tab.dataset.tab === id;
            tab.classList.toggle('active', active);
        });

        els.panels.forEach(panel => {
            const active = panel.dataset.panel === id;
            panel.classList.toggle('active', active);
        });
    }

    function setLoading(isLoading) {
        root.classList.toggle('is-loading', Boolean(isLoading));
    }

    function renderOptions(select, items, selectedValue, placeholder) {
        if (!select) {
            return;
        }

        const normalizedSelected = selectedValue != null ? String(selectedValue) : null;
        const previousValue = select.value;
        select.innerHTML = '';

        if (placeholder) {
            const opt = document.createElement('option');
            opt.value = '';
            opt.textContent = placeholder;
            select.appendChild(opt);
        }

        items.forEach(item => {
            const opt = document.createElement('option');
            if (typeof item === 'string') {
                opt.value = item;
                opt.textContent = item;
                opt.selected = normalizedSelected != null ? normalizedSelected === item : false;
            } else {
                const value = item.value != null ? String(item.value) : '';
                opt.value = value;
                opt.textContent = item.label ?? value;
                opt.selected = normalizedSelected != null ? normalizedSelected === value : false;
            }
            select.appendChild(opt);
        });

        if (normalizedSelected == null && previousValue) {
            select.value = previousValue;
        }
    }

    async function refreshDashboard(options = {}) {
        const activateTabId = options.activateTabId;

        try {
            setLoading(true);
            const data = await fetchJson('/Admin/ReportsDashboard', {
                schoolYear: state.selectedSchoolYear,
                termKey: state.selectedTermKey
            });
            renderDashboard(data);
            if (activateTabId) {
                activateTab(activateTabId);
            }
        } catch (error) {
            console.error('Failed to refresh reports data', error);
        } finally {
            setLoading(false);
        }
    }

    function renderList(container, nodes, emptyMarkup) {
        if (!container) {
            return;
        }
        container.innerHTML = '';

        if (!nodes || !nodes.length) {
            container.innerHTML = emptyMarkup || '';
            return;
        }

        nodes.forEach(node => container.appendChild(node));
    }

    function renderChipList(container, values) {
        if (!container) {
            return;
        }
        container.innerHTML = '';
        if (!values || !values.length) {
            const emptyText = container.dataset.emptyText || 'No data available.';
            const chip = document.createElement('span');
            chip.className = 'chip chip-empty';
            chip.dataset.empty = 'true';
            chip.textContent = emptyText;
            container.appendChild(chip);
            return;
        }

        values.forEach(value => {
            const chip = document.createElement('span');
            chip.className = 'chip';
            chip.textContent = value;
            container.appendChild(chip);
        });
    }

    function setInvalid(select, isInvalid) {
        if (!select) {
            return;
        }
        select.classList.toggle('field-invalid', Boolean(isInvalid));
    }

    function bindTabEvents() {
        if (!els.tabs.length) {
            return;
        }

        els.tabs.forEach(tab => {
            tab.addEventListener('click', () => {
                const id = tab.dataset.tab;
                if (!id) {
                    return;
                }
                activateTab(id);
            });
        });
    }

    function bindFilterEvents() {
        if (els.teacherProgram) {
            els.teacherProgram.addEventListener('change', () => {
                state.teacherProgram = els.teacherProgram.value || '';
                renderTeacherDirectory(state.teacherDirectory);
            });
        }

        if (els.teacherSearch) {
            els.teacherSearch.addEventListener('input', () => {
                state.teacherSearch = els.teacherSearch.value?.toLowerCase().trim() || '';
                renderTeacherDirectory(state.teacherDirectory);
            });
        }
    }

    function bindEvents() {
        bindTabEvents();
        bindFilterEvents();
    }

    function ensureChart(id, type, labels, datasets, options, customizer) {
        const canvas = document.getElementById(id);
        if (!canvas || typeof Chart === 'undefined') {
            return;
        }

        const normalizedLabels = Array.isArray(labels) && labels.length ? labels : ['No data'];
        const normalizedDatasets = Array.isArray(datasets) && datasets.length
            ? datasets.map(ds => ({
                ...ds,
                data: Array.isArray(ds.data) && ds.data.length
                    ? ds.data.map(value => {
                        const numeric = Number(value);
                        return Number.isFinite(numeric) ? numeric : 0;
                    })
                    : [0]
            }))
            : [{
                label: 'No data',
                data: [0],
                backgroundColor: 'rgba(148, 163, 184, 0.45)',
                borderColor: 'rgba(148, 163, 184, 0.9)',
                borderDash: [4, 4]
            }];

        let chart = chartRegistry.get(id);
        const config = {
            type,
            data: { labels: normalizedLabels, datasets: normalizedDatasets },
            options: Object.assign({
                plugins: {
                    tooltip: {
                        callbacks: {
                            label(context) {
                                if (context.dataset.label === 'No data') {
                                    return 'No recorded values yet';
                                }
                                return context.formattedValue;
                            }
                        }
                    }
                }
            }, options)
        };

        if (typeof customizer === 'function') {
            customizer(config);
        }

        if (!chart) {
            chart = new Chart(canvas, config);
            chartRegistry.set(id, chart);
            return;
        }

        chart.data.labels = normalizedLabels;
        chart.data.datasets = normalizedDatasets;
        chart.options = Object.assign({}, chart.options, config.options);
        chart.update();
    }

    function renderOverall(overall) {
        const summary = overall?.Summary || {};
        setField('overall-total', formatNumber(summary.TotalEnrolled));
        setField('overall-growth', formatPercent(summary.GrowthPercent, { showSign: true }));
        setField('overall-gwa', Number.isFinite(summary.AverageGwa) ? summary.AverageGwa.toFixed(2) : '0.00');
        setField('overall-pass', formatPercent(summary.PassRatePercent));
        setField('overall-retention', formatPercent(summary.RetentionPercent));

        const trend = Array.isArray(overall?.EnrollmentTrend) ? overall.EnrollmentTrend : [];
        ensureChart('overallTrendChart', 'line', trend.map(item => item.Label || item.TermKey), [
            {
                label: 'Enrollment',
                data: trend.map(item => item?.Value ?? 0),
                borderColor: '#2563eb',
                backgroundColor: 'rgba(37, 99, 235, 0.15)',
                tension: 0.35,
                pointRadius: 3
            }
        ], {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
                x: { grid: { display: false } },
                y: { grid: { color: 'rgba(15, 23, 42, 0.08)' } }
            }
        });

        const genders = Array.isArray(overall?.Demographics?.GenderSplit) ? overall.Demographics.GenderSplit : [];
        ensureChart('demoGenderChart', 'doughnut', genders.map(item => item.Name || '--'), [
            {
                data: genders.map(item => item.Value ?? 0),
                backgroundColor: ['#2563eb', '#22c55e', '#f97316', '#6366f1'],
                borderWidth: 0
            }
        ], {
            plugins: { legend: { position: 'bottom' } }
        });

        const ageBands = Array.isArray(overall?.Demographics?.AgeBands) ? overall.Demographics.AgeBands : [];
        ensureChart('demoAgeChart', 'bar', ageBands.map(item => item.Name || '--'), [
            {
                label: 'Students',
                data: ageBands.map(item => item.Value ?? 0),
                backgroundColor: 'rgba(37, 99, 235, 0.75)',
                borderRadius: 8
            }
        ], {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
                x: { grid: { display: false } },
                y: { grid: { color: 'rgba(15, 23, 42, 0.08)' } }
            }
        });

        const statuses = Array.isArray(overall?.Demographics?.Statuses) ? overall.Demographics.Statuses : [];
        ensureChart('demoStatusChart', 'doughnut', statuses.map(item => item.Name || '--'), [
            {
                data: statuses.map(item => item.Value ?? 0),
                backgroundColor: ['#22c55e', '#f97316', '#6366f1', '#0ea5e9'],
                borderWidth: 0
            }
        ], {
            plugins: { legend: { position: 'bottom' } }
        });

        const leaderboardNodes = (overall?.ProgramLeaderboard || []).map(item => {
            const li = document.createElement('li');
            const growth = formatPercent(item.GrowthPercent, { showSign: true });
            const growthClass = (item.GrowthPercent ?? 0) >= 0 ? 'up' : 'down';
            li.innerHTML = `<span>${item.Program || '--'}</span><strong>${formatNumber(item.Enrollment)}</strong><small class="${growthClass}">${growth}</small>`;
            return li;
        });
        renderList(els.programLeaderboard, leaderboardNodes, '<li data-empty="true"><span>--</span><strong>0</strong><small>0%</small></li>');

        const failureNodes = (overall?.CourseOutcomes?.HighestFailureRates || []).map(item => {
            const li = document.createElement('li');
            li.textContent = `${item.CourseCode || '--'} `;
            const span = document.createElement('span');
            span.textContent = `${formatPercent(item.MetricValue)} fail`;
            li.appendChild(span);
            return li;
        });
        renderList(els.courseFailure, failureNodes, '<li data-empty="true">No data</li>');

        const successNodes = (overall?.CourseOutcomes?.BestPerforming || []).map(item => {
            const li = document.createElement('li');
            li.textContent = `${item.CourseCode || '--'} `;
            const span = document.createElement('span');
            span.textContent = `${formatPercent(item.MetricValue)} pass`;
            li.appendChild(span);
            return li;
        });
        renderList(els.courseSuccess, successNodes, '<li data-empty="true">No data</li>');

        const riskNodes = (overall?.RiskIndicators || []).map(item => {
            const div = document.createElement('div');
            div.className = 'risk-chip';
            div.textContent = `${formatNumber(item.Count)} ${item.Label}`;
            return div;
        });
        renderList(els.riskList, riskNodes, '<div class="risk-chip" data-empty="true">No risk indicators.</div>');

        const capacity = overall?.Capacity || {};
        setField('capacity-sections', formatNumber(capacity.SectionsNearCapacity));
        setField('capacity-faculty', formatNumber(capacity.FacultyHighLoad));
        setField('capacity-average', formatNumber(capacity.AverageClassSize));
    }

    function renderTeacherDirectory(directory) {
        state.teacherDirectory = Array.isArray(directory) ? directory : [];

        const programs = Array.from(new Set(state.teacherDirectory.map(item => item.Department).filter(Boolean))).sort();
        renderOptions(els.teacherProgram, programs.map(program => ({ value: program, label: program })), state.teacherProgram, 'All Programs');

        if (!els.teacherTableBody) {
            return;
        }

        const filtered = state.teacherDirectory.filter(item => {
            const matchesProgram = !state.teacherProgram || item.Department === state.teacherProgram;
            const matchesSearch = !state.teacherSearch || `${item.Name} ${item.Department}`.toLowerCase().includes(state.teacherSearch);
            return matchesProgram && matchesSearch;
        });

        els.teacherTableBody.innerHTML = '';

        if (!filtered.length) {
            els.teacherTableBody.innerHTML = '<tr data-empty="true"><td colspan="6" class="text-center">No teachers found.</td></tr>';
            return;
        }

        filtered.forEach(item => {
            const row = document.createElement('tr');
            row.dataset.id = String(item.TeacherId);
            row.classList.toggle('active', item.TeacherId === state.selectedTeacherId);
            row.innerHTML = `
                <td>${item.Name || '--'}</td>
                <td>${item.Department || '--'}</td>
                <td>${item.Email || '--'}</td>
                <td>${item.Rank || '--'}</td>
                <td>${formatNumber(item.LoadUnits)} units</td>
                <td>${formatNumber(item.Sections)}</td>`;
            row.addEventListener('click', () => updateTeacherSelection(item.TeacherId));
            els.teacherTableBody.appendChild(row);
        });
    }

    function renderTeacherDetail(detail) {
        const assignments = Array.isArray(detail?.Assignments) ? detail.Assignments : [];
        const submissionStatuses = Array.isArray(detail?.SubmissionStatuses) ? detail.SubmissionStatuses : [];

        setField('teacher-name', detail?.Name || 'Select a teacher');
        setField('teacher-meta', detail?.TeacherId ? `${detail.Rank || '--'} · ${detail.Department || '--'}` : '--');
        setField('teacher-load', `${formatNumber(detail?.TeachingLoadUnits)} units`);
        setField('teacher-sections', formatNumber(detail?.SectionCount));
        setField('teacher-pass', formatPercent(detail?.PassRatePercent));
        setField('teacher-submissions', formatPercent(detail?.SubmissionCompletionPercent));

        if (els.teacherAssignmentsBody) {
            els.teacherAssignmentsBody.innerHTML = '';
            if (!assignments.length) {
                els.teacherAssignmentsBody.innerHTML = '<tr data-empty="true"><td colspan="5" class="text-center">Select a teacher to load assignments.</td></tr>';
            } else {
                assignments.forEach(item => {
                    const row = document.createElement('tr');
                    row.innerHTML = `
                        <td>${item.CourseCode || '--'}</td>
                        <td>${item.Section || '--'}</td>
                        <td>${item.Schedule || '--'}</td>
                        <td>${formatNumber(item.Units)}</td>
                        <td>${formatNumber(item.Enrolled)}</td>`;
                    els.teacherAssignmentsBody.appendChild(row);
                });
            }
        }

        if (els.teacherSubmissionList) {
            els.teacherSubmissionList.innerHTML = '';
            if (!submissionStatuses.length) {
                const message = detail?.TeacherId ? 'No submission records.' : 'Select a teacher to load submission statuses.';
                els.teacherSubmissionList.innerHTML = `<li data-empty="true" class="pending">${message}</li>`;
            } else {
                submissionStatuses.forEach(item => {
                    const li = document.createElement('li');
                    li.className = item.IsComplete ? 'complete' : 'pending';
                    li.textContent = `${item.CourseCode || '--'} · ${item.Status || '--'}`;
                    els.teacherSubmissionList.appendChild(li);
                });
            }
        }

        const submissionSummaryRaw = Array.isArray(detail?.SubmissionSummary) ? detail.SubmissionSummary : [];
        const submissionSummary = submissionSummaryRaw.length ? submissionSummaryRaw : [
            { Name: 'All grades submitted', Value: 0 },
            { Name: 'Some grades are ungraded', Value: 0 }
        ];
        const submissionTotal = submissionSummary.reduce((sum, item) => sum + (Number(item?.Value) || 0), 0);
        const submissionLabels = submissionSummary.map(item => item?.Name || '--');
        const submissionData = submissionSummary.map(item => Number(item?.Value) || 0);

        ensureChart('teacherSubmissionChart', 'doughnut', submissionLabels, [
            {
                data: submissionData,
                borderWidth: 0,
                hoverOffset: 12
            }
        ], {
            plugins: {
                legend: { position: 'bottom' },
                tooltip: {
                    callbacks: {
                        label(context) {
                            const value = context.parsed || 0;
                            const percent = submissionTotal > 0 ? (value / submissionTotal) * 100 : 0;
                            return `${context.label}: ${value.toLocaleString()} (${percent.toFixed(1)}%)`;
                        }
                    }
                }
            },
            animation: {
                animateScale: true,
                animateRotate: true
            }
        }, (config) => {
            const palette = [
                'rgba(37, 99, 235, 0.85)',
                'rgba(249, 115, 22, 0.85)',
                'rgba(14, 165, 233, 0.85)'
            ];
            const faded = [
                'rgba(37, 99, 235, 0.25)',
                'rgba(249, 115, 22, 0.25)',
                'rgba(14, 165, 233, 0.25)'
            ];

            const dataset = config.data?.datasets?.[0];
            if (!dataset || !Array.isArray(dataset.data) || !dataset.data.length) {
                return;
            }

            const colorForIndex = (idx, source) => source[idx % source.length];
            const applyColors = (colors) => {
                dataset.backgroundColor = dataset.data.map((_, idx) => colorForIndex(idx, colors));
                dataset.hoverBackgroundColor = dataset.data.map((_, idx) => colorForIndex(idx, palette));
            };

            applyColors(palette);

            config.options = config.options || {};
            config.options.onHover = (event, elements, chart) => {
                chart.canvas.style.cursor = elements.length ? 'pointer' : 'default';
                if (!elements.length) {
                    applyColors(palette);
                    chart.update('none');
                    return;
                }

                const activeIndex = elements[0].index;
                dataset.backgroundColor = dataset.data.map((_, idx) => idx === activeIndex ? colorForIndex(idx, palette) : colorForIndex(idx, faded));
                chart.update('none');
            };

            config.options.onLeave = (event, elements, chart) => {
                chart.canvas.style.cursor = 'default';
                applyColors(palette);
                chart.update('none');
            };
        });

        const passRates = Array.isArray(detail?.CoursePassRates) ? detail.CoursePassRates : [];
        ensureChart('teacherPassChart', 'bar', passRates.map(item => item.CourseCode || '--'), [
            {
                label: 'Pass %',
                data: passRates.map(item => item.PassRatePercent || 0),
                backgroundColor: ['#2563eb', '#22c55e', '#f97316', '#a855f7'],
                borderRadius: 6
            }
        ], {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
                y: { beginAtZero: true, max: 100 }
            }
        });
    }

    function renderStudent(analytics) {
        const gradeBreakdown = Array.isArray(analytics?.GradeBreakdown) ? analytics.GradeBreakdown : [];

        if (els.studentGradesBody) {
            els.studentGradesBody.innerHTML = '';
            if (!gradeBreakdown.length) {
                els.studentGradesBody.innerHTML = '<tr data-empty="true"><td colspan="8" class="text-center">Select a student.</td></tr>';
            } else {
                gradeBreakdown.forEach(row => {
                    const tr = document.createElement('tr');
                    const formatGwa = (value) => (value != null ? value.toFixed(2) : '--');
                    tr.innerHTML = `
                        <td>${row.EdpCode || '--'}</td>
                        <td>${row.Subject || '--'}</td>
                        <td>${formatGwa(row.Prelim)}</td>
                        <td>${formatGwa(row.Midterm)}</td>
                        <td>${formatGwa(row.Prefinal)}</td>
                        <td>${formatGwa(row.Final)}</td>
                        <td>${formatGwa(row.FinalGrade)}</td>
                        <td class="status-${(row.Status || '').toLowerCase()}">${row.Status || '--'}</td>`;
                    els.studentGradesBody.appendChild(tr);
                });
            }
        }

        const courseGrades = Array.isArray(analytics?.CourseGrades) ? analytics.CourseGrades : [];
        ensureChart('studentCourseChart', 'bar', courseGrades.map(item => item.CourseCode || '--'), [
            {
                label: 'Grade',
                data: courseGrades.map(item => item.Grade || 0),
                backgroundColor: 'rgba(14, 165, 233, 0.65)',
                borderRadius: 6,
                hoverBackgroundColor: '#0284c7'
            }
        ], {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: false },
                tooltip: {
                    callbacks: {
                        label(context) {
                            return `Grade: ${(context.parsed.y ?? 0).toFixed(2)}`;
                        }
                    }
                }
            },
            scales: {
                y: { reverse: true, min: 1, max: 5 }
            },
            hover: {
                mode: 'nearest',
                intersect: true
            }
        });

        const statusMix = Array.isArray(analytics?.StatusMix) ? analytics.StatusMix : [];
        const passFailIncompleteData = statusMix.filter(item => ['pass', 'fail', 'incomplete'].includes((item.Name || '').toLowerCase()));
        ensureChart('studentStatusChart', 'doughnut', passFailIncompleteData.map(item => item.Name || '--'), [
            {
                data: passFailIncompleteData.map(item => item.Value || 0),
                backgroundColor: [
                    'rgba(34, 197, 94, 0.55)',
                    'rgba(239, 68, 68, 0.55)',
                    'rgba(148, 163, 184, 0.55)'
                ],
                hoverBackgroundColor: ['#16a34a', '#dc2626', '#475569'],
                borderWidth: 0,
                hoverOffset: 8
            }
        ], {
            plugins: {
                legend: { position: 'bottom' },
                tooltip: {
                    callbacks: {
                        label(context) {
                            const total = context.dataset.data.reduce((sum, value) => sum + value, 0) || 1;
                            const value = context.parsed || 0;
                            const percent = (value / total) * 100;
                            return `${context.label}: ${value.toLocaleString()} (${percent.toFixed(1)}%)`;
                        }
                    }
                }
            },
            animation: {
                animateScale: true,
                animateRotate: true
            }
        });

        renderChipList(els.strengthContainer, Array.isArray(analytics?.Strengths) ? analytics.Strengths : []);
        renderChipList(els.riskContainer, Array.isArray(analytics?.Risks) ? analytics.Risks : []);
    }

    function filterStudents() {
        const base = Array.isArray(state.studentBaseOptions) ? state.studentBaseOptions : [];
        return base;
    }

    function applyStudentFilters(opts = {}) {
        const filtered = filterStudents();

        renderOptions(els.studentSelector, filtered.map(option => ({
            value: option.StudentId,
            label: `${option.Name || 'Unknown'}${option.Program ? ` (${option.Program})` : ''}`
        })), state.selectedStudentId ?? '', 'Select Student');

        if (els.studentSelector) {
            els.studentSelector.disabled = !filtered.length;
        }

        const hasSelection = filtered.some(option => option.StudentId === state.selectedStudentId);

        if (!hasSelection) {
            state.selectedStudentId = null;
            if (els.studentSelector) {
                els.studentSelector.value = '';
            }
            if (opts.clearView) {
                renderStudent(null);
            }
        }

        if (opts.reloadAnalytics && hasSelection && state.selectedStudentId) {
            loadStudentAnalytics();
        }
    }
    
    function renderDashboard(dashboard) {
        state.dashboard = dashboard || {};
        const schoolYears = Array.isArray(dashboard?.AvailableSchoolYears) ? dashboard.AvailableSchoolYears : [];
        const incomingSchoolYear = dashboard?.SchoolYear || '';
        if (incomingSchoolYear) {
            state.selectedSchoolYear = incomingSchoolYear;
        } else if (!state.selectedSchoolYear && schoolYears.length) {
            state.selectedSchoolYear = schoolYears[schoolYears.length - 1];
        }

        const incomingTermKey = dashboard?.TermKey ?? '';
        if (incomingTermKey || state.selectedTermKey == null) {
            state.selectedTermKey = incomingTermKey;
        }
        state.selectedTeacherId = dashboard?.Teacher?.SelectedTeacher?.TeacherId ?? state.selectedTeacherId ?? null;
        state.selectedStudentId = dashboard?.Student?.SelectedStudentId ?? state.selectedStudentId ?? null;

        renderOptions(els.schoolYear, schoolYears, state.selectedSchoolYear, schoolYears.length ? null : '--');

        const termOptions = Array.isArray(dashboard?.TermOptions) ? dashboard.TermOptions : [];
        renderOptions(els.term, termOptions.map(option => ({
            value: option.TermKey ?? '',
            label: option.Label ?? option.TermKey ?? ''
        })), state.selectedTermKey ?? '', termOptions.length ? null : 'Whole Year');

        state.studentBaseOptions = Array.isArray(dashboard?.Student?.Students) ? dashboard.Student.Students : [];
        state.teacherDirectory = Array.isArray(dashboard?.Teacher?.Directory) ? dashboard.Teacher.Directory : [];

        applyStudentFilters({ clearView: true });

        renderOverall(dashboard.Overall);
        renderTeacherDirectory(state.teacherDirectory);

        let teacherDetail = dashboard?.Teacher?.SelectedTeacher;
        let teacherRendered = false;

        if (teacherDetail?.TeacherId) {
            if (!state.selectedTeacherId) {
                state.selectedTeacherId = teacherDetail.TeacherId;
            }

            if (teacherDetail.TeacherId === state.selectedTeacherId) {
                renderTeacherDetail(teacherDetail);
                teacherRendered = true;
            }
        }

        if (!state.selectedTeacherId && state.teacherDirectory.length) {
            state.selectedTeacherId = state.teacherDirectory[0].TeacherId;
        }

        syncTeacherRowSelection();

        if (!teacherRendered) {
            if (state.selectedTeacherId) {
                updateTeacherSelection(state.selectedTeacherId);
            }
            else {
                renderTeacherDetail(null);
            }
        }

        let studentRendered = false;
        if (dashboard.Student?.Analytics && dashboard.Student?.SelectedStudentId) {
            if (!state.selectedStudentId) {
                state.selectedStudentId = dashboard.Student.SelectedStudentId;
            }

            if (dashboard.Student.SelectedStudentId === state.selectedStudentId) {
                renderStudent(dashboard.Student.Analytics);
                studentRendered = true;
            }
        }

        if (!state.selectedStudentId && state.studentBaseOptions.length) {
            const initialStudent = state.studentBaseOptions[0];
            state.selectedStudentId = initialStudent.StudentId;
            if (els.studentSelector) {
                els.studentSelector.value = String(initialStudent.StudentId);
            }
        }

        if (!studentRendered) {
            if (state.selectedStudentId) {
                loadStudentAnalytics();
            }
            else {
                renderStudent(null);
            }
        }
    }

    async function loadStudentAnalytics() {
        const studentId = state.selectedStudentId;
        if (!studentId) {
            renderStudent(null);
            return;
        }

        try {
            setLoading(true);
            const data = await fetchJson('/Admin/ReportsStudentAnalytics', {
                studentId,
                schoolYear: state.selectedSchoolYear,
                termKey: state.selectedTermKey
            });
            renderStudent(data);
        } catch (error) {
            console.error('Failed to load student analytics', error);
            renderStudent(null);
        } finally {
            setLoading(false);
        }
    }

    if (els.studentSelector) {
        els.studentSelector.addEventListener('change', () => {
            const value = els.studentSelector.value;
            state.selectedStudentId = value ? Number(value) : null;
            if (state.selectedStudentId) {
                loadStudentAnalytics();
            } else {
                renderStudent(null);
            }
        });
    }

    function syncTeacherRowSelection() {
        if (!els.teacherTableBody) {
            return;
        }

        Array.from(els.teacherTableBody.querySelectorAll('tr')).forEach(row => {
            const id = Number(row.dataset?.id);
            row.classList.toggle('active', id === state.selectedTeacherId);
        });
    }

    async function updateTeacherSelection(teacherId) {
        if (!teacherId) {
            return;
        }

        state.selectedTeacherId = teacherId;
        syncTeacherRowSelection();

        try {
            setLoading(true);
            const detail = await fetchJson('/Admin/ReportsTeacherDetail', {
                teacherId,
                schoolYear: state.selectedSchoolYear,
                termKey: state.selectedTermKey
            });
            renderTeacherDetail(detail);
        } catch (error) {
            console.error('Failed to load teacher detail', error);
        } finally {
            setLoading(false);
        }
    }

    function bindTeacherPrint() {
        if (!els.teacherPrint) {
            return;
        }

        els.teacherPrint.addEventListener('click', () => {
            if (!state.selectedTeacherId) {
                window.alert('Select a teacher first to print their teaching assignments.');
                return;
            }

            window.print();
        });
    }

    function initialize() {
        bindEvents();
        bindTeacherPrint();
        if (state.dashboard) {
            renderDashboard(state.dashboard);
        }
        activateTab('overall');
        refreshDashboard({ activateTabId: 'overall' });
        const reportNav = document.querySelector('.sidebar a[data-label="Reports"], .sidebar [data-label="Reports"]');
        if (reportNav) {
            reportNav.addEventListener('click', async () => {
                refreshDashboard({ activateTabId: 'overall' });
            });
        }
    }

    initialize();
})();
