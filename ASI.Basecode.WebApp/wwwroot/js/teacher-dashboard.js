(() => {
    document.addEventListener('DOMContentLoaded', () => {
        const server = window.__TDATA || {};
        const programData = server.programs || {};
        const summaryData = server.summary || {};
        const gradedPct = Number(server.gradedPct || 0);
        const programKeys = Object.keys(programData);

        // Elements
        const coursesList = document.getElementById('coursesList');
        const kpiSubjectsValue = document.getElementById('kpiSubjectsValue');
        const kpiSubjectsMeta = document.getElementById('kpiSubjectsMeta');
        const kpiStudentsValue = document.getElementById('kpiStudentsValue');
        const kpiStudentsMeta = document.getElementById('kpiStudentsMeta');
        const kpiProgramValue = document.getElementById('kpiProgramValue');
        const currentProgramHint = document.getElementById('currentProgramHint');

        // Program buttons (rendered in CSHTML)
        const programButtons = {};
        programKeys.forEach(key => {
            const btn = document.getElementById(`seg${key}`);
            if (btn) programButtons[key] = btn;
        });

        function renderCourses(programKey) {
            const programInfo = programData[programKey];
            const list = (programInfo && programInfo.courses) ? programInfo.courses : [];
            const uniqueStudentsTotal = (programInfo && programInfo.uniqueStudents) ? programInfo.uniqueStudents : 0;

            // Build list
            coursesList.innerHTML = '';
            list.forEach(c => {
                const item = document.createElement('div');
                item.className = 'item';
                item.innerHTML = `
          <div class="dot"><i class="bi bi-journal-text"></i></div>
          <div>
            <div class="i-title">${c.code} • ${c.title}</div>
            <div class="i-sub">${c.students} students</div>
          </div>`;
                coursesList.appendChild(item);
            });

            // KPI updates
            const subjectsCount = list.length;
            kpiSubjectsValue.textContent = subjectsCount;
            kpiStudentsValue.textContent = uniqueStudentsTotal;
            kpiSubjectsMeta.textContent = `${programKey} • ${subjectsCount} ${subjectsCount === 1 ? 'course' : 'courses'}`;
            kpiStudentsMeta.textContent = `${programKey} • students`;
            kpiProgramValue.textContent = programKey;
            if (currentProgramHint) currentProgramHint.textContent = programKey;

            // Toggle button states
            Object.keys(programButtons).forEach(key => {
                const btn = programButtons[key];
                if (!btn) return;
                if (key === programKey) {
                    btn.classList.add('active');
                    btn.setAttribute('aria-pressed', 'true');
                    btn.setAttribute('aria-selected', 'true');
                } else {
                    btn.classList.remove('active');
                    btn.setAttribute('aria-pressed', 'false');
                    btn.setAttribute('aria-selected', 'false');
                }
            });
        }

        // Wire up program buttons
        Object.keys(programButtons).forEach(key => {
            const btn = programButtons[key];
            btn.addEventListener('click', () => renderCourses(key));
            btn.addEventListener('keydown', e => {
                if (['Enter', ' '].includes(e.key)) {
                    e.preventDefault();
                    renderCourses(key);
                }
            });
        });

        // Initial render
        if (programKeys.length > 0) {
            renderCourses(programKeys[0]);
        } else {
            coursesList.innerHTML = '<div class="item">No courses assigned</div>';
        }

        // ===== Donut: orange gradient background =====
        const donutCanvas = document.getElementById('gradingRing');
        if (donutCanvas) {
            const donutCtx = donutCanvas.getContext('2d');
            // vertical gradient top->bottom
            const orangeGradient = donutCtx.createLinearGradient(0, 0, 0, donutCanvas.height);
            orangeGradient.addColorStop(0, '#fff3e0');
            orangeGradient.addColorStop(1, '#ffe0b2');

            new Chart(donutCtx, {
                type: 'doughnut',
                data: {
                    datasets: [{
                        data: [gradedPct, 1 - gradedPct],
                        borderWidth: 0,
                        backgroundColor: [orangeGradient, '#e8eef7'], // gradient + remainder
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
                    id: 'centerTextTeacher',
                    afterDraw(chart) {
                        const { ctx, chartArea } = chart; if (!chartArea) return;
                        const cx = (chartArea.left + chartArea.right) / 2;
                        const cy = (chartArea.top + chartArea.bottom) / 2;
                        ctx.save();
                        ctx.font = '700 44px Inter, system-ui, -apple-system, Segoe UI, Roboto';
                        ctx.fillStyle = '#ef6c00';
                        ctx.textAlign = 'center';
                        ctx.textBaseline = 'middle';
                        ctx.fillText(Math.round(gradedPct * 100) + '%', cx, cy - 12);
                        ctx.font = '12px Inter, system-ui, -apple-system, Segoe UI, Roboto';
                        ctx.fillStyle = '#6b7280';
                        ctx.fillText('Grading Progress', cx, cy + 18);
                        ctx.restore();
                    }
                }]
            });

            // Legend text
            const gradedPctLabel = document.getElementById('gradedPctLabel');
            const remainingPctLabel = document.getElementById('remainingPctLabel');
            if (gradedPctLabel) gradedPctLabel.textContent = Math.round(gradedPct * 100) + '%';
            if (remainingPctLabel) remainingPctLabel.textContent = (100 - Math.round(gradedPct * 100)) + '%';
        }

        // ===== Bar chart: gradients =====
        const barCanvas = document.getElementById('byProgramBar');
        if (barCanvas) {
            const barCtx = barCanvas.getContext('2d');

            // Students = green gradient
            const greenGrad = barCtx.createLinearGradient(0, 0, 0, barCanvas.height);
            greenGrad.addColorStop(0, '#e8f5e9');
            greenGrad.addColorStop(1, '#c8e6c9');

            // Courses = violet gradient
            const violetGrad = barCtx.createLinearGradient(0, 0, 0, barCanvas.height);
            violetGrad.addColorStop(0, '#f3e8ff');
            violetGrad.addColorStop(1, '#e0c3fc');

            const labels = programKeys.length > 0 ? programKeys : ['No Programs'];
            const students = programKeys.map(k => Number(summaryData[k]?.students || 0));
            const courses = programKeys.map(k => Number(summaryData[k]?.courses || 0));

            new Chart(barCtx, {
                type: 'bar',
                data: {
                    labels,
                    datasets: [
                        { label: 'Students', data: students, borderWidth: 0, backgroundColor: greenGrad },
                        { label: 'Courses', data: courses, borderWidth: 0, backgroundColor: violetGrad }
                    ]
                },
                options: {
                    responsive: true, maintainAspectRatio: false,
                    scales: {
                        y: { beginAtZero: true, grid: { color: '#eef2f7' }, ticks: { color: '#64748b', font: { size: 11 } } },
                        x: { grid: { display: false }, ticks: { color: '#64748b', font: { size: 11 } } }
                    },
                    plugins: { legend: { display: true } }
                }
            });
        }
    });
})();
