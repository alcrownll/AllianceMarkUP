(function () {
    const label = document.getElementById('hoverLabel');
    if (!label) return;

    const OFFSET_X = 16;
    const OFFSET_Y = 12;
    let hideTimer = null;

    const clamp = (v, min, max) => Math.max(min, Math.min(max, v));

    function positionLabel(x, y) {
        const rect = label.getBoundingClientRect();
        const maxX = window.innerWidth - rect.width - 8;
        const maxY = window.innerHeight - rect.height - 8;
        const px = clamp(x + OFFSET_X, 8, maxX);
        const py = clamp(y + OFFSET_Y, 8, maxY);
        label.style.left = px + 'px';
        label.style.top = py + 'px';
    }

    function showLabel(text) {
        label.textContent = text || '';
        label.setAttribute('aria-hidden', text ? 'false' : 'true');
        label.classList.add('is-visible');
    }

    function hideLabel() {
        label.classList.remove('is-visible');
        label.setAttribute('aria-hidden', 'true');
    }

    function bind(el) {
        const text = el.dataset.label && el.dataset.label.trim();
        if (!text) return;

        el.addEventListener('mouseenter', e => {
            if (hideTimer) { clearTimeout(hideTimer); hideTimer = null; }
            showLabel(text);
            positionLabel(e.clientX, e.clientY);
        });

        el.addEventListener('mousemove', e => positionLabel(e.clientX, e.clientY));
        el.addEventListener('mouseleave', () => { hideTimer = setTimeout(hideLabel, 60); });

        el.addEventListener('click', e => {
            showLabel(text);
            positionLabel(e.clientX, e.clientY);
            if (hideTimer) clearTimeout(hideTimer);
            hideTimer = setTimeout(hideLabel, 450);
        });

        el.addEventListener('focus', () => {
            const r = el.getBoundingClientRect();
            showLabel(text);
            positionLabel(r.left + r.width / 2, r.top + 8);
        });
        el.addEventListener('blur', hideLabel);
    }

    // Initial bind
    document.querySelectorAll('[data-label]').forEach(bind);

    // Optional: support late-loaded elements (e.g., partials)
    const mo = new MutationObserver(muts => {
        muts.forEach(m => {
            m.addedNodes.forEach(n => {
                if (n.nodeType !== 1) return;
                if (n.matches && n.matches('[data-label]')) bind(n);
                n.querySelectorAll && n.querySelectorAll('[data-label]').forEach(bind);
            });
        });
    });
    mo.observe(document.documentElement, { childList: true, subtree: true });
})();
