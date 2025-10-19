// TeacherCourses JavaScript - Grade Management and Print Functionality
// Global variables for UI state management
let currentAssignedCourseId = 0;

// UI-only functions for panel management
function showEditPanel(edpCode, subject, schedule, assignedCourseId) {
    currentAssignedCourseId = assignedCourseId;
    
    // Update panel header with course information
    document.getElementById("edpCodeText").innerText = edpCode;
    document.getElementById("subjectText").innerText = subject;
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

// UI-only function for rendering students table
function renderStudentsTable(students) {
    const tbody = document.getElementById('studentsTableBody');
    tbody.innerHTML = '';

    if (!students || students.length === 0) {
        tbody.innerHTML = '<tr><td colspan="10" class="text-center text-muted">No students found for this course</td></tr>';
        return;
    }

    students.forEach((student, index) => {
        const row = document.createElement('tr');
        row.innerHTML = `
            <td>${index + 1}</td>
            <td>${student.idNumber || ''}</td>
            <td>${student.lastName || ''}</td>
            <td>${student.firstName || ''}</td>
            <td>${student.courseYear || ''}</td>
            <td>${student.gender || ''}</td>
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
        `;
        tbody.appendChild(row);
    });
}

// Saving grades
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

    // Send data to C# controller
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
            showSuccessMessage('Grades saved successfully!');
            hideEditPanel();
        } else {
            showErrorMessage('Error saving grades: ' + data.message);
        }
    })
    .catch(error => {
        console.error('Error:', error);
        showErrorMessage('An error occurred while saving grades');
    });
}

// UI-only utility functions for user feedback
function showSuccessMessage(message) {
    // You can replace this with a more sophisticated notification system
    alert(message);
}

function showErrorMessage(message) {
    // You can replace this with a more sophisticated notification system
    alert(message);
}

// Print to PDF - Server-side approach
function printGrades(edpCode, subject, schedule, assignedCourseId) {
    console.log('Opening print page for:', edpCode, subject, schedule, assignedCourseId);
    
    // Build URL with parameters for the server-side print action
    const printUrl = `/Teacher/PrintGrades?assignedCourseId=${assignedCourseId}&edpCode=${encodeURIComponent(edpCode)}&subject=${encodeURIComponent(subject)}&schedule=${encodeURIComponent(schedule)}`;
    
    // Open in new window/tab
    window.open(printUrl, '_blank');
}

// Note: populatePrintTable function removed - now using server-side rendering like student version

// Excel Upload Functions
let currentUploadAssignedCourseId = 0;

function openExcelUploadModal(edpCode, subject, schedule, assignedCourseId) {
    currentUploadAssignedCourseId = assignedCourseId;
    
    // Update course info in modal
    document.getElementById('uploadCourseInfo').textContent = `${edpCode} - ${subject} | ${schedule}`;
    
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
            
            // Refresh the grade data if successful
            if (currentAssignedCourseId > 0) {
                loadStudentsForCourse(currentAssignedCourseId);
            }
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