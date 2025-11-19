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
        // Trigger a window resize event to force canvas repaint in Syncfusion editor
        window.dispatchEvent(new Event('resize'));
        // optional micro task to force reflow
        setTimeout(function () { window.dispatchEvent(new Event('resize')); }, 50);
    } catch (e) {
        console.error(e);
    }
};

window.imageEditorModal_getCanvasRect = function (modalId) {
    try {
        var modal = document.getElementById(modalId);
        if (!modal) return null;
        // syncfusion canvas usually lives under a class like .e-image-editor .e-svg-canvas or .e-image-editor-canvas
        var canvas = modal.querySelector('.e-image-editor .e-svg-canvas, .e-image-editor-canvas, .e-image-editor .e-image-editor-canvas');
        if (!canvas) {
            // fallback to any canvas inside editor
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
