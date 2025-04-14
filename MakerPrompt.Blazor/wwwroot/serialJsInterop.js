// Individual exported functions following Microsoft's pattern
export async function checkSupported() {
    if ('serial' in navigator) {
        return true;
    }
    else {
        return false;
    }
}

export async function requestPort() {
    try {
        const port = await navigator.serial.requestPort();
        return {
            name: port.name || 'Unknown',
            manufacturer: getManufacturerInfo(port)
        };
    } catch (error) {
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
        await port.open({
            baudRate: options.baudRate,
            dataBits: options.dataBits,
            stopBits: options.stopBits,
            parity: options.parity,
            flowControl: options.flowControl
        });

        startReading(port, dotNetRef);
        return true;
    } catch (error) {
        console.error('Error opening port:', error);
        if (port) await safeClosePort(port);
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
        console.error('Read error:', error);
        dotNetRef.invokeMethodAsync('OnConnectionChanged', false);
    }
}

async function safeClosePort(port) {
    try {
        await port.close();
    } catch (error) {
        console.error('Error closing port:', error);
    }
}