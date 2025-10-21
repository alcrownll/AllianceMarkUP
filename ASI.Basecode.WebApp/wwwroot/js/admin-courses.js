// admin-course.js
(() => {
    // ===== Toast helper =====
    window.showToast = function (msg, type = 'success') {
        const toastEl = document.getElementById('appToast');
        const toastBody = document.getElementById('appToastBody');
        if (!toastEl || !toastBody) return;

        toastBody.textContent = msg || 'Saved successfully.';
        const wrapper = toastEl.querySelector('.d-flex');
        wrapper.className = 'd-flex text-white rounded shadow-sm ' +
            (type === 'error' ? 'bg-danger' : type === 'info' ? 'bg-primary' : 'bg-success');

        bootstrap.Toast.getOrCreateInstance(toastEl).show();
    };

    // ===== Programs table AJAX loader =====
    async function loadProgramTable(q) {
        const host = document.getElementById('programsTableHost');
        const url = '/admin/programs/list' + (q ? ('?q=' + encodeURIComponent(q)) : '');
        try {
            const res = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            host.innerHTML = await res.text();
        } catch {
            host.innerHTML = '<div class="text-danger text-center py-4">Failed to load programs.</div>';
        }
    }

    // ===== Client-side filter for Courses tab =====
    function filterCoursesTable(query) {
        const tab = document.getElementById('courses');
        const table = tab?.querySelector('table');
        const noMatches = document.getElementById('coursesNoMatches');

        if (!table) {
            if (noMatches) noMatches.classList.toggle('d-none', true);
            return;
        }

        const q = (query || '').trim().toLowerCase();
        let visible = 0;

        // assumes table body rows contain columns: Code, Title, Lec, Lab, Units, etc.
        table.querySelectorAll('tbody tr').forEach(tr => {
            // skip placeholder rows (e.g., “No courses found.” spanning row)
            const tds = tr.querySelectorAll('td');
            if (!tds.length) return;

            const code = (tds[0]?.textContent || '').toLowerCase();
            const title = (tds[1]?.textContent || '').toLowerCase();

            const match = !q || code.includes(q) || title.includes(q);
            tr.classList.toggle('d-none', !match);
            if (match) visible++;
        });

        if (noMatches) noMatches.classList.toggle('d-none', visible !== 0);
    }

    // ===== Unified search wiring =====
    const $search = document.getElementById('globalSearch');
    const $clear = document.getElementById('clearSearch');

    let debounceT = null;
    function activeTabTarget() {
        return document.querySelector('.nav-tabs .nav-link.active')?.getAttribute('data-bs-target') || '#programs';
    }

    function performSearch(immediate = false) {
        const q = $search.value || '';

        const run = () => {
            const target = activeTabTarget();
            if (target === '#programs') {
                loadProgramTable(q);
            } else if (target === '#courses') {
                filterCoursesTable(q);
            }
        };

        if (immediate) {
            clearTimeout(debounceT);
            run();
        } else {
            clearTimeout(debounceT);
            debounceT = setTimeout(run, 220);
        }
    }

    // Initial loads
    document.addEventListener('DOMContentLoaded', () => {
        // Load programs on page load with initial query (if any)
        performSearch(true);
    });

    // When switching tabs, re-apply current query to the new tab
    document.addEventListener('shown.bs.tab', (e) => {
        const target = e.target?.getAttribute('data-bs-target');
        if (target === '#programs') {
            performSearch(true);
        } else if (target === '#courses') {
            // Ensure courses are filtered client-side using current query
            filterCoursesTable($search.value || '');
        }
    });

    // Typing in search box
    $search?.addEventListener('input', () => performSearch(false));

    // Enter = immediate search, Esc = clear
    $search?.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            performSearch(true);
        } else if (e.key === 'Escape') {
            $search.value = '';
            performSearch(true);
        }
    });

    // Clear button (×)
    $clear?.addEventListener('click', () => {
        $search.value = '';
        $search.focus();
        performSearch(true);
    });

    // ===== Existing delete modal & prospectus logic =====

    // Load Programs immediately if Programs tab is active (safe-guard)
    document.addEventListener('DOMContentLoaded', () => {
        const target = activeTabTarget();
        if (target === '#programs') loadProgramTable($search.value || '');
    });

    // DELETE Modal flow
    (function () {
        const modalEl = document.getElementById('confirmProgramDeleteModal');
        const bsModal = modalEl ? bootstrap.Modal.getOrCreateInstance(modalEl) : null;
        const pdCode = document.getElementById('pdProgramCode');
        const pdName = document.getElementById('pdProgramName');
        const pdForce = document.getElementById('pdForce');
        const pdBtn = document.getElementById('pdConfirmBtn');
        const pdSpin = document.getElementById('pdSpinner');

        let pendingDelete = { id: null, row: null };

        document.addEventListener('click', (e) => {
            const btn = e.target.closest('.btn-program-delete');
            if (!btn) return;

            const id = btn.dataset.programId;
            const code = (btn.dataset.programCode || '').toUpperCase();
            const name = btn.dataset.programName || '';

            pendingDelete.id = id;
            pendingDelete.row = btn.closest('tr');

            pdCode.textContent = code || 'CODE';
            pdName.textContent = name || 'Program';
            pdForce.checked = false;

            bsModal?.show();
        });

        pdBtn?.addEventListener('click', async () => {
            if (!pendingDelete.id) return;
            pdBtn.disabled = true;
            pdSpin.classList.remove('d-none');

            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";
            const fd = new FormData();
            fd.append('__RequestVerificationToken', token);
            fd.append('force', pdForce.checked ? 'true' : 'false');

            try {
                let res = await fetch(`/admin/programs/${pendingDelete.id}/delete`, {
                    method: 'POST',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' },
                    body: fd
                });

                if (res.ok) {
                    pendingDelete.row?.remove();
                    bsModal?.hide();
                    showToast('Program deleted.');
                } else if (res.status === 409) {
                    const data = await res.json().catch(() => ({}));
                    pdForce.checked = true;
                    showToast(data.message || 'Program still has attached courses. Enable force delete to proceed.', 'info');
                } else {
                    showToast('Delete failed.', 'error');
                }
            } catch (err) {
                console.error(err);
                showToast('Network error while deleting.', 'error');
            } finally {
                pdBtn.disabled = false;
                pdSpin.classList.add('d-none');
            }
        });
    })();

    // Prospectus preloading
    document.addEventListener('click', async (e) => {
        const btn = e.target.closest('[data-action="manage-courses"]');
        if (!btn) return;
        const programId = btn.dataset.programId;
        const programCode = btn.dataset.programCode || '—';
        const programName = btn.dataset.programName || '—';
        const modal = document.getElementById('programProspectusModal');
        modal.dataset.programId = programId;
        modal.querySelector('#sumCode').textContent = programCode;
        modal.querySelector('#sumName').textContent = programName;

        document.querySelectorAll('tbody[data-term-table]').forEach(tbody => {
            tbody.innerHTML = '<tr><td colspan="7" class="text-center text-muted py-4">Loading…</td></tr>';
        });

        const jobs = [];
        for (let y = 1; y <= 4; y++) for (let t = 1; t <= 2; t++) {
            jobs.push(
                fetch(`/admin/programs/${programId}/term?year=${y}&term=${t}`)
                    .then(r => r.json())
                    .then(d => {
                        const host = document.querySelector(`tbody[data-term-table][data-year="${y}"][data-term="${t}"]`);
                        if (!host) return;
                        host.innerHTML = '';
                        (d.items || []).forEach(it => {
                            const tr = document.createElement('tr');
                            tr.dataset.id = it.id;
                            tr.dataset.courseCode = it.code || '';
                            tr.setAttribute('data-row', 'course');
                            tr.innerHTML = `
                <td>${it.code ?? ''}</td>
                <td>${it.title ?? ''}</td>
                <td class="text-center">${it.lec ?? 0}</td>
                <td class="text-center">${it.lab ?? 0}</td>
                <td class="text-center" data-col="units">${(it.units ?? 0)}</td>
                <td>${it.prereq ?? '—'}</td>
                <td class="text-end"><button class="btn btn-sm btn-outline-danger" data-remove>Remove</button></td>`;
                            host.appendChild(tr);
                        });
                        if (!host.children.length) {
                            host.innerHTML = '<tr><td colspan="7" class="text-center text-muted py-4">No subjects yet.</td></tr>';
                        }
                        if (window.recalcUnits) {
                            const key = `Y${y}T${t}`;
                            window.recalcUnits(key);
                        }
                    })
                    .catch(() => {
                        const host = document.querySelector(`tbody[data-term-table][data-year="${y}"][data-term="${t}"]`);
                        if (host) host.innerHTML = '<tr><td colspan="7" class="text-center text-danger py-4">Failed to load.</td></tr>';
                    })
            );
        }
        await Promise.all(jobs);
        bootstrap.Modal.getOrCreateInstance(modal).show();
    });

    // Nested modal behavior
    (function () {
        const parentEl = document.getElementById('programProspectusModal');
        const childEl = document.getElementById('coursePickerModal');
        if (!parentEl || !childEl) return;

        parentEl.addEventListener('hide.bs.modal', (e) => {
            if (childEl.classList.contains('show')) e.preventDefault();
        });

        childEl.addEventListener('shown.bs.modal', () => {
            const bds = document.querySelectorAll('.modal-backdrop');
            bds[bds.length - 1]?.classList.add('stacked');
            document.body.classList.add('modal-open');
        });

        childEl.addEventListener('hidden.bs.modal', () => {
            if (parentEl.classList.contains('show')) {
                document.body.classList.add('modal-open');
                const lastTrigger = parentEl.querySelector('[data-bs-target="#coursePickerModal"]');
                (lastTrigger || parentEl).focus();
            }
        });

        childEl.querySelector('.btn-close')?.addEventListener('click', (e) => {
            e.preventDefault();
            bootstrap.Modal.getOrCreateInstance(childEl).hide();
        });
    })();
})();
