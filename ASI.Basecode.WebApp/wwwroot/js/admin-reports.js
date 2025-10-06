(function () {
    const root = document.getElementById('reports-page');
    if (!root) {
        return;
    }

    const tabs = Array.from(root.querySelectorAll('.reports-tab'));
    const panels = Array.from(root.querySelectorAll('.tab-panel'));

    function activateTab(id) {
        tabs.forEach(tab => {
            const isActive = tab.dataset.tab === id;
            tab.classList.toggle('active', isActive);
        });
        panels.forEach(panel => {
            const isActive = panel.dataset.panel === id;
            panel.classList.toggle('active', isActive);
        });
    }

    tabs.forEach(tab => {
        tab.addEventListener('click', () => activateTab(tab.dataset.tab));
    });

    function mountPlaceholderChart(canvasId, config) {
        const ctx = document.getElementById(canvasId);
        if (!ctx || typeof Chart === 'undefined') {
            return null;
        }

        return new Chart(ctx, config);
    }

    // Placeholder datasets (replace with live data bindings later)
    const trendChart = mountPlaceholderChart('overallTrendChart', {
        type: 'line',
        data: {
            labels: ['2022', '2023', '2024', '2025', '2026'],
            datasets: [
                {
                    label: 'Enrollment',
                    data: [9500, 10120, 11150, 11980, 12486],
                    borderColor: '#2563eb',
                    backgroundColor: 'rgba(37, 99, 235, 0.15)',
                    tension: 0.35,
                    pointRadius: 5
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
                x: { grid: { display: false } },
                y: { grid: { color: 'rgba(15, 23, 42, 0.08)' } }
            }
        }
    });

    const demoGenderChart = mountPlaceholderChart('demoGenderChart', {
        type: 'doughnut',
        data: {
            labels: ['Female', 'Male', 'Non-binary'],
            datasets: [
                {
                    data: [54, 44, 2],
                    backgroundColor: ['#6366f1', '#22c55e', '#f97316'],
                    borderWidth: 0
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { position: 'bottom' } }
        }
    });

    const demoAgeChart = mountPlaceholderChart('demoAgeChart', {
        type: 'bar',
        data: {
            labels: ['17-18', '19-20', '21-22', '23-24', '25+'],
            datasets: [
                {
                    label: 'Students',
                    data: [1620, 3180, 2480, 1040, 360],
                    backgroundColor: 'rgba(37, 99, 235, 0.75)',
                    borderRadius: 10
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
                x: { grid: { display: false } },
                y: { grid: { color: 'rgba(15, 23, 42, 0.08)' } }
            }
        }
    });

    mountPlaceholderChart('demoStatusChart', {
        type: 'doughnut',
        data: {
            labels: ['Active', 'LOA', 'Graduated'],
            datasets: [
                {
                    data: [78, 8, 14],
                    backgroundColor: ['#22c55e', '#f97316', '#6366f1'],
                    borderWidth: 0
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { position: 'bottom' } }
        }
    });

    mountPlaceholderChart('teacherPassChart', {
        type: 'bar',
        data: {
            labels: ['Free Elective', 'Database', 'Algorithms 2', 'Capstone'],
            datasets: [
                {
                    label: 'Pass %',
                    data: [88, 91, 79, 84],
                    backgroundColor: ['#2563eb', '#22c55e', '#f97316', '#a855f7'],
                    borderRadius: 8
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
                y: { beginAtZero: true, max: 100 }
            }
        }
    });

    mountPlaceholderChart('studentGwaChart', {
        type: 'line',
        data: {
            labels: ['2023-1', '2023-2', '2024-1', '2024-2', '2025-1'],
            datasets: [
                {
                    label: 'GWA',
                    data: [2.45, 2.31, 2.18, 2.35, 2.12],
                    borderColor: '#1d4ed8',
                    backgroundColor: 'rgba(29, 78, 216, 0.2)',
                    tension: 0.35,
                    pointRadius: 5
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
                y: { reverse: true, min: 1.0, max: 3.0 }
            }
        }
    });

    mountPlaceholderChart('studentCourseChart', {
        type: 'bar',
        data: {
            labels: ['MOBDEV', 'DATA', 'ALGO2', 'CAPSTONE', 'MATH'],
            datasets: [
                {
                    label: 'Grade',
                    data: [1.5, 1.75, 2.25, 2.5, 2.75],
                    backgroundColor: '#0ea5e9',
                    borderRadius: 8
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
                y: { reverse: true, min: 1.0, max: 3.5 }
            }
        }
    });

    mountPlaceholderChart('studentStatusChart', {
        type: 'doughnut',
        data: {
            labels: ['Pass', 'Fail', 'Incomplete'],
            datasets: [
                {
                    data: [12, 2, 1],
                    backgroundColor: ['#22c55e', '#ef4444', '#facc15'],
                    borderWidth: 0
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { position: 'bottom' } }
        }
    });

    // default active tab
    activateTab('overall');
})();
