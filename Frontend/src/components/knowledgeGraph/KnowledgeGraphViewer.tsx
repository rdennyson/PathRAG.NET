import React, { useEffect, useRef, useState, useCallback } from 'react';
import { Loader, SelectPicker, Button, Modal, Form, Input, InputGroup } from 'rsuite';
import { FaSearch, FaInfoCircle, FaSearchPlus, FaSearchMinus, FaSync, FaSave, FaHistory } from 'react-icons/fa';
import { useApp } from '../../contexts/AppContext';
import { GraphEntity, Relationship } from '../../types/graph';
import apiService from '../../services/api';

import ReactFlow, {
  ReactFlowProvider,
  addEdge,
  useNodesState,
  useEdgesState,
  Controls,
  MiniMap,
  Background,
  BackgroundVariant,
  Edge,
  Node,
  MarkerType,
} from 'reactflow';
import 'reactflow/dist/style.css';

import DownloadButton from '../ui/download-button';
import GraphHistory from './GraphHistory';
import { saveGraphHistory, loadGraphHistory, defaultGraphHistory } from '../../utils/graphHistory';
import { SavedGraphHistory } from '../../types/graph';

interface KnowledgeGraphViewerProps {
  entityType?: string;
  maxDepth?: number;
  maxNodes?: number;
  searchQuery?: string;
  nodes?: any[];
  edges?: any[];
  initialNodes?: any[];
  initialEdges?: any[];
  vectorStoreId?: string;
}

