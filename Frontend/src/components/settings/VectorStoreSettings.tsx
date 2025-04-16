import React, { useState, useRef } from 'react';
import { 
  Form, 
  Button, 
  Panel, 
  SelectPicker, 
  Loader, 
  Message, 
  toaster,
  Uploader,
  Table,
  IconButton
} from 'rsuite';
import { FaTrash, FaUpload } from 'react-icons/fa';
import { useApp } from '../../contexts/AppContext';
import apiService from '../../services/api';
import { Document } from '../../models/types';

const { Column, HeaderCell, Cell } = Table;

const VectorStoreSettings: React.FC = () => {
  const { 
    vectorStores, 
    createVectorStore, 
    deleteVectorStore, 
    isLoading 
  } = useApp();

  const [selectedVectorStoreId, setSelectedVectorStoreId] = useState<string>('');
  const [newVectorStoreName, setNewVectorStoreName] = useState<string>('');
  const [documents, setDocuments] = useState<Document[]>([]);
  const [isUploading, setIsUploading] = useState<boolean>(false);
  const [isLoadingDocuments, setIsLoadingDocuments] = useState<boolean>(false);
  
  const uploaderRef = useRef<any>(null);

  const handleSelectVectorStore = async (vectorStoreId: string) => {
    setSelectedVectorStoreId(vectorStoreId);
    
    if (vectorStoreId) {
      setIsLoadingDocuments(true);
      try {
        const docs = await apiService.getDocuments(vectorStoreId);
        setDocuments(docs);
      } catch (error) {
        toaster.push(<Message type="error">Failed to load documents</Message>);
      } finally {
        setIsLoadingDocuments(false);
      }
    } else {
      setDocuments([]);
    }
  };

  const handleCreateVectorStore = async () => {
    if (!newVectorStoreName) {
      toaster.push(<Message type="error">Vector store name is required</Message>);
      return;
    }

    try {
      const newVectorStore = await createVectorStore(newVectorStoreName);
      setSelectedVectorStoreId(newVectorStore.id);
      setNewVectorStoreName('');
      toaster.push(<Message type="success">Vector store created successfully</Message>);
    } catch (error) {
      toaster.push(<Message type="error">Failed to create vector store</Message>);
    }
  };

  const handleDeleteVectorStore = async () => {
    if (!selectedVectorStoreId) return;

    try {
      await deleteVectorStore(selectedVectorStoreId);
      setSelectedVectorStoreId('');
      setDocuments([]);
      toaster.push(<Message type="success">Vector store deleted successfully</Message>);
    } catch (error) {
      toaster.push(<Message type="error">Failed to delete vector store</Message>);
    }
  };

  const handleUpload = async (file: File) => {
    if (!selectedVectorStoreId) {
      toaster.push(<Message type="error">Please select a vector store first</Message>);
      return;
    }

    setIsUploading(true);
    try {
      const newDocument = await apiService.uploadDocument(selectedVectorStoreId, file);
      setDocuments([...documents, newDocument]);
      toaster.push(<Message type="success">Document uploaded successfully</Message>);
    } catch (error) {
      toaster.push(<Message type="error">Failed to upload document</Message>);
    } finally {
      setIsUploading(false);
      if (uploaderRef.current) {
        uploaderRef.current.clearFiles();
      }
    }
  };

  const handleDeleteDocument = async (documentId: string) => {
    if (!selectedVectorStoreId) return;

    try {
      await apiService.deleteDocument(selectedVectorStoreId, documentId);
      setDocuments(documents.filter(doc => doc.id !== documentId));
      toaster.push(<Message type="success">Document deleted successfully</Message>);
    } catch (error) {
      toaster.push(<Message type="error">Failed to delete document</Message>);
    }
  };

  return (
    <Panel header="Vector Store Settings" bordered>
      {isLoading ? (
        <Loader center content="Loading..." />
      ) : (
        <>
          <Form fluid>
            <Form.Group>
              <Form.ControlLabel>Select Vector Store</Form.ControlLabel>
              <SelectPicker
                data={vectorStores.map(vs => ({
                  label: vs.name,
                  value: vs.id
                }))}
                value={selectedVectorStoreId}
                onChange={(value, _event) => {
                  if (value) handleSelectVectorStore(value);
                }}
                block
                placeholder="Select a vector store"
                searchable={false}
              />
            </Form.Group>

            <Form.Group>
              <Form.ControlLabel>Create New Vector Store</Form.ControlLabel>
              <div className="flex">
                <Form.Control
                  name="newVectorStoreName"
                  placeholder="Enter vector store name"
                  value={newVectorStoreName}
                  onChange={setNewVectorStoreName}
                  className="flex-grow"
                />
                <Button appearance="primary" onClick={handleCreateVectorStore} className="ml-2">
                  Create
                </Button>
              </div>
            </Form.Group>

            {selectedVectorStoreId && (
              <>
                <Form.Group>
                  <Form.ControlLabel>Upload Documents</Form.ControlLabel>
                  <Uploader
                    ref={uploaderRef}
                    action=""
                    autoUpload={false}
                    multiple
                    draggable
                    fileListVisible={false}
                    onChange={fileList => {
                      const file = fileList[fileList.length - 1]?.blobFile;
                      if (file) {
                        handleUpload(file);
                      }
                    }}
                  >
                    <div className="border-2 border-dashed border-gray-300 p-4 text-center rounded">
                      <FaUpload className="text-3xl mx-auto mb-2" />
                      <p>Click or drag files to this area to upload</p>
                    </div>
                  </Uploader>
                  {isUploading && <Loader content="Uploading..." />}
                </Form.Group>

                <Form.Group>
                  <Form.ControlLabel>Documents</Form.ControlLabel>
                  {isLoadingDocuments ? (
                    <Loader content="Loading documents..." />
                  ) : (
                    <Table
                      height={400}
                      data={documents}
                      autoHeight
                      bordered
                      cellBordered
                      loading={isLoadingDocuments}
                    >
                      <Column width={200} align="center">
                        <HeaderCell>Name</HeaderCell>
                        <Cell dataKey="name" />
                      </Column>
                      <Column width={100} align="center">
                        <HeaderCell>Type</HeaderCell>
                        <Cell dataKey="type" />
                      </Column>
                      <Column width={100} align="center">
                        <HeaderCell>Size (KB)</HeaderCell>
                        <Cell>
                          {rowData => Math.round(rowData.size / 1024)}
                        </Cell>
                      </Column>
                      <Column width={150} align="center">
                        <HeaderCell>Uploaded At</HeaderCell>
                        <Cell>
                          {rowData => new Date(rowData.uploadedAt).toLocaleDateString()}
                        </Cell>
                      </Column>
                      <Column width={80} align="center" fixed="right">
                        <HeaderCell>Action</HeaderCell>
                        <Cell>
                          {rowData => (
                            <IconButton
                              icon={<FaTrash />}
                              size="xs"
                              appearance="subtle"
                              color="red"
                              onClick={() => handleDeleteDocument(rowData.id)}
                            />
                          )}
                        </Cell>
                      </Column>
                    </Table>
                  )}
                </Form.Group>

                <Form.Group>
                  <Button appearance="subtle" color="red" onClick={handleDeleteVectorStore}>
                    Delete Vector Store
                  </Button>
                </Form.Group>
              </>
            )}
          </Form>
        </>
      )}
    </Panel>
  );
};

export default VectorStoreSettings;

