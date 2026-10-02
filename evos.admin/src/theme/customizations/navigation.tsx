import * as React from 'react';
import { Theme, alpha, Components } from '@mui/material/styles';
import { SvgIconProps } from '@mui/material/SvgIcon';
import { buttonBaseClasses } from '@mui/material/ButtonBase';
import { dividerClasses } from '@mui/material/Divider';
import { menuItemClasses } from '@mui/material/MenuItem';
import { selectClasses } from '@mui/material/Select';
import { tabClasses } from '@mui/material/Tab';
import UnfoldMoreRoundedIcon from '@mui/icons-material/UnfoldMoreRounded';
import { gray, brand } from '../themePrimitives';

export const navigationCustomizations: Components<Theme> = {
  MuiMenuItem: {
    styleOverrides: {
      root: ({ theme }) => ({
        borderRadius: (theme.vars || theme).shape.borderRadius,
        padding: '6px 10px',
        [`&.${menuItemClasses.focusVisible}`]: {
          backgroundColor: 'transparent',
        },
        [`&.${menuItemClasses.selected}`]: {
          backgroundColor: alpha(theme.palette.action.selected, 0.25),
        },
      }),
    },
  },
  MuiMenu: {
    styleOverrides: {
      list: {
        gap: '2px',
        padding: '6px',
        [`& .${dividerClasses.root}`]: {
          margin: '4px -6px',
        },
      },
      paper: ({ theme }) => ({
        marginTop: '6px',
        borderRadius: (theme.vars || theme).shape.borderRadius,
        border: `1px solid ${(theme.vars || theme).palette.divider}`,
        backgroundImage: 'none',
        backgroundColor: (theme.vars || theme).palette.background.paper,
        boxShadow: (theme.vars || theme).shadows[3],
        [`& .${buttonBaseClasses.root}`]: {
          '&.Mui-selected': {
            backgroundColor: alpha(theme.palette.action.selected, 0.3),
          },
        },
      }),
    },
  },
  MuiSelect: {
    defaultProps: {
      IconComponent: React.forwardRef<SVGSVGElement, SvgIconProps>((props, ref) => (
        <UnfoldMoreRoundedIcon fontSize="small" {...props} ref={ref} />
      )),
    },
    styleOverrides: {
      root: ({ theme }) => ({
        borderRadius: (theme.vars || theme).shape.borderRadius,
        border: `1px solid ${(theme.vars || theme).palette.divider}`,
        backgroundColor: (theme.vars || theme).palette.background.paper,
        '&:hover': {
          borderColor: gray[400],
        },
        [`&.${selectClasses.focused}`]: {
          borderColor: brand[400],
          outline: `2px solid ${alpha(brand[500], 0.3)}`,
        },
      }),
    },
  },
  MuiDrawer: {
    styleOverrides: {
      paper: ({ theme }) => ({
        backgroundColor: (theme.vars || theme).palette.background.paper,
        borderRight: `1px solid ${(theme.vars || theme).palette.divider}`,
        backgroundImage: 'none',
      }),
    },
  },
  MuiTabs: {
    styleOverrides: {
      indicator: ({ theme }) => ({
        backgroundColor: (theme.vars || theme).palette.primary.main,
      }),
    },
  },
  MuiTab: {
    styleOverrides: {
      root: ({ theme }) => ({
        padding: '6px 12px',
        textTransform: 'none',
        fontWeight: 500,
        color: (theme.vars || theme).palette.text.secondary,
        borderRadius: (theme.vars || theme).shape.borderRadius,
        '&:hover': {
          color: (theme.vars || theme).palette.text.primary,
          backgroundColor: alpha(gray[200], 0.2),
        },
        [`&.${tabClasses.selected}`]: {
          color: (theme.vars || theme).palette.primary.main,
          fontWeight: 600,
        },
      }),
    },
  },
};
