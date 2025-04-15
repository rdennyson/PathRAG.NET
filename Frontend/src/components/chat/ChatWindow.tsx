import React, { useEffect, useRef, useState } from 'react';
import { Panel, Loader, Message, toaster } from 'rsuite';
import ChatMessage from './ChatMessage';
import ChatInput from './ChatInput';
import { ChatMessage as ChatMessageType, ChatSession, QueryRequest, SearchMode } from '../../models/types';
import { useApp } from '../../contexts/AppContext';
import apiService from '../../services/api';

interface ChatWindowProps {
  chatSession: ChatSession;
  onUpdateSession: (updatedSession: ChatSession) => void;
}

const ChatWindow: React.FC<ChatWindowProps> = ({ chatSession, onUpdateSession }) => {
  const { currentAssistant } = useApp();
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [abortController, setAbortController] = useState<AbortController | null>(null);
  
  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    scrollToBottom();
  }, [chatSession.messages]);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  const handleSendMessage = async (content: string, attachments: File[], searchMode: SearchMode) => {
    if (!currentAssistant) {
      toaster.push(<Message type="error">No assistant selected</Message>);
      return;
    }

    // Create user message
    const userMessage: ChatMessageType = {
      id: Date.now().toString(),
      content,
      role: 'user',
      timestamp: new Date().toISOString(),
      attachments: attachments.map(file => file.name)
    };

    // Update chat session with user message
    const updatedSession = {
      ...chatSession,
      messages: [...chatSession.messages, userMessage],
      updatedAt: new Date().toISOString()
    };
    onUpdateSession(updatedSession);

    // Start loading state
    setIsLoading(true);

    // Create abort controller for cancellation
    const controller = new AbortController();
    setAbortController(controller);

    try {
      // Upload attachments if any
      if (attachments.length > 0) {
        // This would be implemented with a file upload API
        // For now, we'll just simulate it
        await new Promise(resolve => setTimeout(resolve, 1000));
      }

      // Create query request
      const queryRequest: QueryRequest = {
        query: content,
        vectorStoreIds: currentAssistant.vectorStoreIds,
        searchMode,
        assistantId: currentAssistant.id
      };

      // Send query to API
      const response = await apiService.query(queryRequest);

      // Create assistant message
      const assistantMessage: ChatMessageType = {
        id: Date.now().toString(),
        content: response.answer,
        role: 'assistant',
        timestamp: new Date().toISOString()
      };

      // Update chat session with assistant message
      const finalSession = {
        ...updatedSession,
        messages: [...updatedSession.messages, assistantMessage],
        updatedAt: new Date().toISOString()
      };
      onUpdateSession(finalSession);
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') {
        // Request was aborted, do nothing
      } else {
        toaster.push(<Message type="error">Failed to get response</Message>);
        console.error(error);
      }
    } finally {
      setIsLoading(false);
      setAbortController(null);
    }
  };

  const handleStopGeneration = () => {
    if (abortController) {
      abortController.abort();
      setIsLoading(false);
      setAbortController(null);
    }
  };

  return (
    <Panel bordered className="h-full flex flex-col">
      <div className="flex-grow overflow-y-auto p-4">
        {chatSession.messages.length === 0 ? (
          <div className="flex items-center justify-center h-full text-gray-500">
            <p>No messages yet. Start a conversation!</p>
          </div>
        ) : (
          chatSession.messages.map(message => (
            <ChatMessage key={message.id} message={message} />
          ))
        )}
        <div ref={messagesEndRef} />
      </div>
      
      <ChatInput
        onSendMessage={handleSendMessage}
        isLoading={isLoading}
        onStopGeneration={handleStopGeneration}
      />
    </Panel>
  );
};

export default ChatWindow;
