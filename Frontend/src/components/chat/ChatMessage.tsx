import React from 'react';
import { Avatar, Panel } from 'rsuite';
import { FaRobot, FaUser } from 'react-icons/fa';
import ReactMarkdown from 'react-markdown';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { vscDarkPlus } from 'react-syntax-highlighter/dist/esm/styles/prism';
import { ChatMessage as ChatMessageType } from '../../models/types';

interface ChatMessageProps {
  message: ChatMessageType;
}

const ChatMessage: React.FC<ChatMessageProps> = ({ message }) => {
  const isUser = message.role === 'user';

  return (
    <div className="flex flex-row gap-4 mb-4">
      <Avatar circle size="sm">
        {isUser ? <FaUser /> : <FaRobot />}
      </Avatar>

      <Panel
        bordered
        className={`${isUser ? 'bg-green-50 dark:bg-green-800' : 'bg-emerald-50 dark:bg-emerald-900'} p-3 rounded-lg`}
        style={{ maxWidth: '80%' }}
      >
        <ReactMarkdown
          components={{
            code({ className, children }) {
              const match = /language-(\w+)/.exec(className || '');
              const language = match ? match[1] : '';
              return match ? (
                <div style={{ margin: 0 }}>
                  <SyntaxHighlighter
                    style={vscDarkPlus}
                    language={language}
                    PreTag="div"
                  >
                    {String(children).replace(/\n$/, '')}
                  </SyntaxHighlighter>
                </div>
              ) : (
                <code className={className}>
                  {children}
                </code>
              );
            }
          }}
        >
          {message.content}
        </ReactMarkdown>

        <div className="text-xs text-gray-500 mt-2">
          {new Date(message.timestamp).toLocaleTimeString()}
        </div>
      </Panel>
    </div>
  );
};

export default ChatMessage;



