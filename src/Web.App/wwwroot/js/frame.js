/*
 * Loaded first by every page that lives in the mainboard's content iframe (_Layout).
 * - Opened directly in its own tab: redirect to the mainboard with the page in the hash (/Main#/path).
 * - Inside the mainboard: tell the parent where we are (hash, title and active menu are kept in sync).
 * See PLAN-WEBAPP §3.10.
 */
(function () {
    'use strict';

    var root = document.documentElement;
    var mainboardUrl = root.getAttribute('data-mainboard-url') || '/Main';
    var path = window.location.pathname + window.location.search;

    if (window.top === window.self) {
        window.location.replace(mainboardUrl + '#' + path);
        return;
    }

    function announce() {
        var body = document.body;
        window.parent.postMessage({
            type: 'navigated',
            url: path,
            title: document.title,
            menuCode: body ? body.getAttribute('data-menu') : null
        }, window.location.origin);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', announce);
    } else {
        announce();
    }

    // Back/forward cache restores do not reload the page: announce again.
    window.addEventListener('pageshow', function (event) {
        if (event.persisted) {
            announce();
        }
    });
})();
