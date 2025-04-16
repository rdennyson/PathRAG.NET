import React, { useEffect } from 'react';
import { Loader } from 'rsuite';

const AuthCallback: React.FC = () => {
  useEffect(() => {
    // The actual authentication handling is done in the AuthContext
    // This component just shows a loading indicator
    console.log('Processing authentication callback');
  }, []);

  return (
    <div className="flex justify-center items-center h-screen">
      <Loader size="lg" content="Completing authentication..." vertical />
    </div>
  );
};

export default AuthCallback;