const KnowledgeGraphViewer: React.FC<KnowledgeGraphViewerProps> = ({
  entityType = '*',
  maxDepth = 2,
  maxNodes = 100,
  searchQuery: initialSearchQuery = '',
  nodes: initialNodes,
  edges: initialEdges,
  vectorStoreId: initialVectorStoreId
}) => {
  const { vectorStores } = useApp();
  const [selectedVectorStoreId, setSelectedVectorStoreId] = useState<string>(initialVectorStoreId || '');
  const [entities, setEntities] = useState<GraphEntity[]>([]);
  const [relationships, setRelationships] = useState<Relationship[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [searchTerm, setSearchTerm] = useState<string>(initialSearchQuery);
  const [selectedEntity, setSelectedEntity] = useState<GraphEntity | null>(null);
  const [showEntityDetails, setShowEntityDetails] = useState<boolean>(false);
  const [showHistorySidebar, setShowHistorySidebar] = useState<boolean>(false);
  // This state is used when saving to history
  const [, setUserInput] = useState<string>('');
  const [submittedUserInput, setSubmittedUserInput] = useState<string>('');
  const [clickedSave, setClickedSave] = useState<boolean>(false);

  // ReactFlow states
  const reactFlowWrapper = useRef<HTMLDivElement>(null);
  const [nodes, setNodes, onNodesChange] = useNodesState([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState([]);
  const [reactFlowInstance, setReactFlowInstance] = useState<any>(null);
  const [searchHistory, setSearchHistory] = useState<SavedGraphHistory[]>([]);

  // Process raw nodes and edges into ReactFlow format
  useEffect(() => {
    if (initialNodes && initialNodes.length > 0) {
      console.log('Processing initial nodes:', initialNodes.length);

      // Convert raw nodes to ReactFlow format
      const processedNodes = initialNodes.map((node: any) => {
        // Generate random position if not provided
        const x = node.x || Math.random() * 800;
        const y = node.y || Math.random() * 600;

        return {
          id: node.id,
          data: {
            label: node.label,
            type: node.type || 'concept',
            description: node.description || '',
            entityId: node.id // Store the original entity ID for later reference
          },
          position: { x, y },
          style: {
            background: node.background || '#e6f7ff',
            color: '#000000',
            border: `1px solid ${node.stroke || '#1890ff'}`,
            width: 180,
            borderRadius: 5,
            padding: 10
          }
        };
      });

      // Process edges from adjacencies
      const processedEdges: Edge[] = [];
      initialNodes.forEach((node: any) => {
        if (node.adjacencies && node.adjacencies.length > 0) {
          node.adjacencies.forEach((edge: any) => {
            processedEdges.push({
              id: edge.id || `${edge.source}-${edge.target}`,
              source: edge.source,
              target: edge.target,
              label: edge.label,
              data: {
                description: edge.description || '',
                type: edge.label || 'related to'
              },
              style: { stroke: edge.color || '#888' },
              markerEnd: {
                type: MarkerType.ArrowClosed,
                color: edge.color || '#888',
              },
            });
          });
        }
      });

      console.log('Setting ReactFlow nodes:', processedNodes.length, 'and edges:', processedEdges.length);
      setNodes(processedNodes);
      setEdges(processedEdges);

      // Force a re-render and center the graph after a short delay
      setTimeout(() => {
        if (reactFlowInstance) {
          console.log('Forcing graph to center');
          reactFlowInstance.fitView({ padding: 0.2 });
        }
      }, 500);
    }
  }, [initialNodes, reactFlowInstance]);

  // Legacy state for compatibility with code that might call setGraphData
  const [, setGraphData] = useState<{ nodes: any[], links: any[] }>({ nodes: [], links: [] });

  // Load initial graph history
  useEffect(() => {
    const currentSearchHistory = loadGraphHistory();
    if (currentSearchHistory.length === 0) {
      setSearchHistory(defaultGraphHistory);
      if (defaultGraphHistory.length > 0) {
        const { nodes: initialNodes, edges: initialEdges } = defaultGraphHistory[0].results;
        setNodes(initialNodes);
        setEdges(initialEdges);
        setClickedSave(true);
      }
    } else {
      setSearchHistory(currentSearchHistory);
      if (currentSearchHistory.length > 0) {
        const { nodes: initialNodes, edges: initialEdges } = currentSearchHistory[0].results;
        setNodes(initialNodes);
        setEdges(initialEdges);
        setClickedSave(true);
      }
    }
  }, []);

  // Initialize ReactFlow
  const onInit = useCallback((instance: any) => {
    console.log('ReactFlow initialized:', instance);
    setReactFlowInstance(instance);
  }, []);

  // Connect nodes
  const onConnect = useCallback((params: any) => {
    setEdges((eds: Edge[]) => addEdge(params, eds));
  }, []);

  // Center graph when nodes or edges change
  const centerGraph = useCallback(() => {
    if (reactFlowInstance) {
      console.log('Centering graph with nodes:', nodes.length, 'edges:', edges.length);
      reactFlowInstance.fitView();
    }
  }, [reactFlowInstance, nodes, edges]);

  useEffect(() => {
    if (nodes.length > 0) {
      console.log('Nodes or edges changed, centering graph');
      // Add a small delay to ensure ReactFlow has processed the nodes
      setTimeout(centerGraph, 100);
    }
  }, [nodes, edges, centerGraph]);

  // Handle saving to history
  const handleSaveToHistory = useCallback(() => {
    const newSearchHistory = [{
      searchValue: submittedUserInput || 'Knowledge Graph',
      timestamp: new Date().toISOString(),
      results: { nodes, edges }
    }, ...searchHistory];
    setSearchHistory(newSearchHistory);
    saveGraphHistory(newSearchHistory);
    setUserInput("");
    setSubmittedUserInput("");
    setClickedSave(true);
  }, [nodes, edges, searchHistory, submittedUserInput]);

  // Handle initial data and vector store changes
  useEffect(() => {
    console.log('Initial nodes:', initialNodes?.length, 'Initial edges:', initialEdges?.length);
    if (initialNodes && initialNodes.length > 0 && initialEdges) {
      console.log('Setting initial nodes and edges');
      setNodes(initialNodes);
      setEdges(initialEdges);
    } else if (selectedVectorStoreId) {
      console.log('Loading graph data for vector store:', selectedVectorStoreId);
      loadGraphData();
    } else if (vectorStores.length > 0) {
      console.log('Setting default vector store');
      setSelectedVectorStoreId(vectorStores[0].id);
    } else {
      console.log('Clearing graph data');
      setEntities([]);
      setRelationships([]);
      setGraphData({ nodes: [], links: [] });
      // Also clear ReactFlow nodes and edges
      setNodes([]);
      setEdges([]);
    }
  }, [selectedVectorStoreId, entityType, maxDepth, maxNodes, initialNodes, initialEdges]);

  useEffect(() => {
    if (searchTerm !== initialSearchQuery) {
      setSearchTerm(initialSearchQuery);
    }
  }, [initialSearchQuery]);

  const loadGraphData = async () => {
    setIsLoading(true);
    try {
      const [entitiesData, relationshipsData] = await Promise.all([
        apiService.getEntities(selectedVectorStoreId),
        apiService.getRelationships(selectedVectorStoreId)
      ]);

      // Filter by entity type if specified
      const filteredEntities = entityType === '*'
        ? entitiesData
        : entitiesData.filter(e => e.type.toLowerCase() === entityType.toLowerCase());

      setEntities(filteredEntities);
      setRelationships(relationshipsData);

      // Transform data for force graph visualization (legacy)
      const graphNodes = filteredEntities.map(entity => ({
        id: entity.id,
        name: entity.name,
        type: entity.type,
        description: entity.description,
        color: getNodeColorByType(entity.type),
        val: 1 // Size factor
      }));

      const entityIds = new Set(graphNodes.map(n => n.id));

      // Only include relationships where both source and target are in our filtered entities
      const links = relationshipsData
        .filter(rel =>
          entityIds.has(rel.sourceEntityId) &&
          entityIds.has(rel.targetEntityId)
        )
        .map(rel => ({
          source: rel.sourceEntityId,
          target: rel.targetEntityId,
          type: rel.type,
          description: rel.description,
          value: rel.weight || 1
        }));

      setGraphData({ nodes: graphNodes, links });

      // Transform data for ReactFlow
      const reactFlowNodes = filteredEntities.map((entity, index) => ({
        id: entity.id,
        data: {
          label: entity.name,
          type: entity.type,
          description: entity.description
        },
        // Position nodes in a circle layout
        position: {
          x: 250 + 200 * Math.cos(2 * Math.PI * index / filteredEntities.length),
          y: 250 + 200 * Math.sin(2 * Math.PI * index / filteredEntities.length)
        },
        style: {
          background: getNodeColorByType(entity.type),
          color: '#000000',
          border: '1px solid #222138',
          width: 180,
          borderRadius: 5,
          padding: 10
        }
      }));

      const reactFlowEdges = relationshipsData
        .filter(rel =>
          entityIds.has(rel.sourceEntityId) &&
          entityIds.has(rel.targetEntityId)
        )
        .map(rel => ({
          id: `${rel.sourceEntityId}-${rel.targetEntityId}`,
          source: rel.sourceEntityId,
          target: rel.targetEntityId,
          label: rel.type,
          style: { stroke: '#888' },
          markerEnd: {
            type: MarkerType.ArrowClosed,
            color: '#888',
          },
        })) as Edge[];

      setNodes(reactFlowNodes);
      setEdges(reactFlowEdges);
    } catch (error) {
      console.error('Failed to load graph data:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const getNodeColorByType = (type: string): string => {
    const typeColors: Record<string, string> = {
      'person': '#FF6B6B',
      'organization': '#4ECDC4',
      'location': '#FFD166',
      'concept': '#6A0572',
      'technology': '#1A535C',
      'system': '#3A86FF',
      'process': '#8338EC'
    };

    return typeColors[type?.toLowerCase()] || '#999999';
  };

  // This function is kept for backward compatibility with the ForceGraph2D implementation
  // but is not actively used with ReactFlow
  /*
  const handleNodeClick = (node: any) => {
    const entity = entities.find(e => e.id === node.id);
    if (entity) {
      setSelectedEntity(entity);
      setShowEntityDetails(true);
    }

    // Highlight connected nodes and links
    const connectedNodes = new Set<string>();
    const connectedLinks = new Set<any>();

    graphData.links.forEach(link => {
      if (link.source.id === node.id || link.target.id === node.id) {
        connectedNodes.add(link.source.id);
        connectedNodes.add(link.target.id);
        connectedLinks.add(link);
      }
    });

    setHighlightNodes(connectedNodes);
    setHighlightLinks(connectedLinks);

    // Center view on node
    if (fgRef.current) {
      fgRef.current.centerAt(node.x, node.y, 1000);
      fgRef.current.zoom(2.5, 1000);
    }
  };
  */

  // This function is kept for backward compatibility with the ForceGraph2D implementation
  // but is not actively used with ReactFlow
  /*
  const handleNodeHover = (node: any) => {
    setHoverNode(node);
    if (!node) return;

    // Highlight connected nodes and links on hover
    const connectedNodes = new Set<string>();
    const connectedLinks = new Set<any>();

    if (node) {
      connectedNodes.add(node.id);
      graphData.links.forEach(link => {
        if (link.source.id === node.id || link.target.id === node.id) {
          connectedNodes.add(link.source.id);
          connectedNodes.add(link.target.id);
          connectedLinks.add(link);
        }
      });
    }

    setHighlightNodes(connectedNodes);
    setHighlightLinks(connectedLinks);
  };
  */

  const handleSearch = async () => {
    if (!searchTerm.trim()) return;
    if (!selectedVectorStoreId) {
      // Show a message to select a vector store first
      alert('Please select a vector store first');
      return;
    }

    setIsLoading(true);
    try {
      // Generate a knowledge graph using the API with the selected vector store
      const graphNodes = await apiService.generateKnowledgeGraph(searchTerm, 20, selectedVectorStoreId);
      console.log('API response:', graphNodes);

      if (!graphNodes || graphNodes.length === 0) {
        console.warn('No graph data returned from API');
        setIsLoading(false);
        return;
      }

      // Convert the API response to ReactFlow nodes and edges
      const reactFlowNodes = graphNodes.map((node: any) => {
        // Generate random position if not provided
        const x = node.x || Math.random() * 800;
        const y = node.y || Math.random() * 600;

        console.log(`Creating node ${node.id} at position (${x}, ${y})`);

        return {
          id: node.id,
          data: {
            label: node.label,
            type: node.type || 'concept',
            description: node.description || '',
            entityId: node.id // Store the original entity ID for later reference
          },
          position: { x, y },
          style: {
            background: node.background || '#e6f7ff',
            color: '#000000',
            border: `1px solid ${node.stroke || '#1890ff'}`,
            width: 180,
            borderRadius: 5,
            padding: 10
          }
        };
      });

      const reactFlowEdges: Edge[] = [];

      // Process adjacencies (edges)
      graphNodes.forEach((node: any) => {
        if (node.adjacencies && node.adjacencies.length > 0) {
          node.adjacencies.forEach((edge: any) => {
            console.log(`Creating edge from ${edge.source} to ${edge.target}`);
            reactFlowEdges.push({
              id: edge.id || `${edge.source}-${edge.target}`,
              source: edge.source,
              target: edge.target,
              label: edge.label,
              data: {
                description: edge.description || '',
                type: edge.label || 'related to'
              },
              style: { stroke: edge.color || '#888' },
              markerEnd: {
                type: MarkerType.ArrowClosed,
                color: edge.color || '#888',
              },
            });
          });
        }
      });

      console.log('Setting nodes:', reactFlowNodes.length, 'and edges:', reactFlowEdges.length);
      setNodes(reactFlowNodes);
      setEdges(reactFlowEdges);

      // Set the submitted user input for saving to history
      setSubmittedUserInput(searchTerm);
      setClickedSave(false);

      // Also fetch the entities and relationships for the selected vector store
      // to have them available for node selection
      try {
        const [entitiesData, relationshipsData] = await Promise.all([
          apiService.getEntities(selectedVectorStoreId),
          apiService.getRelationships(selectedVectorStoreId)
        ]);
        setEntities(entitiesData);
        setRelationships(relationshipsData);
      } catch (error) {
        console.error('Error fetching entities and relationships:', error);
      }

      // Force a re-render and center the graph after a short delay
      setTimeout(() => {
        if (reactFlowInstance) {
          console.log('Forcing graph to center');
          reactFlowInstance.fitView({ padding: 0.2 });
        }
      }, 500);
    } catch (error) {
      console.error('Error generating knowledge graph:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleZoomIn = () => {
    if (reactFlowInstance) {
      reactFlowInstance.zoomIn({ duration: 800 });
    }
  };

  const handleZoomOut = () => {
    if (reactFlowInstance) {
      reactFlowInstance.zoomOut({ duration: 800 });
    }
  };

  const handleRefresh = () => {
    loadGraphData();
    setSelectedEntity(null);
  };

  // Handle node click to show entity details
  const handleNodeClick = useCallback((_: React.MouseEvent, node: Node) => {
    // Find the entity in our entities list
    const entityId = node.data.entityId || node.id;
    const entity = entities.find(e => e.id === entityId);

    if (entity) {
      // Fetch text chunks related to this entity
      setIsLoading(true);
      apiService.getEntityTextChunks(entityId)
        .then(textChunks => {
          // Update the entity with text chunks
          setSelectedEntity({
            ...entity,
            textChunks: textChunks
          });
          setShowEntityDetails(true);
          setIsLoading(false);
        })
        .catch(error => {
          console.error('Error fetching text chunks:', error);
          setSelectedEntity(entity);
          setShowEntityDetails(true);
          setIsLoading(false);
        });

      // Highlight connected nodes and edges
      const connectedNodes = new Set<string>([entityId]);
      const connectedEdges = new Set<string>();

      // Find all relationships connected to this entity
      const connectedRelationships = relationships.filter(r =>
        r.sourceEntityId === entityId || r.targetEntityId === entityId
      );

      // Add connected entities to the highlight set
      connectedRelationships.forEach(rel => {
        if (rel.sourceEntityId === entityId) {
          connectedNodes.add(rel.targetEntityId);
        } else {
          connectedNodes.add(rel.sourceEntityId);
        }

        // Find the edge ID in our edges list
        const edge = edges.find(e =>
          (e.source === rel.sourceEntityId && e.target === rel.targetEntityId) ||
          (e.source === rel.targetEntityId && e.target === rel.sourceEntityId)
        );

        if (edge) {
          connectedEdges.add(edge.id);
        }
      });

      // Update node styles to highlight connected nodes
      const updatedNodes = nodes.map(n => {
        if (connectedNodes.has(n.id)) {
          return {
            ...n,
            style: {
              ...n.style,
              borderWidth: 3,
              borderColor: '#ff0072',
              boxShadow: '0 0 10px #ff0072'
            }
          };
        }
        return {
          ...n,
          style: {
            ...n.style,
            borderWidth: 1,
            borderColor: '#222138',
            boxShadow: 'none'
          }
        };
      });

      // Update edge styles to highlight connected edges
      const updatedEdges = edges.map(e => {
        if (connectedEdges.has(e.id)) {
          return {
            ...e,
            style: { ...e.style, stroke: '#ff0072', strokeWidth: 3 },
            markerEnd: {
              type: MarkerType.ArrowClosed,
              color: '#ff0072'
            }
          };
        }
        return {
          ...e,
          style: { ...e.style, stroke: '#888', strokeWidth: 1 },
          markerEnd: {
            type: MarkerType.ArrowClosed,
            color: '#888'
          }
        };
      }) as Edge[];

      setNodes(updatedNodes);
      setEdges(updatedEdges);
    }
  }, [entities, relationships, nodes, edges]);

  return (
    <div className="h-full flex flex-col">
      <div className="p-4 border-b border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800">
        <div className="flex items-center mb-4">
          <SelectPicker
            data={vectorStores.map(vs => ({
              label: vs.name,
              value: vs.id
            }))}
            value={selectedVectorStoreId}
            onChange={(value, _event) => {
              if (value) setSelectedVectorStoreId(value);
            }}
            placeholder="Select a vector store"
            className="w-64 mr-4"
            searchable={false}
            cleanable={false}
          />

          <div className="relative flex-grow">
            <InputGroup inside>
              <Input
                placeholder="Search entities and relationships..."
                value={searchTerm}
                onChange={setSearchTerm}
                onPressEnter={handleSearch}
              />
              <InputGroup.Button onClick={handleSearch}>
                <FaSearch />
              </InputGroup.Button>
            </InputGroup>
          </div>
        </div>

        <div className="flex justify-between items-center">
          <div className="text-sm text-gray-500">
            <FaInfoCircle className="mr-1" />
            Click on an entity to view details
          </div>
          <div className="flex gap-2">
            <Button appearance="primary" onClick={handleSaveToHistory} size="sm" disabled={nodes.length <= 1 || clickedSave}>
              <FaSave className="mr-1" /> Save
            </Button>
            <Button appearance="primary" onClick={() => setShowHistorySidebar(true)} size="sm">
              <FaHistory className="mr-1" /> History
            </Button>
          </div>
        </div>
      </div>

      <div className="flex-grow relative">
        {isLoading ? (
          <Loader center content="Loading knowledge graph..." />
        ) : (
          <div className="flex h-full">
            {/* History Sidebar */}
            {showHistorySidebar && (
              <div className="w-1/4 h-full overflow-auto border-r border-gray-200 dark:border-gray-700">
                <GraphHistory
                  searchHistory={searchHistory}
                  onHistorySelect={(historyItem) => {
                    setNodes(historyItem.results.nodes);
                    setEdges(historyItem.results.edges);
                    setClickedSave(true);
                  }}
                  onHistoryDelete={(index) => {
                    const newHistory = [...searchHistory];
                    newHistory.splice(index, 1);
                    setSearchHistory(newHistory);
                    saveGraphHistory(newHistory);
                  }}
                />
              </div>
            )}

            {/* ReactFlow Graph */}
            <div className={`${showHistorySidebar ? 'w-3/4' : 'w-full'} h-full`}>
              <ReactFlowProvider>
                <div className="h-full w-full" ref={reactFlowWrapper}>
                  <ReactFlow
                    nodes={nodes}
                    edges={edges}
                    onNodesChange={onNodesChange}
                    onEdgesChange={onEdgesChange}
                    onConnect={onConnect}
                    onInit={onInit}
                    onNodeClick={handleNodeClick}
                    fitView
                    attributionPosition="bottom-right"
                  >
                    <DownloadButton disabled={nodes.length <= 1} />
                    <Controls />
                    <MiniMap nodeStrokeWidth={3} zoomable pannable />
                    <Background variant={BackgroundVariant.Dots} gap={12} size={1} />
                  </ReactFlow>
                </div>
              </ReactFlowProvider>

              {/* Controls */}
              <div className="absolute bottom-4 left-4 flex flex-col gap-2">
                <Button appearance="primary" onClick={handleZoomIn} size="sm">
                  <FaSearchPlus />
                </Button>
                <Button appearance="primary" onClick={handleZoomOut} size="sm">
                  <FaSearchMinus />
                </Button>
                <Button appearance="primary" onClick={handleRefresh} size="sm">
                  <FaSync />
                </Button>
                <Button
                  appearance="primary"
                  onClick={() => setShowHistorySidebar(!showHistorySidebar)}
                  size="sm"
                >
                  <FaHistory />
                </Button>
              </div>
            </div>
          </div>
        )}
      </div>

      <Modal open={showEntityDetails} onClose={() => setShowEntityDetails(false)} size="lg">
        <Modal.Header>
          <Modal.Title>{selectedEntity?.name}</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {selectedEntity && (
            <Form fluid>
              <Form.Group>
                <Form.ControlLabel>Type</Form.ControlLabel>
                <Form.Control
                  name="entityType"
                  readOnly
                  value={selectedEntity.type}
                />
              </Form.Group>

              <Form.Group>
                <Form.ControlLabel>Description</Form.ControlLabel>
                <Form.Control
                  name="entityDescription"
                  readOnly
                  value={selectedEntity.description}
                  as="textarea"
                  rows={3}
                />
              </Form.Group>

              {selectedEntity.keywords && selectedEntity.keywords.length > 0 && (
                <Form.Group>
                  <Form.ControlLabel>Keywords</Form.ControlLabel>
                  <div className="flex flex-wrap gap-2">
                    {selectedEntity.keywords.map((keyword, index) => (
                      <span key={index} className="px-2 py-1 bg-green-100 text-green-800 rounded-full text-sm">
                        {keyword}
                      </span>
                    ))}
                  </div>
                </Form.Group>
              )}

              {selectedEntity.textChunks && selectedEntity.textChunks.length > 0 && (
                <Form.Group>
                  <Form.ControlLabel>Source Text</Form.ControlLabel>
                  <div className="mt-2 max-h-60 overflow-y-auto border border-gray-200 rounded p-3 bg-gray-50">
                    {selectedEntity.textChunks.map((chunk, index) => (
                      <div key={index} className="mb-2 pb-2 border-b border-gray-200 last:border-0">
                        <p className="text-sm">{chunk.content}</p>
                        <p className="text-xs text-gray-500 mt-1">Source: {chunk.fullDocumentId || 'Unknown'}</p>
                      </div>
                    ))}
                  </div>
                </Form.Group>
              )}

              <Form.Group>
                <Form.ControlLabel>Related Entities</Form.ControlLabel>
                <ul className="list-disc pl-5">
                  {relationships
                    .filter(r =>
                      r.sourceEntityId === selectedEntity.id ||
                      r.targetEntityId === selectedEntity.id
                    )
                    .map(r => {
                      const isSource = r.sourceEntityId === selectedEntity.id;
                      const relatedEntityId = isSource ? r.targetEntityId : r.sourceEntityId;
                      const relatedEntity = entities.find(e => e.id === relatedEntityId);

                      return (
                        <li key={r.id}>
                          {isSource ? 'Has' : 'Is'} <strong>{r.type}</strong> {isSource ? 'to' : 'of'}{' '}
                          <strong>{relatedEntity?.name || 'Unknown Entity'}</strong>
                          {r.description && `: ${r.description}`}
                        </li>
                      );
                    })}
                </ul>
              </Form.Group>
            </Form>
          )}
        </Modal.Body>
        <Modal.Footer>
          <Button onClick={() => setShowEntityDetails(false)} appearance="primary">
            Close
          </Button>
        </Modal.Footer>
      </Modal>
    </div>
  );
};

export default KnowledgeGraphViewer;



