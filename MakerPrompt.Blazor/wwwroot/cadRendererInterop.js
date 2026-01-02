// CAD Renderer JavaScript Interop
// Bridges Blazor C# with the CAD Web Worker

window.cadRenderer = {
    worker: null,
    canvas: null,
    
    initialize: async function(canvasId) {
        console.log('Initializing CAD renderer for canvas:', canvasId);
        
        // Get canvas element
        this.canvas = document.getElementById(canvasId);
        if (!this.canvas) {
            console.error('Canvas not found:', canvasId);
            return false;
        }
        
        // Create and initialize worker
        try {
            this.worker = new Worker('makerpromptCadWorker.js');
            
            // Wait for initialization
            return new Promise((resolve) => {
                this.worker.addEventListener('message', function handler(e) {
                    if (e.data.type === 'initialized') {
                        this.removeEventListener('message', handler);
                        resolve(e.data.success);
                    }
                });
                
                // Send init message
                this.worker.postMessage({ type: 'init', data: { canvasId } });
            });
        } catch (error) {
            console.error('Failed to create CAD worker:', error);
            return false;
        }
    },
    
    render: function(sceneSnapshot) {
        if (!this.worker) {
            console.error('CAD worker not initialized');
            return;
        }
        
        this.worker.postMessage({ 
            type: 'render', 
            data: sceneSnapshot 
        });
    },
    
    setCamera: function(cameraState) {
        if (!this.worker) {
            console.error('CAD worker not initialized');
            return;
        }
        
        this.worker.postMessage({ 
            type: 'setCamera', 
            data: cameraState 
        });
    },
    
    dispose: function() {
        if (this.worker) {
            this.worker.postMessage({ type: 'dispose' });
            this.worker.terminate();
            this.worker = null;
        }
        this.canvas = null;
    }
};
