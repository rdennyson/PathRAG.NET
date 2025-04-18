import axios, { AxiosInstance, AxiosRequestConfig } from 'axios';
import { Assistant, ChatSession, Document, GraphEntity, QueryRequest, QueryResponse, Relationship, VectorStore } from '../models/types';

// Define token response interface
export interface TokenResponse {
  accessToken: string;
  idToken: string;
  refreshToken: string;
  tokenType: string;
  expiresIn: number;
}

// Define the API service interface
export interface ApiService {
  // Authentication
  getCurrentUser: () => Promise<any>;
  exchangeCodeForToken: (code: string, state: string) => Promise<TokenResponse>;
  logout: () => Promise<void>;

  // Assistants
  getAssistants: () => Promise<Assistant[]>;
  getAssistant: (id: string) => Promise<Assistant>;
  createAssistant: (assistant: Omit<Assistant, 'id'>) => Promise<Assistant>;
  updateAssistant: (id: string, assistant: Partial<Assistant>) => Promise<Assistant>;
  deleteAssistant: (id: string) => Promise<void>;

  // Vector Stores
  getVectorStores: () => Promise<VectorStore[]>;
  getVectorStore: (id: string) => Promise<VectorStore>;
  createVectorStore: (name: string) => Promise<VectorStore>;
  deleteVectorStore: (id: string) => Promise<void>;

  // Documents
  getDocuments: (vectorStoreId: string) => Promise<Document[]>;
  uploadDocument: (vectorStoreId: string, file: File) => Promise<Document>;
  deleteDocument: (vectorStoreId: string, documentId: string) => Promise<void>;

  // Chat Sessions
  getChatSessions: () => Promise<ChatSession[]>;
  getChatSession: (id: string) => Promise<ChatSession>;
  createChatSession: (assistantId: string) => Promise<ChatSession>;
  deleteChatSession: (id: string) => Promise<void>;
  addChatMessage: (chatSessionId: string, content: string, role: string, attachments: File[]) => Promise<any>;

  // Queries
  query: (queryRequest: QueryRequest) => Promise<QueryResponse>;
  queryStream: (queryRequest: QueryRequest, onChunk: (chunk: string) => void, signal?: AbortSignal) => Promise<void>;

  // Knowledge Graph
  getEntities: (vectorStoreId: string) => Promise<GraphEntity[]>;
  getRelationships: (vectorStoreId: string) => Promise<Relationship[]>;
  generateKnowledgeGraph: (query: string, maxNodes?: number, vectorStoreId?: string) => Promise<any>;
  getEntityTextChunks: (entityId: string) => Promise<any[]>;
}

// Create axios instance with base URL from environment variables
const apiClient: AxiosInstance = axios.create({
  baseURL: `${import.meta.env.VITE_API_BASE_URL}/api` || 'http://localhost:3000/api',
  headers: {
    'Content-Type': 'application/json',
  },
});

// Cache for API responses
const apiCache: Record<string, { data: any, timestamp: number }> = {};
const CACHE_DURATION = 10000; // 10 seconds cache duration

// Request tracking to prevent duplicate in-flight requests
const pendingRequests: Record<string, Promise<any>> = {};

// Add request interceptor for logging and caching
apiClient.interceptors.request.use(
  (config) => {
    // Create a cache key from the request
    const cacheKey = `${config.method}-${config.url}`;

    // Log the request with a timestamp
    console.log(`API Request (${new Date().toISOString()}): ${config.method?.toUpperCase()} ${config.url}`);

    // Add cache key to config for later use
    config.headers = config.headers || {};
    config.headers['X-Cache-Key'] = cacheKey;

    return config;
  },
  (error) => Promise.reject(error)
);

