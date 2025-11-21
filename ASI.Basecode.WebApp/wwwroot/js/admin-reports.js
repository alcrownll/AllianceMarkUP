    async function refreshDashboard({ preserveTeacher = false } = {}) {
        try {
            setLoading(true);
            const params = {
                schoolYear: state.selectedSchoolYear,
                termKey: state.selectedTermKey,
                studentId: state.selectedStudentId,
                studentProgramId: state.selectedStudentProgramId,
                studentCourseId: state.selectedCourseId
            };

            if (preserveTeacher && state.selectedTeacherId) {
                params.teacherId = state.selectedTeacherId;
            }

            const dashboard = await fetchJson('/Admin/ReportsDashboard', params);

            if (!preserveTeacher) {
                state.selectedTeacherId = null;
            }

            renderDropdowns(dashboard);
        } catch (error) {
            console.error('Failed to refresh admin reports dashboard', error);
        } finally {
            setLoading(false);
        }
    }

/* global Chart */
(function () {
    const root = document.getElementById('reports-page');
    if (!root) {
        return;
    }

    const initialDashboard = window.AdminReportsInitial || null;
    const state = {
        dashboard: initialDashboard,
        selectedTeacherId: null,
        selectedStudentId: initialDashboard?.Student?.SelectedStudentId || null,
        selectedCourseId: initialDashboard?.Student?.SelectedCourseId || null,
        selectedStudentProgramId: initialDashboard?.Student?.SelectedProgramId ?? null,
        teacherDirectory: [],
        studentOptions: [],
        studentPrograms: Array.isArray(initialDashboard?.Student?.Programs) ? initialDashboard.Student.Programs : [],
        selectedSchoolYear: initialDashboard?.SchoolYear || initialDashboard?.schoolYear || '',
        selectedTermKey: initialDashboard?.TermKey || initialDashboard?.termKey || ''
    };

    const chartRegistry = new Map();

    const els = {
        tabs: Array.from(root.querySelectorAll('.reports-tab')),
        panels: Array.from(root.querySelectorAll('.tab-panel')),
        studentSelector: root.querySelector('[data-selector="student"]'),
        teacherAssignmentsBody: root.querySelector('[data-table="teacher-assignments"] tbody'),
        teacherSubmissionList: root.querySelector('[data-list="teacher-submissions"]'),
        teacherSelector: root.querySelector('[data-selector="teacher"]'),
        studentGradesBody: root.querySelector('[data-table="student-grades"] tbody'),
        studentProgramFilter: root.querySelector('[data-filter="student-program"]'),
        studentYearFilter: root.querySelector('[data-filter="student-year"]'),
        studentCourseFilter: root.querySelector('[data-filter="student-course"]'),
        loadStudentsButton: root.querySelector('[data-action="load-students"]'),
        studentSection: root.querySelector('[data-section="student-details"]'),
        teacherSection: root.querySelector('[data-section="teacher-details"]')
    };

    const numberFormatter = new Intl.NumberFormat();
    const percentFormatter = new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 });

    const field = (name) => root.querySelector(`[data-field="${name}"]`);

    const readProp = (source, key) => {
        if (!source || !key) {
            return undefined;
        }
        if (Object.prototype.hasOwnProperty.call(source, key)) {
            return source[key];
        }
        const camelKey = key.charAt(0).toLowerCase() + key.slice(1);
        return source[camelKey];
    };

    const toNumber = (value, fallback = 0) => {
        const numeric = Number(value);
        return Number.isFinite(numeric) ? numeric : fallback;
    };

    function ensureChart(canvasId, type, labels = [], datasets = [], options = {}) {
        const canvas = document.getElementById(canvasId);
        if (!canvas || typeof Chart === 'undefined') {
            return;
        }

        if (chartRegistry.has(canvasId)) {
            const existing = chartRegistry.get(canvasId);
            if (existing?.destroy) {
                existing.destroy();
            }
            chartRegistry.delete(canvasId);
        }

        const context = canvas.getContext('2d');
        const config = {
            type,
            data: {
                labels: Array.isArray(labels) ? labels : [],
                datasets: Array.isArray(datasets) ? datasets : []
            },
            options
        };

        const chart = new Chart(context, config);
        chartRegistry.set(canvasId, chart);
    }

    const calculateVolatilityScore = (row) => {
        if (!row) {
            return null;
        }
        const componentKeys = ['Prelim', 'Midterm', 'Prefinal', 'Final'];
        const values = componentKeys
            .map(key => readProp(row, key))
            .map(value => value != null ? toNumber(value, NaN) : NaN)
            .filter(value => Number.isFinite(value));

        if (values.length <= 1) {
            return 0;
        }

        const mean = values.reduce((sum, value) => sum + value, 0) / values.length;
        const variance = values.reduce((sum, value) => sum + Math.pow(value - mean, 2), 0) / values.length;
        return Math.round(Math.sqrt(variance) * 100) / 100;
    };

    const mapVolatilityToLabel = (score) => {
        if (score == null || Number.isNaN(score)) {
            return 'No Data';
        }
        if (score <= 1) {
            return 'Excellent';
        }
        if (score <= 3) {
            return 'Great';
        }
        return 'Needs Attention';
    };

    const mapVolatilityToBadgeClass = (label) => {
        switch (label) {
            case 'Excellent':
                return 'badge bg-success';
            case 'Great':
                return 'badge bg-primary';
            case 'Needs Attention':
                return 'badge bg-danger';
            default:
                return 'badge bg-secondary';
        }
    };

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
        const numeric = toNumber(value, 0);
        return numberFormatter.format(numeric);
    }

    function formatPercent(value, opts = {}) {
        const numeric = toNumber(value, 0);
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

    function toggleStudentSection(isVisible) {
        if (!els.studentSection) {
            return;
        }

        els.studentSection.hidden = !isVisible;
    }

    function toggleTeacherSection(isVisible) {
        if (!els.teacherSection) {
            return;
        }

        els.teacherSection.hidden = !isVisible;
    }

    async function loadStudentDirectory() {
        try {
            setLoading(true);
            const params = {
                schoolYear: state.selectedSchoolYear,
                termKey: state.selectedTermKey,
                programId: state.selectedStudentProgramId,
                courseId: state.selectedCourseId
            };

            const directory = await fetchJson('/Admin/ReportsStudentDirectory', params);
            state.studentOptions = Array.isArray(directory) ? directory : [];

            const mapped = state.studentOptions.map(option => ({
                value: option.studentId ?? option.StudentId,
                label: option.name || option.Name || 'Student'
            }));

            const selectedValue = state.selectedStudentId != null ? state.selectedStudentId : '';
            renderOptions(els.studentSelector, mapped, selectedValue, 'Select Student');

            if (!mapped.length && !state.selectedStudentId) {
                renderStudent(null);
            }
        } catch (error) {
            console.error('Failed to load student directory', error);
        } finally {
            setLoading(false);
        }
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

    function bindStudentEvents() {
        if (els.studentSelector) {
            els.studentSelector.addEventListener('change', async event => {
                const target = event.target;
                const id = target instanceof HTMLSelectElement ? Number(target.value) : NaN;
                if (!Number.isFinite(id) || id <= 0) {
                    state.selectedStudentId = null;
                    toggleStudentSection(false);
                    renderStudent(null);
                    return;
                }

                state.selectedStudentId = id;
                await loadStudentAnalytics(id);
            });
        }

        if (els.studentProgramFilter) {
            els.studentProgramFilter.addEventListener('change', async event => {
                const value = event.target.value;
                state.selectedStudentProgramId = value ? Number(value) : null;
                state.selectedCourseId = null;
                state.selectedStudentId = null;
                await refreshDashboard();
            });
        }

        if (els.studentYearFilter) {
            els.studentYearFilter.addEventListener('change', event => {
                const yearValue = event.target.value || '';
                state.selectedSchoolYear = yearValue;
            });
        }

        if (els.studentCourseFilter) {
            els.studentCourseFilter.addEventListener('change', event => {
                const value = event.target.value;
                state.selectedCourseId = value ? Number(value) : null;
                state.selectedStudentId = null;
                loadStudentDirectory();
            });
        }

        if (els.loadStudentsButton) {
            els.loadStudentsButton.addEventListener('click', () => {
                loadStudentDirectory();
            });
        }
    }

    function bindTeacherEvents() {
        if (els.teacherSelector) {
            els.teacherSelector.addEventListener('change', () => {
                const value = els.teacherSelector.value;
                const teacherId = value ? Number(value) : null;
                if (teacherId) {
                    updateTeacherSelection(teacherId);
                } else {
                    renderTeacherDetail(null);
                }
            });
        }

    }

    function bindEvents() {
        bindTabEvents();
        bindStudentEvents();
        bindTeacherEvents();
        toggleStudentSection(Boolean(state.selectedStudentId));
        toggleTeacherSection(true);
    }

    async function loadStudentAnalytics(id) {
        const studentId = id || state.selectedStudentId;
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

    async function loadTeacherDetail(teacherId) {
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

    function renderStudentConsistency(label, score) {
        const node = root.querySelector('[data-field="student-consistency"]');
        if (!node) {
            return;
        }
        if (!label || label === 'Select a student.') {
            node.innerHTML = 'Select a student.';
            return;
        }

        const badgeClass = mapVolatilityToBadgeClass(label);
        const scoreText = Number.isFinite(score) ? ` (${score.toFixed(2)})` : '';
        node.innerHTML = `<span class="${badgeClass}">${label}</span>${scoreText}`;
    }

    function renderStudentVolatilityList(gradeBreakdown) {
        const container = root.querySelector('[data-list="student-consistency-list"]');
        if (!container) {
            return;
        }

        container.innerHTML = '';
        const rows = Array.isArray(gradeBreakdown) ? gradeBreakdown : [];
        if (!rows.length) {
            container.innerHTML = '<p class="text-muted mb-0">Select a student.</p>';
            return;
        }

        rows.forEach(row => {
            const course = readProp(row, 'Subject') || readProp(row, 'EdpCode') || '--';
            const volatilityScore = calculateVolatilityScore(row);
            const volatilityLabel = mapVolatilityToLabel(volatilityScore);
            const badgeClass = mapVolatilityToBadgeClass(volatilityLabel);
            const scoreText = Number.isFinite(volatilityScore) ? ` (${volatilityScore.toFixed(2)})` : '';
            const item = document.createElement('div');
            item.className = 'consistency-item d-flex justify-content-between align-items-center mb-2';
            item.innerHTML = `
                <span>${course}</span>
                <span class="${badgeClass}" title="Volatility score: ${volatilityScore ?? 0}">${volatilityLabel}${scoreText}</span>`;
            container.appendChild(item);
        });
    }

    function renderStudentComparative(highlights) {
        const container = root.querySelector('[data-list="student-comparative"]');
        if (!container) {
            return;
        }

        container.innerHTML = '';
        const items = Array.isArray(highlights) ? highlights : [];
        if (!items.length) {
            container.innerHTML = '<p class="text-muted mb-0">No comparative data.</p>';
            return;
        }

        items.forEach(item => {
            const card = document.createElement('div');
            const course = readProp(item, 'Course') || 'Course';
            const period = readProp(item, 'PeriodLabel') || 'N/A';
            const gradeValue = readProp(item, 'Grade');
            const grade = Number.isFinite(toNumber(gradeValue, NaN)) ? `${toNumber(gradeValue).toFixed(2)}%` : '--';
            card.className = 'analysis-item';
            card.innerHTML = `
                <div class="d-flex justify-content-between align-items-center mb-1">
                    <strong>${course}</strong>
                    <span class="badge bg-warning text-dark">🏆 Personal Best</span>
                </div>
                <div>Your best: ${grade} in ${period}</div>`;
            container.appendChild(card);
        });
    }
    
    function renderTeacherDetail(detail) {
        const hasDetail = detail != null && (readProp(detail, 'TeacherId') || readProp(detail, 'IsAggregate'));
        toggleTeacherSection(true);

        const teacherId = hasDetail ? readProp(detail, 'TeacherId') : null;
        const isAggregate = hasDetail ? Boolean(readProp(detail, 'IsAggregate')) : false;
        const assignmentsSource = readProp(detail, 'Assignments');
        const submissionStatusesSource = readProp(detail, 'SubmissionStatuses');
        const coursePassRatesSource = readProp(detail, 'CoursePassRates');

        const assignments = hasDetail && Array.isArray(assignmentsSource) ? assignmentsSource : [];
        const submissionStatuses = hasDetail && Array.isArray(submissionStatusesSource) ? submissionStatusesSource : [];
        const coursePassRates = hasDetail && Array.isArray(coursePassRatesSource) ? coursePassRatesSource : [];

        const teacherName = hasDetail ? (readProp(detail, 'Name') || 'Select a teacher') : 'Select a teacher';
        const teacherRank = readProp(detail, 'Rank') || '--';
        const teacherDepartment = readProp(detail, 'Department') || '--';
        
        setField('teacher-name', teacherName);
        setField('teacher-meta', hasDetail ? `${teacherRank} · ${teacherDepartment}` : '--');

        const teachingLoadCount = hasDetail ? toNumber(readProp(detail, 'TeachingLoadCount'), 0) : 0;
        const passRatePercent = hasDetail ? toNumber(readProp(detail, 'PassRatePercent'), 0) : 0;
        const completedCourses = submissionStatuses.filter(item => Boolean(readProp(item, 'IsComplete'))).length;
        const totalAssignments = assignments.length;

        const subjectLabel = teachingLoadCount === 1 ? 'subject' : 'subjects';
        setField('teacher-load', `${formatNumber(teachingLoadCount)} ${subjectLabel}`);
        setField('teacher-pass', `${passRatePercent.toFixed(1)}%`);
        setField('teacher-submissions', totalAssignments ? `${formatNumber(completedCourses)}/${formatNumber(totalAssignments)}` : '0/0');

        const loadCard = root.querySelector('[data-card="teacher-load"]');
        const passCard = root.querySelector('[data-card="teacher-pass"]');
        [loadCard, passCard].forEach(card => {
            if (!card) {
                return;
            }
            card.classList.toggle('metric-card-active', hasDetail);
        });

        if (els.teacherAssignmentsBody) {
            els.teacherAssignmentsBody.innerHTML = '';
            if (!assignments.length) {
                els.teacherAssignmentsBody.innerHTML = '<tr data-empty="true"><td colspan="3" class="text-center">Select a teacher to load assignments.</td></tr>';
            } else {
                assignments.forEach(item => {
                    const courseCode = readProp(item, 'CourseCode') || '--';
                    const subjectName = readProp(item, 'SubjectName') || courseCode;
                    const displaySubject = courseCode && subjectName && subjectName !== courseCode
                        ? `${courseCode} • ${subjectName}`
                        : subjectName || courseCode || '--';
                    const schedule = readProp(item, 'Schedule') || '--';
                    const units = readProp(item, 'Units');

                    const tr = document.createElement('tr');
                    tr.innerHTML = `
                        <td>${displaySubject}</td>
                        <td>${schedule}</td>
                        <td>${formatNumber(units)}</td>`;
                    els.teacherAssignmentsBody.appendChild(tr);
                });
            }
        }

        if (els.teacherSubmissionList) {
            els.teacherSubmissionList.innerHTML = '';
            if (!submissionStatuses.length) {
                const message = hasDetail ? 'No submission records.' : 'Select a teacher to load submission statuses.';
                els.teacherSubmissionList.innerHTML = `<li data-empty="true" class="pending">${message}</li>`;
            } else {
                submissionStatuses.forEach(item => {
                    const isComplete = Boolean(readProp(item, 'IsComplete'));
                    const subjectName = readProp(item, 'SubjectName') || readProp(item, 'CourseCode') || '--';
                    const statusLabel = readProp(item, 'Status') || (isComplete ? 'Complete' : 'Incomplete');
                    const li = document.createElement('li');
                    li.className = isComplete ? 'status-chip status-complete' : 'status-chip status-incomplete';
                    li.textContent = `${subjectName} · ${statusLabel}`;
                    els.teacherSubmissionList.appendChild(li);
                });
            }
        }

        const passRateLabels = coursePassRates.map(item => readProp(item, 'SubjectName') || readProp(item, 'CourseCode') || '--');
        const passRateData = coursePassRates.map(item => toNumber(readProp(item, 'PassRatePercent'), 0));

        ensureChart('teacherPassChart', 'bar', passRateLabels, [
            {
                label: 'Subject Pass %',
                data: passRateData,
                backgroundColor: 'rgba(37, 99, 235, 0.65)',
                borderRadius: 6,
                barThickness: 28,
                maxBarThickness: 32,
                categoryPercentage: 0.6,
                barPercentage: 0.85
            }
        ], {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
                y: { beginAtZero: true, max: 100 },
                x: {
                    ticks: {
                        autoSkip: false,
                        maxRotation: 0,
                        minRotation: 0
                    }
                }
            }
        });
    }
    
    function renderStudent(analytics) {
        toggleStudentSection(Boolean(analytics));

        const studentName = readProp(analytics, 'StudentName') || readProp(analytics, 'Name') || (analytics?.IsAggregate ? 'All Students' : 'Select a student');

        setField('student-name', studentName);

        let courseLabel = 'All Programs';
        if (analytics?.IsAggregate) {
            if (state.selectedStudentProgramId != null) {
                const programOption = state.studentPrograms.find(program => Number(readProp(program, 'ProgramId')) === state.selectedStudentProgramId);
                courseLabel = readProp(programOption, 'DisplayName') || readProp(programOption, 'ProgramName') || readProp(programOption, 'ProgramCode') || courseLabel;
            }
        } else if (state.selectedStudentId) {
            const studentOption = state.studentOptions.find(option => Number(readProp(option, 'StudentId')) === state.selectedStudentId);
            courseLabel = readProp(studentOption, 'Program') || courseLabel;
        }

        setField('student-course', courseLabel);

        const gradeBreakdownRaw = readProp(analytics, 'GradeBreakdown');
        const gradeBreakdown = Array.isArray(gradeBreakdownRaw) ? gradeBreakdownRaw : [];
        const normalizedBreakdown = gradeBreakdown.map(row => {
            const normalizedRow = { ...row };
            const prelim = readProp(row, 'Prelim');
            const midterm = readProp(row, 'Midterm');
            const prefinal = readProp(row, 'Prefinal');
            const finalExam = readProp(row, 'Final');
            const components = [prelim, midterm, prefinal, finalExam];
            const hasAllComponents = components.every(value => value != null && value !== '');
            const finalGradeRaw = readProp(row, 'FinalGrade');
            const finalGradeValue = hasAllComponents && finalGradeRaw != null && finalGradeRaw !== '' && Number.isFinite(toNumber(finalGradeRaw, NaN))
                ? toNumber(finalGradeRaw)
                : null;
            const computedStatus = !hasAllComponents || finalGradeValue == null
                ? 'Incomplete'
                : (finalGradeValue < 3 ? 'Pass' : 'Fail');

            normalizedRow.FinalGrade = finalGradeValue;
            normalizedRow.finalGrade = finalGradeValue;
            normalizedRow.Status = computedStatus;
            normalizedRow.status = computedStatus;
            return normalizedRow;
        });

        const volatilityScores = normalizedBreakdown
            .map(row => calculateVolatilityScore(row))
            .filter(score => score != null);
        const overallVolatilityScore = volatilityScores.length
            ? Math.round((volatilityScores.reduce((sum, score) => sum + score, 0) / volatilityScores.length) * 100) / 100
            : null;
        const overallVolatilityLabel = volatilityScores.length
            ? mapVolatilityToLabel(overallVolatilityScore)
            : 'No Data';

        renderStudentConsistency(overallVolatilityLabel, overallVolatilityScore);
        renderStudentVolatilityList(normalizedBreakdown);
        renderStudentComparative(readProp(analytics, 'ComparativeHighlights'));

        if (els.studentGradesBody) {
            els.studentGradesBody.innerHTML = '';
            if (!normalizedBreakdown.length) {
                els.studentGradesBody.innerHTML = '<tr data-empty="true"><td colspan="8" class="text-center">Select a student.</td></tr>';
            } else {
                normalizedBreakdown.forEach(row => {
                    const tr = document.createElement('tr');
                    const formatGwa = (value) => (value != null ? toNumber(value).toFixed(2) : '--');
                    const edpCode = readProp(row, 'EdpCode') || '--';
                    const subject = readProp(row, 'Subject') || '--';
                    const prelim = readProp(row, 'Prelim');
                    const midterm = readProp(row, 'Midterm');
                    const prefinal = readProp(row, 'Prefinal');
                    const finalExam = readProp(row, 'Final');
                    const finalGrade = readProp(row, 'FinalGrade');
                    const status = readProp(row, 'Status') || '--';
                    tr.innerHTML = `
                        <td>${edpCode}</td>
                        <td>${subject}</td>
                        <td>${formatGwa(prelim)}</td>
                        <td>${formatGwa(midterm)}</td>
                        <td>${formatGwa(prefinal)}</td>
                        <td>${formatGwa(finalExam)}</td>
                        <td>${formatGwa(finalGrade)}</td>
                        <td class="status-${status.toLowerCase()}">${status}</td>`;
                    els.studentGradesBody.appendChild(tr);
                });
            }
        }

        const courseGradesRaw = readProp(analytics, 'CourseGrades');
        const courseGrades = Array.isArray(courseGradesRaw) ? courseGradesRaw : [];
        const courseLabels = courseGrades.map(item => readProp(item, 'CourseCode') || '--');
        const courseGradeData = courseGrades.map(item => {
            const gradeValue = toNumber(readProp(item, 'Grade'), NaN);
            if (!Number.isFinite(gradeValue)) {
                return [5, 5];
            }

            const clampedGrade = Math.min(Math.max(gradeValue, 1), 5);
            return [clampedGrade, 5];
        });

        ensureChart('studentCourseChart', 'bar', courseLabels, [
            {
                label: 'Grade',
                data: courseGradeData,
                backgroundColor: '#60a5fa',
                hoverBackgroundColor: '#3b82f6',
                borderWidth: 0,
                barThickness: 48,
                maxBarThickness: 56,
                categoryPercentage: 0.5,
                barPercentage: 0.9
            }
        ], {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: false },
                tooltip: {
                    callbacks: {
                        label(context) {
                            const rawValue = context.raw;
                            let grade = context.parsed?.y;

                            if (Array.isArray(rawValue)) {
                                grade = rawValue[0];
                            }

                            const numericGrade = toNumber(grade, 0);
                            return `Grade: ${numericGrade.toFixed(2)}`;
                        }
                    }
                }
            },
            scales: {
                y: {
                    reverse: true,
                    min: 1,
                    max: 5,
                    ticks: {
                        stepSize: 0.5,
                        callback(value) {
                            return Number(value).toFixed(1);
                        }
                    },
                    grid: {
                        color: 'rgba(59, 130, 246, 0.15)',
                        drawBorder: false
                    }
                },
                x: {
                    ticks: {
                        autoSkip: false,
                        maxRotation: 0,
                        minRotation: 0
                    },
                    grid: { display: false }
                }
            },
            hover: {
                mode: 'nearest',
                intersect: true
            }
        });

        const statusTotals = normalizedBreakdown.reduce((totals, row) => {
            const status = (readProp(row, 'Status') || 'Incomplete').toLowerCase();
            if (status === 'pass') {
                totals.pass += 1;
            } else if (status === 'fail') {
                totals.fail += 1;
            } else {
                totals.incomplete += 1;
            }
            return totals;
        }, { pass: 0, fail: 0, incomplete: 0 });

        const doughnutLabels = ['Passed', 'Failed', 'Incomplete'];
        const doughnutData = [statusTotals.pass, statusTotals.fail, statusTotals.incomplete];

        ensureChart('studentStatusChart', 'doughnut', doughnutLabels, [
            {
                data: doughnutData,
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
                            const total = context.dataset.data.reduce((sum, value) => sum + toNumber(value, 0), 0) || 1;
                            const value = toNumber(context.parsed, 0);
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
    }

    function renderDropdowns(dashboard) {
        state.dashboard = dashboard || {};

        const studentData = readProp(state.dashboard, 'Student') || {};
        const teacherData = readProp(state.dashboard, 'Teacher') || {};

        const studentOptionsSource = readProp(studentData, 'Students');
        const studentProgramsSource = readProp(studentData, 'Programs');
        const studentCoursesSource = readProp(studentData, 'Courses');
        const teacherDirectorySource = readProp(teacherData, 'Directory');

        state.studentOptions = Array.isArray(studentOptionsSource) ? studentOptionsSource : [];
        state.teacherDirectory = Array.isArray(teacherDirectorySource) ? teacherDirectorySource : [];
        state.studentPrograms = Array.isArray(studentProgramsSource) ? studentProgramsSource : [];

        const rawStudentProgramId = readProp(studentData, 'SelectedProgramId');
        if (rawStudentProgramId != null) {
            const numericStudentProgram = Number(rawStudentProgramId);
            state.selectedStudentProgramId = Number.isFinite(numericStudentProgram)
                ? numericStudentProgram
                : rawStudentProgramId;
        } else if (state.selectedStudentProgramId == null) {
            state.selectedStudentProgramId = null;
        }

        const rawStudentCourseId = readProp(studentData, 'SelectedCourseId');
        if (rawStudentCourseId != null) {
            const numericCourse = Number(rawStudentCourseId);
            state.selectedCourseId = Number.isFinite(numericCourse) ? numericCourse : rawStudentCourseId;
        } else if (state.selectedCourseId == null) {
            state.selectedCourseId = null;
        }

        const studentProgramOptions = Array.isArray(dashboard?.Student?.Programs)
            ? dashboard.Student.Programs.map(option => ({
                value: option.ProgramId,
                label: option.DisplayName || option.ProgramName || option.ProgramCode || 'Program'
            }))
            : [];

        renderOptions(els.studentProgramFilter, studentProgramOptions, state.selectedStudentProgramId ?? '', 'All Programs');

        const courseOptions = Array.isArray(studentCoursesSource)
            ? studentCoursesSource.map(option => ({
                value: readProp(option, 'CourseId'),
                label: readProp(option, 'DisplayName')
                    || readProp(option, 'CourseName')
                    || readProp(option, 'CourseCode')
                    || 'Course'
            }))
            : [];

        renderOptions(els.studentCourseFilter, courseOptions, state.selectedCourseId ?? '', 'All Courses');

        if (els.studentYearFilter && state.selectedSchoolYear) {
            els.studentYearFilter.value = state.selectedSchoolYear;
        }

        renderOptions(els.studentSelector, state.studentOptions.map(option => ({
            value: readProp(option, 'StudentId'),
            label: readProp(option, 'Name') || 'Unknown'
        })), state.selectedStudentId ?? '', 'Select Student');

        renderOptions(els.teacherSelector, state.teacherDirectory.map(option => ({
            value: readProp(option, 'TeacherId'),
            label: readProp(option, 'Name') || 'Unknown'
        })), state.selectedTeacherId ?? '', 'Select Teacher');

        if (!state.selectedTeacherId && els.teacherSelector) {
            els.teacherSelector.value = '';
        }

        const aggregateTeacherDetail = readProp(teacherData, 'AggregateDetail');
        const teacherDetail = readProp(teacherData, 'SelectedTeacher');
        const detailTeacherId = teacherDetail ? Number(readProp(teacherDetail, 'TeacherId')) || null : null;

        if (state.selectedTeacherId != null && teacherDetail && detailTeacherId === Number(state.selectedTeacherId)) {
            renderTeacherDetail(teacherDetail);
        } else {
            renderTeacherDetail(null);
        }

        const aggregateStudentAnalytics = readProp(studentData, 'AggregateAnalytics') || readProp(studentData, 'Analytics');
        let studentRendered = false;

        const studentAnalytics = readProp(studentData, 'Analytics');
        const studentSelectedId = readProp(studentData, 'SelectedStudentId');

        if (studentAnalytics && studentSelectedId) {
            if (!state.selectedStudentId) {
                state.selectedStudentId = studentSelectedId;
            }

            if (studentSelectedId === state.selectedStudentId) {
                renderStudent(studentAnalytics);
                studentRendered = true;
            }
        }

        if (!state.selectedStudentId && state.studentOptions.length) {
            const firstStudent = state.studentOptions[0];
            state.selectedStudentId = readProp(firstStudent, 'StudentId');
            if (els.studentSelector) {
                els.studentSelector.value = String(readProp(firstStudent, 'StudentId'));
            }
        }

        if (!studentRendered && aggregateStudentAnalytics) {
            renderStudent(aggregateStudentAnalytics);
            studentRendered = true;
        }

        if (!studentRendered) {
            if (state.selectedStudentId) {
                loadStudentAnalytics();
            } else {
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

    async function updateTeacherSelection(teacherId) {
        if (!teacherId) {
            return;
        }

        state.selectedTeacherId = teacherId;

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

    function initialize() {
        bindEvents();
        if (state.dashboard) {
            renderDropdowns(state.dashboard);
        }
        activateTab('student');
    }
    initialize();
})();
