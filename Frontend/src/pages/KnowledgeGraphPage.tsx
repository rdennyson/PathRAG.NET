import React, { useState, useEffect,  useCallback } from 'react';
import { Container, Content, Panel, Button, Input, InputGroup, Loader, SelectPicker, IconButton, Tooltip, Whisper } from 'rsuite';
import Layout from '../components/layout/Layout';
import KnowledgeGraphViewer from '../components/knowledgeGraph/KnowledgeGraphViewer';
import { FaSearch, FaRocket, FaStop, FaSave, FaHistory } from 'react-icons/fa';
import apiService from '../services/api';
import { useApp } from '../contexts/AppContext';

interface GraphHistory {
  query: string;
  vectorStoreId?: string;
  nodes: any[];
  edges: any[];
  timestamp: number;
}

const KnowledgeGraphPage: React.FC = () => {
  const { vectorStores } = useApp();
  const [userInput, setUserInput] = useState<string>('');
  const [selectedVectorStore, setSelectedVectorStore] = useState<string | null>(null);
  const [isGenerating, setIsGenerating] = useState<boolean>(false);
  const [showViewer, setShowViewer] = useState<boolean>(false);
  const [nodes, setNodes] = useState<any[]>([]);
  const [edges, setEdges] = useState<any[]>([]);
  const [graphHistory, setGraphHistory] = useState<GraphHistory[]>([]);
  const [showHistory, setShowHistory] = useState<boolean>(false);
  const [clickedSave, setClickedSave] = useState<boolean>(false);

  // Load graph history from local storage on component mount
  useEffect(() => {
    const savedHistory = localStorage.getItem('graphHistory');
    if (savedHistory) {
      try {
        const parsedHistory = JSON.parse(savedHistory) as GraphHistory[];
        setGraphHistory(parsedHistory);

        // Load the most recent graph if available
        if (parsedHistory.length > 0) {
          const mostRecent = parsedHistory[0];
          setNodes(mostRecent.nodes);
          setEdges(mostRecent.edges);
          setUserInput(mostRecent.query);
          setSelectedVectorStore(mostRecent.vectorStoreId || null);
          setClickedSave(true);
          setShowViewer(true);
        }
      } catch (error) {
        console.error('Error parsing graph history:', error);
      }
    }
  }, []);

  // Save graph history to local storage
  const saveGraphHistory = useCallback((history: GraphHistory[]) => {
    localStorage.setItem('graphHistory', JSON.stringify(history));
  }, []);

  // Handle saving the current graph to history
  const handleSaveToHistory = useCallback(() => {
    if (nodes.length <= 1 || !userInput) return;

    const newHistory: GraphHistory = {
      query: userInput,
      vectorStoreId: selectedVectorStore || undefined,
      nodes,
      edges,
      timestamp: Date.now()
    };

    const updatedHistory = [newHistory, ...graphHistory];
    setGraphHistory(updatedHistory);
    saveGraphHistory(updatedHistory);
    setClickedSave(true);
  }, [nodes, edges, userInput, selectedVectorStore, graphHistory, saveGraphHistory]);

  // Handle loading a graph from history
  const handleLoadFromHistory = useCallback((historyItem: GraphHistory) => {
    setNodes(historyItem.nodes);
    setEdges(historyItem.edges);
    setUserInput(historyItem.query);
    setSelectedVectorStore(historyItem.vectorStoreId || null);
    setClickedSave(true);
    setShowHistory(false);
    setShowViewer(true);
  }, []);

  const handleGenerateGraph = useCallback(async () => {
    if (!userInput.trim()) return;

    setIsGenerating(true);
    setClickedSave(false);
    setNodes([]);
    setEdges([]);
    setShowViewer(true);

    try {
      // Use the standard API approach
      const graphNodes = await apiService.generateKnowledgeGraph(
        userInput,
        15, // maxNodes
        selectedVectorStore ? selectedVectorStore : undefined
      );

      console.log('Received graph nodes:', graphNodes);

      // Pass the raw graph nodes directly to the KnowledgeGraphViewer
      // The viewer will handle the conversion to ReactFlow format
      setNodes(graphNodes);
      setIsGenerating(false);
    } catch (error) {
      console.error('Error generating knowledge graph:', error);
      setIsGenerating(false);
    }
  }, [userInput, selectedVectorStore]);

  // Handle canceling the current generation
  const handleCancel = useCallback(() => {
    // Since we're not using streaming anymore, we can just set isGenerating to false
    setIsGenerating(false);
  }, []);

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

              <div className="mb-4">
                <label className="block text-sm font-medium mb-2">Select a Vector Store (Optional):</label>
                <SelectPicker
                  data={vectorStores.map(vs => ({ label: vs.name, value: vs.id }))}
                  value={selectedVectorStore}
                  onChange={setSelectedVectorStore}
                  placeholder="Select Vector Store"
                  block
                  cleanable
                />
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

              <div className="flex gap-2">
                <Button
                  appearance="primary"
                  onClick={handleGenerateGraph}
                  disabled={isGenerating || !userInput.trim()}
                  block
                >
                  {isGenerating ? 'Generating...' : 'Generate Knowledge Graph'}
                </Button>
              </div>

              {graphHistory.length > 0 && (
                <div className="mt-6">
                  <h3 className="text-lg font-semibold mb-3">Recent Graphs</h3>
                  <div className="max-h-60 overflow-y-auto">
                    {graphHistory.slice(0, 5).map((item, index) => (
                      <div
                        key={index}
                        className="p-3 border-b cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-700"
                        onClick={() => handleLoadFromHistory(item)}
                      >
                        <div className="font-medium">{item.query}</div>
                        <div className="text-xs text-gray-500 flex justify-between">
                          <span>{item.vectorStoreId ? `Vector Store: ${vectorStores.find(vs => vs.id === item.vectorStoreId)?.name || 'Unknown'}` : 'No Vector Store'}</span>
                          <span>{new Date(item.timestamp).toLocaleString()}</span>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </Panel>
          ) : (
            <div className="h-full flex flex-col">
              <Panel bordered className="mb-4 p-4">
                <div className="flex flex-col md:flex-row gap-4 items-end">
                  <div className="flex-grow">
                    <label className="block mb-2 font-medium">Search Query</label>
                    <InputGroup>
                      <Input
                        value={userInput}
                        onChange={value => setUserInput(value)}
                        placeholder="Enter a topic or question to generate a knowledge graph"
                        disabled={isGenerating}
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

                  <div className="w-full md:w-64">
                    <label className="block mb-2 font-medium">Vector Store</label>
                    <SelectPicker
                      data={vectorStores.map(vs => ({ label: vs.name, value: vs.id }))}
                      value={selectedVectorStore}
                      onChange={setSelectedVectorStore}
                      placeholder="Select Vector Store"
                      block
                      disabled={isGenerating}
                      cleanable
                    />
                  </div>

                  <div className="flex gap-2">
                    <Button
                      appearance="primary"
                      onClick={handleGenerateGraph}
                      disabled={!userInput.trim() || isGenerating}
                      className="bg-green-600 text-white"
                    >
                      {isGenerating ? <Loader size="xs" /> : 'Generate'}
                    </Button>

                    <Button
                      appearance="subtle"
                      onClick={handleCancel}
                      disabled={!isGenerating}
                    >
                      <FaStop className="mr-2" />
                      Cancel
                    </Button>

                    <Whisper
                      placement="top"
                      trigger="hover"
                      speaker={<Tooltip>Save current graph</Tooltip>}
                    >
                      <IconButton
                        icon={<FaSave />}
                        appearance="subtle"
                        onClick={handleSaveToHistory}
                        disabled={isGenerating || nodes.length <= 1 || clickedSave}
                      />
                    </Whisper>

                    <Whisper
                      placement="top"
                      trigger="hover"
                      speaker={<Tooltip>View history</Tooltip>}
                    >
                      <IconButton
                        icon={<FaHistory />}
                        appearance="subtle"
                        onClick={() => setShowHistory(!showHistory)}
                      />
                    </Whisper>
                  </div>
                </div>
              </Panel>

              {/* Graph History Panel */}
              {showHistory && (
                <Panel bordered className="mb-4 p-4">
                  <h3 className="text-lg font-semibold mb-3">Graph History</h3>
                  {graphHistory.length === 0 ? (
                    <p>No saved graphs yet.</p>
                  ) : (
                    <div className="max-h-60 overflow-y-auto">
                      {graphHistory.map((item, index) => (
                        <div
                          key={index}
                          className="p-3 border-b cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-700"
                          onClick={() => handleLoadFromHistory(item)}
                        >
                          <div className="font-medium">{item.query}</div>
                          <div className="text-xs text-gray-500 flex justify-between">
                            <span>{item.vectorStoreId ? `Vector Store: ${vectorStores.find(vs => vs.id === item.vectorStoreId)?.name || 'Unknown'}` : 'No Vector Store'}</span>
                            <span>{new Date(item.timestamp).toLocaleString()}</span>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </Panel>
              )}

              <Panel bordered className="flex-grow">
                <KnowledgeGraphViewer
                  searchQuery={userInput}
                  initialNodes={nodes}
                  vectorStoreId={selectedVectorStore || undefined}
                />
              </Panel>
            </div>
          )}
        </Content>
      </Container>
    </Layout>
  );
};

export default KnowledgeGraphPage;
