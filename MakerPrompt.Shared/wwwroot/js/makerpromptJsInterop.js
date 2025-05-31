// This is a JavaScript module that is loaded on demand. It can export any number of
// functions, and may import other JavaScript modules if required.

export function showPrompt(message) {
  return prompt(message, 'Type anything here');
}

let renderer = null;

export function initializeViewer(container, gcodeContent) {
    // Destroy existing viewer if any
    if (renderer) {
        renderer.destroy();
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
            color && colorConfig[colorConfig.length - 1].color !== color
        ) {
            colorConfig.push({ toLine: i, color });
        } else {
            colorConfig[colorConfig.length - 1].toLine = i;
        }
    });

    renderer = new gcodeViewer.GCodeRenderer(
        gcodeContent,
        800,
        600,
        new gcodeViewer.Color(0x808080),
    );

    container.append(
        renderer.element(),
    );

    renderer.render();
    return renderer;
}