// This is a JavaScript module that is loaded on demand. It can export any number of
// functions, and may import other JavaScript modules if required.

export function showPrompt(message) {
  return prompt(message, 'Type anything here');
}

// Track a viewer instance per container to avoid leaking a global renderer
// Uses gcode-preview (https://github.com/remcoder/gcode-preview) — MIT license
const viewers = new WeakMap();

export async function initializeViewer(container, gcodeContent) {
    // Dispose any existing instance first
    disposeViewer(container);

    // Binary bgcode files start with the magic bytes "GCDE" — cannot be visualised
    if (gcodeContent.startsWith('GCDE')) {
        throw new Error('Binary G-code (bgcode) cannot be visualised. Use the text view.');
    }

    // Wait for the browser to compute layout so getBoundingClientRect returns real dimensions.
    // This is necessary when the container was just added to the DOM by a conditional render.
    await new Promise(resolve => requestAnimationFrame(resolve));

    const rect = container.getBoundingClientRect();
    if (!document.body.contains(container)) return; // container unmounted during await

    // Create a canvas inside the container div
    const canvas = document.createElement('canvas');
    canvas.width = Math.max(rect.width || 800, 100);
    canvas.height = Math.max(rect.height || 600, 100);
    canvas.style.display = 'block';
    canvas.style.width = '100%';
    canvas.style.height = '100%';
    container.appendChild(canvas);

    const preview = GCodePreview.init({
        canvas,
        buildVolume: { x: 220, y: 220, z: 250 },
        lineWidth: 2,
    });

    preview.processGCode(gcodeContent);
    viewers.set(container, preview);
}

export function disposeViewer(container) {
    const preview = viewers.get(container);
    if (!preview) return;

    viewers.delete(container);
    try { if (typeof preview.dispose === 'function') preview.dispose(); } catch { /* ignore */ }
    container.innerHTML = '';
}

// Scroll a container to the bottom; used by the command prompt history.
export function scrollToBottom(element) {
    if (!element) {
        return;
    }

    // Support both plain elements and Blazor's ElementReference wrappers.
    const target = element instanceof HTMLElement ? element : element.firstElementChild || element;
    if (!target) {
        return;
    }

    target.scrollTop = target.scrollHeight;
}

// Triggers a browser file download from an in-memory string.
export function downloadFile(filename, content) {
    const blob = new Blob([content], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}