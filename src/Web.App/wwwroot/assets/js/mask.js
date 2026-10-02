(function () {
  'use strict';
  if (!window.IMask) return;

  function mask(id, options) {
    var el = document.getElementById(id);
    if (el) IMask(el, options);
  }

  mask('date', { mask: '00/00/0000' });
  mask('phone', { mask: '(000) 000-0000' });
  mask('phoneExt', { mask: [{ mask: '(000) 000-0000' }, { mask: '(000) 000-0000 x00000' }] });
  mask('ccn', { mask: '0000 0000 0000 0000' });
  mask('ssn', { mask: '000-00-0000' });
  mask('currency', { mask: '000,000,000.00' });
  mask('eyescript', { mask: '~0.00 ~0.00 000', definitions: { '~': /[+-]/ } });
  mask('pct', { mask: '00%' });
})();
