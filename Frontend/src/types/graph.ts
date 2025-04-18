export interface TextChunk {
  id: string;
  content: string;
  fullDocumentId?: string;
  vectorStoreId?: string;
  createdAt?: string;
  tokenCount?: number;
}

export interface GraphEntity {
  id: string;
  name: string;
  type: string;
  description: string;
  weight?: number;
  vectorStoreId?: string;
  createdAt?: string;
  keywords?: string[];
  textChunks?: TextChunk[];
}

export interface Relationship {
  id: string;
  sourceEntityId: string;
  targetEntityId: string;
  type: string;
  description: string;
  weight?: number;
  vectorStoreId?: string;
  createdAt?: string;
}

export interface KnowledgeGraphNode {
  id: string;
  label: string;
  type?: string;
  background?: string;
  stroke?: string;
  x: number;
  y: number;
  adjacencies: KnowledgeGraphEdge[];
  description?: string;
}

export interface KnowledgeGraphEdge {
  id: string;
  source: string;
  target: string;
  label: string;
  color?: string;
  description?: string;
}

export interface SavedGraphHistory {
  searchValue: string;
  timestamp?: string;
  results: {
    nodes: any[];
    edges: any[];
  };
}