// API service implementation with mock data for testing
const apiService: ApiService = {

  getCurrentUser: async () => {
    const response = await apiClient.get('/user');
    return response.data;
  },

  exchangeCodeForToken: async (code: string, state: string): Promise<TokenResponse> => {
    const response = await apiClient.post<TokenResponse>('/auth/callback', { code, state });
    return response.data;
  },

  logout: async (): Promise<void> => {
    await apiClient.post('/auth/logout');
  },

  getAssistants: async () => {
    const response = await apiClient.get<Assistant[]>('/assistants');
    return response.data;
  },

  getAssistant: async (id: string) => {
    const response = await apiClient.get<Assistant>(`/assistants/${id}`);
    return response.data;
  },

  createAssistant: async (assistant: Omit<Assistant, 'id'>): Promise<Assistant> => {
    const response = await apiClient.post('/assistants', assistant);
    return response.data;
  },

  updateAssistant: async (id: string, assistant: Partial<Assistant>): Promise<Assistant> => {
    const response = await apiClient.put(`/assistants/${id}`, assistant);
    return response.data;
  },

  deleteAssistant: async (id: string): Promise<void> => {
    await apiClient.delete(`/assistants/${id}`);
  },

  // Vector Stores
  getVectorStores: async (): Promise<VectorStore[]> => {
    const response = await apiClient.get('/vectorstores');
    return response.data;
  },

  getVectorStore: async (id: string): Promise<VectorStore> => {
    const response = await apiClient.get(`/vectorstores/${id}`);
    return response.data;
  },

  createVectorStore: async (name: string): Promise<VectorStore> => {
    const response = await apiClient.post('/vectorstores', { name });
    return response.data;
  },

  deleteVectorStore: async (id: string): Promise<void> => {
    await apiClient.delete(`/vectorstores/${id}`);
  },

  // Documents
  getDocuments: async (vectorStoreId: string): Promise<Document[]> => {
    const response = await apiClient.get(`/vectorstores/${vectorStoreId}/documents`);
    return response.data;
  },

  uploadDocument: async (vectorStoreId: string, file: File): Promise<Document> => {
    const formData = new FormData();
    formData.append('file', file);

    const config: AxiosRequestConfig = {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    };

    const response = await apiClient.post(`/vectorstores/${vectorStoreId}/documents`, formData, config);
    return response.data;
  },

  deleteDocument: async (vectorStoreId: string, documentId: string): Promise<void> => {
    await apiClient.delete(`/vectorstores/${vectorStoreId}/documents/${documentId}`);
  },

  // Chat Sessions with caching
  getChatSessions: async (): Promise<ChatSession[]> => {
    const cacheKey = 'get-/chats';

    // Check if we have a cached response that's still valid
    if (apiCache[cacheKey] && (Date.now() - apiCache[cacheKey].timestamp < CACHE_DURATION)) {
      console.log(`Using cached response for ${cacheKey}`);
      return apiCache[cacheKey].data;
    }

    // Check if there's already a pending request for this endpoint
    if (pendingRequests[cacheKey] !== undefined) {
      console.log(`Using pending request for ${cacheKey}`);
      return pendingRequests[cacheKey];
    }

    // Create a new request and store it in pendingRequests
    const requestPromise = apiClient.get('/chats')
      .then(response => {
        // Cache the response
        apiCache[cacheKey] = {
          data: response.data,
          timestamp: Date.now()
        };
        // Remove from pending requests
        delete pendingRequests[cacheKey];
        return response.data;
      })
      .catch(error => {
        // Remove from pending requests on error
        delete pendingRequests[cacheKey];
        throw error;
      });

    // Store the promise in pendingRequests
    pendingRequests[cacheKey] = requestPromise;
    return requestPromise;
  },

  getChatSession: async (id: string): Promise<ChatSession> => {
    const cacheKey = `get-/chats/${id}`;

    // Check if we have a cached response that's still valid
    if (apiCache[cacheKey] && (Date.now() - apiCache[cacheKey].timestamp < CACHE_DURATION)) {
      console.log(`Using cached response for ${cacheKey}`);
      return apiCache[cacheKey].data;
    }

    // Check if there's already a pending request for this endpoint
    if (pendingRequests[cacheKey] !== undefined) {
      console.log(`Using pending request for ${cacheKey}`);
      return pendingRequests[cacheKey];
    }

    // Create a new request and store it in pendingRequests
    const requestPromise = apiClient.get(`/chats/${id}`)
      .then(response => {
        // Cache the response
        apiCache[cacheKey] = {
          data: response.data,
          timestamp: Date.now()
        };
        // Remove from pending requests
        delete pendingRequests[cacheKey];
        return response.data;
      })
      .catch(error => {
        // Remove from pending requests on error
        delete pendingRequests[cacheKey];
        throw error;
      });

    // Store the promise in pendingRequests
    pendingRequests[cacheKey] = requestPromise;
    return requestPromise;
  },

  createChatSession: async (assistantId: string): Promise<ChatSession> => {
    const response = await apiClient.post('/chats', { assistantId });
    return response.data;
  },

  deleteChatSession: async (id: string): Promise<void> => {
    await apiClient.delete(`/chats/${id}`);
  },

  addChatMessage: async (chatSessionId: string, content: string, role: string, attachments: File[]): Promise<any> => {
    const formData = new FormData();
    formData.append('content', content);
    formData.append('role', role);

    // Add attachments if any
    attachments.forEach(file => {
      formData.append('attachments', file);
    });

    const config: AxiosRequestConfig = {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    };

    const response = await apiClient.post(`/chats/${chatSessionId}/messages`, formData, config);
    return response.data;
  },

  // Queries
  query: async (queryRequest: QueryRequest): Promise<QueryResponse> => {
    const response = await apiClient.post('/query', queryRequest);
    return response.data;
  },

  // Streaming query
  queryStream: async (queryRequest: QueryRequest, onChunk: (chunk: string) => void, signal?: AbortSignal): Promise<void> => {
    try {
      let previousText = '';
      await apiClient.post('/query/stream', queryRequest, {
        responseType: 'text',
        signal,
        onDownloadProgress: (progressEvent) => {
          const responseText = progressEvent.event.target.responseText as string;
          if (responseText && responseText !== previousText) {
            // Get only the new text since the last update
            const newText = responseText.substring(previousText.length);
            if (newText) {
              onChunk(newText);
              previousText = responseText;
            }
          }
        }
      });
    } catch (error: unknown) {
      if (error instanceof Error && error.name === 'CanceledError') {
        // Request was canceled, do nothing
      } else {
        throw error;
      }
    }
  },

  // Knowledge Graph
  getEntities: async (vectorStoreId: string): Promise<GraphEntity[]> => {
    const response = await apiClient.get(`/knowledgegraph/entities/${vectorStoreId}`);
    return response.data;
  },

  getRelationships: async (vectorStoreId: string): Promise<Relationship[]> => {
    const response = await apiClient.get(`/knowledgegraph/relationships/${vectorStoreId}`);
    return response.data;
  },

  generateKnowledgeGraph: async (query: string, maxNodes?: number, vectorStoreId?: string): Promise<any> => {
    const response = await apiClient.post(`/knowledgegraph/generate`, {
      query,
      maxNodes,
      vectorStoreId,
    });
    return response.data;
  },

  getEntityTextChunks: async (entityId: string): Promise<any[]> => {
    const response = await apiClient.get(`/knowledgegraph/entity/${entityId}/textchunks`);
    return response.data;
  },
};

export default apiService;



