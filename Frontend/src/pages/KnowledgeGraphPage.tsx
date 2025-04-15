import React from 'react';
import { Container, Content } from 'rsuite';
import Layout from '../components/layout/Layout';
import KnowledgeGraphViewer from '../components/knowledgeGraph/KnowledgeGraphViewer';

const KnowledgeGraphPage: React.FC = () => {
  return (
    <Layout>
      <Container className="h-[calc(100vh-120px)]">
        <Content className="h-full">
          <KnowledgeGraphViewer />
        </Content>
      </Container>
    </Layout>
  );
};

export default KnowledgeGraphPage;
