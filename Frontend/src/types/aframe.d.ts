/**
 * Type declarations for A-Frame
 */
declare module 'aframe' {
  // Since we're just using this as a global module that registers the AFRAME global,
  // we don't need to define specific exports
  const _default: any;
  export default _default;
}

// Define the global AFRAME object
interface Window {
  AFRAME: {
    registerComponent: (name: string, definition: any) => void;
    registerSystem: (name: string, definition: any) => void;
    registerShader: (name: string, definition: any) => void;
    registerGeometry: (name: string, definition: any) => void;
    registerPrimitive: (name: string, definition: any) => void;
    // Add other AFRAME methods as needed
    [key: string]: any;
  };
}
