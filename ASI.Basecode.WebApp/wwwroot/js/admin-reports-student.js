(function () {
    const ChartLib = window.Chart;
    const initialDashboard = window.AdminReportsInitial || {};

    if (!ChartLib) {
        return;
    }

    const studentSelect = document.querySelector("[data-selector='student']");
    const gradeTableBody = document.querySelector("[data-table='student-grades'] tbody");

    const unitsElements = {
        percent: document.querySelector("[data-field='units-percent']"),
        fill: document.querySelector("[data-field='units-fill']"),
        earned: document.querySelector("[data-field='units-earned']"),
        remaining: document.querySelector("[data-field='units-remaining']"),
        required: document.querySelector("[data-field='units-required']")
    };

    const courseCanvas = document.getElementById("studentCourseChart");
    const workloadCanvas = document.getElementById("studentWorkloadMatrix");

    const workloadQuadrantPlugin = {
        id: "workloadQuadrant",
        afterDraw(chart) {
            const { ctx, chartArea, scales } = chart;
            if (!chartArea || !scales?.x || !scales?.y) {
                return;
            }

            const highUnitThreshold = 3;
            const gradeThresholdPercent = 75;

            const xThresholdPixel = scales.x.getPixelForValue(highUnitThreshold);
            const yThresholdPixel = scales.y.getPixelForValue(gradeThresholdPercent);

            ctx.save();
            ctx.fillStyle = "rgba(220, 38, 38, 0.08)";
            ctx.fillRect(xThresholdPixel, yThresholdPixel, chartArea.right - xThresholdPixel, chartArea.bottom - yThresholdPixel);

            ctx.strokeStyle = "rgba(148, 163, 184, 0.9)";
            ctx.setLineDash([6, 6]);

            ctx.beginPath();
            ctx.moveTo(xThresholdPixel, chartArea.top);
            ctx.lineTo(xThresholdPixel, chartArea.bottom);
            ctx.stroke();

            ctx.beginPath();
            ctx.moveTo(chartArea.left, yThresholdPixel);
            ctx.lineTo(chartArea.right, yThresholdPixel);
            ctx.stroke();

            ctx.restore();
        }
    };

    ChartLib.register(workloadQuadrantPlugin);

    let courseChart;
    let workloadChart;

    function ensureCharts() {
        if (!courseChart && courseCanvas) {
            courseChart = new ChartLib(courseCanvas.getContext("2d"), {
                type: "bar",
                data: {
                    labels: [],
                    datasets: [
                        {
                            label: "Average Grade (%)",
                            data: [],
                            backgroundColor: "#2563eb",
                            borderRadius: 8
                        }
                    ]
                },
                options: {
                    responsive: true,
                    plugins: {
                        legend: { display: false }
                    },
                    scales: {
                        y: {
                            beginAtZero: true,
                            suggestedMax: 100,
                            title: {
                                display: true,
                                text: "Grade (%)"
                            }
                        },
                        x: {
                            title: {
                                display: true,
                                text: "Course"
                            }
                        }
                    }
                }
            });
        }

        if (!workloadChart && workloadCanvas) {
            workloadChart = new ChartLib(workloadCanvas.getContext("2d"), {
                type: "scatter",
                data: {
                    datasets: [
                        {
                            label: "Courses",
                            data: [],
                            pointRadius: 6,
                            pointHoverRadius: 8,
                            backgroundColor: []
                        }
                    ]
                },
                options: {
                    responsive: true,
                    parsing: false,
                    plugins: {
                        legend: { display: false },
                        tooltip: {
                            callbacks: {
                                label(context) {
                                    const point = context.raw || {};
                                    const course = point.courseCode || "Course";
                                    const units = point.x ?? 0;
                                    const percent = point.y ?? 0;
                                    return `${course}: ${percent.toFixed(1)}% • ${units} unit(s)`;
                                }
                            }
                        }
                    },
                    scales: {
                        x: {
                            beginAtZero: true,
                            suggestedMax: 6,
                            ticks: {
                                stepSize: 1
                            },
                            title: {
                                display: true,
                                text: "Units"
                            }
                        },
                        y: {
                            beginAtZero: true,
                            suggestedMax: 100,
                            title: {
                                display: true,
                                text: "Final Grade (%)"
                            }
                        }
                    }
                }
            });
        }
    }

    function formatGrade(value) {
        const number = Number(value);
        return Number.isFinite(number) ? number.toFixed(2) : "0.00";
    }

    function renderCourseChart(courseGrades) {
        ensureCharts();
        if (!courseChart) {
            return;
        }

        const labels = courseGrades.map(course => course.CourseCode || "--");
        const data = courseGrades.map(course => Number(course.Percent ?? 0));

        courseChart.data.labels = labels;
        courseChart.data.datasets[0].data = data;
        courseChart.update();
    }

    function renderWorkloadChart(courseGrades) {
        ensureCharts();
        if (!workloadChart) {
            return;
        }

        const points = courseGrades.map(course => {
            const percent = Number(course.Percent ?? 0);
            const units = Number(course.Units ?? 0);
            return {
                x: units,
                y: percent,
                courseCode: course.CourseCode || "--",
                target: units >= 3 && percent < 75
            };
        });

        const colors = points.map(point => (point.target ? "#dc2626" : "#2563eb"));

        workloadChart.data.datasets[0].data = points;
        workloadChart.data.datasets[0].backgroundColor = colors;
        workloadChart.update();
    }

    function renderUnitsProgress(progress) {
        const earned = Number(progress?.EarnedUnits ?? 0);
        const required = Number(progress?.RequiredUnits ?? 0);
        const percent = required > 0 ? Math.min(100, Math.max(0, (earned / required) * 100)) : 0;
        const remaining = Math.max(0, required - earned);

        if (unitsElements.percent) {
            unitsElements.percent.textContent = `${percent.toFixed(1)}%`;
        }
        if (unitsElements.fill) {
            unitsElements.fill.style.width = `${percent.toFixed(1)}%`;
        }
        if (unitsElements.earned) {
            unitsElements.earned.textContent = earned.toString();
        }
        if (unitsElements.remaining) {
            unitsElements.remaining.textContent = `${remaining} units remaining`;
        }
        if (unitsElements.required) {
            unitsElements.required.textContent = required.toString();
        }
    }

    function renderGradeTable(rows) {
        if (!gradeTableBody) {
            return;
        }

        gradeTableBody.innerHTML = "";

        if (!rows || !rows.length) {
            const emptyRow = document.createElement("tr");
            emptyRow.setAttribute("data-empty", "true");
            const cell = document.createElement("td");
            cell.colSpan = 8;
            cell.className = "text-center";
            cell.textContent = "Select a student.";
            emptyRow.appendChild(cell);
            gradeTableBody.appendChild(emptyRow);
            return;
        }

        rows.forEach(row => {
            const tr = document.createElement("tr");
            tr.innerHTML = `
                <td>${row.EdpCode || "--"}</td>
                <td>${row.Subject || "--"}</td>
                <td>${formatGrade(row.Prelim)}</td>
                <td>${formatGrade(row.Midterm)}</td>
                <td>${formatGrade(row.Prefinal)}</td>
                <td>${formatGrade(row.Final)}</td>
                <td>${formatGrade(row.FinalGrade)}</td>
                <td>${row.Status || "--"}</td>
            `;
            gradeTableBody.appendChild(tr);
        });
    }

    function renderAnalytics(analytics) {
        const courseGrades = analytics?.CourseGrades || [];
        renderCourseChart(courseGrades);
        renderWorkloadChart(courseGrades);
        renderUnitsProgress(analytics?.UnitsProgress);
        renderGradeTable(analytics?.GradeBreakdown || []);
    }

    async function loadStudentAnalytics(studentId) {
        if (!studentId) {
            renderAnalytics(null);
            return;
        }

        try {
            const response = await fetch(`/Admin/ReportsStudentAnalytics?studentId=${studentId}`, {
                headers: {
                    Accept: "application/json"
                }
            });

            if (!response.ok) {
                throw new Error(`Request failed (${response.status})`);
            }

            const analytics = await response.json();
            renderAnalytics(analytics);
        } catch (error) {
            console.error("Unable to load student analytics", error);
        }
    }

    function initialize() {
        const initialSelection = studentSelect && studentSelect.value
            ? Number(studentSelect.value)
            : initialDashboard?.Student?.SelectedStudentId ?? null;

        if (studentSelect) {
            studentSelect.addEventListener("change", () => {
                const selected = studentSelect.value ? Number(studentSelect.value) : null;
                loadStudentAnalytics(selected);
            });
        }

        const initialAnalytics = initialDashboard?.Student?.Analytics || null;
        renderAnalytics(initialAnalytics);

        if (!initialAnalytics && initialSelection) {
            loadStudentAnalytics(initialSelection);
        }
    }

    initialize();
})();