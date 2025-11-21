// TeacherCourses JS for UI stuff and AJAX calls to backend
let currentAssignedCourseId = 0;

// ===== Toast helper =====
window.showToast = function (msg, type = 'success') {
    const container = initToastContainer();
    const toastId = 'toast-' + Date.now();
    const iconClass = type === 'error' ? 'bi-exclamation-circle-fill' : type === 'info' ? 'bi-info-circle-fill' : 'bi-check-circle-fill';
    const bgClass = type === 'error' ? 'bg-danger' : type === 'info' ? 'bg-primary' : 'bg-success';
    
    const toastHtml = `
        <div id="${toastId}" class="toast align-items-center text-white ${bgClass} border-0" role="alert" aria-live="assertive" aria-atomic="true">
            <div class="d-flex">
                <div class="toast-body d-flex align-items-center gap-2">
                    <i class="bi ${iconClass}"></i>
                    ${msg}
                </div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>
        </div>
    `;
    container.insertAdjacentHTML('beforeend', toastHtml);
    const toastElement = document.getElementById(toastId);
    const delay = type === 'error' ? 5000 : 3000;
    const toast = new bootstrap.Toast(toastElement, { delay: delay });
    toast.show();
    toastElement.addEventListener('hidden.bs.toast', () => toastElement.remove());
};

// ===== Refresh grades table =====
window.refreshGradesTable = async function () {
    const host = document.querySelector('.teacher-grades-card tbody');
    if (!host) {
        console.warn('Grades table not found');
        return;
    }

    try {
        // Get current filter parameters from the form
        const filterForm = document.getElementById('filterForm');
        const params = new URLSearchParams();
        
        if (filterForm) {
            const formData = new FormData(filterForm);
            for (let [key, value] of formData.entries()) {
                if (value) params.append(key, value);
            }
        }

        const url = '/Teacher/AssignedCourses?' + params.toString();
        const res = await fetch(url, {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });

        if (!res.ok) {
            console.error('Error fetching updated grades');
            showToast('Failed to refresh grades table', 'error');
            return;
        }

        // Parse the HTML response
        const html = await res.text();
        const parser = new DOMParser();
        const doc = parser.parseFromString(html, 'text/html');
        
        // Extract the table body from the response
        const newTableBody = doc.querySelector('.teacher-grades-card tbody');
        if (newTableBody) {
            host.innerHTML = newTableBody.innerHTML;
            console.log('Grades table refreshed successfully');
        }
    } catch (err) {
        console.error('Failed to refresh grades:', err);
        showToast('Failed to refresh grades table', 'error');
    }
};

// Panel
function showEditPanel(edpCode, course, program, schedule, assignedCourseId) {
    currentAssignedCourseId = assignedCourseId;
    
    // Update panel header with course information
    document.getElementById("edpCodeText").innerText = edpCode;
    document.getElementById("courseText").innerText = course;
    document.getElementById("programText").innerText = program;
    document.getElementById("scheduleText").innerText = schedule;
    
    // Load students data from server
    loadStudentsForCourse(assignedCourseId);
    
    // Show the edit panel
    document.getElementById("editPanel").classList.remove("d-none");
}

function hideEditPanel() {
    document.getElementById("editPanel").classList.add("d-none");
    currentAssignedCourseId = 0;
}

