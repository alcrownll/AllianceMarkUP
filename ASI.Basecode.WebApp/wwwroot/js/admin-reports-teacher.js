(function () {
    const ChartLib = window.Chart;
    const initialDashboard = window.AdminReportsInitial || {};

    if (!ChartLib) {
        return;
    }

    const teacherSelect = document.querySelector("[data-selector='teacher']");
    const nameField = document.querySelector("[data-field='teacher-name']");
    const metaField = document.querySelector("[data-field='teacher-meta']");
    const loadField = document.querySelector("[data-field='teacher-load']");
    const sectionField = document.querySelector("[data-field='teacher-sections']");
    const passField = document.querySelector("[data-field='teacher-pass']");
    const submissionField = document.querySelector("[data-field='teacher-submissions']");
    const assignmentsBody = document.querySelector("[data-table='teacher-assignments'] tbody");
    const submissionList = document.querySelector("[data-list='teacher-submissions']");

    const courseCanvas = document.getElementById("teacherCourseChart");
    const submissionCanvas = document.getElementById("teacherSubmissionChart");

    let courseChart;
    let submissionChart;
    let currentDetail = null;

    function ensureCharts() {
        if (!courseChart && courseCanvas) {
            courseChart = new ChartLib(courseCanvas.getContext("2d"), {
                type: "bar",
                data: {
                    labels: [],
                    datasets: [
                        {
                            label: "Pass Rate (%)",
                            data: [],
                            backgroundColor: "#22c55e",
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
                        x: {
                            title: {
                                display: true,
                                text: "Course"
                            }
                        },
                        y: {
                            beginAtZero: true,
                            suggestedMax: 100,
                            title: {
                                display: true,
                                text: "Pass Rate (%)"
                            }
                        }
                    }
                }
            });
        }

        if (!submissionChart && submissionCanvas) {
            submissionChart = new ChartLib(submissionCanvas.getContext("2d"), {
                type: "doughnut",
                data: {
                    labels: [],
                    datasets: [
                        {
                            data: [],
                            backgroundColor: ["#2563eb", "#f97316", "#64748b"]
                        }
                    ]
                },
                options: {
                    responsive: true,
                    plugins: {
                        legend: {
                            position: "bottom"
                        }
                    },
                    cutout: "60%"
                }
            });
        }
    }

    function renderAssignments(assignments) {
        if (!assignmentsBody) {
            return;
        }

        assignmentsBody.innerHTML = "";

        if (!assignments || !assignments.length) {
            const row = document.createElement("tr");
            row.setAttribute("data-empty", "true");
            const cell = document.createElement("td");
            cell.colSpan = 5;
            cell.className = "text-center";
            cell.textContent = "No assignments available.";
            row.appendChild(cell);
            assignmentsBody.appendChild(row);
            return;
        }

        assignments.forEach(assignment => {
            const row = document.createElement("tr");
            row.innerHTML = `
                <td>${assignment.CourseCode || "--"}</td>
                <td>${assignment.Section || "--"}</td>
                <td>${assignment.Schedule || "--"}</td>
                <td>${assignment.Units ?? 0}</td>
                <td>${assignment.Enrolled ?? 0}</td>
            `;
            assignmentsBody.appendChild(row);
        });
    }

    function renderSubmissionStatuses(statuses) {
        if (!submissionList) {
            return;
        }

        submissionList.innerHTML = "";

        if (!statuses || !statuses.length) {
            const item = document.createElement("li");
            item.className = "pending";
            item.setAttribute("data-empty", "true");
            item.textContent = "No submission updates.";
            submissionList.appendChild(item);
            return;
        }

        statuses.forEach(status => {
            const item = document.createElement("li");
            item.className = status.IsComplete ? "complete" : "pending";
            item.textContent = `${status.CourseCode || "--"}: ${status.Status || "--"}`;
            submissionList.appendChild(item);
        });
    }

    function renderCourseSummary(coursePassRates) {
        ensureCharts();
        if (!courseChart) {
            return;
        }

        const labels = (coursePassRates || []).map(item => item.CourseCode || "--");
        const data = (coursePassRates || []).map(item => Number(item.PassRatePercent ?? 0));

        courseChart.data.labels = labels;
        courseChart.data.datasets[0].data = data;
        courseChart.update();
    }

    function renderSubmissionProgress(summary) {
        ensureCharts();
        if (!submissionChart) {
            return;
        }

        const labels = (summary || []).map(item => item.Name || "");
        const data = (summary || []).map(item => Number(item.Value ?? 0));

        submissionChart.data.labels = labels.length ? labels : ["Completed", "Pending"];
        submissionChart.data.datasets[0].data = data.length ? data : [0, 0];
        submissionChart.update();
    }

    function renderOverview(detail) {
        const teacherName = detail?.Name || "Select a teacher";
        const metaParts = [];
        if (detail?.Department) {
            metaParts.push(detail.Department);
        }
        if (detail?.Email) {
            metaParts.push(detail.Email);
        }
        if (detail?.Rank) {
            metaParts.push(detail.Rank);
        }

        if (nameField) {
            nameField.textContent = teacherName;
        }
        if (metaField) {
            metaField.textContent = metaParts.length ? metaParts.join(" • ") : "--";
        }
        if (loadField) {
            loadField.textContent = `${Number(detail?.TeachingLoadUnits ?? 0).toFixed(1)} units`;
        }
        if (sectionField) {
            sectionField.textContent = Number(detail?.SectionCount ?? 0).toString();
        }
        if (passField) {
            passField.textContent = `${Number(detail?.PassRatePercent ?? 0).toFixed(1)}%`;
        }
        if (submissionField) {
            submissionField.textContent = `${Number(detail?.SubmissionCompletionPercent ?? 0).toFixed(1)}%`;
        }
    }

    function renderDetail(detail) {
        currentDetail = detail || null;
        renderOverview(detail);
        renderCourseSummary(detail?.CoursePassRates);
        renderSubmissionProgress(detail?.SubmissionSummary);
        renderAssignments(detail?.Assignments);
        renderSubmissionStatuses(detail?.SubmissionStatuses);
    }

    async function loadTeacherDetail(teacherId) {
        if (!teacherId) {
            renderDetail(null);
            return;
        }

        try {
            const response = await fetch(`/Admin/ReportsTeacherDetail?teacherId=${teacherId}`, {
                headers: {
                    Accept: "application/json"
                }
            });

            if (!response.ok) {
                throw new Error(`Request failed (${response.status})`);
            }

            const detail = await response.json();
            renderDetail(detail);
        } catch (error) {
            console.error("Unable to load teacher detail", error);
        }
    }

    function exportAssignments(detail) {
        const assignments = detail?.Assignments;
        if (!assignments || !assignments.length) {
            console.warn("Nothing to export");
            return;
        }

        const header = ["Course", "Section", "Schedule", "Units", "Enrolled"];
        const rows = assignments.map(assignment => [
            assignment.CourseCode || "",
            assignment.Section || "",
            assignment.Schedule || "",
            assignment.Units ?? "",
            assignment.Enrolled ?? ""
        ]);

        const csvLines = [header, ...rows].map(row => row
            .map(value => {
                const text = String(value ?? "");
                if (text.includes(",") || text.includes("\"") || text.includes("\n")) {
                    return `"${text.replace(/"/g, '""')}"`;
                }
                return text;
            })
            .join(","));

        const blob = new Blob([csvLines.join("\r\n")], { type: "text/csv;charset=utf-8;" });
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        const teacherId = detail?.TeacherId || "teacher";
        link.href = url;
        link.download = `teacher-assignments-${teacherId}.csv`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
    }

    function initialize() {
        const initialTeacherId = teacherSelect && teacherSelect.value
            ? Number(teacherSelect.value)
            : initialDashboard?.Teacher?.SelectedTeacher?.TeacherId ?? null;

        if (teacherSelect) {
            teacherSelect.addEventListener("change", () => {
                const teacherId = teacherSelect.value ? Number(teacherSelect.value) : null;
                loadTeacherDetail(teacherId);
            });
        }

        document.addEventListener("click", event => {
            const exportButton = event.target.closest("[data-action='export-teacher']");
            if (exportButton) {
                exportAssignments(currentDetail);
                return;
            }

            const printButton = event.target.closest("[data-action='print-teacher']");
            if (printButton) {
                window.print();
            }
        });

        const initialDetail = initialDashboard?.Teacher?.SelectedTeacher || null;
        renderDetail(initialDetail);

        if (!initialDetail && initialTeacherId) {
            loadTeacherDetail(initialTeacherId);
        }
    }

    initialize();
})();