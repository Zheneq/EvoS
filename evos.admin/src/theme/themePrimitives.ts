import { createTheme, alpha, Shadows, colors } from '@mui/material';

const defaultTheme = createTheme();

export const brand = {
  50: 'hsl(210, 100%, 97%)',
  100: 'hsl(210, 100%, 92%)',
  200: 'hsl(210, 100%, 82%)',
  300: 'hsl(210, 100%, 71%)',
  400: 'hsl(210, 100%, 60%)',
  500: 'hsl(210, 100%, 45%)',
  600: 'hsl(210, 100%, 38%)',
  700: 'hsl(210, 100%, 30%)',
  800: 'hsl(210, 100%, 22%)',
  900: 'hsl(210, 100%, 15%)',
};

export const gray = {
  50: 'hsl(220, 20%, 97%)',
  100: 'hsl(220, 15%, 93%)',
  200: 'hsl(220, 13%, 88%)',
  300: 'hsl(220, 12%, 78%)',
  400: 'hsl(220, 10%, 60%)',
  500: 'hsl(220, 9%, 46%)',
  600: 'hsl(220, 12%, 35%)',
  700: 'hsl(220, 16%, 22%)',
  800: 'hsl(220, 20%, 12%)',
  900: 'hsl(220, 25%, 7%)',
};

export const green = {
  50: 'hsl(120, 80%, 98%)',
  100: 'hsl(120, 75%, 94%)',
  200: 'hsl(120, 75%, 87%)',
  300: 'hsl(120, 61%, 77%)',
  400: 'hsl(120, 44%, 53%)',
  500: 'hsl(120, 59%, 30%)',
  600: 'hsl(120, 70%, 25%)',
  700: 'hsl(120, 75%, 16%)',
  800: 'hsl(120, 84%, 10%)',
  900: 'hsl(120, 87%, 6%)',
};

export const orange = {
  50: 'hsl(45, 100%, 97%)',
  100: 'hsl(45, 92%, 90%)',
  200: 'hsl(45, 94%, 80%)',
  300: 'hsl(45, 90%, 65%)',
  400: 'hsl(45, 90%, 40%)',
  500: 'hsl(45, 90%, 35%)',
  600: 'hsl(45, 91%, 25%)',
  700: 'hsl(45, 94%, 20%)',
  800: 'hsl(45, 95%, 16%)',
  900: 'hsl(45, 93%, 12%)',
};

export const red = {
  50: 'hsl(0, 100%, 97%)',
  100: 'hsl(0, 92%, 90%)',
  200: 'hsl(0, 94%, 80%)',
  300: 'hsl(0, 90%, 65%)',
  400: 'hsl(0, 90%, 40%)',
  500: 'hsl(0, 90%, 30%)',
  600: 'hsl(0, 91%, 25%)',
  700: 'hsl(0, 94%, 18%)',
  800: 'hsl(0, 95%, 12%)',
  900: 'hsl(0, 93%, 6%)',
};

export const atlasTeams = {
  teamA: {
    main: colors.blue[500],
    dark: colors.blue[900],
    light: colors.blue[300],
    contrastText: '#fff',
  },
  teamB: {
    main: colors.red[500],
    dark: colors.red[900],
    light: colors.red[300],
    contrastText: '#fff',
  },
  teamSpectator: {
    main: colors.yellow[500],
    dark: colors.yellow[900],
    light: colors.yellow[300],
    contrastText: '#000',
  },
  teamOther: {
    main: colors.grey[500],
    dark: colors.grey[900],
    light: colors.grey[300],
    contrastText: '#fff',
  },
  header: {
    main: colors.blue[700],
    dark: colors.blue[900],
    light: colors.blue[400],
    contrastText: '#fff',
  },
};

