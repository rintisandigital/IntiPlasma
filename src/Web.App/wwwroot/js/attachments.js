/*
 * Attachments of a form (partial _Attachments): upload on drop/choose, keep the ids as hidden inputs posted
 * with the form, remove from the list (the use case releases the attachment when the form is saved).
 */
(function () {
    'use strict';

    var csrf = document.body.getAttribute('data-csrf-token');

    function count(box) { return box.querySelectorAll('.attachment-item').length; }

    function add(box, item) {
        var li = document.createElement('li');
        li.className = 'd-flex align-items-center gap-2 py-1 border-bottom attachment-item';

        var hidden = document.createElement('input');
        hidden.type = 'hidden';
        hidden.name = box.dataset.field;
        hidden.value = item.id;

        var icon = document.createElement('i');
        icon.className = 'ti ' + (item.kind === 'Photo' ? 'ti-photo' : 'ti-file-type-pdf') + ' text-primary';

        var link = document.createElement('a');
        link.href = item.url;
        link.target = '_blank';
        link.className = 'text-truncate';
        link.textContent = item.fileName;

        var size = document.createElement('span');
        size.className = 'text-muted fs-12 ms-auto';
        size.textContent = Math.round(item.sizeBytes / 1024).toLocaleString('id-ID') + ' KB';

        var remove = document.createElement('button');
        remove.type = 'button';
        remove.className = 'btn btn-sm btn-light attachment-remove';
        remove.title = 'Remove';
        remove.innerHTML = '<i class="ti ti-x"></i>';

        li.append(hidden, icon, link, size, remove);
        box.querySelector('.attachment-list').appendChild(li);
    }

    function upload(box, files) {
        var progress = box.querySelector('.attachment-progress');
        var max = parseInt(box.dataset.max, 10);
        var queue = Array.prototype.slice.call(files);

        function next() {
            if (queue.length === 0) {
                progress.textContent = '';
                return;
            }
            if (count(box) >= max) {
                window.app && window.app.toastError('At most ' + max + ' attachments are allowed.');
                progress.textContent = '';
                return;
            }

            var file = queue.shift();
            var data = new FormData();
            data.append('file', file);
            progress.textContent = 'Uploading ' + file.name + '…';

            fetch(box.dataset.uploadUrl, {
                method: 'POST',
                body: data,
                credentials: 'same-origin',
                headers: { 'X-CSRF-TOKEN': csrf, 'X-Requested-With': 'XMLHttpRequest' }
            })
                .then(function (r) { return r.json().then(function (body) { return { ok: r.ok, body: body }; }); })
                .then(function (result) {
                    if (result.ok) {
                        add(box, result.body);
                    } else {
                        window.app && window.app.toastError(file.name + ': ' + (result.body.message || 'upload failed'));
                    }
                })
                .catch(function () { window.app && window.app.toastError(file.name + ': upload failed'); })
                .then(next);
        }

        next();
    }

    document.querySelectorAll('.attachments').forEach(function (box) {
        var input = box.querySelector('.attachment-input');
        var drop = box.querySelector('.attachment-drop');

        box.addEventListener('click', function (e) {
            var remove = e.target.closest('.attachment-remove');
            if (remove) {
                remove.closest('.attachment-item').remove();
            }
        });

        if (!input) {
            return;
        }

        input.addEventListener('change', function () {
            upload(box, input.files);
            input.value = '';
        });

        ['dragenter', 'dragover'].forEach(function (name) {
            drop.addEventListener(name, function (e) { e.preventDefault(); drop.classList.add('border-primary'); });
        });
        ['dragleave', 'drop'].forEach(function (name) {
            drop.addEventListener(name, function (e) { e.preventDefault(); drop.classList.remove('border-primary'); });
        });
        drop.addEventListener('drop', function (e) { upload(box, e.dataTransfer.files); });
    });
})();
