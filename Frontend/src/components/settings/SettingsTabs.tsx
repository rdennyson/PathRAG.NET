import React from 'react';
import { Tabs, Tab } from 'rsuite';
import AssistantSettings from './AssistantSettings';
import VectorStoreSettings from './VectorStoreSettings';

const SettingsTabs: React.FC = () => {
  return (
    <Tabs defaultActiveKey="assistants">
      <Tab eventKey="assistants" title="Assistants">
        <AssistantSettings />
      </Tab>
      <Tab eventKey="vectorStores" title="Vector Stores">
        <VectorStoreSettings />
      </Tab>
    </Tabs>
  );
};

export default SettingsTabs;