export const colorSchemes = {
  light: {
    palette: {
      primary: {
        light: brand[200],
        main: brand[500],
        dark: brand[700],
        contrastText: brand[50],
      },
      info: {
        light: brand[100],
        main: brand[400],
        dark: brand[700],
        contrastText: gray[50],
      },
      warning: {
        light: orange[300],
        main: orange[400],
        dark: orange[800],
      },
      error: {
        light: red[300],
        main: red[400],
        dark: red[800],
      },
      success: {
        light: green[300],
        main: green[400],
        dark: green[800],
      },
      grey: {
        ...gray,
      },
      divider: alpha(gray[300], 0.6),
      background: {
        default: 'hsl(220, 35%, 98%)',
        paper: 'hsl(0, 0%, 100%)',
      },
      text: {
        primary: gray[800],
        secondary: gray[600],
        warning: orange[400],
      },
      action: {
        hover: alpha(gray[200], 0.4),
        selected: alpha(gray[200], 0.6),
      },
      ...atlasTeams,
    },
  },
  dark: {
    palette: {
      primary: {
        contrastText: brand[50],
        light: brand[300],
        main: brand[400],
        dark: brand[700],
      },
      info: {
        contrastText: brand[300],
        light: brand[500],
        main: brand[700],
        dark: brand[900],
      },
      warning: {
        light: orange[400],
        main: orange[500],
        dark: orange[700],
      },
      error: {
        light: red[400],
        main: red[500],
        dark: red[700],
      },
      success: {
        light: green[400],
        main: green[500],
        dark: green[700],
      },
      grey: {
        ...gray,
      },
      divider: alpha(gray[700], 0.6),
      background: {
        default: gray[900],
        paper: 'hsl(220, 25%, 10%)',
      },
      text: {
        primary: 'hsl(0, 0%, 100%)',
        secondary: gray[400],
      },
      action: {
        hover: alpha(gray[600], 0.2),
        selected: alpha(gray[600], 0.35),
      },
      ...atlasTeams,
    },
  },
};

export const typography = {
  fontFamily: 'Inter, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif',
  h1: {
    fontSize: defaultTheme.typography.pxToRem(40),
    fontWeight: 700,
    lineHeight: 1.2,
    letterSpacing: -0.5,
  },
  h2: {
    fontSize: defaultTheme.typography.pxToRem(32),
    fontWeight: 700,
    lineHeight: 1.2,
    letterSpacing: -0.3,
  },
  h3: {
    fontSize: defaultTheme.typography.pxToRem(26),
    fontWeight: 600,
    lineHeight: 1.3,
  },
  h4: {
    fontSize: defaultTheme.typography.pxToRem(22),
    fontWeight: 600,
    lineHeight: 1.4,
  },
  h5: {
    fontSize: defaultTheme.typography.pxToRem(18),
    fontWeight: 600,
  },
  h6: {
    fontSize: defaultTheme.typography.pxToRem(16),
    fontWeight: 600,
  },
  subtitle1: {
    fontSize: defaultTheme.typography.pxToRem(16),
    fontWeight: 500,
  },
  subtitle2: {
    fontSize: defaultTheme.typography.pxToRem(14),
    fontWeight: 500,
  },
  body1: {
    fontSize: defaultTheme.typography.pxToRem(14),
    lineHeight: 1.5,
  },
  body2: {
    fontSize: defaultTheme.typography.pxToRem(13),
    fontWeight: 400,
    lineHeight: 1.5,
  },
  caption: {
    fontSize: defaultTheme.typography.pxToRem(12),
    fontWeight: 400,
  },
};

export const shape = {
  borderRadius: 8,
};

const defaultShadows: Shadows = [
  'none',
  '0px 2px 4px rgba(0, 0, 0, 0.05), 0px 1px 2px rgba(0, 0, 0, 0.08)',
  '0px 4px 8px rgba(0, 0, 0, 0.07), 0px 2px 4px rgba(0, 0, 0, 0.06)',
  '0px 8px 16px rgba(0, 0, 0, 0.08), 0px 4px 8px rgba(0, 0, 0, 0.06)',
  '0px 12px 24px rgba(0, 0, 0, 0.09), 0px 6px 12px rgba(0, 0, 0, 0.06)',
  ...defaultTheme.shadows.slice(5),
] as Shadows;

export const shadows = defaultShadows;
