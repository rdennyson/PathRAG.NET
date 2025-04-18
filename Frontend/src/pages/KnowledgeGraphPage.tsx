import React, { useState } from 'react';
import { Container, Content, Panel, Button, Input, InputGroup, Loader } from 'rsuite';
import Layout from '../components/layout/Layout';
import KnowledgeGraphViewer from '../components/knowledgeGraph/KnowledgeGraphViewer';
import { FaSearch, FaRocket } from 'react-icons/fa';
import apiService from '../services/api';

const KnowledgeGraphPage: React.FC = () => {
  const [userInput, setUserInput] = useState<string>('');
  const [isGenerating, setIsGenerating] = useState<boolean>(false);
  const [showViewer, setShowViewer] = useState<boolean>(false);

  const handleGenerateGraph = async () => {
    if (!userInput.trim()) return;

    setIsGenerating(true);
    try {
      // Call the backend API to generate a knowledge graph
      await apiService.generateKnowledgeGraph(userInput, 20);
      setShowViewer(true);
    } catch (error) {
      console.error('Error generating graph:', error);
    } finally {
      setIsGenerating(false);
    }
  };

  return (
    <Layout>
      <Container className="h-[calc(100vh-120px)]">
        <Content className="h-full">
          {!showViewer ? (
            <Panel className="mx-auto mt-10 max-w-3xl p-6 border rounded-md">
              <h2 className="text-2xl font-bold mb-4">Create an AI-Generated Knowledge Graph</h2>

              <div className="bg-green-50 dark:bg-green-900 p-4 rounded-md mb-6 flex items-start">
                <FaRocket className="text-green-600 dark:text-green-400 mt-1 mr-3 flex-shrink-0" />
                <div>
                  <p className="font-medium text-green-800 dark:text-green-300">Knowledge graphs offer a non-linear structure to information.</p>
                  <p className="text-green-700 dark:text-green-400 mt-1">They're helpful for learning, understanding complex topics, and visualizing relationships between concepts.</p>
                </div>
              </div>

              <div className="mb-6">
                <label className="block text-sm font-medium mb-2">Enter a topic or question:</label>
                <InputGroup>
                  <Input
                    value={userInput}
                    onChange={value => setUserInput(value)}
                    placeholder="e.g., Artificial Intelligence, Climate Change, Quantum Computing..."
                    onPressEnter={handleGenerateGraph}
                  />
                  <InputGroup.Button
                    onClick={handleGenerateGraph}
                    disabled={isGenerating || !userInput.trim()}
                  >
                    {isGenerating ? <Loader size="xs" /> : <FaSearch />}
                  </InputGroup.Button>
                </InputGroup>
              </div>

              <Button
                appearance="primary"
                block
                onClick={handleGenerateGraph}
                disabled={isGenerating || !userInput.trim()}
              >
                {isGenerating ? 'Generating...' : 'Generate Knowledge Graph'}
              </Button>
            </Panel>
          ) : (
            <KnowledgeGraphViewer searchQuery={userInput} />
          )}
        </Content>
      </Container>
    </Layout>
  );
};

export default KnowledgeGraphPage;