// AJAX call to C# backend for data loading
function loadStudentsForCourse(assignedCourseId) {
    fetch(`/Teacher/GetStudentsForCourse?assignedCourseId=${assignedCourseId}`)
        .then(response => {
            if (!response.ok) {
                throw new Error('Network response was not ok');
            }
            return response.json();
        })
        .then(data => {
            if (data.success) {
                renderStudentsTable(data.students);
            } else {
                showErrorMessage('Error loading students: ' + data.message);
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showErrorMessage('An error occurred while loading students');
        });
}

// Grade View filtering
document.getElementById('filterForm').addEventListener('change', function() {
    this.submit();
});

// Clear Filters
document.addEventListener('DOMContentLoaded', function() {
    const clearFiltersBtn = document.getElementById('clearFiltersBtn');
    const filterForm = document.getElementById('filterForm');
    
    if (clearFiltersBtn && filterForm) {
        clearFiltersBtn.addEventListener('click', function(e) {
            e.preventDefault();
            
            filterForm.querySelectorAll('input[type="text"]').forEach(input => {
                input.value = '';
            });
            
            filterForm.querySelectorAll('select').forEach(select => {
                select.selectedIndex = 0;
            });
            
            filterForm.submit();
        });
    }
});

// Calculate remarks based on grades (same logic as backend)
function calculateRemarks(prelims, midterm, semiFinal, final) {
    const scores = [prelims, midterm, semiFinal, final];
    const weights = [0.3, 0.3, 0.2, 0.2];
    
    let weightedTotal = 0;
    let weightSum = 0;
    
    for (let i = 0; i < scores.length; i++) {
        if (scores[i] !== null && scores[i] !== undefined && scores[i] !== '') {
            const scoreValue = parseFloat(scores[i]);
            if (!isNaN(scoreValue)) {
                weightedTotal += scoreValue * weights[i];
                weightSum += weights[i];
            }
        }
    }
    
    if (weightSum <= 0) return 'INCOMPLETE';
    
    const gpa = Math.round((weightedTotal / weightSum) * 100) / 100;
    return gpa <= 3.0 ? 'PASSED' : 'FAILED';
}

// Get badge class for remarks
function getRemarksBadgeClass(remarks) {
    if (remarks === 'PASSED') return 'bg-success';
    if (remarks === 'FAILED') return 'bg-danger';
    return 'bg-secondary';
}

// Rendering students table
function renderStudentsTable(students) {
    const tbody = document.getElementById('studentsTableBody');
    tbody.innerHTML = '';

    if (!students || students.length === 0) {
        tbody.innerHTML = '<tr><td colspan="11" class="text-center text-muted">No students found for this course</td></tr>';
        return;
    }

    students.forEach((student, index) => {
        const row = document.createElement('tr');
        const remarks = calculateRemarks(student.prelims, student.midterm, student.semiFinal, student.final);
        const badgeClass = getRemarksBadgeClass(remarks);
        
        row.innerHTML = `
            <td>${index + 1}</td>
            <td>${student.idNumber || ''}</td>
            <td>${student.lastName || ''}</td>
            <td>${student.firstName || ''}</td>
            <td>${student.programYear || ''}</td>
            <td>
                <input type="number" step="0.1" min="1.0" max="5.0" 
                       value="${student.prelims || ''}" 
                       data-grade-type="prelims" 
                       data-student-id="${student.studentId}"
                       data-grade-id="${student.gradeId}"
                       class="form-control form-control-sm grade-input" />
            </td>
            <td>
                <input type="number" step="0.1" min="1.0" max="5.0" 
                       value="${student.midterm || ''}" 
                       data-grade-type="midterm" 
                       data-student-id="${student.studentId}"
                       data-grade-id="${student.gradeId}"
                       class="form-control form-control-sm grade-input" />
            </td>
            <td>
                <input type="number" step="0.1" min="1.0" max="5.0" 
                       value="${student.semiFinal || ''}" 
                       data-grade-type="semifinal" 
                       data-student-id="${student.studentId}"
                       data-grade-id="${student.gradeId}"
                       class="form-control form-control-sm grade-input" />
            </td>
            <td>
                <input type="number" step="0.1" min="1.0" max="5.0" 
                       value="${student.final || ''}" 
                       data-grade-type="final" 
                       data-student-id="${student.studentId}"
                       data-grade-id="${student.gradeId}"
                       class="form-control form-control-sm grade-input" />
            </td>
            <td>
                <span class="badge ${badgeClass} remarks-badge" data-student-id="${student.studentId}">
                    ${remarks}
                </span>
            </td>
        `;
        tbody.appendChild(row);
    });
    
    // Attach event listeners to grade inputs for real-time remarks calculation
    document.querySelectorAll('.grade-input').forEach(input => {
        input.addEventListener('input', updateRemarksForStudent);
    });
}

// Update remarks in real-time as grades change
function updateRemarksForStudent(event) {
    const input = event.target;
    const studentId = input.getAttribute('data-student-id');
    const row = input.closest('tr');
    
    // Get all grade inputs for this student
    const prelims = parseFloat(row.querySelector('[data-grade-type="prelims"]').value) || null;
    const midterm = parseFloat(row.querySelector('[data-grade-type="midterm"]').value) || null;
    const semiFinal = parseFloat(row.querySelector('[data-grade-type="semifinal"]').value) || null;
    const final = parseFloat(row.querySelector('[data-grade-type="final"]').value) || null;
    
    // Calculate new remarks
    const remarks = calculateRemarks(prelims, midterm, semiFinal, final);
    const badgeClass = getRemarksBadgeClass(remarks);
    
    // Update the remarks badge
    const remarksBadge = row.querySelector('[data-student-id="' + studentId + '"].remarks-badge');
    if (remarksBadge) {
        remarksBadge.textContent = remarks;
        remarksBadge.className = `badge ${badgeClass} remarks-badge`;
        remarksBadge.setAttribute('data-student-id', studentId);
    }
}

// Save grades
function saveGrades() {
    const gradeInputs = document.querySelectorAll('.grade-input');
    const grades = [];

    // Collect grade data from UI inputs
    gradeInputs.forEach(input => {
        const value = parseFloat(input.value);
        if (!isNaN(value) && value >= 1.0 && value <= 5.0) {
            const gradeType = input.getAttribute('data-grade-type');
            const studentId = parseInt(input.getAttribute('data-student-id'));
            const gradeId = parseInt(input.getAttribute('data-grade-id'));

            let existingGrade = grades.find(g => g.gradeId === gradeId);
            if (!existingGrade) {
                existingGrade = {
                    gradeId: gradeId,
                    studentId: studentId,
                    assignedCourseId: currentAssignedCourseId,
                    prelims: null,
                    midterm: null,
                    semiFinal: null,
                    final: null
                };
                grades.push(existingGrade);
            }

            existingGrade[gradeType] = value;
        }
    });

    if (grades.length === 0) {
        showErrorMessage('No valid grades to save');
        return;
    }

    // Send data to controller
    fetch('/Teacher/UpdateGrades', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value
        },
        body: JSON.stringify(grades)
    })
    .then(response => {
        if (!response.ok) {
            throw new Error('Network response was not ok');
        }
        return response.json();
    })
    .then(data => {
        if (data.success) {
            showToast('Grades saved successfully!', 'success');
            hideEditPanel();
            // Refresh the grades table with updated data
            window.refreshGradesTable();
        } else {
            showToast('Error saving grades: ' + data.message, 'error');
        }
    })
    .catch(error => {
        console.error('Error:', error);
        showToast('An error occurred while saving grades', 'error');
    });
}

