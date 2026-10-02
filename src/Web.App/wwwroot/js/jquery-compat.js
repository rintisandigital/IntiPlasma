/*
 * The template ships jQuery 4, which removed a few long-deprecated helpers that jquery-validation /
 * jquery-validation-unobtrusive (and older plugins) still call. Load right after jQuery.
 */
(function ($) {
    'use strict';

    if (!$) {
        return;
    }

    $.parseJSON = $.parseJSON || JSON.parse;
    $.isArray = $.isArray || Array.isArray;
    $.isFunction = $.isFunction || function (value) { return typeof value === 'function'; };
    $.trim = $.trim || function (value) { return value == null ? '' : String(value).trim(); };
})(window.jQuery);
