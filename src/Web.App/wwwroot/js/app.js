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

        /* Integer input: digits and a leading minus sign. */
        $(document).on('keypress', '.NumberOnly', function (e) {
            var val = $(this).val();
            if ('-0123456789'.indexOf(e.key) < 0) { return false; }
            if (e.key === '-' && val !== '') { return false; }
            return true;
        });

        /* Decimal input in Indonesian format (W-16): comma as the decimal separator. */
        $(document).on('keypress', '.DecimalOnly', function (e) {
            var val = $(this).val();
            if ('-,0123456789'.indexOf(e.key) < 0) { return false; }
            if (e.key === '-' && val !== '') { return false; }
            if (e.key === ',' && (val === '' || val === '-' || val.indexOf(',') > -1)) { return false; }
            return true;
        });
    });
})(jQuery);
