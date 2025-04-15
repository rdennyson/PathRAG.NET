import React from 'react';
import { Button } from 'rsuite';
import { useAuth } from '../../contexts/AuthContext';

const LogoutButton: React.FC = () => {
  const { logout } = useAuth();

  return (
    <Button appearance="subtle" onClick={logout}>
      Sign Out
    </Button>
  );
};

export default LogoutButton;
