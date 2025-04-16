import React, { createContext, useContext, useState, useEffect, ReactNode, useRef, useCallback } from 'react';
import { Assistant, ChatSession, VectorStore } from '../models/types';
import apiService from '../services/api';
import { useAuth } from './AuthContext';

interface AppContextType {
  assistants: Assistant[];
  vectorStores: VectorStore[];
  chatSessions: ChatSession[];
  currentChatSession: ChatSession | null;
  currentAssistant: Assistant | null;
  isLoading: boolean;
  error: string | null;
  fetchAssistants: () => Promise<void>;
  fetchVectorStores: () => Promise<void>;
  fetchChatSessions: () => Promise<void>;
  setCurrentChatSession: (session: ChatSession | null) => void;
  setCurrentAssistant: (assistant: Assistant | null) => void;
  setChatSessions: (sessions: ChatSession[] | ((prev: ChatSession[]) => ChatSession[])) => void;
  createAssistant: (assistant: Omit<Assistant, 'id'>) => Promise<Assistant>;
  updateAssistant: (id: string, assistant: Partial<Assistant>) => Promise<Assistant>;
  deleteAssistant: (id: string) => Promise<void>;
  createVectorStore: (name: string) => Promise<VectorStore>;
  deleteVectorStore: (id: string) => Promise<void>;
  createChatSession: (assistantId: string) => Promise<ChatSession>;
  deleteChatSession: (id: string) => Promise<void>;
}

const AppContext = createContext<AppContextType | undefined>(undefined);

