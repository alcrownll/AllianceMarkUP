(function () {
  "use strict";

  // ---------- tiny helpers ----------
  const $$ = (sel, root = document) => Array.from(root.querySelectorAll(sel));
  const $ = (sel, root = document) => root.querySelector(sel);

  function refreshPickers() {
    if (!window.jQuery || !jQuery.fn.selectpicker) return;
    $(".selectpicker").selectpicker("refresh");
  }

  function debounce(fn, ms) {
    let t;
    return (...a) => {
      clearTimeout(t);
      t = setTimeout(() => fn(...a), ms);
    };
  }
  function readInput(name, root = document) {
    return (root.querySelector(`input[name="${name}"]`)?.value ?? "").trim();
  }
  function toInt(x, def = 0) {
    const n = parseInt(x, 10);
    return Number.isFinite(n) ? n : def;
  }
  function buildUrl(path, params = {}) {
    const u = new URL(path, window.location.origin);
    Object.entries(params).forEach(([k, v]) => {
      if (v !== undefined && v !== null && String(v).length)
        u.searchParams.set(k, v);
    });
    return u.toString();
  }

  // ---------- Course -> Type -> Units ----------
  function attachCourseTypeUnits({
    courseSel,
    typeSel,
    unitsInput,
    initialType,
  }) {
    const selCourse = $(courseSel);
    const selType = $(typeSel);
    const unitsBox = $(unitsInput);

    const initRaw = (
      initialType ||
      selType?.dataset?.initialType ||
      selType?.value ||
      ""
    ).trim();
    const initNorm = initRaw
      .toLowerCase()
      .replace(/^lec$/, "lecture")
      .replace(/^lab$/, "laboratory");

    function syncUnitsFromType() {
      const u =
        selType?.options?.[selType.selectedIndex]?.getAttribute("data-units");
      if (unitsBox) unitsBox.value = u || "";
    }

    function rebuildTypeOptions(preserve = true) {
      if (unitsBox) unitsBox.value = "";
      const opt = selCourse?.options?.[selCourse.selectedIndex];
      if (!opt || !opt.value) {
        if (selType) {
          selType.innerHTML = '<option value="">-- Select --</option>';
          selType.disabled = true;
        }
        return;
      }
      const lec = toInt(opt.getAttribute("data-lec"), 0);
      const lab = toInt(opt.getAttribute("data-lab"), 0);

      const prevRaw = preserve ? selType?.value || "" : "";
      const options = [];
      if (lec > 0)
        options.push({ text: "Lecture", value: "Lecture", units: lec });
      if (lab > 0)
        options.push({ text: "Laboratory", value: "Laboratory", units: lab });

      if (selType) {
        selType.innerHTML =
          '<option value="">-- Select --</option>' +
          options
            .map(
              (o) =>
                `<option value="${o.value}" data-units="${o.units}">${o.text}</option>`
            )
            .join("");
        selType.disabled = options.length === 0;

        let chosen = "";
        if (options.some((o) => o.value === prevRaw)) {
          chosen = prevRaw;
        } else {
          const fromInitRaw = options.find(
            (o) => o.value.toLowerCase() === initRaw.toLowerCase()
          )?.value;
          const fromNorm = options.find(
            (o) => o.value.toLowerCase() === initNorm
          )?.value;
          chosen = fromInitRaw || fromNorm || "";
        }
        selType.value = chosen;
        syncUnitsFromType();
      }
    }

    selCourse &&
      selCourse.addEventListener("change", () => rebuildTypeOptions(true));
    selType && selType.addEventListener("change", syncUnitsFromType);
    rebuildTypeOptions(true);
  }

  // ---------- Room & Schedule UI ----------
  function initRoomScheduleUI({ syncPair = true, minGapMinutes = 60 } = {}) {
    const typeSel = document.querySelector("#Type");
    const roomSel = document.querySelector("#RoomSelect");
    const startTime = document.querySelector("#StartTime");
    const endTime = document.querySelector("#EndTime");
    const dayBtns = Array.from(document.querySelectorAll(".day-btn"));
    const dayLive = document.querySelector("#DayLiveText");
    const csvHidden = document.querySelector("#ScheduleDaysCsv");

    if (!typeSel || !roomSel) return;

    const ROOM_SET = {
      Lecture: [
        "A2-401",
        "A2-402",
        "A2-403",
        "A2-404",
        "211",
        "212",
        "M1",
        "A31",
        "A32",
      ],
      Laboratory: ["C1", "C2", "C4", "CISCO", "A35", "AInnovation Lab"],
    };

    // ---- time helpers ----
    const MIN_MINUTES = 7 * 60 + 30; // 07:30
    const MAX_MINUTES = 21 * 60 + 30; // 21:30

    function parseHHMM(v) {
      if (!v || !/^\d{2}:\d{2}$/.test(v)) return null;
      const [h, m] = v.split(":").map(Number);
      return h * 60 + m;
    }

    function toHHMM(totalMinutes) {
      const h = Math.floor(totalMinutes / 60);
      const m = totalMinutes % 60;
      return String(h).padStart(2, "0") + ":" + String(m).padStart(2, "0");
    }

    function clampMinutes(m) {
      if (m < MIN_MINUTES) return MIN_MINUTES;
      if (m > MAX_MINUTES) return MAX_MINUTES;
      return m;
    }

    function clampInput(el) {
      if (!el || !el.value) return;
      const mins = parseHHMM(el.value);
      if (mins == null) return;
      el.value = toHHMM(clampMinutes(mins));
    }

    function fillRoomsFor(typeVal) {
      const list = ROOM_SET[typeVal] || [];
      const current = roomSel.value;
      roomSel.innerHTML =
        '<option value="">-- Select Room --</option>' +
        list.map((r) => `<option value="${r}">${r}</option>`).join("");

      if (list.includes(current)) roomSel.value = current;
      roomSel.removeAttribute("disabled");
    }

    // set min/max/step attrs
    [startTime, endTime].forEach((el) => {
      if (!el) return;
      el.setAttribute("min", "07:30");
      el.setAttribute("max", "21:30");
      el.setAttribute("step", "900"); // 15 mins
    });

    const dayMap = {
      Monday: 1,
      Tuesday: 2,
      Wednesday: 3,
      Thursday: 4,
      Friday: 5,
      Saturday: 6,
    };

    function syncDayLiveAndCsv() {
      const pickedLabels = dayBtns
        .filter((b) => b.classList.contains("active"))
        .map((b) => b.dataset.day);

      const pickedNums = pickedLabels.map((lbl) => dayMap[lbl]).filter(Boolean);

      if (dayLive)
        dayLive.textContent = pickedLabels.length
          ? pickedLabels.join(", ")
          : "";
      if (csvHidden) csvHidden.value = pickedNums.join(",");
    }

    typeSel.addEventListener("change", () => fillRoomsFor(typeSel.value));
    fillRoomsFor(typeSel.value);

    startTime &&
      startTime.addEventListener("change", () => {
        clampInput(startTime);
        if (!syncPair || !endTime) return;

        const s = parseHHMM(startTime.value);
        const e = parseHHMM(endTime.value);

        if (s == null) return;

        const minEnd = clampMinutes(s + minGapMinutes);

        if (e == null || e < minEnd) {
          endTime.value = toHHMM(minEnd);
        }
      });

    endTime &&
      endTime.addEventListener("change", () => {
        clampInput(endTime);
        if (!syncPair || !startTime) return;

        const e = parseHHMM(endTime.value);
        const s = parseHHMM(startTime.value);

        if (e == null) return;

        const maxStart = clampMinutes(e - minGapMinutes);

        // Trigger auto-set ONLY if start is empty or too late vs min gap.
        if (s == null || s > maxStart) {
          startTime.value = toHHMM(maxStart);
        }
      });

    // days
    dayBtns.forEach((btn) => {
      btn.addEventListener("click", () => {
        btn.classList.toggle("active");
        btn.classList.toggle("btn-primary");
        btn.classList.toggle("btn-outline-secondary");
        syncDayLiveAndCsv();
      });
    });

    syncDayLiveAndCsv();
  }

  function wireScheduleSubmitGuard(formSel) {
    const form = $(formSel);
    if (!form) return;

    function parseHHMM(v) {
      if (!v || !/^\d{2}:\d{2}$/.test(v)) return null;
      const [h, m] = v.split(":").map(Number);
      return h * 60 + m;
    }
    function withinWindow(hhmm, min, max) {
      const m = parseHHMM(hhmm);
      return m !== null && m >= min && m <= max;
    }
    function getPickedDays() {
      return $$(".day-btn.active")
        .map((b) => b.dataset.day)
        .map(
          (n) =>
            ({
              Monday: 1,
              Tuesday: 2,
              Wednesday: 3,
              Thursday: 4,
              Friday: 5,
              Saturday: 6,
            }[n])
        )
        .filter(Boolean);
    }

    form.addEventListener("submit", (e) => {
      const days = getPickedDays();
      const csv = $("#ScheduleDaysCsv");
      if (csv) csv.value = days.join(",");

      if (days.length === 0) return;

      const start = $("#StartTime")?.value || "";
      const end = $("#EndTime")?.value || "";
      const room = $("#RoomSelect")?.value || "";

      if (!withinWindow(start, 450, 1290) || !withinWindow(end, 450, 1290)) {
        e.preventDefault();
        alert("Time must be within 07:30 to 21:30.");
        return;
      }
      if (parseHHMM(end) <= parseHHMM(start)) {
        e.preventDefault();
        alert("End time must be later than start time.");
        return;
      }
      if (!room) {
        e.preventDefault();
        alert("Room is required when picking any day.");
      }
    });
  }

  // ---------- Index page (toasts) ----------
  function initAdminAssignIndex() {
    $$(".js-auto-toast").forEach((el) => {
      try {
        new bootstrap.Toast(el).show();
      } catch (e) {
        console.warn("Toast init failed:", e);
      }
    });
  }

  // ---------- Students Section ----------
  function initAdminAssignPhase2() {
    if (!$("#CourseId") || !$("#Type") || !$("#Units")) return;
    if (!$("#ModeHidden") || !$("#blockFields")) return;

    attachCourseTypeUnits({
      courseSel: "#CourseId",
      typeSel: "#Type",
      unitsInput: "#Units",
    });

    initRoomScheduleUI({ syncPair: true, minGapMinutes: 60 });
    wireScheduleSubmitGuard("#assignForm");

    const sectionSets = {
      "1st Year": ["1A", "1B", "1C", "1D", "1E"],
      "2nd Year": ["2A", "2B", "2C", "2D", "2E"],
      "3rd Year": ["3A", "3B", "3C", "3D", "3E"],
      "4th Year": ["4A", "4B", "4C", "4D", "4E"],
    };

    const blockProgramSel = $("#BlockProgramId");
    const blockYearSel = $("#BlockYearLevel");
    const blockSectionSel = $("#BlockSection");

    blockSectionSel && blockSectionSel.removeAttribute("disabled");

    const serverSelectedSection = readInput("blockSection") || "";

    function populateBlockSections(preserveSelected) {
      if (!blockYearSel || !blockSectionSel) return;
      const yr = blockYearSel.value;
      const list = sectionSets[yr] || [];
      const current = preserveSelected ? serverSelectedSection : "";
      blockSectionSel.innerHTML =
        '<option value="">Select Section</option>' +
        list.map((s) => `<option value="${s}">${s}</option>`).join("");
      blockSectionSel.value = current && list.includes(current) ? current : "";
      refreshPickers();
    }

    function syncBlockSectionState() {
      if (!blockSectionSel) return;
      const hasYear = (blockYearSel?.value || "").trim() !== "";
      if (hasYear) {
        blockSectionSel.disabled = false;
        blockSectionSel.removeAttribute("disabled");
      } else {
        blockSectionSel.disabled = true;
        blockSectionSel.setAttribute("disabled", "disabled");
        blockSectionSel.value = "";
        blockSectionSel.innerHTML = '<option value="">Select Section</option>';
      }
    }

    blockYearSel &&
      blockYearSel.addEventListener("change", () => {
        populateBlockSections(false);
        syncBlockSectionState();
        if (!blockSectionSel.disabled) blockSectionSel.focus();
      });

    populateBlockSections(true);
    syncBlockSectionState();

    // mode UI
    const radioBlock = $("#extraOff");
    const radioManual = $("#extraOn");
    const manualWrap = $("#manualFields");
    const modeHidden = $("#ModeHidden");

    function currentMode() {
      return (radioManual?.checked ? "manual" : "block") || "block";
    }
    function syncModeUI() {
      const isManual = currentMode() === "manual";
      if (manualWrap) manualWrap.style.display = isManual ? "block" : "none";
      if (modeHidden) modeHidden.value = isManual ? "manual" : "block";
      if (isManual && (blockSectionSel?.value || "").trim() !== "")
        fetchManualTable(1);
    }
    radioBlock && radioBlock.addEventListener("change", syncModeUI);
    radioManual && radioManual.addEventListener("change", syncModeUI);
    syncModeUI();

    // AJAX manual table
    const host = $("#manualTableHost");
    const spin = $("#manualTableSpinner");
    const statusVal = readInput("status") || "Active";
    const pageSizeVal = readInput("pageSize") || "10";

    function showSpinner(on) {
      if (spin) spin.classList.toggle("d-none", !on);
    }

    async function fetchManualTable(page) {
      if (currentMode() !== "manual") return;

      const p = blockProgramSel?.value || "";
      const y = blockYearSel?.value || "";
      const s = blockSectionSel?.value || "";

      const url = buildUrl("/AdminAssign/ManualTable", {
        blockProgram: p,
        blockYear: y,
        blockSection: s,
        status: statusVal,
        page: String(page || 1),
        pageSize: String(pageSizeVal || 10),
      });

      showSpinner(true);
      try {
        const resp = await fetch(url, {
          headers: { "X-Requested-With": "XMLHttpRequest" },
        });
        const html = await resp.text();
        if (host) host.innerHTML = html;
        wireSelectAll();
        wirePagination();
      } catch (e) {
        console.error("Failed to load manual table:", e);
      } finally {
        showSpinner(false);
      }
    }
    const debouncedFetch = debounce((page) => fetchManualTable(page), 200);

    function wireSelectAll() {
      const selectAll = $("#selectAllStudents", host || document);
      if (selectAll) {
        selectAll.addEventListener("change", function () {
          $$("#manualTableHost .student-checkbox").forEach(
            (cb) => (cb.checked = selectAll.checked)
          );
        });
      }
    }

    function wirePagination() {
      $$("#manualTableHost a.js-page").forEach((a) => {
        a.addEventListener("click", function (e) {
          e.preventDefault();
          const p = toInt(this.getAttribute("data-page"), 1);
          if (p) fetchManualTable(p);
        });
      });
    }

    blockSectionSel &&
      blockSectionSel.addEventListener("change", () => debouncedFetch(1));

    const btnReset = $("#btnResetBlockFilters");

    async function fetchManualTableReset(page) {
      const url = buildUrl("/AdminAssign/ManualTable", {
        status: statusVal,
        page: String(page || 1),
        pageSize: String(pageSizeVal || 10),
      });
      showSpinner(true);
      try {
        const resp = await fetch(url, {
          headers: { "X-Requested-With": "XMLHttpRequest" },
        });
        const html = await resp.text();
        if (host) host.innerHTML = html;
        wireSelectAll();
        wirePagination();
      } catch (e) {
        console.error("Failed to reset manual table:", e);
      } finally {
        showSpinner(false);
      }
    }

    function resetBlockUI() {
      if (blockProgramSel) {
        blockProgramSel.selectedIndex = 0;
        if (window.jQuery) {
          $("#BlockProgramId.selectpicker").selectpicker("refresh");
        }
      }

      if (blockYearSel) blockYearSel.value = "";

      if (blockSectionSel) {
        blockSectionSel.value = "";
        blockSectionSel.setAttribute("disabled", "disabled");
        blockSectionSel.innerHTML = '<option value="">Select Section</option>';
      }
    }

    window.resetBlockFilters = function () {
      resetBlockUI();
      const isManual = $("#extraOn")?.checked;
      if (isManual) fetchManualTableReset(1);
    };

    btnReset?.addEventListener("click", () => window.resetBlockFilters());
    blockSectionSel &&
      blockSectionSel.addEventListener("change", () => {
        if ((blockSectionSel.value || "").trim() === "") {
          window.resetBlockFilters();
        }
      });

    wireSelectAll();
    wirePagination();
  }

  // ---------- View/Edit page (add/remove students modal etc.) ----------
  function initAdminAssignView() {
    if (!$("#addStudentsModal")) return;

    attachCourseTypeUnits({
      courseSel: "#CourseId",
      typeSel: "#Type",
      unitsInput: "#Units",
      initialType: $("#Type")?.dataset?.initialType || $("#Type")?.value || "",
    });

    initRoomScheduleUI({ syncPair: true, minGapMinutes: 60 });
    hydrateScheduleFromServer();
    wireScheduleSubmitGuard("#editForm");

    const tbody = $("#enrolledBody");
    const chkAll = $("#chkRemoveAll");
    const getRowBoxes = () => $$("#enrolledBody .remove-checkbox");

    function refreshRemoveSummary() {
      const picked = getRowBoxes().filter((x) => x.checked);
      const wrap = $("#removeSummary");
      const cEl = $("#removeCount");
      const nEl = $("#removeNames");
      if (!wrap || !cEl || !nEl) return;

      if (picked.length > 0) {
        const pills = picked
          .map((cb) => {
            const tr = cb.closest("tr");
            const name = tr?.children?.[1]?.textContent?.trim() || "";
            return `<span class="pill">${name}</span>`;
          })
          .join("");
        wrap.classList.remove("d-none");
        cEl.textContent = String(picked.length);
        nEl.innerHTML = pills;
      } else {
        wrap.classList.add("d-none");
        cEl.textContent = "0";
        nEl.innerHTML = "";
      }
      refreshPickers();
    }

    function syncHeaderCheckbox() {
      const boxes = getRowBoxes();
      if (!chkAll || boxes.length === 0) return;
      const checkedCount = boxes.filter((b) => b.checked).length;
      chkAll.indeterminate = checkedCount > 0 && checkedCount < boxes.length;
      chkAll.checked = checkedCount === boxes.length;
    }

    tbody?.addEventListener("change", (e) => {
      if (e.target.classList.contains("remove-checkbox")) {
        refreshRemoveSummary();
        syncHeaderCheckbox();
      }
    });

    chkAll?.addEventListener("change", () => {
      const on = !!chkAll.checked;
      getRowBoxes().forEach((cb) => {
        cb.checked = on;
      });
      refreshRemoveSummary();
      syncHeaderCheckbox();
    });

    syncHeaderCheckbox();

    const btnOpen = $("#btnOpenAddStudents");
    const modalEl = $("#addStudentsModal");
    const bsModal = modalEl ? new bootstrap.Modal(modalEl) : null;
    const host = $("#addStudentsBody");
    const spinner = $("#addSpinner");
    const addBucket = $("#addStudentsContainer");

    const assignedCourseId =
      readInput("AssignedCourseId") ||
      $('#editForm input[name="AssignedCourseId"]')?.value ||
      "";

    async function loadAddTable(page) {
      if (!host) return;
      spinner && spinner.classList.remove("d-none");
      try {
        const url = buildUrl("/AdminAssign/AddStudentsTable", {
          id: assignedCourseId,
          status: "Active",
          page: String(page || 1),
          pageSize: "10",
        });
        const resp = await fetch(url, {
          headers: { "X-Requested-With": "XMLHttpRequest" },
        });
        const html = await resp.text();
        host.innerHTML = html;
        spinner && spinner.classList.add("d-none");
        wireAddTable();
      } catch (e) {
        host.innerHTML =
          '<div class="alert alert-danger">Failed to load student list.</div>';
        console.error(e);
      }
    }

    function wireAddTable() {
      const selectAll = $("#selectAllStudents", host);
      if (selectAll) {
        selectAll.addEventListener("change", function () {
          const toToggle = $$(".student-add-checkbox, .student-checkbox", host);
          toToggle.forEach((cb) => {
            cb.checked = selectAll.checked;
          });
        });
      }
      $$("#addStudentsBody a.js-page").forEach((a) => {
        a.addEventListener("click", function (e) {
          e.preventDefault();
          const p = toInt(this.getAttribute("data-page"), 1);
          if (p) loadAddTable(p);
        });
      });
    }

    function ensurePendingSeparator() {
      const firstPending = $("#enrolledBody tr.pending-add");
      const hasSep = !!$("#enrolledBody tr.pending-sep");
      if (firstPending && !hasSep) {
        const sep = document.createElement("tr");
        sep.className = "pending-sep";
        sep.innerHTML = `<td colspan="6">Pending additions</td>`;
        firstPending.parentNode.insertBefore(sep, firstPending);
      }
      if (!firstPending && hasSep) {
        $("#enrolledBody tr.pending-sep")?.remove();
      }
    }

    function buildPreviewRowFromModalCheckbox(cb) {
      const tr = cb.closest("tr");
      if (!tr) return null;

      const name = tr.children?.[1]?.textContent?.trim() || "";
      const prog = tr.children?.[2]?.textContent?.trim() || "";
      const year = tr.children?.[3]?.textContent?.trim() || "";
      const sec = tr.children?.[4]?.textContent?.trim() || "";
      const studStatus = tr.children?.[5]?.textContent?.trim() || "Active";

      const studentId = toInt(cb.value, NaN);
      if (!Number.isFinite(studentId)) return null;

      if (
        document.querySelector(
          `#enrolledBody tr[data-student-id="${studentId}"]`
        )
      )
        return null;

      const row = document.createElement("tr");
      row.setAttribute("data-student-id", String(studentId));
      row.classList.add("pending-add");

      row.innerHTML = `
        <td><!-- no delete checkbox for pending adds --></td>
        <td class="d-flex align-items-center gap-2">
          <span class="chip-soft-primary">to add</span>
          <span>${name}</span>
        </td>
        <td>${prog}</td>
        <td>${year}</td>
        <td>${sec}</td>
        <td>${studStatus}</td>
      `;
      return row;
    }

    $("#btnCommitAdd")?.addEventListener("click", () => {
      const picks = $$(
        ".student-add-checkbox:checked, .student-checkbox:checked",
        host || document
      );
      if (picks.length === 0) {
        bsModal?.hide();
        return;
      }

      const existingHidden = new Set(
        $$('#addStudentsContainer input[name="SelectedStudentIds"]').map((i) =>
          toInt(i.value, NaN)
        )
      );

      const body = $("#enrolledBody");
      picks.forEach((cb) => {
        const sid = toInt(cb.value, NaN);
        if (Number.isFinite(sid) && !existingHidden.has(sid)) {
          const h = document.createElement("input");
          h.type = "hidden";
          h.name = "SelectedStudentIds";
          h.value = String(sid);
          addBucket?.appendChild(h);

          const row = buildPreviewRowFromModalCheckbox(cb);
          if (row) body?.appendChild(row);
        }
      });

      ensurePendingSeparator();
      bsModal?.hide();
    });

    btnOpen?.addEventListener("click", () => {
      bsModal?.show();
      loadAddTable(1);
    });

    // ----------- schedule hydrate for edit -----------
    function setPickedDays(days) {
      const map = {
        1: "Monday",
        2: "Tuesday",
        3: "Wednesday",
        4: "Thursday",
        5: "Friday",
        6: "Saturday",
      };
      days.forEach((d) => {
        const btn = document.querySelector(`.day-btn[data-day="${map[d]}"]`);
        if (btn && !btn.classList.contains("active")) {
          btn.classList.add("active", "btn-primary");
          btn.classList.remove("btn-outline-secondary");
        }
      });
      const live = $("#DayLiveText");
      if (live) {
        live.textContent = days
          .map(
            (d) =>
              ({
                1: "Monday",
                2: "Tuesday",
                3: "Wednesday",
                4: "Thursday",
                5: "Friday",
                6: "Saturday",
              }[d])
          )
          .join(", ");
      }
      const csvHidden = document.querySelector("#ScheduleDaysCsv");
      if (csvHidden) csvHidden.value = days.join(",");
    }

    function hydrateScheduleFromServer() {
      const host = document.querySelector("#ScheduleData");
      if (!host) return;

      const roomSel = document.querySelector("#RoomSelect");
      const startEl = document.querySelector("#StartTime");
      const endEl = document.querySelector("#EndTime");

      const csv = (host.dataset.existingDays || "").trim();
      const room = (host.dataset.room || "").trim();
      const start = (host.dataset.start || "").trim();
      const end = (host.dataset.end || "").trim();

      if (csv) {
        const ints = csv
          .split(",")
          .map((s) => parseInt(s, 10))
          .filter((n) => n >= 1 && n <= 6);
        setPickedDays(ints);
      }
      if (start && startEl) startEl.value = start;
      if (end && endEl) endEl.value = end;

      if (room && roomSel) {
        const hasOption = Array.from(roomSel.options).some(
          (o) => o.value === room
        );
        if (!hasOption) {
          const opt = document.createElement("option");
          opt.value = room;
          opt.textContent = room;
          roomSel.appendChild(opt);
        }
        roomSel.value = room;
        // never disable the select; it must post
        roomSel.removeAttribute("disabled");
      }
    }
  }

  // ---------- DOM Ready wiring ----------
  document.addEventListener("DOMContentLoaded", () => {
    if ($(".admin-accounts-container")) initAdminAssignIndex();
    if ($("#blockFields")) initAdminAssignPhase2();
    if ($("#addStudentsModal")) initAdminAssignView();

    if ($("#CourseId") && $("#Type") && $("#Units")) {
      attachCourseTypeUnits({
        courseSel: "#CourseId",
        typeSel: "#Type",
        unitsInput: "#Units",
      });
    }
  });
})();
