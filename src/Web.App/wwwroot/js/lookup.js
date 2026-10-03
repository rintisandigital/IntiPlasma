/*
 * Searchable dropdowns: <select data-lookup="/Lookup/Items?category=Feed"> becomes a Tom-Select that queries the
 * URL with ?q=. Options already in the markup (the current value) are kept. Call window.lookup.init(element)
 * for selects added later (e.g. new table rows).
 */
(function () {
    'use strict';

    function init(select) {
        if (select.tomselect || !window.TomSelect) {
            return;
        }

        var url = select.getAttribute('data-lookup');

        new TomSelect(select, {
            valueField: 'value',
            labelField: 'text',
            searchField: ['text'],
            maxOptions: 20,
            loadThrottle: 250,
            preload: 'focus',
            load: function (query, callback) {
                var separator = url.indexOf('?') >= 0 ? '&' : '?';
                fetch(url + separator + 'q=' + encodeURIComponent(query), {
                    credentials: 'same-origin',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                })
                    .then(function (r) { return r.ok ? r.json() : []; })
                    .then(callback)
                    .catch(function () { callback(); });
            }
        });
    }

    window.lookup = { init: init };

    document.querySelectorAll('select[data-lookup]').forEach(init);
})();
