import React from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { MsalProvider } from '@azure/msal-react';
import { PublicClientApplication } from '@azure/msal-browser';
import { CustomProvider } from 'rsuite';
import 'rsuite/dist/rsuite.min.css';

import { msalConfig } from './services/authConfig';
import { AuthProvider } from './contexts/AuthContext';
import { AppProvider } from './contexts/AppContext';
import AuthGuard from './components/auth/AuthGuard';

import LoginPage from './pages/LoginPage';
import SearchPage from './pages/SearchPage';
import KnowledgeGraphPage from './pages/KnowledgeGraphPage';
import SettingsPage from './pages/SettingsPage';

// Initialize MSAL instance
const msalInstance = new PublicClientApplication(msalConfig);

const App: React.FC = () => {
  return (
    <MsalProvider instance={msalInstance}>
      <CustomProvider theme="dark">
        <AuthProvider>
          <AppProvider>
            <Router>
              <Routes>
                <Route path="/login" element={<LoginPage />} />

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
            </Router>
          </AppProvider>
        </AuthProvider>
      </CustomProvider>
    </MsalProvider>
  );
};

export default App;
