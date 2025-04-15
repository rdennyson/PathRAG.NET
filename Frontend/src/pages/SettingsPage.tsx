import React from 'react';
import { Container, Content } from 'rsuite';
import Layout from '../components/layout/Layout';
import SettingsTabs from '../components/settings/SettingsTabs';

const SettingsPage: React.FC = () => {
  return (
    <Layout>
      <Container>
        <Content className="p-4">
          <h2 className="mb-4">Settings</h2>
          <SettingsTabs />
        </Content>
      </Container>
    </Layout>
  );
};

export default SettingsPage;
