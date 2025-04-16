import React from 'react';
import { Tabs } from 'rsuite';
import AssistantSettings from './AssistantSettings';
import VectorStoreSettings from './VectorStoreSettings';

const SettingsTabs: React.FC = () => {
  return (
    <Tabs defaultActiveKey="assistants">
      <Tabs.Tab eventKey="assistants" title="Assistants">
        <AssistantSettings />
      </Tabs.Tab>
      <Tabs.Tab eventKey="vectorStores" title="Vector Stores">
        <VectorStoreSettings />
      </Tabs.Tab>
    </Tabs>
  );
};

export default SettingsTabs;
