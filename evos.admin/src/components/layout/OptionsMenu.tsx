import * as React from 'react';
import { useNavigate } from 'react-router-dom';
import { useSignOut } from 'react-auth-kit';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import ListItemText from '@mui/material/ListItemText';
import ListItemIcon from '@mui/material/ListItemIcon';
import LogoutRoundedIcon from '@mui/icons-material/LogoutRounded';
import MoreVertRoundedIcon from '@mui/icons-material/MoreVertRounded';
import MenuButton from './MenuButton';

interface OptionsMenuProps {
  onLogout?: () => void;
}

export default function OptionsMenu({ onLogout }: OptionsMenuProps) {
  const [anchorEl, setAnchorEl] = React.useState<null | HTMLElement>(null);
  const open = Boolean(anchorEl);
  const signOut = useSignOut();
  const navigate = useNavigate();

  const handleClick = (event: React.MouseEvent<HTMLElement>) => {
    setAnchorEl(event.currentTarget);
  };

  const handleClose = () => {
    setAnchorEl(null);
  };

  const handleLogout = () => {
    handleClose();
    if (onLogout) onLogout();
    signOut();
    navigate('/login');
  };

  return (
    <React.Fragment>
      <MenuButton
        aria-label="Open options menu"
        onClick={handleClick}
        sx={{ border: 'none', backgroundColor: 'transparent' }}
      >
        <MoreVertRoundedIcon fontSize="small" />
      </MenuButton>
      <Menu
        anchorEl={anchorEl}
        id="options-menu"
        open={open}
        onClose={handleClose}
        transformOrigin={{ horizontal: 'right', vertical: 'bottom' }}
        anchorOrigin={{ horizontal: 'right', vertical: 'top' }}
      >
        <MenuItem onClick={handleLogout} sx={{ color: 'error.main' }}>
          <ListItemIcon sx={{ color: 'error.main' }}>
            <LogoutRoundedIcon fontSize="small" />
          </ListItemIcon>
          <ListItemText primary="Log out" primaryTypographyProps={{ fontSize: '0.85rem', fontWeight: 500 }} />
        </MenuItem>
      </Menu>
    </React.Fragment>
  );
}