// Toast Container Setup
function initToastContainer() {
    let toastContainer = document.querySelector('.toast-container.teacher-toast-container');
    if (!toastContainer) {
        toastContainer = document.createElement('div');
        toastContainer.className = 'toast-container position-fixed top-0 end-0 p-3 teacher-toast-container';
        toastContainer.style.zIndex = '1100';
        document.body.appendChild(toastContainer);
    }
    return toastContainer;
}

// Print to PDF
function printGrades(edpCode, course, program, schedule, assignedCourseId) {
    console.log('Opening print page for:', edpCode, course, program, schedule, assignedCourseId);
    
    const printUrl = `/Teacher/PrintGrades?assignedCourseId=${assignedCourseId}`;
    
    window.open(printUrl, '_blank');
}

// Excel Upload Functions
let currentUploadAssignedCourseId = 0;

function openExcelUploadModal(edpCode, course, program, schedule, assignedCourseId) {
    currentUploadAssignedCourseId = assignedCourseId;
    
    // Update course info in modal
    document.getElementById('uploadCourseInfo').textContent = `${edpCode} - ${course} | ${program} | ${schedule}`;
    
    // Reset modal state
    document.getElementById('excelFileInput').value = '';
    document.getElementById('uploadProgress').style.display = 'none';
    document.getElementById('uploadResults').style.display = 'none';
    
    // Show modal
    const modal = new bootstrap.Modal(document.getElementById('excelUploadModal'));
    modal.show();
}

function downloadTemplate() {
    if (currentUploadAssignedCourseId === 0) {
        showErrorMessage('No course selected');
        return;
    }
    
    // Create a temporary link to download the template
    const link = document.createElement('a');
    link.href = `/Teacher/DownloadGradeTemplate?assignedCourseId=${currentUploadAssignedCourseId}`;
    link.target = '_blank';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}

