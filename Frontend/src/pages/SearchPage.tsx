import React, { useEffect, useState } from 'react';
import { Container, Content, Grid, Row, Col, Panel, Button, Message, toaster, SelectPicker } from 'rsuite';
import { FaPlus, FaRobot } from 'react-icons/fa';
import Layout from '../components/layout/Layout';
import ChatSidebar from '../components/chat/ChatSidebar';
import ChatWindow from '../components/chat/ChatWindow';
import { useApp } from '../contexts/AppContext';
import { ChatSession } from '../models/types';
import apiService from '../services/api';

const SearchPage: React.FC = () => {
  const {
    chatSessions,
    currentChatSession,
    setCurrentChatSession,
    currentAssistant,
    setCurrentAssistant,
    assistants,
    fetchChatSessions,
    setChatSessions
  } = useApp();

  const [localChatSession, setLocalChatSession] = useState<ChatSession | null>(null);

  // Fetch chat sessions only once when the component mounts
  const chatSessionsFetchedRef = React.useRef(false);

  useEffect(() => {
    // Only fetch chat sessions if we haven't already
    if (!chatSessionsFetchedRef.current) {
      console.log('Manually fetching chat sessions from SearchPage');
      fetchChatSessions();
      chatSessionsFetchedRef.current = true;
    }
  }, []);

  // Use a ref to track the last loaded chat session ID to prevent duplicate API calls
  const lastLoadedSessionId = React.useRef<string | null>(null);

  // Track if we're currently loading a chat session to prevent duplicate calls
  const isLoadingChatSessionRef = React.useRef(false);

  useEffect(() => {
    // Only load the chat session if it has changed and we're not already loading one
    if (currentChatSession &&
        currentChatSession.id !== lastLoadedSessionId.current &&
        !isLoadingChatSessionRef.current) {

      // Load full chat session with messages
      const loadChatSession = async () => {
        try {
          // Set loading flag to prevent duplicate calls
          isLoadingChatSessionRef.current = true;

          // Update the ref to prevent duplicate calls
          lastLoadedSessionId.current = currentChatSession.id;

          console.log(`Loading chat session ${currentChatSession.id}`);
          const fullSession = await apiService.getChatSession(currentChatSession.id);
          console.log(`Chat session loaded: ${fullSession.id}`);

          setLocalChatSession(fullSession);

          // Set the assistant for this chat session
          const assistant = assistants.find(a => a.id === fullSession.assistantId);
          if (assistant && (!currentAssistant || currentAssistant.id !== assistant.id)) {
            setCurrentAssistant(assistant);
          }
        } catch (error) {
          console.error('Failed to load chat session:', error);
          toaster.push(<Message type="error">Failed to load chat session</Message>);
          setLocalChatSession(currentChatSession); // Fallback to the basic session info
        } finally {
          // Reset loading flag
          isLoadingChatSessionRef.current = false;
        }
      };

      loadChatSession();
    } else if (!currentChatSession && chatSessions.length > 0 && !isLoadingChatSessionRef.current) {
      // Only set current chat session if it's not already set and we're not loading
      console.log('Setting current chat session to first session in list');
      setCurrentChatSession(chatSessions[0]);
      // Don't set localChatSession here, let the above effect handle it
    } else if (!currentChatSession) {
      setLocalChatSession(null);
      lastLoadedSessionId.current = null;
    }
  }, [currentChatSession, chatSessions.length]);

  const handleCreateSession = async () => {
    if (!currentAssistant) {
      toaster.push(<Message type="error">Please select an assistant first</Message>);
      return;
    }

    try {
      const newSession = await apiService.createChatSession(currentAssistant.id);
      setCurrentChatSession(newSession);
      setLocalChatSession(newSession);
      toaster.push(<Message type="success">New chat session created</Message>);
    } catch (error) {
      toaster.push(<Message type="error">Failed to create chat session</Message>);
    }
  };

  const handleDeleteSession = async (sessionId: string) => {
    try {
      await apiService.deleteChatSession(sessionId);

      // Refresh chat sessions
      fetchChatSessions();

      // If the deleted session was the current one, reset it
      if (currentChatSession?.id === sessionId) {
        setCurrentChatSession(null);
        setLocalChatSession(null);
      }

      toaster.push(<Message type="success">Chat session deleted</Message>);
    } catch (error) {
      toaster.push(<Message type="error">Failed to delete chat session</Message>);
    }
  };

  const handleUpdateSession = async (updatedSession: ChatSession) => {
    // Update local state
    setLocalChatSession(updatedSession);

    // Update global state - only if the ID matches to prevent unnecessary updates
    if (currentChatSession?.id === updatedSession.id) {
      setCurrentChatSession(updatedSession);
    }

    // Update chat sessions list
    setChatSessions(prevSessions =>
      prevSessions.map(session =>
        session.id === updatedSession.id ? updatedSession : session
      )
    );

    // Note: We've moved the message saving logic to ChatWindow.tsx
    // to ensure messages are properly saved in the correct order
  };

  return (
    <Layout>
      <Container className="h-[calc(100vh-120px)]">
        <Content className="h-full">
          <Grid fluid className="h-full">
            <Row className="h-full">
              <Col xs={6} className="h-full">
                <ChatSidebar
                  chatSessions={chatSessions}
                  currentChatSession={localChatSession}
                  onSelectSession={setCurrentChatSession}
                  onCreateSession={handleCreateSession}
                  onDeleteSession={handleDeleteSession}
                />
              </Col>

              <Col xs={18} className="h-full">
                {localChatSession ? (
                  <ChatWindow
                    chatSession={localChatSession}
                    onUpdateSession={handleUpdateSession}
                  />
                ) : (
                  <Panel bordered className="h-full flex flex-col">
                    <div className="p-3 border-b border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800">
                      <div className="flex items-center">
                        <FaRobot className="mr-2 text-blue-500" />
                        <SelectPicker
                          data={assistants.map(a => ({
                            label: a.name,
                            value: a.id
                          }))}
                          value={currentAssistant?.id || ''}
                          onChange={(value) => {
                            const assistant = assistants.find(a => a.id === value);
                            if (assistant) setCurrentAssistant(assistant);
                          }}
                          placeholder="Select an assistant"
                          className="w-full"
                          searchable={false}
                          cleanable={false}
                        />
                      </div>
                    </div>
                    <div className="flex-grow flex items-center justify-center">
                      <div className="text-center">
                        <h3 className="mb-4">No chat session selected</h3>
                        <Button
                          appearance="primary"
                          onClick={handleCreateSession}
                          disabled={!currentAssistant}
                        >
                          <FaPlus className="mr-2" /> Create New Chat
                        </Button>
                        {!currentAssistant && (
                          <p className="mt-3 text-gray-500">Please select an assistant first</p>
                        )}
                      </div>
                    </div>
                  </Panel>
                )}
              </Col>
            </Row>
          </Grid>
        </Content>
      </Container>
    </Layout>
  );
};

export default SearchPage;

