import React from 'react';
import { Button } from 'rsuite';
import { useAuth } from '../../contexts/AuthContext';

const LoginButton: React.FC = () => {
  const { login } = useAuth();

  return (
    <Button appearance="primary" onClick={login}>
      Sign In
    </Button>
  );
};

export default LoginButton;
