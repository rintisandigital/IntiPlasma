/**
 * Form Picker
 */

'use strict';

(function () {
  // Flat Picker
  // --------------------------------------------------------------------
  const flatpickrDate = document.querySelector('#flatpickr-date'),
    flatpickrTime = document.querySelector('#flatpickr-time'),
    flatpickrDateTime = document.querySelector('#flatpickr-datetime'),
    flatpickrMulti = document.querySelector('#flatpickr-multi'),
    flatpickrRange = document.querySelector('#flatpickr-range'),
    flatpickrInline = document.querySelector('#flatpickr-inline'),
    flatpickrFriendly = document.querySelector('#flatpickr-human-friendly'),
    flatpickrDisabledRange = document.querySelector('#flatpickr-disabled-range');

  // Date
  if (flatpickrDate) {
    flatpickrDate.flatpickr({
      monthSelectorType: 'static'
    });
  }

  // Time
  if (flatpickrTime) {
    flatpickrTime.flatpickr({
      enableTime: true,
      noCalendar: true
    });
  }

  // Datetime
  if (flatpickrDateTime) {
    flatpickrDateTime.flatpickr({
      enableTime: true,
      dateFormat: 'Y-m-d H:i'
    });
  }

  // Multi Date Select
  if (flatpickrMulti) {
    flatpickrMulti.flatpickr({
      weekNumbers: true,
      enableTime: true,
      mode: 'multiple',
      minDate: 'today'
    });
  }

  // Range
  if (typeof flatpickrRange != undefined) {
    flatpickrRange.flatpickr({
      mode: 'range'
    });
  }

  // Inline
  if (flatpickrInline) {
    flatpickrInline.flatpickr({
      inline: true,
      allowInput: false,
      monthSelectorType: 'static'
    });
  }

  // Human Friendly
  if (flatpickrFriendly) {
    flatpickrFriendly.flatpickr({
      altInput: true,
      altFormat: 'F j, Y',
      dateFormat: 'Y-m-d'
    });
  }

  // Disabled Date Range
  if (flatpickrDisabledRange) {
    const fromDate = new Date(Date.now() - 3600 * 1000 * 48);
    const toDate = new Date(Date.now() + 3600 * 1000 * 48);

    flatpickrDisabledRange.flatpickr({
      dateFormat: 'Y-m-d',
      disable: [
        {
          from: fromDate.toISOString().split('T')[0],
          to: toDate.toISOString().split('T')[0]
        }
      ]
    });
  }
})();

