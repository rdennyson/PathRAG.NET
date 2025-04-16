import React from 'react';
import { Link } from 'react-router-dom';
import { Navbar, Nav, Dropdown, Avatar } from 'rsuite';
import { FaCog, FaSignOutAlt, FaUser } from 'react-icons/fa';
import { useAuth } from '../../contexts/AuthContext';
import LogoutButton from '../auth/LogoutButton';

const Header: React.FC = () => {
  const { user, isAuthenticated } = useAuth();

  return (
    <Navbar appearance="inverse" className="bg-gradient-to-r from-blue-900 to-purple-900">
      <Navbar.Brand as={Link} to="/">
        <img
          src="/logo.svg"
          alt="PathRAG"
          height="30"
          className="mr-2"
        />
        PathRAG
      </Navbar.Brand>

      {isAuthenticated && (
        <>
          <Nav>
            <Nav.Item as={Link} to="/search">Search</Nav.Item>
            <Nav.Item as={Link} to="/knowledge-graph">Knowledge Graph</Nav.Item>

          </Nav>

          <Nav pullRight>
            <Nav.Item as={Link} to="/settings">
              <FaCog />
            </Nav.Item>

            <Dropdown
              title={
                <Avatar
                  circle
                  src={user?.email ? `https://www.gravatar.com/avatar/${btoa(user.email)}?d=mp` : undefined}
                  alt={user?.name || 'User'}
                >
                  {!user?.email && <FaUser />}
                </Avatar>
              }
            >
              <Dropdown.Item panel style={{ padding: 10, width: 160 }}>
                <p>Signed in as</p>
                <strong>{user?.name}</strong>
              </Dropdown.Item>
              <Dropdown.Item divider />
              <Dropdown.Item as={Link} to="/settings">Settings</Dropdown.Item>
              <Dropdown.Item icon={<FaSignOutAlt />}>
                <LogoutButton />
              </Dropdown.Item>
            </Dropdown>
          </Nav>
        </>
      )}
    </Navbar>
  );
};

export default Header;
