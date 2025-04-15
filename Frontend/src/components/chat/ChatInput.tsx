import React, { useState, useRef } from 'react';
import { Input, Button, ButtonGroup, Dropdown, IconButton, Loader, Uploader } from 'rsuite';
import { FaMicrophone, FaPaperPlane, FaStop, FaPaperclip, FaCog } from 'react-icons/fa';
import { SearchMode } from '../../models/types';

interface ChatInputProps {
  onSendMessage: (message: string, attachments: File[], searchMode: SearchMode) => Promise<void>;
  isLoading: boolean;
  onStopGeneration: () => void;
}

const ChatInput: React.FC<ChatInputProps> = ({ onSendMessage, isLoading, onStopGeneration }) => {
  const [message, setMessage] = useState<string>('');
  const [attachments, setAttachments] = useState<File[]>([]);
  const [searchMode, setSearchMode] = useState<SearchMode>(SearchMode.Hybrid);
  const [isRecording, setIsRecording] = useState<boolean>(false);
  const uploaderRef = useRef<any>(null);
  
  const handleSendMessage = async () => {
    if (!message.trim() && attachments.length === 0) return;
    
    await onSendMessage(message, attachments, searchMode);
    setMessage('');
    setAttachments([]);
    
    if (uploaderRef.current) {
      uploaderRef.current.clearFiles();
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSendMessage();
    }
  };

  const toggleRecording = () => {
    // This would be implemented with a speech recognition API
    setIsRecording(!isRecording);
    
    if (!isRecording) {
      // Start recording
      if ('webkitSpeechRecognition' in window) {
        const SpeechRecognition = (window as any).webkitSpeechRecognition;
        const recognition = new SpeechRecognition();
        
        recognition.continuous = true;
        recognition.interimResults = true;
        
        recognition.onresult = (event: any) => {
          const transcript = Array.from(event.results)
            .map((result: any) => result[0])
            .map((result: any) => result.transcript)
            .join('');
          
          setMessage(transcript);
        };
        
        recognition.start();
        
        // Store recognition instance to stop it later
        (window as any).recognition = recognition;
      } else {
        alert('Speech recognition is not supported in this browser.');
        setIsRecording(false);
      }
    } else {
      // Stop recording
      if ((window as any).recognition) {
        (window as any).recognition.stop();
      }
    }
  };

  return (
    <div className="p-4 border-t border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 rounded-lg shadow-lg">
      <div className="flex items-center mb-2">
        <Dropdown
          title={`Search Mode: ${searchMode}`}
          icon={<FaCog />}
          placement="topStart"
        >
          <Dropdown.Item 
            active={searchMode === SearchMode.Semantic} 
            onSelect={() => setSearchMode(SearchMode.Semantic)}
          >
            Semantic Search
          </Dropdown.Item>
          <Dropdown.Item 
            active={searchMode === SearchMode.Hybrid} 
            onSelect={() => setSearchMode(SearchMode.Hybrid)}
          >
            Hybrid Search
          </Dropdown.Item>
          <Dropdown.Item 
            active={searchMode === SearchMode.Graph} 
            onSelect={() => setSearchMode(SearchMode.Graph)}
          >
            Graph Search
          </Dropdown.Item>
        </Dropdown>
        
        <div className="ml-auto">
          {attachments.length > 0 && (
            <span className="text-sm text-gray-500 mr-2">
              {attachments.length} file(s) attached
            </span>
          )}
        </div>
      </div>
      
      <div className="flex items-end">
        <Uploader
          ref={uploaderRef}
          listType="picture"
          action=""
          autoUpload={false}
          multiple
          onChange={fileList => {
            const files = fileList.map(file => file.blobFile).filter(Boolean) as File[];
            setAttachments(files);
          }}
          className="mr-2"
          renderTrigger={({ onClick }, ref) => (
            <IconButton
              ref={ref}
              icon={<FaPaperclip />}
              onClick={onClick}
              appearance="subtle"
              className="mb-2"
            />
          )}
        />
        
        <Input
          as="textarea"
          rows={3}
          placeholder="Type your message here..."
          value={message}
          onChange={setMessage}
          onKeyDown={handleKeyDown}
          disabled={isLoading}
          className="flex-grow mr-2"
        />
        
        <ButtonGroup vertical>
          {isLoading ? (
            <Button appearance="primary" color="red" onClick={onStopGeneration}>
              <FaStop />
            </Button>
          ) : (
            <Button appearance="primary" onClick={handleSendMessage} disabled={!message.trim() && attachments.length === 0}>
              <FaPaperPlane />
            </Button>
          )}
          
          <Button
            appearance={isRecording ? 'primary' : 'subtle'}
            color={isRecording ? 'red' : 'blue'}
            onClick={toggleRecording}
            disabled={isLoading}
          >
            <FaMicrophone />
          </Button>
        </ButtonGroup>
      </div>
      
      {isLoading && (
        <div className="mt-2 flex items-center">
          <Loader size="sm" />
          <span className="ml-2 text-sm text-gray-500">Generating response...</span>
        </div>
      )}
    </div>
  );
};

export default ChatInput;
