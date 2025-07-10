const DRAG_THRESHOLD = 5;
// const CLICK_TIMEOUT = 200; // Milliseconds to wait before assuming not a click

function makeDraggable(element, onDragEndCallback, onClickCallback) {
    let isDragging = false;
    let dragInitiated = false;
    let startX;
    let startY;
    let initialLeft;
    let initialTop;
    // let clickTimeout; // Removed timeout variable

    element.addEventListener('mousedown', dragStart);

    function dragStart(e) {
        startX = e.clientX;
        startY = e.clientY;
        initialLeft = parseInt(element.style.left) || 0;
        initialTop = parseInt(element.style.top) || 0;
        isDragging = false;
        dragInitiated = false;

        // Removed timeout logic
        // clickTimeout = setTimeout(() => { ... });

        document.addEventListener('mousemove', drag);
        document.addEventListener('mouseup', dragEnd);
    }

    function drag(e) {
        const deltaX = Math.abs(e.clientX - startX);
        const deltaY = Math.abs(e.clientY - startY);

        if (!isDragging && (deltaX > DRAG_THRESHOLD || deltaY > DRAG_THRESHOLD)) {
            // Drag initiated
            isDragging = true;
            dragInitiated = true; // Drag initiated
            // clearTimeout(clickTimeout); // Removed timeout clear
             // Prevent default browser drag behavior
             e.preventDefault();
             e.stopPropagation(); // Stop propagation on drag start
        }

        if (isDragging) {
            // Continue preventing default behavior during drag
            e.preventDefault();
            e.stopPropagation(); // Stop propagation during drag
            const newLeft = initialLeft + (e.clientX - startX);
            const newTop = initialTop + (e.clientY - startY);
            element.style.left = `${newLeft}px`;
            element.style.top = `${newTop}px`;
        }
    }

    function dragEnd(e) {
        // Check immediately if a drag was initiated
        if (dragInitiated) {
            // It was a drag, call the drag end callback
            const finalLeft = parseInt(element.style.left);
            const finalTop = parseInt(element.style.top);
            if (onDragEndCallback) {
                onDragEndCallback(element.id, finalLeft, finalTop);
            }
            // Prevent default to avoid triggering a click after drag
            e.preventDefault();
            e.stopPropagation();
        } else { // If dragInitiated is false, it means it was a click or very minimal movement
             // It was a click, call the click callback
             if (onClickCallback) {
                 onClickCallback(element.id);
             }
             // Do NOT preventDefault or stopPropagation here for clicks to allow natural event flow
        }

        isDragging = false;
        dragInitiated = false; // Reset flags
        document.removeEventListener('mousemove', drag);
        document.removeEventListener('mouseup', dragEnd);
    }
} 