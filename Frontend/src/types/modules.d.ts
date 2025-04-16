/**
 * Global module declarations for modules without type definitions
 */

// This allows importing any module without TypeScript errors
declare module '*' {
  const value: any;
  export default value;
  export * from value;
}
