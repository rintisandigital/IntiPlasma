/*
 * Searchable dropdowns: <select data-lookup="/Lookup/Items?category=Feed"> becomes a Tom-Select that queries the
 * URL with ?q=. Options already in the markup (the current value) are kept. Call window.lookup.init(element)
 * for selects added later (e.g. new table rows).
 *
 * data-lookup-depends="warehouseId:#FromWarehouseId" adds the value of another field to the query (comma separated
 * for several); when that field changes, the loaded options and the current choice are cleared.
 */
(function () {
    'use strict';

    function dependencies(select) {
        var spec = select.getAttribute('data-lookup-depends');
        if (!spec) {
            return [];
        }

        return spec.split(',').map(function (pair) {
            var parts = pair.split(':');
            return { name: parts[0].trim(), field: document.querySelector(parts[1].trim()) };
        }).filter(function (d) { return d.field; });
    }

    function init(select) {
        if (select.tomselect || !window.TomSelect) {
            return;
        }

        var url = select.getAttribute('data-lookup');
        var depends = dependencies(select);

        var control = new TomSelect(select, {
            valueField: 'value',
            labelField: 'text',
            searchField: ['text'],
            maxOptions: 20,
            loadThrottle: 250,
            preload: 'focus',
            closeAfterSelect: true,
            // Leave the field after a choice; otherwise the focus preload reopens the list over the form buttons.
            onItemAdd: function () { this.blur(); },
            load: function (query, callback) {
                var separator = url.indexOf('?') >= 0 ? '&' : '?';
                var extra = depends.map(function (d) {
                    return '&' + encodeURIComponent(d.name) + '=' + encodeURIComponent(d.field.value || '');
                }).join('');
                fetch(url + separator + 'q=' + encodeURIComponent(query) + extra, {
                    credentials: 'same-origin',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                })
                    .then(function (r) { return r.ok ? r.json() : []; })
                    .then(callback)
                    .catch(function () { callback(); });
            }
        });

        depends.forEach(function (d) {
            d.field.addEventListener('change', function () {
                control.clear();
                control.clearOptions();
                // Tom-Select remembers loaded queries; forget them so the next search uses the new value.
                control.loadedSearches = {};
            });
        });
    }

    window.lookup = { init: init };

    document.querySelectorAll('select[data-lookup]').forEach(init);
})();
