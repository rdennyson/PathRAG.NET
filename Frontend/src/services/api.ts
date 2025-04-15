import axios, { AxiosInstance, AxiosRequestConfig } from 'axios';
import { Assistant, ChatSession, Document, GraphEntity, QueryRequest, QueryResponse, Relationship, VectorStore } from '../models/types';

// Create axios instance with base URL from environment variables
const apiClient: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5001/api',
  headers: {
    'Content-Type': 'application/json',
  },
});

// Add request interceptor to add auth token
apiClient.interceptors.request.use(
  (config) => {
    const token = sessionStorage.getItem('msal.idtoken');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// API service
export const apiService = {
  // Authentication
  getCurrentUser: async () => {
    const response = await apiClient.get('/user');
    return response.data;
  },

  // Assistants
  getAssistants: async (): Promise<Assistant[]> => {
    const response = await apiClient.get('/assistants');
    return response.data;
  },

  getAssistant: async (id: string): Promise<Assistant> => {
    const response = await apiClient.get(`/assistants/${id}`);
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

  // Chat Sessions
  getChatSessions: async (): Promise<ChatSession[]> => {
    const response = await apiClient.get('/chats');
    return response.data;
  },

  getChatSession: async (id: string): Promise<ChatSession> => {
    const response = await apiClient.get(`/chats/${id}`);
    return response.data;
  },

  createChatSession: async (assistantId: string): Promise<ChatSession> => {
    const response = await apiClient.post('/chats', { assistantId });
    return response.data;
  },

  deleteChatSession: async (id: string): Promise<void> => {
    await apiClient.delete(`/chats/${id}`);
  },

  // Queries
  query: async (queryRequest: QueryRequest): Promise<QueryResponse> => {
    const response = await apiClient.post('/query', queryRequest);
    return response.data;
  },

  // Knowledge Graph
  getEntities: async (vectorStoreId: string): Promise<GraphEntity[]> => {
    const response = await apiClient.get(`/vectorstores/${vectorStoreId}/entities`);
    return response.data;
  },

  getRelationships: async (vectorStoreId: string): Promise<Relationship[]> => {
    const response = await apiClient.get(`/vectorstores/${vectorStoreId}/relationships`);
    return response.data;
  },
};

export default apiService;
