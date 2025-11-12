//cruicial part for dragging
let isDragging = false;
let lastX = 0;
let lastY = 0;
let mouseMoved = false;

const dragLayer = document.getElementById('dragLayer');

function onMouseMove(e) {
    const dx = e.screenX - lastX;
    const dy = e.screenY - lastY;

    if (!mouseMoved && (Math.abs(dx) > 3 || Math.abs(dy) > 3)) {
        mouseMoved = true;
        isDragging = true;
        console.log('dragging started');
    }

    if (isDragging) {
        window.chrome.webview.postMessage({ type: 'drag', dx, dy });
        lastX = e.screenX;
        lastY = e.screenY;
    }
}

function onMouseUp() {
    if (!mouseMoved) {
        try {
            openInBrowser();
    
        }
        catch (error) {
            console.error('Error opening URL:', error);
        }
    }
    window.chrome.webview.postMessage({ type: 'drag_done' });
    isDragging = false;
    mouseMoved = false;

    document.removeEventListener('mousemove', onMouseMove);
    document.removeEventListener('mouseup', onMouseUp);
}

dragLayer.addEventListener('mousedown', (e) => {
    lastX = e.screenX;
    lastY = e.screenY;
    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup', onMouseUp);
});
