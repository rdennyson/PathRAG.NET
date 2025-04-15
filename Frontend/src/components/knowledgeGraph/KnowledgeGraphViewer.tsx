import React, { useEffect, useRef, useState } from 'react';
import { Panel, Loader, SelectPicker, Button, Modal, Form, Input } from 'rsuite';
import { FaSearch, FaInfoCircle } from 'react-icons/fa';
import { useApp } from '../../contexts/AppContext';
import { GraphEntity, Relationship } from '../../models/types';
import apiService from '../../services/api';

// This would be replaced with a proper graph visualization library like Cytoscape.js or vis.js
// For now, we'll create a simplified version
const KnowledgeGraphViewer: React.FC = () => {
  const { vectorStores } = useApp();
  const [selectedVectorStoreId, setSelectedVectorStoreId] = useState<string>('');
  const [entities, setEntities] = useState<GraphEntity[]>([]);
  const [relationships, setRelationships] = useState<Relationship[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [searchTerm, setSearchTerm] = useState<string>('');
  const [selectedEntity, setSelectedEntity] = useState<GraphEntity | null>(null);
  const [showEntityDetails, setShowEntityDetails] = useState<boolean>(false);
  
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    if (selectedVectorStoreId) {
      loadGraphData();
    } else {
      setEntities([]);
      setRelationships([]);
    }
  }, [selectedVectorStoreId]);

  useEffect(() => {
    if (entities.length > 0 && relationships.length > 0) {
      renderGraph();
    }
  }, [entities, relationships, searchTerm]);

  const loadGraphData = async () => {
    setIsLoading(true);
    try {
      const [entitiesData, relationshipsData] = await Promise.all([
        apiService.getEntities(selectedVectorStoreId),
        apiService.getRelationships(selectedVectorStoreId)
      ]);
      
      setEntities(entitiesData);
      setRelationships(relationshipsData);
    } catch (error) {
      console.error('Failed to load graph data:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const renderGraph = () => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    // Clear canvas
    ctx.clearRect(0, 0, canvas.width, canvas.height);

    // Filter entities and relationships based on search term
    const filteredEntities = searchTerm
      ? entities.filter(e => 
          e.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
          e.description.toLowerCase().includes(searchTerm.toLowerCase())
        )
      : entities;

    const filteredRelationships = searchTerm
      ? relationships.filter(r => {
          const sourceEntity = entities.find(e => e.id === r.sourceEntityId);
          const targetEntity = entities.find(e => e.id === r.targetEntityId);
          
          return (
            sourceEntity && targetEntity && (
              sourceEntity.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
              targetEntity.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
              r.type.toLowerCase().includes(searchTerm.toLowerCase()) ||
              r.description.toLowerCase().includes(searchTerm.toLowerCase())
            )
          );
        })
      : relationships;

    // Create a map of entity positions
    const entityPositions = new Map<string, { x: number, y: number }>();
    
    // Position entities in a circle
    const centerX = canvas.width / 2;
    const centerY = canvas.height / 2;
    const radius = Math.min(centerX, centerY) - 50;
    
    filteredEntities.forEach((entity, index) => {
      const angle = (index / filteredEntities.length) * 2 * Math.PI;
      const x = centerX + radius * Math.cos(angle);
      const y = centerY + radius * Math.sin(angle);
      
      entityPositions.set(entity.id, { x, y });
    });

    // Draw relationships
    ctx.strokeStyle = '#673ab7';
    ctx.lineWidth = 2;
    
    filteredRelationships.forEach(relationship => {
      const sourcePos = entityPositions.get(relationship.sourceEntityId);
      const targetPos = entityPositions.get(relationship.targetEntityId);
      
      if (sourcePos && targetPos) {
        // Draw line
        ctx.beginPath();
        ctx.moveTo(sourcePos.x, sourcePos.y);
        ctx.lineTo(targetPos.x, targetPos.y);
        ctx.stroke();
        
        // Draw relationship type
        const midX = (sourcePos.x + targetPos.x) / 2;
        const midY = (sourcePos.y + targetPos.y) / 2;
        
        ctx.fillStyle = '#673ab7';
        ctx.font = '10px Arial';
        ctx.fillText(relationship.type, midX, midY);
      }
    });

    // Draw entities
    filteredEntities.forEach(entity => {
      const pos = entityPositions.get(entity.id);
      if (!pos) return;
      
      // Draw circle
      ctx.beginPath();
      ctx.arc(pos.x, pos.y, 20, 0, 2 * Math.PI);
      
      // Color based on entity type
      switch (entity.type.toLowerCase()) {
        case 'person':
          ctx.fillStyle = '#3498ff';
          break;
        case 'organization':
          ctx.fillStyle = '#ff5733';
          break;
        case 'location':
          ctx.fillStyle = '#27ae60';
          break;
        case 'concept':
          ctx.fillStyle = '#f1c40f';
          break;
        default:
          ctx.fillStyle = '#95a5a6';
      }
      
      ctx.fill();
      
      // Draw entity name
      ctx.fillStyle = '#ffffff';
      ctx.font = 'bold 10px Arial';
      ctx.textAlign = 'center';
      ctx.fillText(entity.name, pos.x, pos.y);
    });

    // Add click handler to canvas
    canvas.onclick = (event) => {
      const rect = canvas.getBoundingClientRect();
      const x = event.clientX - rect.left;
      const y = event.clientY - rect.top;
      
      // Check if click is on an entity
      for (const entity of filteredEntities) {
        const pos = entityPositions.get(entity.id);
        if (!pos) continue;
        
        const distance = Math.sqrt(Math.pow(x - pos.x, 2) + Math.pow(y - pos.y, 2));
        if (distance <= 20) {
          setSelectedEntity(entity);
          setShowEntityDetails(true);
          break;
        }
      }
    };
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
            onChange={setSelectedVectorStoreId}
            placeholder="Select a vector store"
            className="w-64 mr-4"
            searchable={false}
          />
          
          <div className="relative flex-grow">
            <Input
              placeholder="Search entities and relationships..."
              value={searchTerm}
              onChange={setSearchTerm}
              className="pr-10"
            />
            <FaSearch className="absolute right-3 top-1/2 transform -translate-y-1/2 text-gray-400" />
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
          <canvas
            ref={canvasRef}
            width={800}
            height={600}
            className="w-full h-full"
          />
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
                <Form.Control readOnly value={selectedEntity.type} />
              </Form.Group>
              
              <Form.Group>
                <Form.ControlLabel>Description</Form.ControlLabel>
                <Form.Control
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
