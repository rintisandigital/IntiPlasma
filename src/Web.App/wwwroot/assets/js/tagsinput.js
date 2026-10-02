/*
 * Vanilla JavaScript implementation for Tagsinput
 * Converts input[data-role="tagsinput"] and input.input-tags elements into interactive tag chips.
 */
(function () {
	'use strict';

	function injectStyles() {
		if (document.getElementById('tagsinput-default-styles')) return;
		var style = document.createElement('style');
		style.id = 'tagsinput-default-styles';
		style.textContent = [
			'.bootstrap-tagsinput .tag { display: inline-flex; align-items: center; padding: 4px 8px; margin: 2px; border-radius: 5px; background-color: #E8E9EA; color: #212529; font-size: 13px; }',
			'.bootstrap-tagsinput .tag [data-role="remove"] { margin-left: 6px; cursor: pointer; display: inline-flex; align-items: center; }',
			'.bootstrap-tagsinput .tag [data-role="remove"]:after { content: "\\eb55"; font-family: "tabler-icons"; font-size: 14px; line-height: 1; }'
		].join('\n');
		document.head.appendChild(style);
	}

	function initTagsInput(input) {
		if (!input || input._tagsInput) return;

		// Parse initial values from input value (comma-separated)
		var initialValues = (input.value || '')
			.split(',')
			.map(function (val) { return val.trim(); })
			.filter(Boolean);

		// Container wrapper matching Bootstrap tagsinput structure
		var wrapper = document.createElement('div');
		wrapper.className = 'bootstrap-tagsinput';
		if (input.classList.contains('form-control')) {
			wrapper.classList.add('form-control');
		}

		// Inner text input for typing new tags
		var textInput = document.createElement('input');
		textInput.type = 'text';
		textInput.placeholder = input.placeholder || '';
		textInput.style.border = 'none';
		textInput.style.outline = 'none';
		textInput.style.boxShadow = 'none';
		textInput.style.background = 'transparent';
		textInput.style.padding = '0';
		textInput.style.margin = '2px';
		textInput.style.display = 'inline-block';
		textInput.style.width = 'auto';

		// Insert wrapper before input, move textInput into wrapper, hide original input
		input.parentNode.insertBefore(wrapper, input);
		wrapper.appendChild(textInput);
		input.style.display = 'none';

		// Sync wrapper tags back to hidden input value
		function syncValue() {
			var tagElements = wrapper.querySelectorAll('.tag');
			var tags = Array.prototype.map.call(tagElements, function (el) {
				return el.getAttribute('data-tag');
			});
			input.value = tags.join(',');
			input.dispatchEvent(new Event('change', { bubbles: true }));
		}

		// Add a tag chip
		function addTag(text) {
			text = text.trim();
			if (!text) return;

			// Prevent duplicate tags
			var existingTags = Array.prototype.map.call(wrapper.querySelectorAll('.tag'), function (el) {
				return el.getAttribute('data-tag');
			});
			if (existingTags.indexOf(text) !== -1) return;

			var tag = document.createElement('span');
			tag.className = 'tag badge-info';
			tag.setAttribute('data-tag', text);

			var textSpan = document.createElement('span');
			textSpan.textContent = text;
			tag.appendChild(textSpan);

			var removeBtn = document.createElement('span');
			removeBtn.setAttribute('data-role', 'remove');
			removeBtn.addEventListener('click', function (e) {
				e.stopPropagation();
				tag.remove();
				syncValue();
			});
			tag.appendChild(removeBtn);

			wrapper.insertBefore(tag, textInput);
			syncValue();
		}

		// Process initial comma-separated value
		initialValues.forEach(addTag);

		// Key events: Enter, Comma to add tag, Backspace to delete last tag
		textInput.addEventListener('keydown', function (e) {
			if (e.key === 'Enter' || e.key === ',') {
				e.preventDefault();
				addTag(textInput.value);
				textInput.value = '';
			} else if (e.key === 'Backspace' && textInput.value === '') {
				var tags = wrapper.querySelectorAll('.tag');
				if (tags.length > 0) {
					tags[tags.length - 1].remove();
					syncValue();
				}
			}
		});

		// Handle pasting comma-separated text
		textInput.addEventListener('paste', function (e) {
			e.preventDefault();
			var pastedData = (e.clipboardData || window.clipboardData).getData('text');
			if (pastedData) {
				pastedData.split(',').forEach(function (val) {
					addTag(val);
				});
				textInput.value = '';
			}
		});

		// Add tag on blur if user typed something without pressing enter
		textInput.addEventListener('blur', function () {
			if (textInput.value.trim() !== '') {
				addTag(textInput.value);
				textInput.value = '';
			}
		});

		// Focus inner text input when clicking anywhere on wrapper
		wrapper.addEventListener('click', function (e) {
			if (!e.target.hasAttribute('data-role')) {
				textInput.focus();
			}
		});

		input._tagsInput = wrapper;
	}

	function initAllTagsInput() {
		injectStyles();
		var selectors = 'input[data-role="tagsinput"], input.input-tags, select[multiple][data-role="tagsinput"]';
		document.querySelectorAll(selectors).forEach(initTagsInput);
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', initAllTagsInput);
	} else {
		initAllTagsInput();
	}
})();
