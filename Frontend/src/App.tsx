import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { CustomProvider } from 'rsuite';
import 'rsuite/dist/rsuite.min.css';

import AuthCallback from './components/auth/AuthCallback';
import AuthGuard from './components/auth/AuthGuard';

import LoginPage from './pages/LoginPage';
import SearchPage from './pages/SearchPage';
import KnowledgeGraphPage from './pages/KnowledgeGraphPage';

import SettingsPage from './pages/SettingsPage';

const App: React.FC = () => {
  return (
    <CustomProvider theme="dark">
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/callback" element={<AuthCallback />} />
        <Route
          path="/search"
          element={
            <AuthGuard>
              <SearchPage />
            </AuthGuard>
          }
        />
        <Route
          path="/knowledge-graph"
          element={
            <AuthGuard>
              <KnowledgeGraphPage />
            </AuthGuard>
          }
        />

        <Route
          path="/settings"
          element={
            <AuthGuard>
              <SettingsPage />
            </AuthGuard>
          }
        />
        <Route path="/" element={<Navigate to="/search" replace />} />
      </Routes>
    </CustomProvider>
  );
};

export default App;



