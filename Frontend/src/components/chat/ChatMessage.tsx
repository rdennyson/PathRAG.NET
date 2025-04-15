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
    <div className={`flex ${isUser ? 'justify-end' : 'justify-start'} mb-4`}>
      <div className={`flex ${isUser ? 'flex-row-reverse' : 'flex-row'} max-w-3xl`}>
        <Avatar
          circle
          size="sm"
          className={`${isUser ? 'ml-3' : 'mr-3'} mt-1`}
          style={{ background: isUser ? '#3498ff' : '#673ab7' }}
        >
          {isUser ? <FaUser /> : <FaRobot />}
        </Avatar>
        
        <Panel
          bordered
          className={`${isUser ? 'bg-blue-50 dark:bg-blue-900' : 'bg-purple-50 dark:bg-purple-900'} p-3 rounded-lg`}
          style={{ maxWidth: '80%' }}
        >
          <ReactMarkdown
            components={{
              code({ node, inline, className, children, ...props }) {
                const match = /language-(\w+)/.exec(className || '');
                return !inline && match ? (
                  <SyntaxHighlighter
                    style={vscDarkPlus}
                    language={match[1]}
                    PreTag="div"
                    {...props}
                  >
                    {String(children).replace(/\n$/, '')}
                  </SyntaxHighlighter>
                ) : (
                  <code className={className} {...props}>
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
    </div>
  );
};

export default ChatMessage;
