//cruicial part for dragging
let isDragging = false;
let lastX = 0;
let lastY = 0;

document.getElementById('dragLayer').addEventListener('mousedown', (e) => {
    isDragging = true;
    lastX = e.screenX;
    lastY = e.screenY;
});

document.addEventListener('mousemove', (e) => {
    if (!isDragging) return;
    const dx = e.screenX - lastX;
    const dy = e.screenY - lastY;
    window.chrome.webview.postMessage({ type: 'drag', dx, dy });
    lastX = e.screenX;
    lastY = e.screenY;
});

document.addEventListener('mouseup', () => {
    isDragging = false;
});