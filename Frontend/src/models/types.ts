// User model
export interface User {
  id: string;
  name: string;
  email: string;
}

// Assistant model
export interface Assistant {
  id: string;
  name: string;
  message: string;
  temperature: number;
  vectorStoreIds: string[];
}

// Vector Store model
export interface VectorStore {
  id: string;
  name: string;
  documentCount: number;
  createdAt: string;
}

// Document model
export interface Document {
  id: string;
  name: string;
  size: number;
  type: string;
  vectorStoreId: string;
  uploadedAt: string;
}

// Chat message model
export interface ChatMessage {
  id: string;
  content: string;
  role: 'user' | 'assistant';
  timestamp: string;
  attachments?: string[];
}

// Chat session model
export interface ChatSession {
  id: string;
  title: string;
  messages: ChatMessage[];
  assistantId: string;
  createdAt: string;
  updatedAt: string;
}

// Knowledge Graph Entity model
export interface GraphEntity {
  id: string;
  name: string;
  type: string;
  description: string;
  vectorStoreId: string;
}

// Knowledge Graph Relationship model
export interface Relationship {
  id: string;
  sourceEntityId: string;
  targetEntityId: string;
  type: string;
  description: string;
  vectorStoreId: string;
}

// Search mode enum
export enum SearchMode {
  Semantic = 'semantic',
  Hybrid = 'hybrid',
  Graph = 'graph'
}

// Query request model
export interface QueryRequest {
  query: string;
  vectorStoreIds: string[];
  searchMode: SearchMode;
  assistantId: string;
}

// Query response model
export interface QueryResponse {
  answer: string;
  sources: string[];
  entities?: GraphEntity[];
  relationships?: Relationship[];
}
