/*
 * Unit dropdowns that follow the item of the same table row:
 *
 *   <tr> ... <select class="line-item" data-units-url="/Lookup/ItemUnits"> ... <select class="line-uom"> ...
 *        <select class="line-tax"> (optional: set to the item's default tax code) </tr>
 *
 * When the item changes, the unit list is replaced by the item's base unit and conversion units (base selected).
 */
(function () {
    'use strict';

    function fill(itemSelect) {
        var row = itemSelect.closest('tr');
        var uom = row && row.querySelector('select.line-uom');
        if (!uom) {
            return;
        }

        uom.innerHTML = '';
        if (!itemSelect.value) {
            return;
        }

        var url = itemSelect.getAttribute('data-units-url') + '?itemId=' + encodeURIComponent(itemSelect.value);
        fetch(url, { credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (data) {
                if (!data) {
                    return;
                }

                data.units.forEach(function (unit) {
                    var option = document.createElement('option');
                    option.value = unit.value;
                    option.textContent = unit.text;
                    uom.appendChild(option);
                });

                var tax = row.querySelector('select.line-tax');
                if (tax && data.taxCodeId && tax.querySelector('option[value="' + data.taxCodeId + '"]')) {
                    tax.value = data.taxCodeId;
                }

                uom.dispatchEvent(new Event('change', { bubbles: true }));
            });
    }

    document.addEventListener('change', function (e) {
        if (e.target.matches && e.target.matches('select.line-item')) {
            fill(e.target);
        }
    });
})();
