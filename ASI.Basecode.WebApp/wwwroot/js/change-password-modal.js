(function () {
    if (window.__changePwBoundV2) return;
    window.__changePwBoundV2 = true;

    const form = document.getElementById('changePwForm');
    const btnSubmit = document.getElementById('btnSubmitPw');
    const alertBox = document.getElementById('pwAlert');
    const modalEl = document.getElementById('changePasswordModal');

    const newPw = document.getElementById('newPassword');
    const oldPw = document.getElementById('oldPassword');
    const cfmPw = document.getElementById('confirmPassword');

    const reqsList = document.getElementById('pwReqs');
    const meterBar = document.getElementById('pwMeterBar');
    const hint = document.getElementById('pwHint');

    // Toggle eyes
    document.querySelectorAll('.pwm-eye').forEach(btn => {
        btn.addEventListener('click', () => {
            const id = btn.getAttribute('data-target');
            const input = document.getElementById(id);
            if (!input) return;
            input.type = input.type === 'password' ? 'text' : 'password';
            const icon = btn.querySelector('i');
            icon.classList.toggle('fa-eye');
            icon.classList.toggle('fa-eye-slash');
        });
    });

    function setMsg(forName, msg) {
        const el = document.querySelector('.pwm-err[data-for="' + forName + '"]');
        if (!el) return;
        el.textContent = msg || '';
    }

    function checkStrength(pw) {
        const hasLen = pw.length >= 8;
        const hasMix = /[A-Za-z]/.test(pw) && /\d/.test(pw);
        const hasSym = /[^A-Za-z0-9]/.test(pw);
        const score = (hasLen ? 1 : 0) + (hasMix ? 1 : 0) + (hasSym ? 1 : 0);

        reqsList?.querySelector('[data-rule="len"]')?.classList.toggle('ok', hasLen);
        reqsList?.querySelector('[data-rule="mix"]')?.classList.toggle('ok', hasMix);
        reqsList?.querySelector('[data-rule="sym"]')?.classList.toggle('ok', hasSym);

        if (meterBar) {
            meterBar.style.width = (score / 3 * 100) + '%';
            meterBar.dataset.level = String(score);
        }

        if (!hint) return score === 3;

        if (score <= 1) {
            hint.classList.remove('text-success', 'text-warning');
            hint.classList.add('text-danger');
            hint.textContent = 'Password is weak. Add length, numbers & symbols.';
        } else if (score === 2) {
            hint.classList.remove('text-success', 'text-danger');
            hint.classList.add('text-warning');
            hint.textContent = 'Almost there — add another type for better strength.';
        } else {
            hint.classList.remove('text-danger', 'text-warning');
            hint.classList.add('text-success');
            hint.textContent = 'Looks good! Strong password.';
        }
        return score === 3;
    }

    function validate() {
        let ok = true;

        // New first
        const strong = checkStrength(newPw.value.trim());
        if (!newPw.value.trim()) { setMsg('newPassword', 'Please enter a new password.'); ok = false; }
        else if (!strong) { setMsg('newPassword', 'Make your password stronger to continue.'); ok = false; }
        else setMsg('newPassword', '');

        if (!cfmPw.value.trim()) { setMsg('confirmPassword', 'Please confirm your new password.'); ok = false; }
        else if (newPw.value !== cfmPw.value) { setMsg('confirmPassword', 'Passwords do not match.'); ok = false; }
        else setMsg('confirmPassword', '');

        if (!oldPw.value.trim()) { setMsg('oldPassword', 'Please enter your current password.'); ok = false; }
        else setMsg('oldPassword', '');

        if (oldPw.value && newPw.value && oldPw.value === newPw.value) {
            setMsg('newPassword', 'New password must be different from the old password.');
            ok = false;
        }

        if (btnSubmit) btnSubmit.disabled = !ok;
        return ok;
    }

    [oldPw, newPw, cfmPw].forEach(i => i?.addEventListener('input', validate));

    function showMsg(kind, msg) {
        if (!alertBox) return;
        alertBox.className = 'alert alert-' + kind;
        alertBox.textContent = msg;
        alertBox.classList.remove('d-none');
    }
    function clearMsg() {
        if (!alertBox) return;
        alertBox.className = 'alert d-none';
        alertBox.textContent = '';
    }

    form?.addEventListener('submit', async (e) => {
        e.preventDefault();
        clearMsg();
        if (!validate()) return;

        btnSubmit.disabled = true;
        try {
            const fd = new FormData(form);
            const res = await fetch(form.action, { method: 'POST', body: fd, headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            let data = null;
            const ct = res.headers.get('content-type') || '';
            if (ct.includes('application/json')) {
                data = await res.json();
            } else {
                await res.text();
                data = { ok: false, message: res.ok ? 'Unexpected server response.' : `Server error (${res.status}).` };
            }

            if (!res.ok || !data?.ok) {
                showMsg('danger', data?.message || 'Unable to change password.');
            } else {
                showMsg('success', 'New password is changed successfully.');
                setTimeout(() => {
                    form.reset(); checkStrength(''); validate();
                    const bsModal = bootstrap.Modal.getOrCreateInstance(modalEl);
                    bsModal.hide();
                    const host = document.querySelector('.profile-card') || document.body;
                    const div = document.createElement('div');
                    div.className = 'alert alert-success mb-3';
                    div.textContent = 'Password updated.';
                    host.prepend(div);
                    setTimeout(() => div.remove(), 4000);
                }, 900);
            }
        } catch {
            showMsg('danger', 'Network error while changing password.');
        } finally {
            btnSubmit.disabled = false;
        }
    });

    modalEl?.addEventListener('show.bs.modal', () => {
        form?.reset();
        checkStrength('');
        validate();
        clearMsg();
    });
})();
