import React, { useEffect, useState } from 'react';
import { Container, Content, Grid, Row, Col, Panel, Button, Message, toaster } from 'rsuite';
import { FaPlus } from 'react-icons/fa';
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
    fetchChatSessions,
    isLoading 
  } = useApp();

  const [localChatSession, setLocalChatSession] = useState<ChatSession | null>(null);

  useEffect(() => {
    fetchChatSessions();
  }, []);

  useEffect(() => {
    if (currentChatSession) {
      setLocalChatSession(currentChatSession);
    } else if (chatSessions.length > 0) {
      setCurrentChatSession(chatSessions[0]);
      setLocalChatSession(chatSessions[0]);
    } else {
      setLocalChatSession(null);
    }
  }, [currentChatSession, chatSessions]);

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
    setLocalChatSession(updatedSession);
    
    // In a real app, you would save the updated session to the server
    // For now, we'll just update it locally
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
                  <Panel bordered className="h-full flex items-center justify-center">
                    <div className="text-center">
                      <h3 className="mb-4">No chat session selected</h3>
                      <Button appearance="primary" onClick={handleCreateSession}>
                        <FaPlus className="mr-2" /> Create New Chat
                      </Button>
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
