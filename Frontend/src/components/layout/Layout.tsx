import React, { ReactNode } from 'react';
import { Container, Content, Footer } from 'rsuite';
import Header from './Header';

interface LayoutProps {
  children: ReactNode;
}

const Layout: React.FC<LayoutProps> = ({ children }) => {
  return (
    <Container className="min-h-screen bg-gray-100 dark:bg-gray-900">
      <Header />
      <Content className="p-4">{children}</Content>
      <Footer className="p-4 text-center text-gray-500">
        © {new Date().getFullYear()} PathRAG - All Rights Reserved
      </Footer>
    </Container>
  );
};

export default Layout;
