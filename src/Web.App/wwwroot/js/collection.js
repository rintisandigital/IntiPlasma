/*
 * Editable line tables posted as lists (Rates[0].RatePercent, Rates[1]...):
 *
 *   <table data-collection="Rates">
 *     <tbody>... rows (tr.collection-row) with names "Rates[n].Field" ...</tbody>
 *   </table>
 *   <template data-collection-template="Rates"><tr class="collection-row">... names "Rates[__i__].Field" ...</tr></template>
 *   <button type="button" data-collection-add="Rates">Add</button>
 *   rows contain <button type="button" class="collection-remove">.
 *
 * Indexes are renumbered before submit, so removed rows leave no gaps (MVC list binding needs 0..n-1).
 */
(function () {
    'use strict';

    function rows(table) { return table.querySelectorAll('tbody tr.collection-row'); }

    function renumber(table) {
        var name = table.getAttribute('data-collection');
        var pattern = new RegExp('^' + name + '\\[\\d+\\]');
        rows(table).forEach(function (row, index) {
            row.querySelectorAll('[name]').forEach(function (input) {
                input.name = input.name.replace(pattern, name + '[' + index + ']');
            });
        });
        var empty = table.querySelector('tr.collection-empty');
        if (empty) { empty.hidden = rows(table).length > 0; }
    }

    document.addEventListener('click', function (e) {
        var add = e.target.closest('[data-collection-add]');
        if (add) {
            var name = add.getAttribute('data-collection-add');
            var table = document.querySelector('table[data-collection="' + name + '"]');
            var template = document.querySelector('template[data-collection-template="' + name + '"]');
            var html = template.innerHTML.replace(/__i__/g, String(rows(table).length));
            table.querySelector('tbody').insertAdjacentHTML('beforeend', html);
            var row = rows(table)[rows(table).length - 1];
            if (window.lookup) { row.querySelectorAll('select[data-lookup]').forEach(window.lookup.init); }
            renumber(table);
            var first = row.querySelector('input, select');
            if (first) { first.focus(); }
            return;
        }

        var remove = e.target.closest('.collection-remove');
        if (remove) {
            var owner = remove.closest('table[data-collection]');
            remove.closest('tr').remove();
            renumber(owner);
        }
    });

    document.addEventListener('submit', function (e) {
        e.target.querySelectorAll('table[data-collection]').forEach(renumber);
    }, true);

    document.querySelectorAll('table[data-collection]').forEach(renumber);
})();
