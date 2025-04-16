import React from 'react';
import { Navigate } from 'react-router-dom';
import { Container, Content, Panel, FlexboxGrid, Button, Divider } from 'rsuite';
import { FaMicrosoft } from 'react-icons/fa';
import { useAuth } from '../contexts/AuthContext';

const LoginPage: React.FC = () => {
  const { isAuthenticated, isLoading, login } = useAuth();

  if (isLoading) {
    return <div>Loading...</div>;
  }

  if (isAuthenticated) {
    return <Navigate to="/search" replace />;
  }

  return (
    <Container className="h-screen bg-gradient-to-br from-blue-900 to-purple-900">
      <Content>
        <FlexboxGrid justify="center" align="middle" className="h-screen">
          <FlexboxGrid.Item colspan={12}>
            <Panel
              header="Welcome to PathRAG"
              bordered
              className="bg-white dark:bg-gray-800 shadow-lg rounded-lg"
            >
              <div className="text-center mb-6">
                <img
                  src="/logo.svg"
                  alt="PathRAG Logo"
                  className="w-24 h-24 mx-auto mb-4"
                />
                <p className="text-gray-600 dark:text-gray-300">
                  Sign in to access your knowledge graph and AI assistant
                </p>
              </div>

              <Divider>Sign In</Divider>

              <div className="flex flex-col items-center">
                <Button
                  appearance="primary"
                  block
                  onClick={login}
                  className="mb-4"
                  size="lg"
                >
                  <FaMicrosoft className="mr-2" /> Sign in with Microsoft
                </Button>
              </div>

              <div className="mt-6 text-center text-sm text-gray-500">
                <p>
                  By signing in, you agree to our{' '}
                  <a href="#" className="text-blue-500 hover:underline">
                    Terms of Service
                  </a>{' '}
                  and{' '}
                  <a href="#" className="text-blue-500 hover:underline">
                    Privacy Policy
                  </a>
                </p>
              </div>
            </Panel>
          </FlexboxGrid.Item>
        </FlexboxGrid>
      </Content>
    </Container>
  );
};

export default LoginPage;
