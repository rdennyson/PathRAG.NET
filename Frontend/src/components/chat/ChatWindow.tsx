import React, { useEffect, useRef, useState } from 'react';
import { Panel, Message, toaster, SelectPicker } from 'rsuite';
import { FaRobot } from 'react-icons/fa';
import ChatMessage from './ChatMessage';
import ChatInput from './ChatInput';
import { ChatMessage as ChatMessageType, ChatSession, QueryRequest, SearchMode, Assistant } from '../../models/types';
import { useApp } from '../../contexts/AppContext';
import apiService from '../../services/api';

interface ChatWindowProps {
  chatSession: ChatSession;
  onUpdateSession: (updatedSession: ChatSession) => void;
}

const ChatWindow: React.FC<ChatWindowProps> = ({ chatSession, onUpdateSession }) => {
  const { assistants, currentAssistant, setCurrentAssistant } = useApp();
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [abortController, setAbortController] = useState<AbortController | null>(null);
  const [selectedAssistant, setSelectedAssistant] = useState<Assistant | null>(null);

  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (chatSession?.messages) {
      scrollToBottom();
    }
  }, [chatSession?.messages]);

  // Set the selected assistant based on the chat session's assistantId
  // Use a ref to track the last assistant ID to prevent unnecessary updates
  const lastAssistantIdRef = useRef<string | null>(null);

  useEffect(() => {
    // Only update if the assistant ID has changed
    if (lastAssistantIdRef.current !== chatSession.assistantId) {
      console.log(`Assistant ID changed from ${lastAssistantIdRef.current} to ${chatSession.assistantId}`);
      lastAssistantIdRef.current = chatSession.assistantId;

      const assistant = assistants.find(a => a.id === chatSession.assistantId);
      if (assistant) {
        // Only update if the assistant has changed
        if (!selectedAssistant || selectedAssistant.id !== assistant.id) {
          console.log(`Setting selected assistant to ${assistant.name}`);
          setSelectedAssistant(assistant);
        }

        // Only update the current assistant in the app context if needed
        if (!currentAssistant || currentAssistant.id !== assistant.id) {
          console.log(`Setting current assistant to ${assistant.name}`);
          setCurrentAssistant(assistant);
        }
      }
    }
  }, [chatSession.assistantId, assistants]);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  const handleSendMessage = async (content: string, attachments: File[], searchMode: SearchMode, useStreaming: boolean) => {
    if (!selectedAssistant) {
      toaster.push(<Message type="error">No assistant selected</Message>);
      return;
    }

    // Create user message with a temporary ID (will be replaced with server ID)
    const userMessage: ChatMessageType = {
      id: `temp-${Date.now()}`,
      content,
      role: 'user',
      timestamp: new Date().toISOString(),
      attachments: attachments.map(file => file.name)
    };

    // Update chat session with user message
    const updatedSession = {
      ...chatSession,
      messages: [...(chatSession.messages || []), userMessage],
      updatedAt: new Date().toISOString()
    };
    onUpdateSession(updatedSession);

    // Start loading state
    setIsLoading(true);

    // Create abort controller for cancellation
    const controller = new AbortController();
    setAbortController(controller);

    try {
      // Save user message to the backend first
      console.log('Saving user message to backend...');
      const savedMessage = await apiService.addChatMessage(
        chatSession.id,
        content,
        'user',
        attachments
      );

      console.log('User message saved:', savedMessage);

      // Replace temporary message ID with server-generated ID
      const messagesWithSavedUserMessage = updatedSession.messages.map(msg =>
        msg.id === userMessage.id ? { ...msg, id: savedMessage.id } : msg
      );

      const sessionWithSavedUserMessage = {
        ...updatedSession,
        messages: messagesWithSavedUserMessage
      };

      // Create query request
      const queryRequest: QueryRequest = {
        query: content,
        vectorStoreIds: selectedAssistant.vectorStoreIds,
        searchMode,
        assistantId: selectedAssistant.id
      };

      // Create a placeholder assistant message for streaming
      const assistantMessage: ChatMessageType = {
        id: `temp-assistant-${Date.now()}`,
        content: '',
        role: 'assistant',
        timestamp: new Date().toISOString()
      };

      // Add the empty assistant message to the session
      const sessionWithAssistantMessage = {
        ...sessionWithSavedUserMessage,
        messages: [...(sessionWithSavedUserMessage.messages || []), assistantMessage],
        updatedAt: new Date().toISOString()
      };
      onUpdateSession(sessionWithAssistantMessage);

      // Create an abort controller for the streaming request
      const controller = new AbortController();
      setAbortController(controller);

      if (useStreaming) {
        // Use streaming API
        try {
          let streamedContent = '';

          await apiService.queryStream(
            queryRequest,
            (chunk) => {
              // Update the content with each chunk
              streamedContent += chunk;

              // Update the message in the session
              const updatedMessages = [...(sessionWithAssistantMessage.messages || [])];
              if (updatedMessages.length > 0) {
                updatedMessages[updatedMessages.length - 1] = {
                  ...assistantMessage,
                  content: streamedContent
                };
              }

              // Update the session
              const streamedSession = {
                ...sessionWithAssistantMessage,
                messages: updatedMessages,
                updatedAt: new Date().toISOString()
              };
              onUpdateSession(streamedSession);
            },
            controller.signal
          );

          // After streaming is complete, save the assistant message to the backend
          console.log('Saving assistant response to backend...');
          const savedAssistantMessage = await apiService.addChatMessage(
            chatSession.id,
            streamedContent,
            'assistant',
            []
          );
          console.log('Assistant message saved:', savedAssistantMessage);

          // Replace temporary message ID with server-generated ID
          const finalMessages = sessionWithAssistantMessage.messages.map(msg =>
            msg.id === assistantMessage.id ? { ...msg, id: savedAssistantMessage.id, content: streamedContent } : msg
          );

          // Update the session with the saved assistant message
          const finalStreamedSession = {
            ...sessionWithAssistantMessage,
            messages: finalMessages,
            updatedAt: new Date().toISOString()
          };
          onUpdateSession(finalStreamedSession);
        } catch (streamError) {
          if (streamError instanceof DOMException && streamError.name === 'AbortError') {
            // Request was aborted, do nothing
          } else {
            console.error('Streaming error:', streamError);
            // Fallback to non-streaming API if streaming fails
            const response = await apiService.query(queryRequest);

            // Update the message with the full response
            const updatedMessages = [...(sessionWithAssistantMessage.messages || [])];
            if (updatedMessages.length > 0) {
              updatedMessages[updatedMessages.length - 1] = {
                ...assistantMessage,
                content: response.answer
              };
            }

            // Update the session
            const finalSession = {
              ...sessionWithAssistantMessage,
              messages: updatedMessages,
              updatedAt: new Date().toISOString()
            };
            onUpdateSession(finalSession);
          }
        }
      } else {
        // Use non-streaming API
        try {
          const response = await apiService.query(queryRequest);

          // Update the message with the full response
          const updatedMessages = [...(sessionWithAssistantMessage.messages || [])];
          if (updatedMessages.length > 0) {
            updatedMessages[updatedMessages.length - 1] = {
              ...assistantMessage,
              content: response.answer
            };
          }

          // Update the session
          const sessionWithResponse = {
            ...sessionWithAssistantMessage,
            messages: updatedMessages,
            updatedAt: new Date().toISOString()
          };
          onUpdateSession(sessionWithResponse);

          // Save the assistant message to the backend
          console.log('Saving assistant response to backend...');
          const savedAssistantMessage = await apiService.addChatMessage(
            chatSession.id,
            response.answer,
            'assistant',
            []
          );
          console.log('Assistant message saved:', savedAssistantMessage);

          // Replace temporary message ID with server-generated ID
          const finalMessages = sessionWithResponse.messages.map(msg =>
            msg.id === assistantMessage.id ? { ...msg, id: savedAssistantMessage.id } : msg
          );

          // Update the session with the saved assistant message
          const finalSession = {
            ...sessionWithResponse,
            messages: finalMessages,
            updatedAt: new Date().toISOString()
          };
          onUpdateSession(finalSession);
        } catch (queryError) {
          if (queryError instanceof DOMException && queryError.name === 'AbortError') {
            // Request was aborted, do nothing
          } else {
            console.error('Query error:', queryError);
            toaster.push(<Message type="error">Failed to get response</Message>);
          }
        }
      }
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

  // Handle assistant selection
  const handleAssistantChange = (assistantId: string | null) => {
    if (assistantId) {
      const assistant = assistants.find(a => a.id === assistantId);
      if (assistant) {
        setSelectedAssistant(assistant);
        setCurrentAssistant(assistant);
      }
    }
  };

  return (
    <Panel bordered className="h-full flex flex-col">
      {/* Assistant selector at the top */}
      <div className="p-3 border-b border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800">
        <div className="flex items-center">
          <FaRobot className="mr-2 text-blue-500" />
          <SelectPicker
            data={assistants.map(a => ({
              label: a.name,
              value: a.id
            }))}
            value={selectedAssistant?.id || ''}
            onChange={handleAssistantChange}
            placeholder="Select an assistant"
            className="w-full"
            searchable={false}
            cleanable={false}
          />
        </div>
      </div>

      <div className="flex-grow overflow-y-auto p-4">
        {!chatSession.messages || chatSession.messages.length === 0 ? (
          <div className="flex items-center justify-center h-full text-gray-500">
            <p>No messages yet. Start a conversation!</p>
          </div>
        ) : (
          (chatSession.messages || []).map(message => (
            <ChatMessage key={message.id} message={message} />
          ))
        )}
        <div ref={messagesEndRef} />
      </div>

      {selectedAssistant ? (
        <ChatInput
          onSendMessage={handleSendMessage}
          isLoading={isLoading}
          onStopGeneration={handleStopGeneration}
        />
      ) : (
        <div className="p-4 border-t border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-center">
          <p className="mb-2 text-gray-500">Please select an assistant to start chatting</p>
        </div>
      )}
    </Panel>
  );
};

export default ChatWindow;

