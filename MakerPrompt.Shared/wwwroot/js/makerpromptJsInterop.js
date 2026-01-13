// This is a JavaScript module that is loaded on demand. It can export any number of
// functions, and may import other JavaScript modules if required.

export function showPrompt(message) {
  return prompt(message, 'Type anything here');
}

// Track a viewer instance per container to avoid leaking a global renderer
const viewers = new WeakMap();

export function initializeViewer(container, gcodeContent) {
    const existing = viewers.get(container);
    if (existing) {
        existing.destroy();
        viewers.delete(container);
    }

    const TRANSPARENT_COLOR = new gcodeViewer.Color();

    const INNER_COLOR = new gcodeViewer.Color("#ff0000");
    const OUTER_COLOR = new gcodeViewer.Color("#00ff00");
    const SKIRT_COLOR = new gcodeViewer.Color("#ffff00");
    const FILL_COLOR = new gcodeViewer.Color("#ffff00");
    const BOTTOM_FILL_COLOR = new gcodeViewer.Color("#ffff00");
    const INTERNAL_FILL = new gcodeViewer.Color("#00ffff");
    const TOP_FILL = new gcodeViewer.Color("#ff00ff");
    const DEFAULT_COLOR = new gcodeViewer.Color("#0000ff");
    let colorConfig = [];

    gcodeContent.split("\n").forEach(function (line, i) {
        let color;
        if (line.startsWith(";TYPE:WALL-INNER")) {
            color = INNER_COLOR;
        } else if (line.startsWith(";TYPE:WALL-OUTER")) {
            color = OUTER_COLOR;
        } else if (line.startsWith(";TYPE:SKIRT")) {
            color = SKIRT_COLOR;
        } else if (line.startsWith(";TYPE:FILL")) {
            color = FILL_COLOR;
        } else if (line.startsWith(";TYPE:BOTTOM-FILL")) {
            color = BOTTOM_FILL_COLOR;
        } else if (line.startsWith(";TYPE:INTERNAL-FILL")) {
            color = INTERNAL_FILL;
        } else if (line.startsWith(";TYPE:TOP-FILL")) {
            color = TOP_FILL;
        }

        if (
            colorConfig.length === 0 ||
            (color && colorConfig[colorConfig.length - 1].color !== color)
        ) {
            colorConfig.push({ toLine: i, color });
        } else {
            colorConfig[colorConfig.length - 1].toLine = i;
        }
    });

    const rect = container.getBoundingClientRect();
    const width = rect.width || container.clientWidth || 800;
    const height = rect.height || container.clientHeight || 600;

    const renderer = new gcodeViewer.GCodeRenderer(
        gcodeContent,
        width,
        height,
        new gcodeViewer.Color(0x808080),
    );

    const element = renderer.element();
    element.style.width = "100%";
    element.style.height = "100%";
    element.style.maxWidth = "100%";
    element.style.maxHeight = "100%";

    container.append(element);

    renderer.render();
    viewers.set(container, renderer);
    return renderer;
}

export function disposeViewer(container) {
    const renderer = viewers.get(container);
    if (!renderer) {
        return;
    }

    try {
        renderer.destroy();
    } catch {
        // ignore viewer dispose errors
    }

    viewers.delete(container);
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