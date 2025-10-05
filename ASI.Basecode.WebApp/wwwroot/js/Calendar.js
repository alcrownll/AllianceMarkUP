// ============================
// Utility functions
// ============================
function toLocalInput(d) {
    return d ? new Date(d).toISOString().slice(0, 16) : '';
}
function toUtc(s) {
    return s ? new Date(s).toISOString() : null;
}

// ============================
// Modal helpers
// ============================
function openModal({ id, title, start, end, allDay, location, description }, isAdmin) {
    const modalEl = document.getElementById('eventModal');
    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);

    const $id = document.getElementById('eventId');
    const $title = document.getElementById('title');
    const $start = document.getElementById('start');
    const $end = document.getElementById('end');
    const $allDay = document.getElementById('allDay');
    const $loc = document.getElementById('location');
    const $desc = document.getElementById('description');
    const $del = document.getElementById('deleteBtn');

    $id.value = id || '';
    $title.value = title || '';
    $start.value = toLocalInput(start || new Date());
    $end.value = toLocalInput(end || '');
    $allDay.checked = !!allDay;
    $loc.value = location || '';
    $desc.value = description || '';

    // read-only for non-admin
    for (const el of document.querySelectorAll('#eventForm input, #eventForm textarea')) {
        el.disabled = !isAdmin;
    }
    const submitBtn = document.querySelector('#eventForm button[type="submit"]');
    if (submitBtn) submitBtn.classList.toggle('d-none', !isAdmin);
    $del.classList.toggle('d-none', !isAdmin || !$id.value);

    modal.show();
}

async function saveEvent(isAdmin, calendar) {
    if (!isAdmin) return;
    const payload = {
        title: document.getElementById('title').value,
        startUtc: toUtc(document.getElementById('start').value),
        endUtc: toUtc(document.getElementById('end').value),
        allDay: document.getElementById('allDay').checked,
        location: document.getElementById('location').value || null,
        description: document.getElementById('description').value || null
    };
    const id = document.getElementById('eventId').value;
    const endpoint = id ? `/api/events/${id}` : '/api/events';
    const method = id ? 'PUT' : 'POST';

    const res = await fetch(endpoint, {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });
    if (!res.ok) throw new Error('Save failed');

    bootstrap.Modal.getInstance(document.getElementById('eventModal')).hide();
    calendar.refetchEvents();
}

async function deleteEvent(calendar) {
    const id = document.getElementById('eventId').value;
    if (!id) return;
    if (!confirm('Delete this event?')) return;
    const res = await fetch(`/api/events/${id}`, { method: 'DELETE' });
    if (!res.ok) throw new Error('Delete failed');

    bootstrap.Modal.getInstance(document.getElementById('eventModal')).hide();
    calendar.refetchEvents();
}

// ============================
// Page Init
// ============================
document.addEventListener('DOMContentLoaded', () => {
    // --------------------------
    // 1. FullCalendar (main page)
    // --------------------------
    const calEl = document.getElementById('calendar');
    if (calEl) {
        const isAdmin = calEl.dataset.isAdmin === 'true';
        const eventsUrl = calEl.dataset.eventsUrl || '/api/events';

        const calendar = new FullCalendar.Calendar(calEl, {
            initialView: 'dayGridMonth',
            height: 'auto',
            timeZone: 'local',
            headerToolbar: {
                left: 'prev,next today',
                center: 'title',
                right: 'dayGridMonth,timeGridWeek,timeGridDay,listWeek'
            },
            selectable: isAdmin,
            select: info => {
                if (!isAdmin) return;
                openModal({ start: info.start, end: info.end, allDay: info.allDay }, isAdmin);
            },
            eventClick: info => {
                const e = info.event;
                openModal({
                    id: e.id,
                    title: e.title,
                    start: e.start,
                    end: e.end,
                    allDay: e.allDay
                }, isAdmin);
            },
            events: function (fetchInfo, success, failure) {
                const url = `${eventsUrl}?start=${encodeURIComponent(fetchInfo.startStr)}&end=${encodeURIComponent(fetchInfo.endStr)}`;
                fetch(url, { credentials: 'same-origin' })
                    .then(r => r.json())
                    .then(data => success(data))
                    .catch(err => failure(err));
            },
            eventTimeFormat: { hour: '2-digit', minute: '2-digit', meridiem: true }
        });
        calendar.render();

        const addBtn = document.getElementById('addEventBtn');
        if (addBtn) addBtn.addEventListener('click', () => openModal({ start: new Date(), allDay: true }, isAdmin));

        document.getElementById('eventForm')?.addEventListener('submit', async (ev) => {
            ev.preventDefault();
            try { await saveEvent(isAdmin, calendar); } catch (e) { alert(e.message); }
        });

        document.getElementById('deleteBtn')?.addEventListener('click', async () => {
            try { await deleteEvent(calendar); } catch (e) { alert(e.message); }
        });
    }

    // --------------------------
    // 2. Mini Calendar (sidebar)
    // --------------------------
    const miniEl = document.getElementById('miniCalendar');
    if (miniEl && window.flatpickr) {
        flatpickr(miniEl, {
            inline: true,
            defaultDate: new Date(),
            disableMobile: true,
            // If you want to make date clicks jump to full calendar, uncomment:
            // onChange: (dates) => {
            //   if (dates.length) {
            //     const iso = dates[0].toISOString();
            //     window.location.href = `/Calendar/Index?date=${encodeURIComponent(iso)}`;
            //   }
            // }
        });
    }
});
