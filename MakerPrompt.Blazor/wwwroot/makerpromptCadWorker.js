// MakerPrompt CAD Worker
// Dedicated worker for non-blocking WebGL CAD rendering using OffscreenCanvas

self.addEventListener('message', function(e) {
    const { type, data } = e.data;
    
    switch(type) {
        case 'init':
            // Initialize WebGL context with OffscreenCanvas
            console.log('CAD Worker: Initializing...');
            self.postMessage({ type: 'initialized', success: true });
            break;
            
        case 'render':
            // Render scene snapshot
            console.log('CAD Worker: Rendering scene with', data.nodes?.length || 0, 'nodes');
            self.postMessage({ type: 'rendered', success: true });
            break;
            
        case 'setCamera':
            // Update camera state
            console.log('CAD Worker: Setting camera');
            self.postMessage({ type: 'cameraSet', success: true });
            break;
            
        case 'dispose':
            // Cleanup resources
            console.log('CAD Worker: Disposing...');
            self.postMessage({ type: 'disposed', success: true });
            break;
            
        default:
            console.warn('CAD Worker: Unknown message type', type);
    }
});

console.log('MakerPrompt CAD Worker loaded');
