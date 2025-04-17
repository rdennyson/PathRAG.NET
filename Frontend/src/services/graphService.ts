import axios from 'axios';
import { GraphEntity, Relationship } from '../models/types';

const API_URL = `${import.meta.env.VITE_API_BASE_URL}/api` || 'http://localhost:3000/api';

interface GraphData {
  entities: GraphEntity[];
  relationships: Relationship[];
}

interface PageRankResult {
  [key: string]: number;
}

interface Node2VecResult {
  [key: string]: number[];
}

/**
 * Fetches graph data from the backend
 * @param {string} label - Optional label to filter entities by type
 * @param {number} maxDepth - Maximum depth of relationships to fetch
 * @param {number} maxNodes - Maximum number of nodes to fetch
 * @returns {Promise<GraphData>} - Graph data with entities and relationships
 */
export const fetchGraphData = async (label = '*', maxDepth = 2, maxNodes = 100): Promise<GraphData> => {
  try {
    const response = await axios.get<GraphData>(`${API_URL}/graph`, {
      params: {
        label,
        maxDepth,
        maxNodes
      }
    });
    return response.data;
  } catch (error) {
    console.error('Error fetching graph data:', error);
    throw error;
  }
};

/**
 * Fetches all available entity labels/types from the backend
 * @returns {Promise<string[]>} - Array of entity labels/types
 */
export const fetchGraphLabels = async (): Promise<string[]> => {
  try {
    const response = await axios.get<string[]>(`${API_URL}/graph/labels`);
    return response.data;
  } catch (error) {
    console.error('Error fetching graph labels:', error);
    throw error;
  }
};

/**
 * Fetches details for a specific entity
 * @param {string} entityId - ID of the entity to fetch
 * @returns {Promise<GraphEntity>} - Entity details
 */
export const fetchEntityDetails = async (entityId: string): Promise<GraphEntity> => {
  try {
    const response = await axios.get<GraphEntity>(`${API_URL}/graph/entity/${entityId}`);
    return response.data;
  } catch (error) {
    console.error(`Error fetching entity details for ${entityId}:`, error);
    throw error;
  }
};

/**
 * Fetches relationships for a specific entity
 * @param {string} entityId - ID of the entity to fetch relationships for
 * @returns {Promise<Relationship[]>} - Array of relationships
 */
export const fetchEntityRelationships = async (entityId: string): Promise<Relationship[]> => {
  try {
    const response = await axios.get<Relationship[]>(`${API_URL}/graph/entity/${entityId}/relationships`);
    return response.data;
  } catch (error) {
    console.error(`Error fetching relationships for entity ${entityId}:`, error);
    throw error;
  }
};

/**
 * Updates an entity's properties
 * @param {string} entityId - ID of the entity to update
 * @param {Partial<GraphEntity>} data - Updated entity data
 * @returns {Promise<GraphEntity>} - Updated entity
 */
export const updateEntity = async (entityId: string, data: Partial<GraphEntity>): Promise<GraphEntity> => {
  try {
    const response = await axios.put<GraphEntity>(`${API_URL}/graph/entity/${entityId}`, data);
    return response.data;
  } catch (error) {
    console.error(`Error updating entity ${entityId}:`, error);
    throw error;
  }
};

/**
 * Updates a relationship's properties
 * @param {string} relationshipId - ID of the relationship to update
 * @param {Partial<Relationship>} data - Updated relationship data
 * @returns {Promise<Relationship>} - Updated relationship
 */
export const updateRelationship = async (relationshipId: string, data: Partial<Relationship>): Promise<Relationship> => {
  try {
    const response = await axios.put<Relationship>(`${API_URL}/graph/relationship/${relationshipId}`, data);
    return response.data;
  } catch (error) {
    console.error(`Error updating relationship ${relationshipId}:`, error);
    throw error;
  }
};

/**
 * Runs a PageRank algorithm on the graph
 * @returns {Promise<PageRankResult>} - PageRank results for each node
 */
export const runPageRank = async (): Promise<PageRankResult> => {
  try {
    const response = await axios.post<PageRankResult>(`${API_URL}/graph/pagerank`);
    return response.data;
  } catch (error) {
    console.error('Error running PageRank:', error);
    throw error;
  }
};

/**
 * Runs a Node2Vec embedding algorithm on the graph
 * @returns {Promise<Node2VecResult>} - Node2Vec embeddings for each node
 */
export const runNode2Vec = async (): Promise<Node2VecResult> => {
  try {
    const response = await axios.post<Node2VecResult>(`${API_URL}/graph/node2vec`);
    return response.data;
  } catch (error) {
    console.error('Error running Node2Vec:', error);
    throw error;
  }
};

