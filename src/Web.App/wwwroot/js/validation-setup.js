/*
 * jquery-validation tweaks, loaded right after the validation scripts (_ValidationScriptsPartial).
 *
 * step: the stock rule rejects a value with more decimal places than the step, so "20.000000" (a quantity read back
 * from a numeric(18,6) column) fails step="0.001" without any visible message. Check the value itself instead:
 * valid when it is a whole multiple of the step.
 */
(function ($) {
    'use strict';

    if (!$ || !$.validator) {
        return;
    }

    $.validator.methods.step = function (value, element, param) {
        if (this.optional(element)) {
            return true;
        }

        var number = Number(value);
        var step = Number(param);
        if (isNaN(number) || !step) {
            return true;
        }

        var ratio = number / step;
        return Math.abs(ratio - Math.round(ratio)) < 1e-6;
    };
})(window.jQuery);
