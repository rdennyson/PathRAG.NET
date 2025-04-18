import React from 'react';
import { Panel, List, FlexboxGrid, Button } from 'rsuite';
import { SavedGraphHistory } from '../../utils/graphHistory';
import { FaTrash, FaArrowRight } from 'react-icons/fa';

interface GraphHistoryProps {
  searchHistory: SavedGraphHistory[];
  onHistorySelect: (historyItem: SavedGraphHistory) => void;
  onHistoryDelete?: (index: number) => void;
  className?: string;
}

const GraphHistory: React.FC<GraphHistoryProps> = ({
  searchHistory,
  onHistorySelect,
  onHistoryDelete,
  className = ''
}) => {
  if (!searchHistory || searchHistory.length === 0) {
    return (
      <Panel className={`${className} p-4`} bordered>
        <h4 className="text-lg font-semibold mb-4">Search History</h4>
        <p className="text-gray-500">No search history available.</p>
      </Panel>
    );
  }

  return (
    <Panel className={`${className} p-4`} bordered>
      <h4 className="text-lg font-semibold mb-4">Search History</h4>
      <List>
        {searchHistory.map((item, index) => (
          <List.Item key={index} className="mb-2 border rounded-md p-2 hover:bg-gray-50 dark:hover:bg-gray-700">
            <FlexboxGrid align="middle">
              <FlexboxGrid.Item colspan={16}>
                <div className="truncate font-medium">{item.searchValue}</div>
                <div className="text-xs text-gray-500">
                  {item.timestamp ? new Date(item.timestamp).toLocaleString() : 'Unknown date'}
                </div>
                <div className="text-xs text-gray-500">
                  {item.results.nodes.length} nodes, {item.results.edges.length} edges
                </div>
              </FlexboxGrid.Item>
              <FlexboxGrid.Item colspan={8} className="text-right">
                <Button
                  appearance="ghost"
                  size="xs"
                  onClick={() => onHistorySelect(item)}
                  className="mr-1"
                >
                  <FaArrowRight />
                </Button>
                {onHistoryDelete && (
                  <Button
                    appearance="ghost"
                    size="xs"
                    onClick={() => onHistoryDelete(index)}
                    className="text-red-500"
                  >
                    <FaTrash />
                  </Button>
                )}
              </FlexboxGrid.Item>
            </FlexboxGrid>
          </List.Item>
        ))}
      </List>
    </Panel>
  );
};

export default GraphHistory;
