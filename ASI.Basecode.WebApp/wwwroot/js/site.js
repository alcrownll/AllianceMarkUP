/*let dpicn = document.querySelector(".dpicn");
let dropdown = document.querySelector(".dropdown");

dpicn.addEventListener("click", () => {
    dropdown.classList.toggle("dropdown-open");
})
*/

document.addEventListener('DOMContentLoaded', function () {
    const mini = document.getElementById('miniCalendar');
    if (mini && window.flatpickr) {
        flatpickr(mini, {
            inline: true,
            defaultDate: new Date(),
            disableMobile: true,
            // If you want date click to jump to the full calendar page, uncomment:
            // onChange: (dates) => {
            //   if (dates.length) {
            //     const iso = dates[0].toISOString();
            //     window.location.href = `/Calendar/Index?date=${encodeURIComponent(iso)}`;
            //   }
            // }
        });
    }
});
