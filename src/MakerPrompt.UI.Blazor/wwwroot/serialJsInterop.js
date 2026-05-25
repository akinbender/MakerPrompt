// Individual exported functions following Microsoft's pattern
export async function checkSupported() {
    return navigator.serial != undefined;
}

export async function requestPort() {
    try {
        const port = await navigator.serial.requestPort();
        console.debug('[WebSerial] Port requested:', port);
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
    console.debug('[WebSerial] Granted ports:', ports);
    return ports.map(port => ({
        name: port.name || 'Unknown',
        manufacturer: getManufacturerInfo(port)
    }));
}

export async function openPort(options, dotNetRef) {
    let port;
    try {
        console.debug('[WebSerial] Opening port with options:', options);
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
            console.debug('[WebSerial] Port opened.');
        } else {
            console.debug('[WebSerial] Port was already open, reusing existing streams.');
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
        if (!port || !port.writable) {
            console.warn('[WebSerial] writeData called with no writable port.');
            return;
        }

        writer = port.writable.getWriter();
        const encoder = new TextEncoder();
        const text = data.endsWith('\n') ? data : data + '\n';
        console.debug('[WebSerial] Writing data:', text);
        await writer.write(encoder.encode(text));
    } catch (error) {
        console.error('[WebSerial] Error while writing data:', error);
    } finally {
        try {
            writer?.releaseLock();
        } catch {
            // ignore release errors
        }
    }
}

export async function closePort(port) {
    await safeClosePort(port);
}

// Track active readers per port to avoid locking the same stream multiple times
const activeReaders = new WeakMap();

// Helper functions
function getManufacturerInfo(port) {
    const info = port.getInfo();
    if (info.usbVendorId && info.usbProductId) {
        return `USB ${info.usbVendorId}:${info.usbProductId}`;
    }
    return 'Generic Serial';
}

async function startReading(port, dotNetRef) {
    if (!port.readable) {
        console.warn('[WebSerial] startReading called but port.readable is null.');
        return;
    }

    // If we already have a reader for this port, do not create another
    if (activeReaders.has(port)) {
        console.debug('[WebSerial] Reader already active for port, skipping startReading.');
        return;
    }

    console.debug('[WebSerial] Starting reader for port.');
    const reader = port.readable.getReader();
    activeReaders.set(port, reader);

    try {
        while (true) {
            const { value, done } = await reader.read();
            if (done) {
                console.debug('[WebSerial] Reader loop done.');
                break;
            }

            if (value) {
                const text = new TextDecoder().decode(value);
                console.debug('[WebSerial] Data received:', text);
                dotNetRef.invokeMethodAsync('OnDataReceived', text);
            }
        }
    } catch (error) {
        // Framing errors and similar serial exceptions are expected on some devices.
        console.error('[WebSerial] Read error:', error);
        try {
            await dotNetRef.invokeMethodAsync('OnConnectionChanged', false);
        } catch {
            // Swallow interop errors so we do not crash the app on shutdown.
        }
    } finally {
        try {
            reader.releaseLock();
        } catch {
            // ignore release errors
        }
        activeReaders.delete(port);
        console.debug('[WebSerial] Reader released and removed for port.');
    }
}

async function safeClosePort(port) {
    try {
        console.debug('[WebSerial] Closing port.');
        const reader = activeReaders.get(port);
        if (reader) {
            try {
                await reader.cancel();
            } catch {
                // ignore cancel errors
            }
            try {
                reader.releaseLock();
            } catch {
                // ignore release errors
            }
            activeReaders.delete(port);
            console.debug('[WebSerial] Active reader cancelled and released during close.');
        }

        // If there is no readable or writable, the port is already closed.
        if (!port.readable && !port.writable) {
            console.debug('[WebSerial] Port already closed.');
            return;
        }

        await port.close();
        console.debug('[WebSerial] Port closed.');
    } catch (error) {
        // Some browsers throw if the stream is locked when closing; just log and move on.
        console.error('[WebSerial] Error closing port:', error);
    }
}