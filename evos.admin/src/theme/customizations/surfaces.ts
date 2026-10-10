import { alpha, Theme, Components } from '@mui/material/styles';
import { gray } from '../themePrimitives';

export const surfacesCustomizations: Components<Theme> = {
  MuiAccordion: {
    defaultProps: {
      elevation: 0,
      disableGutters: true,
    },
    styleOverrides: {
      root: ({ theme }) => ({
        padding: 4,
        overflow: 'clip',
        backgroundColor: (theme.vars || theme).palette.background.default,
        border: '1px solid',
        borderColor: (theme.vars || theme).palette.divider,
        ':before': {
          backgroundColor: 'transparent',
        },
        '&:not(:last-of-type)': {
          borderBottom: 'none',
        },
        '&:first-of-type': {
          borderTopLeftRadius: (theme.vars || theme).shape.borderRadius,
          borderTopRightRadius: (theme.vars || theme).shape.borderRadius,
        },
        '&:last-of-type': {
          borderBottomLeftRadius: (theme.vars || theme).shape.borderRadius,
          borderBottomRightRadius: (theme.vars || theme).shape.borderRadius,
        },
      }),
    },
  },
  MuiAccordionSummary: {
    styleOverrides: {
      root: ({ theme }) => ({
        border: 'none',
        borderRadius: 8,
        '&:hover': { backgroundColor: gray[50] },
        '&:focus-visible': { backgroundColor: 'transparent' },
        ...theme.applyStyles('dark', {
          '&:hover': { backgroundColor: gray[800] },
        }),
      }),
    },
  },
  MuiPaper: {
    defaultProps: {
      elevation: 0,
    },
    styleOverrides: {
      root: ({ theme }) => ({
        backgroundImage: 'none',
        border: `1px solid ${(theme.vars || theme).palette.divider}`,
      }),
    },
  },
  MuiCard: {
    styleOverrides: {
      root: ({ theme }) => ({
        padding: 16,
        gap: 16,
        transition: 'all 150ms ease',
        backgroundColor: (theme.vars || theme).palette.background.paper,
        borderRadius: (theme.vars || theme).shape.borderRadius,
        border: `1px solid ${(theme.vars || theme).palette.divider}`,
        boxShadow: (theme.vars || theme).shadows[1],
        variants: [
          {
            props: {
              variant: 'outlined',
            },
            style: {
              border: `1px solid ${(theme.vars || theme).palette.divider}`,
              boxShadow: 'none',
              backgroundColor: (theme.vars || theme).palette.background.paper,
              ...theme.applyStyles('dark', {
                backgroundColor: alpha(gray[900], 0.6),
              }),
            },
          },
        ],
      }),
    },
  },
};
