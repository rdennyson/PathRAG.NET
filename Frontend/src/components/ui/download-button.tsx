import React, { useCallback } from 'react';
import { toPng } from 'html-to-image';
import { Button } from 'rsuite';
import { FaDownload } from 'react-icons/fa';
import { useReactFlow } from 'reactflow';

interface DownloadButtonProps {
  disabled?: boolean;
}

const DownloadButton: React.FC<DownloadButtonProps> = ({ disabled = false }) => {
  const { getNodes } = useReactFlow();

  const onClick = useCallback(() => {
    // Get the ReactFlow container
    const flowElement = document.querySelector('.react-flow') as HTMLElement;
    if (!flowElement) return;

    // Create a filename based on the first node's label or current date
    const nodes = getNodes();
    const firstNode = nodes.length > 0 ? nodes[0] : null;
    const filename = firstNode?.data?.label 
      ? `knowledge-graph-${firstNode.data.label.replace(/\s+/g, '-').toLowerCase()}.png`
      : `knowledge-graph-${new Date().toISOString().split('T')[0]}.png`;

    // Convert the ReactFlow container to PNG
    toPng(flowElement, {
      backgroundColor: '#ffffff',
      quality: 1,
      pixelRatio: 2,
    })
      .then((dataUrl) => {
        // Create a download link and trigger the download
        const link = document.createElement('a');
        link.download = filename;
        link.href = dataUrl;
        link.click();
      })
      .catch((error) => {
        console.error('Error generating image:', error);
      });
  }, [getNodes]);

  return (
    <Button 
      appearance="primary" 
      onClick={onClick} 
      disabled={disabled}
      style={{ 
        position: 'absolute', 
        top: '10px', 
        right: '10px', 
        zIndex: 5 
      }}
      size="sm"
    >
      <FaDownload style={{ marginRight: '5px' }} /> Download
    </Button>
  );
};

export default DownloadButton;
