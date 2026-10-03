/*
 * Mainboard (Views/Main/Index.cshtml): header + two-column sidebar + content iframe. See PLAN-WEBAPP §3.10.
 *
 * Navigation model: menu links target the iframe ("content-frame"), so the browser keeps the iframe's
 * history and Back/Forward work natively. Every page in the iframe posts a "navigated" message (frame.js);
 * the mainboard mirrors it in its own URL hash (/Main#/sales/orders?page=2) with replaceState, which makes
 * pages bookmarkable and refresh-safe without creating extra history entries.
 */
(function () {
    'use strict';

    var body = document.body;
    var frame = document.querySelector('iframe[name="content-frame"]');
    var loader = document.querySelector('.frame-loader');
    var appTitle = body.getAttribute('data-app-title') || document.title;
    var homeUrl = body.getAttribute('data-home-url');
    var origin = window.location.origin;

    /* Only app-internal paths may be loaded into the iframe (no open redirect via the hash). */
    function isInternalPath(path) {
        return typeof path === 'string' &&
            path.charAt(0) === '/' &&
            path.charAt(1) !== '/' &&
            path.charAt(1) !== '\\';
    }

    function pathFromHash() {
        var hash = window.location.hash ? window.location.hash.substring(1) : '';
        try {
            hash = decodeURIComponent(hash);
        } catch (e) {
            return null;
        }
        return isInternalPath(hash) ? hash : null;
    }

    function showLoader() {
        if (loader) {
            loader.classList.add('show');
        }
    }

    function hideLoader() {
        if (loader) {
            loader.classList.remove('show');
        }
    }

    function load(path) {
        showLoader();
        frame.contentWindow.location.replace(path);
    }

    /* Sizes the iframe to the space below the header. */
    function adjustFrameHeight() {
        var header = document.querySelector('.header');
        var headerHeight = header ? header.offsetHeight : 0;
        frame.style.height = Math.max(window.innerHeight - headerHeight - 16, 200) + 'px';
    }

    /* Highlights the menu link of the current page and opens its module tab. */
    function activateMenu(url, menuCode) {
        var links = document.querySelectorAll('a[target="content-frame"]');
        var best = null;
        var bestLength = -1;
        var pathname = url.split('?')[0].split('#')[0].toLowerCase();

        links.forEach(function (link) {
            link.classList.remove('active');

            var code = link.getAttribute('data-menu-code');
            var linkPath = (link.getAttribute('href') || '').split('?')[0].toLowerCase();

            if (menuCode && code === menuCode) {
                best = link;
                bestLength = Number.MAX_SAFE_INTEGER;
            } else if (linkPath && (pathname === linkPath || pathname.indexOf(linkPath + '/') === 0) &&
                linkPath.length > bestLength) {
                best = link;
                bestLength = linkPath.length;
            }
        });

        if (!best) {
            return;
        }

        best.classList.add('active');

        var pane = best.closest('.tab-pane');
        if (pane && window.bootstrap) {
            var tabLink = document.querySelector('[data-bs-target="#' + pane.id + '"]');
            if (tabLink && !tabLink.classList.contains('active')) {
                bootstrap.Tab.getOrCreateInstance(tabLink).show();
            }
        }
    }

    window.addEventListener('message', function (event) {
        if (event.origin !== origin || event.source !== frame.contentWindow) {
            return;
        }

        var message = event.data || {};
        if (message.type !== 'navigated' || !isInternalPath(message.url)) {
            return;
        }

        hideLoader();
        history.replaceState(null, '', '#' + message.url);
        document.title = message.title ? message.title : appTitle;
        activateMenu(message.url, message.menuCode);
    });

    /* A manually edited hash (or a bookmark opened in the same tab) loads that page. */
    window.addEventListener('hashchange', function () {
        var path = pathFromHash();
        if (path) {
            load(path);
        }
    });

    frame.addEventListener('load', hideLoader);

    document.addEventListener('click', function (event) {
        var link = event.target.closest('a[target="content-frame"]');
        if (link && !event.ctrlKey && !event.metaKey && !event.shiftKey) {
            showLoader();
        }
    });

    /* Branch switcher: remember the selection, then reload the current page with the new default filter. */
    document.addEventListener('click', function (event) {
        var item = event.target.closest('[data-branch-id]');
        if (!item) {
            return;
        }

        event.preventDefault();

        var switcher = item.closest('.branch-switcher');
        var data = new URLSearchParams();
        data.append('branchId', item.getAttribute('data-branch-id'));

        fetch(switcher.getAttribute('data-switch-url'), {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'X-Requested-With': 'XMLHttpRequest',
                'X-CSRF-TOKEN': body.getAttribute('data-csrf-token')
            },
            body: data
        }).then(function (response) {
            if (response.status === 401) {
                window.location.reload();
                return;
            }
            if (!response.ok) {
                return;
            }

            switcher.querySelectorAll('[data-branch-id]').forEach(function (el) {
                el.classList.toggle('active', el === item);
            });
            switcher.querySelector('.active-branch-name').textContent = item.getAttribute('data-branch-name');

            showLoader();
            frame.contentWindow.location.reload();
        });
    });

    /* Menu search (header): matches the sidebar links, i.e. only menus the user may view. */
    (function () {
        var form = document.querySelector('.menu-search');
        if (!form) {
            return;
        }

        var input = form.querySelector('.menu-search-input');
        var list = form.querySelector('.menu-search-list');
        var empty = form.querySelector('.menu-search-empty');
        var toggle = form.querySelector('[data-bs-toggle="dropdown"]');
        var maxResults = 10;

        function groupTitle(link) {
            var pane = link.closest('.tab-pane');
            var tab = pane ? document.querySelector('[data-bs-target="#' + pane.id + '"]') : null;
            return tab ? tab.getAttribute('title') : '';
        }

        function entries() {
            var seen = {};
            var result = [];
            document.querySelectorAll('#two-col-sidebar a[target="content-frame"][data-menu-code]').forEach(function (link) {
                var code = link.getAttribute('data-menu-code');
                if (seen[code]) {
                    return;
                }
                seen[code] = true;
                result.push({ title: link.textContent.trim(), group: groupTitle(link), url: link.getAttribute('href') });
            });
            return result;
        }

        function open(url) {
            showLoader();
            window.open(url, 'content-frame');
            input.value = '';
            render();
            if (window.bootstrap && toggle) {
                bootstrap.Dropdown.getOrCreateInstance(toggle).hide();
            }
        }

        function render() {
            var term = input.value.trim().toLowerCase();
            var matches = entries().filter(function (e) {
                return !term || e.title.toLowerCase().indexOf(term) >= 0 || e.group.toLowerCase().indexOf(term) >= 0;
            }).slice(0, maxResults);

            list.innerHTML = '';
            matches.forEach(function (e, index) {
                var item = document.createElement('li');
                var link = document.createElement('a');
                link.href = e.url;
                link.className = 'dropdown-item rounded-1' + (index === 0 && term ? ' active' : '');
                link.setAttribute('data-menu-url', e.url);
                link.textContent = e.title;
                if (e.group) {
                    var group = document.createElement('small');
                    group.className = 'text-muted ms-2';
                    group.textContent = e.group;
                    link.appendChild(group);
                }
                item.appendChild(link);
                list.appendChild(item);
            });
            empty.hidden = matches.length > 0;
        }

        input.addEventListener('input', function () {
            render();
            if (window.bootstrap && toggle) {
                bootstrap.Dropdown.getOrCreateInstance(toggle).show();
            }
        });
        input.addEventListener('focus', render);

        input.addEventListener('keydown', function (event) {
            if (event.key === 'Enter') {
                event.preventDefault();
                var first = list.querySelector('[data-menu-url]');
                if (first) {
                    open(first.getAttribute('data-menu-url'));
                }
            } else if (event.key === 'Escape') {
                input.value = '';
                input.blur();
                if (window.bootstrap && toggle) {
                    bootstrap.Dropdown.getOrCreateInstance(toggle).hide();
                }
            }
        });

        list.addEventListener('click', function (event) {
            var link = event.target.closest('[data-menu-url]');
            if (link) {
                event.preventDefault();
                open(link.getAttribute('data-menu-url'));
            }
        });

        form.addEventListener('submit', function (event) { event.preventDefault(); });

        /* Bootstrap closes the dropdown on Escape (it stops the key event before it reaches the input) and on an
           outside click: start the next search from scratch. */
        if (toggle) {
            toggle.addEventListener('hidden.bs.dropdown', function () {
                input.value = '';
                render();
            });
        }

        /* Ctrl+K / Cmd+K focuses the search box. */
        document.addEventListener('keydown', function (event) {
            if ((event.ctrlKey || event.metaKey) && (event.key === 'k' || event.key === 'K')) {
                event.preventDefault();
                input.focus();
            }
        });
    })();

    window.addEventListener('resize', adjustFrameHeight);
    adjustFrameHeight();

    load(pathFromHash() || homeUrl);
})();
