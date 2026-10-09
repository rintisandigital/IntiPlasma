/*
 * Shared behaviour of the pages inside the content iframe (_Layout): toasts, confirmations, submit-once,
 * AJAX antiforgery header and numeric input filters.
 */
(function ($) {
    'use strict';

    var csrfToken = document.body.getAttribute('data-csrf-token');

    $.ajaxSetup({
        headers: {
            'X-CSRF-TOKEN': csrfToken,
            'X-Requested-With': 'XMLHttpRequest'
        }
    });

    function toast(heading, message, icon, bgColor) {
        $.toast({
            heading: heading,
            text: message,
            showHideTransition: 'slide',
            icon: icon,
            bgColor: bgColor,
            loaderBg: '#ffffff',
            position: 'bottom-right',
            hideAfter: 7000
        });
    }

    window.app = {
        toastSuccess: function (message) { toast('Success', message, 'success', '#27ae60'); },
        toastError: function (message) { toast('Error', message, 'error', '#c0392b'); }
    };

    $(function () {
        /* Messages from the previous request (TempData), rendered as data attributes by the layout. */
        var notice = document.getElementById('page-notice');
        if (notice) {
            var success = notice.getAttribute('data-success');
            var error = notice.getAttribute('data-error');
            if (success) { window.app.toastSuccess(success); }
            if (error) { window.app.toastError(error); }
        }

        /* <form data-confirm="Approve this document?"> asks before submitting. */
        $(document).on('submit', 'form[data-confirm]', function (event) {
            var form = this;
            if (form.getAttribute('data-confirmed') === 'true') {
                return;
            }

            event.preventDefault();

            Swal.fire({
                title: 'Are you sure?',
                text: form.getAttribute('data-confirm'),
                icon: 'question',
                showCancelButton: true,
                confirmButtonText: 'Yes',
                cancelButtonText: 'Cancel',
                confirmButtonColor: '#0984E3'
            }).then(function (result) {
                if (result.isConfirmed) {
                    form.setAttribute('data-confirmed', 'true');
                    form.requestSubmit ? form.requestSubmit() : form.submit();
                }
            });
        });

        /* Disable submit buttons once a (valid) form is submitted, against double clicks. */
        $(document).on('submit', 'form', function (event) {
            var form = this;
            if (event.isDefaultPrevented() || ($.fn.valid && $(form).data('validator') && !$(form).valid())) {
                return;
            }
            $(form).find('button[type="submit"], input[type="submit"]').prop('disabled', true);
        });

        /*
         * Behaviour that used to be inline handlers (not allowed by the Content-Security-Policy, W10):
         * <select data-autosubmit> submits its form on change; data-autosubmit="value" only when a value is chosen.
         * <select data-copy-label="#field"> copies the chosen option's text into another field.
         * <a href="#" data-fill-target="#field" data-fill-value="…"> fills a field.
         */
        $(document).on('change', '[data-autosubmit]', function () {
            if (this.getAttribute('data-autosubmit') === 'value' && !this.value) { return; }
            this.form.submit();
        });

        $(document).on('change', 'select[data-copy-label]', function () {
            var target = document.querySelector(this.getAttribute('data-copy-label'));
            if (target) { target.value = this.selectedOptions[0] ? this.selectedOptions[0].text : ''; }
        });

        $(document).on('click', '[data-fill-target]', function (event) {
            event.preventDefault();
            var target = document.querySelector(this.getAttribute('data-fill-target'));
            if (target) {
                target.value = this.getAttribute('data-fill-value');
                $(target).trigger('change');
            }
        });

        /* Placeholder links (dropdown toggles, menu headers) never navigate. */
        $(document).on('click', 'a[href="#"]', function (event) { event.preventDefault(); });

        /*
         * Accessible names for fields without a <label> (W10): cells of line tables get "<column> — <row>",
         * filter selects their "All …" option text, Tom-Select inputs the name of the select they replace.
         * Runs again for rows added later (dynamic lines, Tom-Select wrappers).
         */
        function textOf(element) {
            return element ? element.textContent.replace(/\s+/g, ' ').trim() : '';
        }

        function hasName(field) {
            if (field.getAttribute('aria-label') || field.getAttribute('aria-labelledby') || field.closest('label')) {
                return true;
            }
            return !!(field.id && document.querySelector('label[for="' + CSS.escape(field.id) + '"]'));
        }

        function nameFromTable(field) {
            var cell = field.closest('td, th');
            var row = cell && cell.parentElement;
            var table = row && row.closest('table');
            if (!table) { return ''; }

            var headers = table.querySelectorAll('thead tr:last-child th');
            var column = textOf(headers[cell.cellIndex]);
            var first = row.cells[0];
            var rowName = first && first !== cell ? textOf(first) : '';
            if (!rowName && row.parentElement) {
                rowName = 'line ' + (Array.prototype.indexOf.call(row.parentElement.rows, row) + 1);
            }
            return [column, rowName].filter(Boolean).join(' — ');
        }

        function nameFields(root) {
            root.querySelectorAll('input:not([type=hidden]):not([type=submit]):not([type=button]), select, textarea').forEach(function (field) {
                if (hasName(field)) { return; }

                var name = nameFromTable(field);
                if (!name && field.tagName === 'SELECT' && field.options.length && field.options[0].value === '') {
                    name = textOf(field.options[0]);
                }
                if (!name) { name = field.getAttribute('placeholder') || field.getAttribute('title') || ''; }
                if (name) { field.setAttribute('aria-label', name); }
            });

            root.querySelectorAll('select.tomselected').forEach(function (select) {
                var input = select.tomselect && select.tomselect.control_input;
                if (input && !input.getAttribute('aria-label')) {
                    var label = select.getAttribute('aria-label') ||
                        textOf(select.id ? document.querySelector('label[for="' + CSS.escape(select.id) + '"]') : null) ||
                        nameFromTable(select);
                    if (label) { input.setAttribute('aria-label', label); }
                }
            });
        }

        nameFields(document);
        var pending = null;
        new MutationObserver(function () {
            clearTimeout(pending);
            pending = setTimeout(function () { nameFields(document); }, 50);
        }).observe(document.body, { childList: true, subtree: true });

        /* Integer input: digits and a leading minus sign. */
        $(document).on('keypress', '.NumberOnly', function (e) {
            var val = $(this).val();
            if ('-0123456789'.indexOf(e.key) < 0) { return false; }
            if (e.key === '-' && val !== '') { return false; }
            return true;
        });

        /* Decimal input: the request culture is en-US, so the decimal separator is a dot. */
        $(document).on('keypress', '.DecimalOnly', function (e) {
            var val = $(this).val();
            if ('-.0123456789'.indexOf(e.key) < 0) { return false; }
            if (e.key === '-' && val !== '') { return false; }
            if (e.key === '.' && (val === '' || val === '-' || val.indexOf('.') > -1)) { return false; }
            return true;
        });

        /* <input type="checkbox" data-check-all="name"> toggles every checkbox named "name" of its form. */
        $(document).on('change', '[data-check-all]', function () {
            var name = $(this).attr('data-check-all');
            $(this.form || document).find('input[type="checkbox"][name="' + name + '"]').prop('checked', this.checked);
        });

        /* <select data-submit-on-change> reloads a filter form as soon as the choice changes. */
        $(document).on('change', 'select[data-submit-on-change]', function () {
            if (this.form) { this.form.submit(); }
        });
    });
})(jQuery);