export const AppProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const { isAuthenticated } = useAuth();
  const [assistants, setAssistants] = useState<Assistant[]>([]);
  const [vectorStores, setVectorStores] = useState<VectorStore[]>([]);
  const [chatSessions, setChatSessions] = useState<ChatSession[]>([]);
  const [currentChatSession, setCurrentChatSession] = useState<ChatSession | null>(null);
  const [currentAssistant, setCurrentAssistant] = useState<Assistant | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  // Define fetch functions with useCallback
  const fetchAssistants = useCallback(async () => {
    if (!isAuthenticated) return;

    setIsLoading(true);
    setError(null);

    try {
      const data = await apiService.getAssistants();
      setAssistants(data);

      // Set current assistant if none is selected
      if (!currentAssistant && data.length > 0) {
        setCurrentAssistant(data[0]);
      }
    } catch (err) {
      setError('Failed to fetch assistants');
      console.error(err);
    } finally {
      setIsLoading(false);
    }
  }, [isAuthenticated, currentAssistant, setCurrentAssistant]);

  const fetchVectorStores = useCallback(async () => {
    if (!isAuthenticated) return;

    setIsLoading(true);
    setError(null);

    try {
      const data = await apiService.getVectorStores();
      setVectorStores(data);
    } catch (err) {
      setError('Failed to fetch vector stores');
      console.error(err);
    } finally {
      setIsLoading(false);
    }
  }, [isAuthenticated]);

  // Fetch data when authenticated
  useEffect(() => {
    if (isAuthenticated) {
      // Only fetch assistants and vector stores automatically
      // Chat sessions will be fetched manually to prevent excessive API calls
      fetchAssistants();
      fetchVectorStores();

      // Log that we're not automatically fetching chat sessions
      console.log('Automatic fetching of chat sessions is disabled to prevent excessive API calls');
    }
  }, [isAuthenticated, fetchAssistants, fetchVectorStores]);

  // Use a ref to track if we've already fetched chat sessions
  const chatSessionsFetched = useRef(false);

  // Use a ref to track the last fetch time to prevent duplicate calls
  const lastFetchTime = useRef(0);

  const fetchChatSessions = useCallback(async () => {
    if (!isAuthenticated) return;

    // Prevent duplicate calls within 1 second
    const now = Date.now();
    if (now - lastFetchTime.current < 1000) {
      console.log('Skipping duplicate fetchChatSessions call');
      return;
    }

    lastFetchTime.current = now;
    setIsLoading(true);
    setError(null);

    try {
      const data = await apiService.getChatSessions();
      setChatSessions(data);
      chatSessionsFetched.current = true;

      // Set current chat session if none is selected
      if (!currentChatSession && data.length > 0) {
        setCurrentChatSession(data[0]);
      }
    } catch (err) {
      setError('Failed to fetch chat sessions');
      console.error(err);
    } finally {
      setIsLoading(false);
    }
  }, [isAuthenticated, currentChatSession, setCurrentChatSession]);

  const createAssistant = async (assistant: Omit<Assistant, 'id'>) => {
    setIsLoading(true);
    setError(null);

    try {
      const newAssistant = await apiService.createAssistant(assistant);
      setAssistants([...assistants, newAssistant]);
      return newAssistant;
    } catch (err) {
      setError('Failed to create assistant');
      console.error(err);
      throw err;
    } finally {
      setIsLoading(false);
    }
  };

  const updateAssistant = async (id: string, assistant: Partial<Assistant>) => {
    setIsLoading(true);
    setError(null);

    try {
      const updatedAssistant = await apiService.updateAssistant(id, assistant);
      setAssistants(assistants.map(a => a.id === id ? updatedAssistant : a));

      // Update current assistant if it's the one being updated
      if (currentAssistant?.id === id) {
        setCurrentAssistant(updatedAssistant);
      }

      return updatedAssistant;
    } catch (err) {
      setError('Failed to update assistant');
      console.error(err);
      throw err;
    } finally {
      setIsLoading(false);
    }
  };

  const deleteAssistant = async (id: string) => {
    setIsLoading(true);
    setError(null);

    try {
      await apiService.deleteAssistant(id);
      setAssistants(assistants.filter(a => a.id !== id));

      // Clear current assistant if it's the one being deleted
      if (currentAssistant?.id === id) {
        setCurrentAssistant(assistants.length > 1 ? assistants.find(a => a.id !== id) || null : null);
      }
    } catch (err) {
      setError('Failed to delete assistant');
      console.error(err);
      throw err;
    } finally {
      setIsLoading(false);
    }
  };

  const createVectorStore = async (name: string) => {
    setIsLoading(true);
    setError(null);

    try {
      const newVectorStore = await apiService.createVectorStore(name);
      setVectorStores([...vectorStores, newVectorStore]);
      return newVectorStore;
    } catch (err) {
      setError('Failed to create vector store');
      console.error(err);
      throw err;
    } finally {
      setIsLoading(false);
    }
  };

  const deleteVectorStore = async (id: string) => {
    setIsLoading(true);
    setError(null);

    try {
      await apiService.deleteVectorStore(id);
      setVectorStores(vectorStores.filter(vs => vs.id !== id));

      // Update assistants that use this vector store
      const updatedAssistants = assistants.map(assistant => {
        if (assistant.vectorStoreIds.includes(id)) {
          return {
            ...assistant,
            vectorStoreIds: assistant.vectorStoreIds.filter(vsId => vsId !== id)
          };
        }
        return assistant;
      });

      setAssistants(updatedAssistants);

      // Update current assistant if needed
      if (currentAssistant && currentAssistant.vectorStoreIds.includes(id)) {
        setCurrentAssistant({
          ...currentAssistant,
          vectorStoreIds: currentAssistant.vectorStoreIds.filter(vsId => vsId !== id)
        });
      }
    } catch (err) {
      setError('Failed to delete vector store');
      console.error(err);
      throw err;
    } finally {
      setIsLoading(false);
    }
  };

  const createChatSession = async (assistantId: string) => {
    setIsLoading(true);
    setError(null);

    try {
      const newChatSession = await apiService.createChatSession(assistantId);
      setChatSessions([...chatSessions, newChatSession]);
      setCurrentChatSession(newChatSession);
      return newChatSession;
    } catch (err) {
      setError('Failed to create chat session');
      console.error(err);
      throw err;
    } finally {
      setIsLoading(false);
    }
  };

  const deleteChatSession = async (id: string) => {
    setIsLoading(true);
    setError(null);

    try {
      await apiService.deleteChatSession(id);
      setChatSessions(chatSessions.filter(cs => cs.id !== id));

      // Clear current chat session if it's the one being deleted
      if (currentChatSession?.id === id) {
        setCurrentChatSession(chatSessions.length > 1 ? chatSessions.find(cs => cs.id !== id) || null : null);
      }
    } catch (err) {
      setError('Failed to delete chat session');
      console.error(err);
      throw err;
    } finally {
      setIsLoading(false);
    }
  };

  const value = {
    assistants,
    vectorStores,
    chatSessions,
    currentChatSession,
    currentAssistant,
    isLoading,
    error,
    fetchAssistants,
    fetchVectorStores,
    fetchChatSessions,
    setCurrentChatSession,
    setCurrentAssistant,
    setChatSessions,
    createAssistant,
    updateAssistant,
    deleteAssistant,
    createVectorStore,
    deleteVectorStore,
    createChatSession,
    deleteChatSession,
  };

  return <AppContext.Provider value={value}>{children}</AppContext.Provider>;
};

export const useApp = (): AppContextType => {
  const context = useContext(AppContext);
  if (context === undefined) {
    throw new Error('useApp must be used within an AppProvider');
  }
  return context;
};

export default AppContext;
