(function () {
	"use strict";
	if (!window.Dropzone) return;
	Dropzone.autoDiscover = false;

	document.addEventListener('DOMContentLoaded', function () {
		document.querySelectorAll('[data-plugin="dropzone"]').forEach(function (form) {
			var options = { url: form.getAttribute('action') };

			var previewsContainer = form.getAttribute('data-previews-container');
			if (previewsContainer) options.previewsContainer = previewsContainer;

			var uploadPreviewTemplateSelector = form.getAttribute('data-upload-preview-template');
			if (uploadPreviewTemplateSelector) {
				var tpl = document.querySelector(uploadPreviewTemplateSelector);
				if (tpl) options.previewTemplate = tpl.innerHTML;
			}

			new Dropzone(form, options);
		});
	});
})();
