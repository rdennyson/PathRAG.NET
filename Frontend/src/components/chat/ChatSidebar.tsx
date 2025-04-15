import React from 'react';
import { List, Button, IconButton, Dropdown, Nav, Sidenav } from 'rsuite';
import { FaPlus, FaEllipsisV, FaTrash, FaRobot } from 'react-icons/fa';
import { ChatSession, Assistant } from '../../models/types';
import { useApp } from '../../contexts/AppContext';

interface ChatSidebarProps {
  chatSessions: ChatSession[];
  currentChatSession: ChatSession | null;
  onSelectSession: (session: ChatSession) => void;
  onCreateSession: () => void;
  onDeleteSession: (sessionId: string) => void;
}

const ChatSidebar: React.FC<ChatSidebarProps> = ({
  chatSessions,
  currentChatSession,
  onSelectSession,
  onCreateSession,
  onDeleteSession
}) => {
  const { assistants } = useApp();

  const getAssistantName = (assistantId: string): string => {
    const assistant = assistants.find(a => a.id === assistantId);
    return assistant ? assistant.name : 'Unknown Assistant';
  };

  const getChatTitle = (session: ChatSession): string => {
    if (session.title) return session.title;
    
    // Generate title from first message or use default
    const firstUserMessage = session.messages.find(m => m.role === 'user');
    if (firstUserMessage) {
      const content = firstUserMessage.content;
      return content.length > 30 ? content.substring(0, 30) + '...' : content;
    }
    
    return 'New Chat';
  };

  return (
    <div className="h-full flex flex-col border-r border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800">
      <div className="p-3 border-b border-gray-200 dark:border-gray-700">
        <Button appearance="primary" block onClick={onCreateSession}>
          <FaPlus className="mr-2" /> New Chat
        </Button>
      </div>
      
      <div className="flex-grow overflow-y-auto">
        <Sidenav>
          <Sidenav.Body>
            <Nav>
              {chatSessions.length === 0 ? (
                <div className="p-4 text-center text-gray-500">
                  No chat sessions yet
                </div>
              ) : (
                chatSessions.map(session => (
                  <Nav.Item
                    key={session.id}
                    active={currentChatSession?.id === session.id}
                    onClick={() => onSelectSession(session)}
                    className="flex items-center justify-between p-3 hover:bg-gray-100 dark:hover:bg-gray-700 cursor-pointer"
                  >
                    <div className="flex-grow truncate">
                      <div className="font-medium truncate">{getChatTitle(session)}</div>
                      <div className="text-xs text-gray-500 flex items-center">
                        <FaRobot className="mr-1" />
                        {getAssistantName(session.assistantId)}
                      </div>
                    </div>
                    
                    <Dropdown
                      placement="rightStart"
                      renderToggle={(props, ref) => (
                        <IconButton
                          {...props}
                          ref={ref}
                          icon={<FaEllipsisV />}
                          appearance="subtle"
                          size="xs"
                          onClick={e => e.stopPropagation()}
                        />
                      )}
                    >
                      <Dropdown.Item
                        icon={<FaTrash />}
                        onClick={e => {
                          e.stopPropagation();
                          onDeleteSession(session.id);
                        }}
                      >
                        Delete
                      </Dropdown.Item>
                    </Dropdown>
                  </Nav.Item>
                ))
              )}
            </Nav>
          </Sidenav.Body>
        </Sidenav>
      </div>
    </div>
  );
};

export default ChatSidebar;