function uploadExcelFile() {
    const fileInput = document.getElementById('excelFileInput');
    const file = fileInput.files[0];
    
    if (!file) {
        showExcelUploadError('Please select an Excel file');
        return;
    }
    
    if (currentUploadAssignedCourseId === 0) {
        showExcelUploadError('No course selected');
        return;
    }
    
    // Validate file type
    const allowedTypes = [
        'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', // .xlsx
        'application/vnd.ms-excel' // .xls
    ];
    
    if (!allowedTypes.includes(file.type) && !file.name.match(/\.(xlsx|xls)$/i)) {
        showExcelUploadError('Please select a valid Excel file (.xlsx or .xls)');
        return;
    }
    
    // Validate file size (5MB)
    if (file.size > 5 * 1024 * 1024) {
        showExcelUploadError('File size must be less than 5MB');
        return;
    }
    
    // Show progress
    document.getElementById('uploadProgress').style.display = 'block';
    document.getElementById('uploadResults').style.display = 'none';
    document.getElementById('uploadExcelBtn').disabled = true;
    
    // Create FormData
    const formData = new FormData();
    formData.append('excelFile', file);
    formData.append('assignedCourseId', currentUploadAssignedCourseId);
    
    // Upload file
    fetch('/Teacher/UploadExcelGrades', {
        method: 'POST',
        headers: {
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value
        },
        body: formData
    })
    .then(response => response.json())
    .then(data => {
        document.getElementById('uploadProgress').style.display = 'none';
        document.getElementById('uploadExcelBtn').disabled = false;
        
        if (data.success) {
            showExcelUploadSuccess(data.message, data.processedCount, data.errorCount, data.errors);
        } else {
            showExcelUploadError(data.message, data.errors);
        }
    })
    .catch(error => {
        console.error('Error:', error);
        document.getElementById('uploadProgress').style.display = 'none';
        document.getElementById('uploadExcelBtn').disabled = false;
        showExcelUploadError('An error occurred while uploading the file');
    });
}

function showExcelUploadSuccess(message, processedCount, errorCount, errors) {
    const resultsDiv = document.getElementById('uploadResults');
    const alertDiv = document.getElementById('uploadResultAlert');
    const messageDiv = document.getElementById('uploadResultMessage');
    const errorListDiv = document.getElementById('uploadErrorList');
    const errorsList = document.getElementById('uploadErrors');
    
    alertDiv.className = 'alert alert-success';
    messageDiv.innerHTML = `
        <strong>Upload Successful!</strong><br>
        ${message}<br>
        <small>Processed: ${processedCount} students</small>
    `;
    
    if (errors && errors.length > 0) {
        errorsList.innerHTML = errors.map(error => `<li>${error}</li>`).join('');
        errorListDiv.style.display = 'block';
    } else {
        errorListDiv.style.display = 'none';
    }
    
    resultsDiv.style.display = 'block';
    
    // Refresh table data and close modal after showing results (like admin pattern)
    setTimeout(async () => {
        await window.refreshGradesTable();
        showSuccessMessage('Grades updated successfully!');
        
        // Close the modal
        const excelUploadModal = document.getElementById('excelUploadModal');
        if (excelUploadModal) {
            const modal = bootstrap.Modal.getInstance(excelUploadModal);
            if (modal) {
                modal.hide();
            }
        }
    }, 1500);
}

function showExcelUploadError(message, errors) {
    const resultsDiv = document.getElementById('uploadResults');
    const alertDiv = document.getElementById('uploadResultAlert');
    const messageDiv = document.getElementById('uploadResultMessage');
    const errorListDiv = document.getElementById('uploadErrorList');
    const errorsList = document.getElementById('uploadErrors');
    
    alertDiv.className = 'alert alert-danger';
    messageDiv.innerHTML = `<strong>Upload Failed!</strong><br>${message}`;
    
    if (errors && errors.length > 0) {
        errorsList.innerHTML = errors.map(error => `<li>${error}</li>`).join('');
        errorListDiv.style.display = 'block';
    } else {
        errorListDiv.style.display = 'none';
    }
    
    resultsDiv.style.display = 'block';
}

// Event listeners for Excel upload modal
document.addEventListener('DOMContentLoaded', function() {
    // Download template button
    document.getElementById('downloadTemplateBtn')?.addEventListener('click', downloadTemplate);
    
    // Upload excel button
    document.getElementById('uploadExcelBtn')?.addEventListener('click', uploadExcelFile);
    
    // File input change event for validation
    document.getElementById('excelFileInput')?.addEventListener('change', function() {
        const file = this.files[0];
        if (file) {
            // Reset previous results
            document.getElementById('uploadResults').style.display = 'none';
            
            // Basic validation
            if (!file.name.match(/\.(xlsx|xls)$/i)) {
                showExcelUploadError('Please select a valid Excel file (.xlsx or .xls)');
                this.value = '';
                return;
            }
            
            if (file.size > 5 * 1024 * 1024) {
                showExcelUploadError('File size must be less than 5MB');
                this.value = '';
                return;
            }
        }
    });
});