// Individual exported functions following Microsoft's pattern
export async function checkSupported() {
    return navigator.serial != undefined;
}

export async function requestPort() {
    try {
        const port = await navigator.serial.requestPort();
        return {
            name: port.name || 'Unknown',
            manufacturer: getManufacturerInfo(port)
        };
    } catch (error) {
        // User can cancel the chooser dialog; treat that as a benign case.
        if (error && error.name === 'NotFoundError') {
            console.warn('Serial port selection canceled by user.');
            return null;
        }

        console.error('Port request failed:', error);
        throw error;
    }
}

export async function getGrantedPorts() {
    const ports = await navigator.serial.getPorts();
    return ports.map(port => ({
        name: port.name || 'Unknown',
        manufacturer: getManufacturerInfo(port)
    }));
}

export async function openPort(options, dotNetRef) {
    let port;
    try {
        port = await navigator.serial.requestPort();

        // If the port is already open, avoid reopening and just start reading.
        if (!port.readable && !port.writable) {
            await port.open({
                baudRate: options.baudRate,
                dataBits: options.dataBits,
                stopBits: options.stopBits,
                parity: options.parity,
                flowControl: options.flowControl
            });
        }

        startReading(port, dotNetRef);
        return port;
    } catch (error) {
        // User canceled the dialog - do not treat this as a fatal error.
        if (error && error.name === 'NotFoundError') {
            console.warn('Serial port open canceled by user.');
            return null;
        }

        console.error('Error opening port:', error);

        // Only attempt a close if the port exists and is actually open.
        if (port && (port.readable || port.writable)) {
            await safeClosePort(port);
        }

        throw error;
    }
}

export async function writeData(port, data) {
    let writer;
    try {
        writer = port.writable.getWriter();
        const encoder = new TextEncoder();
        await writer.write(encoder.encode(data));
    } finally {
        writer?.releaseLock();
    }
}

export async function closePort(port) {
    await safeClosePort(port);
}

// Helper functions
function getManufacturerInfo(port) {
    const info = port.getInfo();
    if (info.usbVendorId && info.usbProductId) {
        return `USB ${info.usbVendorId}:${info.usbProductId}`;
    }
    return 'Generic Serial';
}

async function startReading(port, dotNetRef) {
    let reader;
    try {
        while (port.readable) {
            reader = port.readable.getReader();
            try {
                while (true) {
                    const { value, done } = await reader.read();
                    if (done) break;

                    const text = new TextDecoder().decode(value);
                    dotNetRef.invokeMethodAsync('OnDataReceived', text);
                }
            } finally {
                reader.releaseLock();
            }
        }
    } catch (error) {
        // Framing errors and similar serial exceptions are expected on some devices.
        console.error('Read error:', error);
        try {
            await dotNetRef.invokeMethodAsync('OnConnectionChanged', false);
        } catch {
            // Swallow interop errors so we do not crash the app on shutdown.
        }
    }
}

async function safeClosePort(port) {
    try {
        // If there is an active reader, wait for it to finish by canceling it via closing.
        if (!port.readable && !port.writable) {
            return; // already closed
        }

        await port.close();
    } catch (error) {
        // Some browsers throw if the stream is locked when closing; just log and move on.
        console.error('Error closing port:', error);
    }
}