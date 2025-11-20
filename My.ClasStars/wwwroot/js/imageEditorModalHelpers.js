window.imageEditorModal_triggerFile = function (modalId) {
    try {
        var modal = document.getElementById(modalId);
        if (!modal) {
            var fallback = document.querySelector('input[type="file"]');
            if (fallback) fallback.click();
            return;
        }
        var input = modal.querySelector('input[type="file"]');
        if (input) input.click();
        else {
            var fallback = document.querySelector('input[type="file"]');
            if (fallback) fallback.click();
        }
    } catch (e) {
        console.error(e);
    }
};

window.imageEditorModal_dispatchResize = function () {
    try {
        window.dispatchEvent(new Event('resize'));
        setTimeout(function () { window.dispatchEvent(new Event('resize')); }, 50);
    } catch (e) {
        console.error(e);
    }
};

window.imageEditorModal_getCanvasRect = function (modalId) {
    try {
        var modal = document.getElementById(modalId);
        if (!modal) return null;
        var canvas = modal.querySelector('.e-image-editor .e-svg-canvas, .e-image-editor-canvas, .e-image-editor .e-image-editor-canvas');
        if (!canvas) {
            canvas = modal.querySelector('.e-image-editor canvas');
        }
        if (!canvas) return null;
        var rect = canvas.getBoundingClientRect();
        return { left: rect.left, top: rect.top, width: rect.width, height: rect.height };
    } catch (e) {
        console.error(e);
        return null;
    }
};
window.imageEditorModal_attachGlobalInputChangeListener = function (modalId, dotNetRef) {
    try {
        if (!dotNetRef) return;

        if (window._imageEditorGlobalListenerAttached) return;
        window._imageEditorGlobalListenerAttached = true;

        var SfImageEditor = document.getElementById('SfImageEditor');

        SfImageEditor.addEventListener('change', function (ev) {
            try {
                var t = ev.target || ev.srcElement;
                if (!t) return;
                if (t.tagName !== 'INPUT' || t.type !== 'file') return;

                var handle = function (inputElem) {
                    try {
                        if (!inputElem.files || inputElem.files.length === 0) return;

                        var f = inputElem.files[0];
                        if (!f) return;

                        var reader = new FileReader();
                        reader.onload = function (e) {
                            try {
                                var dataUrl = e.target.result;
                                var img = new Image();
                                img.onload = function () {
                                    try {
                                        var w = img.naturalWidth || img.width;
                                        var h = img.naturalHeight || img.height;
                                        var isSquare = Math.abs(w - h) <= 2;
                                        dotNetRef.invokeMethodAsync('OnSfEditorFileSelected', f.name || '', isSquare)
                                            .catch(function (err) { console.error('invokeMethodAsync error', err); });
                                    } catch (ie) {
                                        console.error('img.onload error', ie);
                                        dotNetRef.invokeMethodAsync('OnSfEditorFileSelected', f.name || '', false).catch(() => { });
                                    }
                                };
                                img.onerror = function () {
                                    dotNetRef.invokeMethodAsync('OnSfEditorFileSelected', f.name || '', false).catch(() => { });
                                };
                                img.src = dataUrl;
                            } catch (readerErr) {
                                console.error('reader.onload error', readerErr);
                                dotNetRef.invokeMethodAsync('OnSfEditorFileSelected', f.name || '', false).catch(() => { });
                            }
                        };
                        reader.onerror = function () {
                            console.error('FileReader error');
                            dotNetRef.invokeMethodAsync('OnSfEditorFileSelected', f.name || '', false).catch(() => { });
                        };
                        reader.readAsDataURL(f);
                    } catch (ex) {
                        console.error('handle input file ex', ex);
                    }
                };

                handle(t);

                setTimeout(function () { handle(t); }, 50);
                setTimeout(function () { handle(t); }, 200);
            } catch (e) {
                console.error('global change handler', e);
            }
        }, true);                   

        function attachBrowseAnchorPolling() {
            var modal = document.getElementById(modalId);
            if (!modal) return;
            var anchor = modal.querySelector('#SfImageEditor_dropBrowse, .e-ie-drop-browse');
            if (!anchor) return;

            if (anchor._imgBrowseClickHandlerAttached) return;
            anchor._imgBrowseClickHandlerAttached = true;

            anchor.addEventListener('click', function (ev) {
                var attempts = 0;
                var maxAttempts = 20;
                var interval = 100;
                var poll = setInterval(function () {
                    attempts++;
                    var all = document.querySelectorAll('input[type="file"]');
                    for (var i = 0; i < all.length; i++) {
                        try {
                            var ii = all[i];
                            if (ii.files && ii.files.length > 0) {
                                var f = ii.files[0];
                                if (f) {
                                    var reader = new FileReader();
                                    reader.onload = function (e) {
                                        var img = new Image();
                                        img.onload = function () {
                                            var w = img.naturalWidth || img.width;
                                            var h = img.naturalHeight || img.height;
                                            var isSquare = Math.abs(w - h) <= 2;
                                            dotNetRef.invokeMethodAsync('OnSfEditorFileSelected', f.name || '', isSquare).catch(() => { });
                                        };
                                        img.onerror = function () { dotNetRef.invokeMethodAsync('OnSfEditorFileSelected', f.name || '', false).catch(() => { }); };
                                        img.src = e.target.result;
                                    };
                                    reader.readAsDataURL(f);
                                }
                                clearInterval(poll);
                                return;
                            }
                        } catch (e) {              }
                    }

                    if (attempts >= maxAttempts) {
                        clearInterval(poll);
                    }
                }, interval);
            }, false);
        }

        try { attachBrowseAnchorPolling(); } catch (e) { setTimeout(attachBrowseAnchorPolling, 100); setTimeout(attachBrowseAnchorPolling, 500); }

    } catch (e) {
        console.error('attachGlobalInputChangeListener', e);
    }
};

window.imageEditorModal_detachGlobalInputChangeListener = function () {
    try {
        window._imageEditorGlobalListenerAttached = false;
    } catch (e) { console.error(e); }
};