// * Pickers previously backed by jQuery plugins - now flatpickr / daterangepicker.js
document.addEventListener('DOMContentLoaded', function () {
  // Bootstrap Datepicker -> flatpickr
  // --------------------------------------------------------------------
  var bsDatepickerBasic = document.querySelector('#bs-datepicker-basic'),
    bsDatepickerFormat = document.querySelector('#bs-datepicker-format'),
    bsDatepickerRange = document.querySelector('#bs-datepicker-daterange'),
    bsDatepickerDisabledDays = document.querySelector('#bs-datepicker-disabled-days'),
    bsDatepickerMultidate = document.querySelector('#bs-datepicker-multidate'),
    bsDatepickerOptions = document.querySelector('#bs-datepicker-options'),
    bsDatepickerAutoclose = document.querySelector('#bs-datepicker-autoclose'),
    bsDatepickerInlinedate = document.querySelector('#bs-datepicker-inline');

  if (!window.flatpickr) return;

  // Basic
  if (bsDatepickerBasic) {
    flatpickr(bsDatepickerBasic, {});
  }

  // Format
  if (bsDatepickerFormat) {
    flatpickr(bsDatepickerFormat, { dateFormat: 'd/m/Y' });
  }

  // Range
  if (bsDatepickerRange) {
    flatpickr(bsDatepickerRange, { mode: 'range' });
  }

  // Disabled Days (Sun/Sat)
  if (bsDatepickerDisabledDays) {
    flatpickr(bsDatepickerDisabledDays, {
      disable: [function (date) { return date.getDay() === 0 || date.getDay() === 6; }]
    });
  }

  // Multiple
  if (bsDatepickerMultidate) {
    flatpickr(bsDatepickerMultidate, { mode: 'multiple' });
  }

  // Options
  if (bsDatepickerOptions) {
    flatpickr(bsDatepickerOptions, { weekNumbers: true, clickOpens: true, allowInput: true });
  }

  // Auto close (flatpickr closes on select by default)
  if (bsDatepickerAutoclose) {
    flatpickr(bsDatepickerAutoclose, {});
  }

  // Inline picker
  if (bsDatepickerInlinedate) {
    flatpickr(bsDatepickerInlinedate, { inline: true });
  }

  // Bootstrap Daterange Picker -> daterangepicker.js
  // --------------------------------------------------------------------
  var bsRangePickerBasic = document.querySelector('#bs-rangepicker-basic'),
    bsRangePickerSingle = document.querySelector('#bs-rangepicker-single'),
    bsRangePickerTime = document.querySelector('#bs-rangepicker-time'),
    bsRangePickerRange = document.querySelector('#bs-rangepicker-range'),
    bsRangePickerWeekNum = document.querySelector('#bs-rangepicker-week-num'),
    bsRangePickerDropdown = document.querySelector('#bs-rangepicker-dropdown'),
    bsRangePickerCancelBtn = document.getElementsByClassName('cancelBtn');

  if (window.vanillaDateRangePicker) {
    // Basic
    if (bsRangePickerBasic) vanillaDateRangePicker(bsRangePickerBasic);

    // Single (plain date, not a range)
    if (bsRangePickerSingle) flatpickr(bsRangePickerSingle, {});

    // Time & Date
    if (bsRangePickerTime) {
      flatpickr(bsRangePickerTime, { mode: 'range', enableTime: true, dateFormat: 'm/d/Y h:i K' });
    }

    if (bsRangePickerRange) vanillaDateRangePicker(bsRangePickerRange);

    // Week Numbers
    if (bsRangePickerWeekNum) flatpickr(bsRangePickerWeekNum, { mode: 'range', weekNumbers: true });

    // Dropdown (month/year dropdowns are flatpickr's default, unlike the static header)
    if (bsRangePickerDropdown) flatpickr(bsRangePickerDropdown, { mode: 'range', monthSelectorType: 'dropdown' });
  }

  // Adding btn-label-secondary class in cancel btn
  for (var i = 0; i < bsRangePickerCancelBtn.length; i++) {
    bsRangePickerCancelBtn[i].classList.remove('btn-default');
    bsRangePickerCancelBtn[i].classList.add('btn-label-primary');
  }

  // jQuery Timepicker -> flatpickr (enableTime, noCalendar)
  // --------------------------------------------------------------------
  var basicTimepicker = document.querySelector('#timepicker-basic'),
    minMaxTimepicker = document.querySelector('#timepicker-min-max'),
    disabledTimepicker = document.querySelector('#timepicker-disabled-times'),
    formatTimepicker = document.querySelector('#timepicker-format'),
    stepTimepicker = document.querySelector('#timepicker-step'),
    altHourTimepicker = document.querySelector('#timepicker-24hours');

  // Basic
  if (basicTimepicker) {
    flatpickr(basicTimepicker, { enableTime: true, noCalendar: true, dateFormat: 'h:i K' });
  }

  // Min & Max
  if (minMaxTimepicker) {
    flatpickr(minMaxTimepicker, {
      enableTime: true, noCalendar: true, dateFormat: 'h:i K',
      minTime: '14:00', maxTime: '19:00'
    });
  }

  // Disabled Picker (12am-3am, 4am-4:30am unavailable)
  if (disabledTimepicker) {
    flatpickr(disabledTimepicker, {
      enableTime: true, noCalendar: true, dateFormat: 'h:i K',
      disable: [
        function (date) {
          var h = date.getHours() + date.getMinutes() / 60;
          return (h >= 0 && h < 3) || (h >= 4 && h < 4.5);
        }
      ]
    });
  }

  // Format Picker
  if (formatTimepicker) {
    flatpickr(formatTimepicker, { enableTime: true, noCalendar: true, dateFormat: 'H:i:S', enableSeconds: true, time_24hr: true });
  }

  // Steps Picker
  if (stepTimepicker) {
    flatpickr(stepTimepicker, { enableTime: true, noCalendar: true, dateFormat: 'h:i K', minuteIncrement: 15 });
  }

  // 24 Hours Format
  if (altHourTimepicker) {
    flatpickr(altHourTimepicker, { enableTime: true, noCalendar: true, dateFormat: 'H:i:S', enableSeconds: true, time_24hr: true });
  }
});
