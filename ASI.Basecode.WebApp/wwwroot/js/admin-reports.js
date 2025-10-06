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
        studentTermKey: '',
        teacherSearch: '',
        teacherProgram: '',
        teacherDirectory: []
    };

    const chartRegistry = new Map();

    const els = {
        tabs: Array.from(root.querySelectorAll('.reports-tab')),
        panels: Array.from(root.querySelectorAll('.tab-panel')),
        schoolYear: root.querySelector('[data-selector="school-year"]'),
        term: root.querySelector('[data-selector="term"]'),
        studentSelector: root.querySelector('[data-selector="student"]'),
        studentTerm: root.querySelector('[data-selector="student-term"]'),
        teacherSearch: root.querySelector('[data-filter="teacher-search"]'),
        teacherProgram: root.querySelector('[data-filter="teacher-department"]'),
        teacherTableBody: root.querySelector('[data-table="teacher-directory"] tbody'),
        teacherAssignmentsBody: root.querySelector('[data-table="teacher-assignments"] tbody'),
        teacherSubmissionList: root.querySelector('[data-list="teacher-submissions"]'),
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
        studentSnapshotBody: root.querySelector('[data-table="student-snapshot"] tbody'),
        strengthContainer: root.querySelector('.chip-list.strength'),
        riskContainer: root.querySelector('.chip-list.risk')
    };

    const numberFormatter = new Intl.NumberFormat();
    const percentFormatter = new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 });

    const field = (name) => root.querySelector(`[data-field="${name}"]`);

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
            return;
        }

        values.forEach(value => {
            const chip = document.createElement('span');
            chip.className = 'chip';
            chip.textContent = value;
            container.appendChild(chip);
        });
    }

    function ensureChart(id, type, labels, datasets, options) {
        const canvas = document.getElementById(id);
        if (!canvas || typeof Chart === 'undefined') {
            return;
        }

        let chart = chartRegistry.get(id);
        if (!chart) {
            chart = new Chart(canvas, {
                type,
                data: { labels, datasets },
                options
            });
            chartRegistry.set(id, chart);
            return;
        }

        chart.data.labels = labels;
        chart.data.datasets = datasets;
        chart.options = Object.assign({}, chart.options, options);
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
            if (item.TeacherId === state.selectedTeacherId) {
                row.classList.add('active');
            }
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
        if (!detail) {
            setField('teacher-name', 'Select a teacher');
            setField('teacher-meta', '--');
            return;
        }

        setField('teacher-name', detail.Name || 'Select a teacher');
        setField('teacher-meta', detail.TeacherId ? `${detail.Rank || '--'} · ${detail.Department || '--'}` : '--');
        setField('teacher-load', `${formatNumber(detail.TeachingLoadUnits)} units`);
        setField('teacher-sections', formatNumber(detail.SectionCount));
        setField('teacher-pass', formatPercent(detail.PassRatePercent));
        setField('teacher-submissions', formatPercent(detail.SubmissionCompletionPercent));

        if (els.teacherAssignmentsBody) {
            const assignments = Array.isArray(detail.Assignments) ? detail.Assignments : [];
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
            const submissions = Array.isArray(detail.SubmissionStatuses) ? detail.SubmissionStatuses : [];
            els.teacherSubmissionList.innerHTML = '';
            if (!submissions.length) {
                els.teacherSubmissionList.innerHTML = '<li data-empty="true">No submission records.</li>';
            } else {
                submissions.forEach(item => {
                    const li = document.createElement('li');
                    li.className = item.IsComplete ? 'complete' : 'pending';
                    li.textContent = `${item.CourseCode || '--'} · ${item.Status || '--'}`;
                    els.teacherSubmissionList.appendChild(li);
                });
            }
        }

        const passRates = Array.isArray(detail.CoursePassRates) ? detail.CoursePassRates : [];
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
        if (!analytics) {
            return;
        }

        const gwaTrend = Array.isArray(analytics.GwaTrend) ? analytics.GwaTrend : [];
        ensureChart('studentGwaChart', 'line', gwaTrend.map(item => item.Label || item.TermKey), [
            {
                label: 'GWA',
                data: gwaTrend.map(item => item.Gwa || 0),
                borderColor: '#1d4ed8',
                backgroundColor: 'rgba(29, 78, 216, 0.2)',
                tension: 0.35,
                pointRadius: 3
            }
        ], {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
                y: { reverse: true, min: 1, max: 3 }
            }
        });

        const courseGrades = Array.isArray(analytics.CourseGrades) ? analytics.CourseGrades : [];
        ensureChart('studentCourseChart', 'bar', courseGrades.map(item => item.CourseCode || '--'), [
            {
                label: 'Grade',
                data: courseGrades.map(item => item.Grade || 0),
                backgroundColor: '#0ea5e9',
                borderRadius: 6
            }
        ], {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
                y: { reverse: true, min: 1, max: 3.5 }
            }
        });

        const statusMix = Array.isArray(analytics.StatusMix) ? analytics.StatusMix : [];
        ensureChart('studentStatusChart', 'doughnut', statusMix.map(item => item.Name || '--'), [
            {
                data: statusMix.map(item => item.Value || 0),
                backgroundColor: ['#22c55e', '#ef4444', '#facc15'],
                borderWidth: 0
            }
        ], {
            plugins: { legend: { position: 'bottom' } }
        });

        const progress = analytics.UnitsProgress || { EarnedUnits: 0, RequiredUnits: 0 };
        const earned = progress.EarnedUnits || 0;
        const required = progress.RequiredUnits || 0;
        const percent = required > 0 ? Math.min(100, Math.round((earned / required) * 100)) : 0;
        const progressBar = field('student-units-progress');
        if (progressBar) {
            progressBar.style.width = `${percent}%`;
            progressBar.setAttribute('aria-valuenow', String(percent));
            progressBar.textContent = `${percent}%`;
        }
        setField('student-units-label', `${formatNumber(earned)} of ${formatNumber(required)} required units completed.`);
        setField('student-attendance', formatPercent(analytics.Engagement?.AttendancePercent));
        setField('student-submissions', formatPercent(analytics.Engagement?.OnTimeSubmissionPercent));
        setField('student-missing', formatNumber(analytics.Engagement?.MissingWorkCount));
        setField('summary-it-pass', formatNumber(0));
        setField('summary-cs-pass', formatNumber(0));
        setField('summary-ungraded', formatNumber(analytics.Engagement?.MissingWorkCount || 0));

        renderChipList(els.strengthContainer, analytics.Strengths || []);
        renderChipList(els.riskContainer, analytics.Risks || []);

        if (els.studentSnapshotBody) {
            els.studentSnapshotBody.innerHTML = '<tr data-empty="true"><td colspan="9" class="text-center">No records yet.</td></tr>';
        }
    }

    function renderDashboard(dashboard) {
        state.dashboard = dashboard || {};
        const schoolYears = Array.isArray(dashboard?.AvailableSchoolYears) ? dashboard.AvailableSchoolYears : [];
        state.selectedSchoolYear = dashboard?.SchoolYear || state.selectedSchoolYear || schoolYears[schoolYears.length - 1] || '';
        state.selectedTermKey = dashboard?.TermKey ?? state.selectedTermKey ?? '';
        state.selectedTeacherId = dashboard?.Teacher?.SelectedTeacher?.TeacherId ?? state.selectedTeacherId ?? null;
        state.selectedStudentId = dashboard?.Student?.SelectedStudentId ?? state.selectedStudentId ?? null;

        renderOptions(els.schoolYear, schoolYears, state.selectedSchoolYear, schoolYears.length ? null : '--');

        const termOptions = Array.isArray(dashboard?.TermOptions) ? dashboard.TermOptions : [];
        renderOptions(els.term, termOptions.map(option => ({
            value: option.TermKey ?? '',
            label: option.Label ?? option.TermKey ?? ''
        })), state.selectedTermKey ?? '', termOptions.length ? null : 'Whole Year');

        const studentOptions = Array.isArray(dashboard?.Student?.Students) ? dashboard.Student.Students : [];
        if (state.selectedStudentId == null && studentOptions.length) {
            state.selectedStudentId = studentOptions[0].StudentId;
        }
        renderOptions(els.studentSelector, studentOptions.map(option => ({
            value: option.StudentId,
            label: `${option.Name || 'Unknown'}${option.Program ? ` (${option.Program})` : ''}`
        })), state.selectedStudentId ?? '', studentOptions.length ? null : 'No students');
        if (els.studentSelector) {
            els.studentSelector.disabled = !studentOptions.length;
        }

        renderOptions(els.studentTerm, termOptions.filter(option => option.TermKey).map(option => ({
            value: option.TermKey,
            label: option.Label ?? option.TermKey
        })), state.studentTermKey ?? '', 'All Terms');

        renderOverall(dashboard.Overall);
        renderTeacherDirectory(dashboard.Teacher?.Directory);
        renderTeacherDetail(dashboard.Teacher?.SelectedTeacher);
        renderStudent(dashboard.Student?.Analytics);
    }

    function queryString(params) {
        const query = new URLSearchParams();
        Object.entries(params || {}).forEach(([key, value]) => {
            if (value === undefined || value === null || value === '') {
                return;
            }
            query.append(key, value);
        });
        const str = query.toString();
        return str ? `?${str}` : '';
    }

    async function fetchJson(endpoint, params) {
        const response = await fetch(`${endpoint}${queryString(params)}`, {
            headers: { Accept: 'application/json' }
        });

        if (!response.ok) {
            throw new Error(`Request failed (${response.status})`);
        }

        return response.json();
    }

    async function reloadDashboard() {
        try {
            setLoading(true);
            const data = await fetchJson('/Admin/ReportsDashboard', {
                schoolYear: state.selectedSchoolYear,
                termKey: state.selectedTermKey,
                teacherId: state.selectedTeacherId,
                studentId: state.selectedStudentId
            });
            renderDashboard(data);
        } catch (error) {
            console.error('Failed to load dashboard', error);
        } finally {
            setLoading(false);
        }
    }

    async function updateTeacherSelection(teacherId) {
        if (!teacherId) {
            return;
        }

        state.selectedTeacherId = teacherId;

        if (els.teacherTableBody) {
            els.teacherTableBody.querySelectorAll('tr').forEach(row => {
                row.classList.toggle('active', row.dataset.id === String(teacherId));
            });
        }

        try {
            setLoading(true);
            const data = await fetchJson('/Admin/ReportsTeacherDetail', {
                teacherId,
                schoolYear: state.selectedSchoolYear,
                termKey: state.selectedTermKey
            });
            renderTeacherDetail(data);
        } catch (error) {
            console.error('Failed to load teacher detail', error);
        } finally {
            setLoading(false);
        }
    }

    async function loadStudentAnalytics() {
        const studentId = state.selectedStudentId;
        if (!studentId) {
            return;
        }

        try {
            setLoading(true);
            const data = await fetchJson('/Admin/ReportsStudentAnalytics', {
                studentId,
                schoolYear: state.selectedSchoolYear,
                termKey: state.studentTermKey || state.selectedTermKey
            });
            renderStudent(data);
        } catch (error) {
            console.error('Failed to load student analytics', error);
        } finally {
            setLoading(false);
        }
    }

    function bindEvents() {
        els.tabs.forEach(tab => {
            tab.addEventListener('click', () => activateTab(tab.dataset.tab));
        });

        if (els.schoolYear) {
            els.schoolYear.addEventListener('change', () => {
                state.selectedSchoolYear = els.schoolYear.value || '';
                reloadDashboard();
            });
        }

        if (els.term) {
            els.term.addEventListener('change', () => {
                state.selectedTermKey = els.term.value || '';
                reloadDashboard();
            });
        }

        if (els.teacherSearch) {
            els.teacherSearch.addEventListener('input', event => {
                state.teacherSearch = event.target.value.trim().toLowerCase();
                renderTeacherDirectory(state.teacherDirectory);
            });
        }

        if (els.teacherProgram) {
            els.teacherProgram.addEventListener('change', () => {
                state.teacherProgram = els.teacherProgram.value || '';
                renderTeacherDirectory(state.teacherDirectory);
            });
        }

        if (els.studentSelector) {
            els.studentSelector.addEventListener('change', () => {
                const value = els.studentSelector.value;
                state.selectedStudentId = value ? Number(value) : null;
                loadStudentAnalytics();
            });
        }

        if (els.studentTerm) {
            els.studentTerm.addEventListener('change', () => {
                state.studentTermKey = els.studentTerm.value || '';
                loadStudentAnalytics();
            });
        }
    }

    function initialize() {
        bindEvents();
        if (state.dashboard) {
            renderDashboard(state.dashboard);
        }
        activateTab('student');
    }

    initialize();
})();
