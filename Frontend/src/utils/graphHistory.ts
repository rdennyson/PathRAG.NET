import { Node, Edge } from 'reactflow';

export interface GraphData {
  nodes: Node[];
  edges: Edge[];
}

export interface SavedGraphHistory {
  searchValue: string;
  results: GraphData;
  timestamp?: string;
}

const STORAGE_KEY = 'pathrag-graph-history';

export const saveGraphHistory = (history: SavedGraphHistory[]): void => {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(history));
  } catch (error) {
    console.error('Failed to save graph history to localStorage:', error);
  }
};

export const loadGraphHistory = (): SavedGraphHistory[] => {
  try {
    const savedHistory = localStorage.getItem(STORAGE_KEY);
    return savedHistory ? JSON.parse(savedHistory) : [];
  } catch (error) {
    console.error('Failed to load graph history from localStorage:', error);
    return [];
  }
};

// Default example graph data
export const defaultGraphHistory: SavedGraphHistory[] = [
  {
    searchValue: 'Example Knowledge Graph',
    timestamp: new Date().toISOString(),
    results: {
      nodes: [
        {
          id: '1',
          data: { label: 'Knowledge Graph' },
          position: { x: 250, y: 100 },
          style: { background: '#e6f7ff', color: '#000000' }
        },
        {
          id: '2',
          data: { label: 'Entities' },
          position: { x: 100, y: 200 },
          style: { background: '#d9f7be', color: '#000000' }
        },
        {
          id: '3',
          data: { label: 'Relationships' },
          position: { x: 400, y: 200 },
          style: { background: '#fff1b8', color: '#000000' }
        },
        {
          id: '4',
          data: { label: 'Concepts' },
          position: { x: 250, y: 300 },
          style: { background: '#ffd6e7', color: '#000000' }
        }
      ],
      edges: [
        { id: 'e1-2', source: '1', target: '2', label: 'contains' },
        { id: 'e1-3', source: '1', target: '3', label: 'defines' },
        { id: 'e2-4', source: '2', target: '4', label: 'represents' },
        { id: 'e3-4', source: '3', target: '4', label: 'connects' }
      ]
    }
  }
];
