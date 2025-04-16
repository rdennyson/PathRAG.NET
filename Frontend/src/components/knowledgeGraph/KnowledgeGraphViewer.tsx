import React, { useEffect, useRef, useState } from 'react';
import { Loader, SelectPicker, Button, Modal, Form, Input, InputGroup } from 'rsuite';
import { ForceGraph2D } from 'react-force-graph';
import { FaSearch, FaInfoCircle, FaSearchPlus, FaSearchMinus, FaSync, FaCog } from 'react-icons/fa';
import { useApp } from '../../contexts/AppContext';
import { GraphEntity, Relationship } from '../../models/types';
import apiService from '../../services/api';

interface KnowledgeGraphViewerProps {
  entityType?: string;
  maxDepth?: number;
  maxNodes?: number;
  searchQuery?: string;
}

const KnowledgeGraphViewer: React.FC<KnowledgeGraphViewerProps> = ({
  entityType = '*',
  maxDepth = 2,
  maxNodes = 100,
  searchQuery: initialSearchQuery = ''
}) => {
  const { vectorStores } = useApp();
  const [selectedVectorStoreId, setSelectedVectorStoreId] = useState<string>('');
  const [entities, setEntities] = useState<GraphEntity[]>([]);
  const [relationships, setRelationships] = useState<Relationship[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [searchTerm, setSearchTerm] = useState<string>(initialSearchQuery);
  const [selectedEntity, setSelectedEntity] = useState<GraphEntity | null>(null);
  const [showEntityDetails, setShowEntityDetails] = useState<boolean>(false);

  // Force Graph specific states
  const [graphData, setGraphData] = useState<{ nodes: any[], links: any[] }>({ nodes: [], links: [] });
  const [highlightNodes, setHighlightNodes] = useState<Set<string>>(new Set());
  const [highlightLinks, setHighlightLinks] = useState<Set<any>>(new Set());
  const [, setHoverNode] = useState<any>(null);

  const fgRef = useRef<any>();

  useEffect(() => {
    if (selectedVectorStoreId) {
      loadGraphData();
    } else if (vectorStores.length > 0) {
      setSelectedVectorStoreId(vectorStores[0].id);
    } else {
      setEntities([]);
      setRelationships([]);
      setGraphData({ nodes: [], links: [] });
    }
  }, [selectedVectorStoreId, entityType, maxDepth, maxNodes]);

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

      // Transform data for force graph visualization
      const nodes = filteredEntities.map(entity => ({
        id: entity.id,
        name: entity.name,
        type: entity.type,
        description: entity.description,
        color: getNodeColorByType(entity.type),
        val: 1 // Size factor
      }));

      const entityIds = new Set(nodes.map(n => n.id));

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

      setGraphData({ nodes, links });
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

  const handleSearch = () => {
    if (!searchTerm) return;

    const foundNode = graphData.nodes.find(node =>
      node.name.toLowerCase().includes(searchTerm.toLowerCase())
    );

    if (foundNode) {
      handleNodeClick(foundNode);
    }
  };

  const handleZoomIn = () => {
    if (fgRef.current) {
      fgRef.current.zoom(fgRef.current.zoom() * 1.5, 800);
    }
  };

  const handleZoomOut = () => {
    if (fgRef.current) {
      fgRef.current.zoom(fgRef.current.zoom() / 1.5, 800);
    }
  };

  const handleRefresh = () => {
    loadGraphData();
    setSelectedEntity(null);
    setHighlightNodes(new Set());
    setHighlightLinks(new Set());
  };

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

        <div className="text-sm text-gray-500">
          <FaInfoCircle className="mr-1" />
          Click on an entity to view details
        </div>
      </div>

      <div className="flex-grow relative">
        {isLoading ? (
          <Loader center content="Loading knowledge graph..." />
        ) : (
          <>
            <ForceGraph2D
              ref={fgRef}
              graphData={graphData}
              nodeLabel={node => `${node.name} (${node.type})`}
              nodeColor={node =>
                highlightNodes.size > 0
                  ? highlightNodes.has(node.id)
                    ? node.color
                    : 'rgba(200,200,200,0.3)'
                  : node.color
              }
              linkWidth={link => highlightLinks.has(link) ? 3 : 1}
              linkColor={link => highlightLinks.has(link) ? '#ff5722' : '#999999'}
              nodeCanvasObject={(node, ctx, globalScale) => {
                const label = node.name;
                const fontSize = 12/globalScale;
                ctx.font = `${fontSize}px Sans-Serif`;
                const textWidth = ctx.measureText(label).width;
                const bckgDimensions = [textWidth, fontSize].map(n => n + fontSize * 0.2);

                // Node circle
                ctx.beginPath();
                ctx.arc(node.x, node.y, 5, 0, 2 * Math.PI, false);
                ctx.fillStyle = node.color;
                ctx.fill();

                // Text background
                ctx.fillStyle = 'rgba(255, 255, 255, 0.8)';
                ctx.fillRect(
                  node.x - bckgDimensions[0] / 2,
                  node.y + 8,
                  bckgDimensions[0],
                  bckgDimensions[1]
                );

                // Text
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.fillStyle = '#000000';
                ctx.fillText(label, node.x, node.y + 8 + fontSize / 2);
              }}
              onNodeClick={handleNodeClick}
              onNodeHover={handleNodeHover}
              cooldownTicks={100}
              linkDirectionalArrowLength={3.5}
              linkDirectionalArrowRelPos={1}
              linkCurvature={0.25}
            />

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
              <Button appearance="primary" size="sm">
                <FaCog />
              </Button>
            </div>
          </>
        )}
      </div>

      <Modal open={showEntityDetails} onClose={() => setShowEntityDetails(false)}>
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



