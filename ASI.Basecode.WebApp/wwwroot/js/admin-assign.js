(function () {
    'use strict';

    const $$ = (sel, root = document) => Array.from(root.querySelectorAll(sel));
    const $ = (sel, root = document) => root.querySelector(sel);

    function debounce(fn, ms) {
        let t;
        return (...args) => { clearTimeout(t); t = setTimeout(() => fn.apply(null, args), ms); };
    }

    function readInput(name, root = document) {
        return (root.querySelector(`input[name="${name}"]`)?.value ?? '').trim();
    }

    function toInt(x, def = 0) {
        const n = parseInt(x, 10);
        return Number.isFinite(n) ? n : def;
    }

    function buildUrl(path, params = {}) {
        const u = new URL(path, window.location.origin);
        Object.entries(params).forEach(([k, v]) => {
            if (v !== undefined && v !== null && String(v).length) u.searchParams.set(k, v);
        });
        return u.toString();
    }

    function attachCourseTypeUnits({ courseSel, typeSel, unitsInput, initialType }) {
        const selCourse = $(courseSel);
        const selType = $(typeSel);
        const unitsBox = $(unitsInput);
        const initType = initialType || selType?.dataset?.initialType || selType?.value || '';

        function syncUnitsFromType() {
            const u = selType?.options?.[selType.selectedIndex]?.getAttribute('data-units');
            if (unitsBox) unitsBox.value = u || '';
        }

        function rebuildTypeOptions(preserve = true) {
            if (unitsBox) unitsBox.value = '';
            const opt = selCourse?.options?.[selCourse.selectedIndex];
            if (!opt || !opt.value) {
                if (selType) {
                    selType.innerHTML = '<option value="">-- Select --</option>';
                    selType.disabled = true;
                }
                return;
            }
            const lec = toInt(opt.getAttribute('data-lec'), 0);
            const lab = toInt(opt.getAttribute('data-lab'), 0);
            const prevVal = preserve ? (selType?.value || initType) : '';

            const options = [];
            if (lec > 0) options.push({ text: 'Lecture', value: 'Lecture', units: lec });
            if (lab > 0) options.push({ text: 'Laboratory', value: 'Laboratory', units: lab });

            if (selType) {
                selType.innerHTML =
                    '<option value="">-- Select --</option>' +
                    options.map(o => `<option value="${o.value}" data-units="${o.units}">${o.text}</option>`).join('');
                selType.disabled = options.length === 0;

                const chosen = options.some(o => o.value === prevVal) ? prevVal : '';
                selType.value = chosen;
                syncUnitsFromType();
            }
        }

        selCourse && selCourse.addEventListener('change', () => rebuildTypeOptions(true));
        selType && selType.addEventListener('change', syncUnitsFromType);
        rebuildTypeOptions(true);
    }

    function initAdminAssignIndex() {
        $$('.js-auto-toast').forEach(el => {
            try {
                const toast = new bootstrap.Toast(el);
                toast.show();
            } catch (e) {
                console.warn('Toast init failed:', e);
            }
        });
    }

    function initAdminAssignPhase2() {
        if (!$('#CourseId') || !$('#Type') || !$('#Units')) return; 
        if (!$('#ModeHidden') || !$('#blockFields')) return;       

        attachCourseTypeUnits({
            courseSel: '#CourseId',
            typeSel: '#Type',
            unitsInput: '#Units'
        });

        const sectionSets = {
            '1st Year': ['1A', '1B', '1C', '1D', '1E'],
            '2nd Year': ['2A', '2B', '2C', '2D', '2E'],
            '3rd Year': ['3A', '3B', '3C', '3D', '3E'],
            '4th Year': ['4A', '4B', '4C', '4D', '4E']
        };
        const blockProgramSel = $('#BlockProgramId');
        const blockYearSel = $('#BlockYearLevel');
        const blockSectionSel = $('#BlockSection');

        const serverSelectedSection = readInput('blockSection') || '';

        function populateBlockSections(preserveSelected) {
            if (!blockYearSel || !blockSectionSel) return;
            const yr = blockYearSel.value;
            const list = sectionSets[yr] || [];
            const current = preserveSelected ? serverSelectedSection : '';
            blockSectionSel.innerHTML =
                '<option value="">Select Section</option>' +
                list.map(s => `<option value="${s}">${s}</option>`).join('');
            blockSectionSel.value = (current && list.includes(current)) ? current : '';
        }

        function syncBlockSectionState() {
            if (!blockSectionSel) return;
            const hasYear = (blockYearSel?.value || '').trim() !== '';
            blockSectionSel.disabled = !hasYear;
            if (!hasYear) {
                blockSectionSel.value = '';
                blockSectionSel.innerHTML = '<option value="">Select Section</option>';
            }
        }

        blockYearSel && blockYearSel.addEventListener('change', () => {
            populateBlockSections(false);
            syncBlockSectionState();
        });

        populateBlockSections(true);
        syncBlockSectionState();

        const radioBlock = $('#extraOff');
        const radioManual = $('#extraOn');
        const manualWrap = $('#manualFields');
        const modeHidden = $('#ModeHidden');

        function currentMode() {
            return (radioManual?.checked ? 'manual' : 'block') || 'block';
        }
        function syncModeUI() {
            const isManual = currentMode() === 'manual';
            if (manualWrap) manualWrap.style.display = isManual ? 'block' : 'none';
            if (modeHidden) modeHidden.value = isManual ? 'manual' : 'block';
            if (isManual && (blockSectionSel?.value || '').trim() !== '') {
                fetchManualTable(1);
            }
        }
        radioBlock && radioBlock.addEventListener('change', syncModeUI);
        radioManual && radioManual.addEventListener('change', syncModeUI);
        syncModeUI();

        // 4) Manual table (AJAX)
        const host = $('#manualTableHost');
        const spin = $('#manualTableSpinner');
        const statusVal = readInput('status') || 'Active';
        const pageSizeVal = readInput('pageSize') || '10';

        function showSpinner(on) { if (spin) spin.classList.toggle('d-none', !on); }

        async function fetchManualTable(page) {
            if (currentMode() !== 'manual') return;

            const p = blockProgramSel?.value || '';
            const y = blockYearSel?.value || '';
            const s = blockSectionSel?.value || '';

            const url = buildUrl('/AdminAssign/ManualTable', {
                blockProgram: p,
                blockYear: y,
                blockSection: s,
                status: statusVal,
                page: String(page || 1),
                pageSize: String(pageSizeVal || 10)
            });

            showSpinner(true);
            try {
                const resp = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
                const html = await resp.text();
                if (host) host.innerHTML = html;
                wireSelectAll();
                wirePagination();
            } catch (e) {
                console.error('Failed to load manual table:', e);
            } finally {
                showSpinner(false);
            }
        }
        const debouncedFetch = debounce((page) => fetchManualTable(page), 200);

        function wireSelectAll() {
            const selectAll = $('#selectAllStudents', host || document);
            if (selectAll) {
                selectAll.addEventListener('change', function () {
                    $$('#manualTableHost .student-checkbox').forEach(cb => cb.checked = selectAll.checked);
                });
            }
        }

        function wirePagination() {
            $$('#manualTableHost a.js-page').forEach(a => {
                a.addEventListener('click', function (e) {
                    e.preventDefault();
                    const p = toInt(this.getAttribute('data-page'), 1);
                    if (p) fetchManualTable(p);
                });
            });
        }

        blockSectionSel && blockSectionSel.addEventListener('change', () => debouncedFetch(1));

        const btnReset = $('#btnResetBlockFilters');

        async function fetchManualTableReset(page) {
            const url = buildUrl('/AdminAssign/ManualTable', {
                status: statusVal,
                page: String(page || 1),
                pageSize: String(pageSizeVal || 10)
            });
            showSpinner(true);
            try {
                const resp = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
                const html = await resp.text();
                if (host) host.innerHTML = html;
                wireSelectAll();
                wirePagination();
            } catch (e) {
                console.error('Failed to reset manual table:', e);
            } finally {
                showSpinner(false);
            }
        }

        function resetBlockUI() {
            if (blockProgramSel) blockProgramSel.selectedIndex = 0; // first option
            if (blockYearSel) blockYearSel.value = '';
            if (blockSectionSel) {
                blockSectionSel.value = '';
                blockSectionSel.disabled = true;
                blockSectionSel.innerHTML = '<option value="">Select Section</option>';
            }
        }

        // Expose for partials/other hooks, but safe if not used
        window.resetBlockFilters = function () {
            resetBlockUI();
            const isManual = $('#extraOn')?.checked;
            if (isManual) fetchManualTableReset(1);
        };

        btnReset?.addEventListener('click', () => window.resetBlockFilters());
        blockSectionSel && blockSectionSel.addEventListener('change', () => {
            if ((blockSectionSel.value || '').trim() === '') {
                window.resetBlockFilters();
            }
        });

        wireSelectAll();
        wirePagination();
    }

    function initAdminAssignView() {
        if (!$('#addStudentsModal')) return;

        attachCourseTypeUnits({
            courseSel: '#CourseId',
            typeSel: '#Type',
            unitsInput: '#Units',
            initialType: ($('#Type')?.dataset?.initialType || $('#Type')?.value || '')
        });

        const tbody = $('#enrolledBody');
        const chkAll = $('#chkRemoveAll');

        const getRowBoxes = () => $$('#enrolledBody .remove-checkbox');

        function refreshRemoveSummary() {
            const picked = getRowBoxes().filter(x => x.checked);
            const wrap = $('#removeSummary');
            const cEl = $('#removeCount');
            const nEl = $('#removeNames');

            if (!wrap || !cEl || !nEl) return;

            if (picked.length > 0) {
                const pills = picked.map(cb => {
                    const tr = cb.closest('tr');
                    const name = tr?.children?.[1]?.textContent?.trim() || '';
                    return `<span class="pill">${name}</span>`;
                }).join('');

                wrap.classList.remove('d-none');
                cEl.textContent = String(picked.length);
                nEl.innerHTML = pills;
            } else {
                wrap.classList.add('d-none');
                cEl.textContent = '0';
                nEl.innerHTML = '';
            }
        }

        function syncHeaderCheckbox() {
            const boxes = getRowBoxes();
            if (!chkAll || boxes.length === 0) return;
            const checkedCount = boxes.filter(b => b.checked).length;
            chkAll.indeterminate = checkedCount > 0 && checkedCount < boxes.length;
            chkAll.checked = checkedCount === boxes.length;
        }

        tbody?.addEventListener('change', e => {
            if (e.target.classList.contains('remove-checkbox')) {
                refreshRemoveSummary();
                syncHeaderCheckbox();
            }
        });

        chkAll?.addEventListener('change', () => {
            const on = !!chkAll.checked;
            getRowBoxes().forEach(cb => { cb.checked = on; });
            refreshRemoveSummary();
            syncHeaderCheckbox();
        });

        syncHeaderCheckbox();

        const btnOpen = $('#btnOpenAddStudents');
        const modalEl = $('#addStudentsModal');
        const bsModal = modalEl ? new bootstrap.Modal(modalEl) : null;
        const host = $('#addStudentsBody');
        const spinner = $('#addSpinner');
        const addBucket = $('#addStudentsContainer');

        const assignedCourseId = readInput('AssignedCourseId') || $('#editForm input[name="AssignedCourseId"]')?.value || '';

        async function loadAddTable(page) {
            if (!host) return;
            spinner && spinner.classList.remove('d-none');
            try {
                const url = buildUrl('/AdminAssign/AddStudentsTable', {
                    id: assignedCourseId,
                    status: 'Active',
                    page: String(page || 1),
                    pageSize: '10'
                });
                const resp = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
                const html = await resp.text();
                host.innerHTML = html;
                spinner && spinner.classList.add('d-none');
                wireAddTable();
            } catch (e) {
                host.innerHTML = '<div class="alert alert-danger">Failed to load student list.</div>';
                console.error(e);
            }
        }

        function wireAddTable() {
            const selectAll = $('#selectAllStudents', host);
            if (selectAll) {
                selectAll.addEventListener('change', function () {
                    const toToggle = $$('.student-add-checkbox, .student-checkbox', host);
                    toToggle.forEach(cb => { cb.checked = selectAll.checked; });
                });
            }
            $$('#addStudentsBody a.js-page').forEach(a => {
                a.addEventListener('click', function (e) {
                    e.preventDefault();
                    const p = toInt(this.getAttribute('data-page'), 1);
                    if (p) loadAddTable(p);
                });
            });
        }

        function ensurePendingSeparator() {
            const firstPending = $('#enrolledBody tr.pending-add');
            const hasSep = !!$('#enrolledBody tr.pending-sep');
            if (firstPending && !hasSep) {
                const sep = document.createElement('tr');
                sep.className = 'pending-sep';
                sep.innerHTML = `<td colspan="6">Pending additions</td>`;
                firstPending.parentNode.insertBefore(sep, firstPending);
            }
            if (!firstPending && hasSep) {
                $('#enrolledBody tr.pending-sep')?.remove();
            }
        }

        function buildPreviewRowFromModalCheckbox(cb) {
            const tr = cb.closest('tr');
            if (!tr) return null;

            const name = tr.children?.[1]?.textContent?.trim() || '';
            const prog = tr.children?.[2]?.textContent?.trim() || '';
            const year = tr.children?.[3]?.textContent?.trim() || '';
            const sec = tr.children?.[4]?.textContent?.trim() || '';
            const status = tr.children?.[5]?.textContent?.trim() || 'Active';

            const studentId = toInt(cb.value, NaN);
            if (!Number.isFinite(studentId)) return null;

            if (document.querySelector(`#enrolledBody tr[data-student-id="${studentId}"]`)) {
                return null;
            }

            const row = document.createElement('tr');
            row.setAttribute('data-student-id', String(studentId));
            row.classList.add('pending-add');

            row.innerHTML = `
        <td><!-- no delete checkbox for pending adds --></td>
        <td class="d-flex align-items-center gap-2">
            <span class="chip-soft-primary">to add</span>
            <span>${name}</span>
        </td>
        <td>${prog}</td>
        <td>${year}</td>
        <td>${sec}</td>
        <td>${status}</td>
      `;
            return row;
        }

        $('#btnCommitAdd')?.addEventListener('click', () => {
            const picks = $$('.student-add-checkbox:checked, .student-checkbox:checked', host || document);
            if (picks.length === 0) {
                bsModal?.hide();
                return;
            }

            const existingHidden = new Set(
                $$('#addStudentsContainer input[name="AddStudentIds"]').map(i => toInt(i.value, NaN))
            );

            const body = $('#enrolledBody');
            picks.forEach(cb => {
                const sid = toInt(cb.value, NaN);
                if (Number.isFinite(sid) && !existingHidden.has(sid)) {
                    // hidden input for POST
                    const h = document.createElement('input');
                    h.type = 'hidden';
                    h.name = 'AddStudentIds';
                    h.value = String(sid);
                    addBucket?.appendChild(h);

                    // preview row
                    const row = buildPreviewRowFromModalCheckbox(cb);
                    if (row) body?.appendChild(row);
                }
            });

            ensurePendingSeparator();
            bsModal?.hide();
        });

        btnOpen?.addEventListener('click', () => {
            bsModal?.show();
            loadAddTable(1);
        });
    }


    document.addEventListener('DOMContentLoaded', () => {
        // Lightweight page detection and init
        if ($('.admin-accounts-container')) {
            // AdminAssign Index
            initAdminAssignIndex();
        }

        if ($('#blockFields')) {
            // AdminAssign Phase 2 (Assign)
            initAdminAssignPhase2();
        }

        if ($('#addStudentsModal')) {
            // AdminAssign View/Edit
            initAdminAssignView();
        }

        if ($('#CourseId') && $('#Type') && $('#Units')) {
            attachCourseTypeUnits({
                courseSel: '#CourseId',
                typeSel: '#Type',
                unitsInput: '#Units'
            });
        }
    });
})();
