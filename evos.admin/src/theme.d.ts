import {PaletteColor, PaletteColorOptions} from "@mui/material";
import React from "react";

declare module '@mui/material/styles' {
    interface Theme {
        size: {
            basicWidth: string;
        };
        transform: {
            skewA: string;
            skewB: string;
        };
    }

    interface ThemeOptions {
        size: {
            basicWidth: React.CSSProperties['width'];
        };
        transform: {
            skewA: React.CSSProperties['transform'];
            skewB: React.CSSProperties['transform'];
        };
    }

    interface Palette {
        teamA: PaletteColor;
        teamB: PaletteColor;
        teamSpectator: PaletteColor;
        teamOther: PaletteColor;
        header: PaletteColor;
    }

    interface PaletteOptions {
        teamA?: PaletteColorOptions;
        teamB?: PaletteColorOptions;
        teamSpectator?: PaletteColorOptions;
        teamOther?: PaletteColorOptions;
        header?: PaletteColorOptions;
    }
}