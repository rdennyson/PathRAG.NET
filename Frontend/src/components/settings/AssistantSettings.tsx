import React, { useState } from 'react';
import { 
  Form, 
  Button, 
  Input, 
  InputNumber, 
  Panel, 
  SelectPicker, 
  TagPicker, 
  Loader, 
  Message, 
  toaster 
} from 'rsuite';
import { useApp } from '../../contexts/AppContext';
import { Assistant } from '../../models/types';

const AssistantSettings: React.FC = () => {
  const { 
    assistants, 
    vectorStores, 
    createAssistant, 
    updateAssistant, 
    deleteAssistant, 
    isLoading 
  } = useApp();

  const [selectedAssistantId, setSelectedAssistantId] = useState<string>('');
  const [formValue, setFormValue] = useState<Partial<Assistant>>({
    name: '',
    message: '',
    temperature: 0.7,
    vectorStoreIds: []
  });
  const [formError, setFormError] = useState<Record<string, string>>({});

  const handleSelectAssistant = (assistantId: string) => {
    if (!assistantId) {
      setFormValue({
        name: '',
        message: '',
        temperature: 0.7,
        vectorStoreIds: []
      });
      setSelectedAssistantId('');
      return;
    }

    const assistant = assistants.find(a => a.id === assistantId);
    if (assistant) {
      setFormValue({
        name: assistant.name,
        message: assistant.message,
        temperature: assistant.temperature,
        vectorStoreIds: assistant.vectorStoreIds
      });
      setSelectedAssistantId(assistantId);
    }
  };

  const handleSubmit = async () => {
    if (!formValue.name || !formValue.message) {
      setFormError({
        name: !formValue.name ? 'Name is required' : '',
        message: !formValue.message ? 'Message is required' : ''
      });
      return;
    }

    try {
      if (selectedAssistantId) {
        // Update existing assistant
        await updateAssistant(selectedAssistantId, formValue);
        toaster.push(<Message type="success">Assistant updated successfully</Message>);
      } else {
        // Create new assistant
        const newAssistant = await createAssistant(formValue as Omit<Assistant, 'id'>);
        setSelectedAssistantId(newAssistant.id);
        toaster.push(<Message type="success">Assistant created successfully</Message>);
      }
    } catch (error) {
      toaster.push(<Message type="error">Failed to save assistant</Message>);
    }
  };

  const handleDelete = async () => {
    if (!selectedAssistantId) return;

    try {
      await deleteAssistant(selectedAssistantId);
      setSelectedAssistantId('');
      setFormValue({
        name: '',
        message: '',
        temperature: 0.7,
        vectorStoreIds: []
      });
      toaster.push(<Message type="success">Assistant deleted successfully</Message>);
    } catch (error) {
      toaster.push(<Message type="error">Failed to delete assistant</Message>);
    }
  };

  return (
    <Panel header="Assistant Settings" bordered>
      {isLoading ? (
        <Loader center content="Loading..." />
      ) : (
        <>
          <Form fluid formValue={formValue} onChange={setFormValue} formError={formError}>
            <Form.Group>
              <Form.ControlLabel>Select Assistant</Form.ControlLabel>
              <SelectPicker
                data={assistants.map(a => ({
                  label: a.name,
                  value: a.id
                }))}
                value={selectedAssistantId}
                onChange={(value, _event) => {
                  if (value) handleSelectAssistant(value);
                }}
                block
                cleanable={false}
              />
            </Form.Group>

            <Form.Group>
              <Form.ControlLabel>Name</Form.ControlLabel>
              <Form.Control
                name="name"
                placeholder="Enter assistant name"
                errorMessage={formError.name}
                errorPlacement="bottomStart"
              />
            </Form.Group>

            <Form.Group>
              <Form.ControlLabel>System Message</Form.ControlLabel>
              <Input
                as="textarea"
                name="message"
                rows={5}
                placeholder="Enter system message for the assistant"
                value={formValue.message}
                onChange={value => setFormValue({ ...formValue, message: value })}
              />
              {formError.message && (
                <Form.HelpText className="text-red-500">{formError.message}</Form.HelpText>
              )}
            </Form.Group>

            <Form.Group>
              <Form.ControlLabel>Temperature</Form.ControlLabel>
              <InputNumber
                min={0}
                max={1}
                step={0.1}
                value={formValue.temperature}
                onChange={value => setFormValue({ ...formValue, temperature: value as number })}
              />
              <Form.HelpText>Controls randomness: 0 is deterministic, 1 is creative</Form.HelpText>
            </Form.Group>

            <Form.Group>
              <Form.ControlLabel>Vector Stores</Form.ControlLabel>
              <TagPicker
                data={vectorStores.map(vs => ({
                  label: vs.name,
                  value: vs.id
                }))}
                value={formValue.vectorStoreIds}
                onChange={value => setFormValue({ ...formValue, vectorStoreIds: value })}
                block
                placeholder="Select vector stores"
              />
              <Form.HelpText>Select the vector stores this assistant can access</Form.HelpText>
            </Form.Group>

            <Form.Group>
              <Button appearance="primary" onClick={handleSubmit} loading={isLoading}>
                {selectedAssistantId ? 'Update Assistant' : 'Create Assistant'}
              </Button>
              {selectedAssistantId && (
                <Button appearance="subtle" color="red" onClick={handleDelete} loading={isLoading} className="ml-2">
                  Delete Assistant
                </Button>
              )}
            </Form.Group>
          </Form>
        </>
      )}
    </Panel>
  );
};

export default AssistantSettings;

