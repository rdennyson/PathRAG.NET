import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { User } from '../models/types';
import apiService from '../services/api';

interface AuthContextType {
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: () => Promise<void>;
  logout: () => Promise<void>;
  getAccessToken: () => Promise<string>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const navigate = useNavigate();
  const location = useLocation();
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(false);

  // Check for authentication callback
  useEffect(() => {
    // The callback is now handled by the backend
    // The backend will set the cookies and redirect to /search
    // We don't need to do anything here
  }, []);

  // Check authentication status on mount
  useEffect(() => {
    const checkAuth = async () => {
      setIsLoading(true);
      try {
        // Fetch user profile - this will work if the cookies are present
        const userData = await apiService.getCurrentUser();
        setUser(userData);
        setIsAuthenticated(true);
      } catch (error) {
        console.error('Error fetching user data:', error);
        setUser(null);
        setIsAuthenticated(false);

        // Redirect to login if not on login page
        if (location.pathname !== '/login' && location.pathname !== '/callback') {
          navigate('/login');
        }
      } finally {
        setIsLoading(false);
      }
    };

    checkAuth();
  }, [navigate, location.pathname]);

  // Login function - redirects to the login endpoint
  const login = async () => {
    window.location.href = `${import.meta.env.VITE_API_BASE_URL}/login`;
  };

  // Logout function
  const logout = async () => {
    // Call the logout endpoint to clear cookies
    try {
      await apiService.logout();
    } catch (error) {
      console.error('Error during logout:', error);
    }

    setUser(null);
    setIsAuthenticated(false);
    navigate('/login');
  };

  // Get access token function - not needed anymore as cookies are used
  const getAccessToken = async (): Promise<string> => {
    // This is just a placeholder - we don't need to get the token manually anymore
    // as it's sent automatically with cookies
    return 'token-in-cookie';
  };

  const value = {
    user,
    isAuthenticated, // Use the state variable
    isLoading,
    login,
    logout,
    getAccessToken
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuth = (): AuthContextType => {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};

export default AuthContext;


