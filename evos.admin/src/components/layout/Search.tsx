import * as React from 'react';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import FormControl from '@mui/material/FormControl';
import InputAdornment from '@mui/material/InputAdornment';
import OutlinedInput from '@mui/material/OutlinedInput';
import SearchRoundedIcon from '@mui/icons-material/SearchRounded';

interface SearchProps {
  onSearchSubmit?: () => void;
  fullWidth?: boolean;
}

export default function Search({ onSearchSubmit, fullWidth = false }: SearchProps) {
  const [query, setQuery] = useState('');
  const navigate = useNavigate();

  const handleKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    if (event.key !== 'Enter' || !query.trim()) return;

    event.preventDefault();
    navigate(`/account/?query=${encodeURIComponent(query.trim())}`);
    if (onSearchSubmit) {
      onSearchSubmit();
    }
  };

  return (
    <FormControl sx={{ width: fullWidth ? '100%' : { xs: '100%', md: '28ch' } }} variant="outlined">
      <OutlinedInput
        size="small"
        id="player-search"
        placeholder="Find player..."
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        onKeyDown={handleKeyDown}
        startAdornment={
          <InputAdornment position="start" sx={{ color: 'text.secondary' }}>
            <SearchRoundedIcon fontSize="small" />
          </InputAdornment>
        }
        inputProps={{
          'aria-label': 'search player',
        }}
      />
    </FormControl>
  );
}
