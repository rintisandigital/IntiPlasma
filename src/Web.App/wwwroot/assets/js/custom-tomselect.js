(function () {
    "use strict";

    function initTomSelect(selector, options) {
        document.querySelectorAll(selector).forEach(function (el) {
            if (el.tomselect) return;
            try {
                new TomSelect(el, options || {});
            } catch (err) {
                console.error('TomSelect init failed for', el, err);
            }
        });
    }

    /* basic select2 */
    initTomSelect('.select2');

    /* multiple select */
    initTomSelect('.js-example-basic-multiple');

    // Single Select Placeholder
    initTomSelect('#select2-with-placeholder', {
        placeholder: "Select a state",
        allowEmptyOption: true,
        plugins: ['clear_button']
    });

    /* single select with placeholder */
    initTomSelect('#select2-placeholder-single', {
        placeholder: "Select a state",
        allowEmptyOption: true,
        plugins: ['clear_button']
    });

    /* multiple select with placeholder */
    initTomSelect('.js-example-placeholder-multiple', {
        placeholder: "Select"
    });

    /* templating */
    initTomSelect('.js-example-templating', {
        placeholder: "Choose Customer",
        render: {
            option: function (data, escape) {
                var baseUrl = "../assets/images/faces/select2";
                var value = (data.value || '').toLowerCase();
                return '<div><img src="' + baseUrl + '/' + escape(value) + '.jpg" class="img-flag" /> ' + escape(data.text) + '</div>';
            }
        }
    });

    /* with images */
    initTomSelect('.select2-client-search', {
        placeholder: "Choose Client",
        render: {
            option: function (data, escape) {
                var value = (data.value || '').toLowerCase();
                return '<div><img src="../assets/images/faces/select2/' + escape(value) + '.jpg" /> ' + escape(data.text) + '</div>';
            },
            item: function (data, escape) {
                var value = (data.value || '').toLowerCase();
                return '<div><img src="../assets/images/faces/select2/' + escape(value) + '.jpg" /> ' + escape(data.text) + '</div>';
            }
        }
    });

    /* max selections limiting */
    initTomSelect('.js-example-basic-multiple-limit-max', {
        maxItems: 3,
        placeholder: "Choose Person"
    });

    /* Disabling select 2 controls */
    initTomSelect('.js-example-disabled');
    initTomSelect('.js-example-disabled-multi');

    function setTomSelectDisabled(selector, disabled) {
        document.querySelectorAll(selector).forEach(function (el) {
            if (el.tomselect) {
                disabled ? el.tomselect.disable() : el.tomselect.enable();
            } else {
                el.disabled = disabled;
            }
        });
    }

    document.querySelectorAll(".js-programmatic-enable").forEach(function (btn) {
        btn.addEventListener("click", function () {
            setTomSelectDisabled(".js-example-disabled", false);
            setTomSelectDisabled(".js-example-disabled-multi", false);
        });
    });
    document.querySelectorAll(".js-programmatic-disable").forEach(function (btn) {
        btn.addEventListener("click", function () {
            setTomSelectDisabled(".js-example-disabled", true);
            setTomSelectDisabled(".js-example-disabled-multi", true);
        });
    });

})();
