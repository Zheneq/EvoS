import React, {useEffect} from 'react';
import './App.css';
import StatusPage from "./components/pages/StatusPage";
import {BrowserRouter, Route, Routes} from "react-router-dom";
import LoginPage from "./components/pages/LoginPage";
import NavBar from "./components/generic/Navbar";
import AdminPage from "./components/pages/AdminPage";
import ProfilePage from "./components/pages/ProfilePage";
import {colors, createTheme, CssBaseline, Paper, ThemeProvider} from "@mui/material";
import ProfileSearchPage from "./components/pages/ProfileSearchPage";
import CodesPage from './components/pages/CodesPage';
import ChatHistoryPage from "./components/pages/ChatHistoryPage";
import MatchPage from "./components/pages/MatchPage";
import MatchHistoryPage from "./components/pages/MatchHistoryPage";
import ReportHistoryPage from "./components/pages/ReportHistoryPage";

const theme = createTheme({
    components: {
        MuiCssBaseline: {
            styleOverrides: {
                html: {
                    margin: 0,
                    minHeight: '100vh',
                },
                body: {
                    backgroundColor: '#000',
                    color: 'white',
                    textAlign: 'center',
                    margin: 0,
                    minHeight: '100vh',
                }
            }
        }
    },
    size: {
        basicWidth: 600,
    },
    transform: {
        skewA: 'skewX(-15deg)',
        skewB: 'skewX(15deg)',
    },
    palette: {
        // mode: 'dark',
        primary: {
            main: colors.blue[700],
        },
        secondary: {
            main: colors.orange[500],
        },
        background: {
            default: '#000',
            paper: '#282c34',
        },
        action : {
            active: '#fff',
            hover: 'rgba(255, 255, 255, 0.08)',
            selected: 'rgba(255, 255, 255, 0.16)',
            disabled: 'rgba(255, 255, 255, 0.3)',
            disabledBackground: 'rgba(255, 255, 255, 0.12)',
        },
        text: {
            primary: '#fff',
            secondary: 'rgba(255, 255, 255, 0.7)',
            disabled: 'rgba(255, 255, 255, 0.5)',
        },
        divider: 'rgba(255, 255, 255, 0.12)',
        teamA: {
            main: colors.blue[500],
            dark: colors.blue[900],
        },
        teamB: {
            main: colors.red[500],
            dark: colors.red[900],
        },
        teamSpectator: {
            main: colors.yellow[500],
            dark: colors.yellow[900],
        },
        teamOther: {
            main: colors.grey[500],
            dark: colors.grey[900],
        }
    }
});

interface PageProps {
    title: string;
    children?: React.ReactNode;
}

function Page(props: PageProps) {
    useEffect(() => {
        document.title = props.title || "Atlas Reactor";
    }, [props.title]);
    return <>{props.children}</>;
}

const page = (title: string, content: React.ReactNode) => {
    return <Page title={title}>{content}</Page>;
}

function App() {
    return (
        <ThemeProvider theme={theme}>
            <CssBaseline />
            <BrowserRouter>
                <NavBar/>
                <Paper sx={{
                    width: 'calc(100% - 16px)',
                    padding: 1,
                    margin: 'auto',
                    overflow: 'auto',
                    borderTopLeftRadius: 0,
                    borderTopRightRadius: 0,
                }}>
                    <Routes>
                        <Route path="/" element={page("Lobby status", <StatusPage/>)}/>
                        <Route path="/login" element={page("Atlas Reactor: Login", <LoginPage/>)}/>
                        <Route path="/admin" element={page("Admin panel", <AdminPage/>)}/>
                        <Route path="/codes" element={page("Codes", <CodesPage/>)}/>
                        <Route path="/account" element={page("Search", <ProfileSearchPage/>)}/>
                        <Route path="/account/:accountId" element={page("Account", <ProfilePage/>)}/>
                        <Route path="/account/:accountId/chat" element={page("Chat History", <ChatHistoryPage/>)}/>
                        <Route path="/account/:accountId/matches/:matchId" element={page("Match", <MatchPage/>)}/>
                        <Route path="/account/:accountId/matches" element={page("Match History", <MatchHistoryPage/>)}/>
                        <Route path="/account/:accountId/feedback" element={page("Report History", <ReportHistoryPage/>)}/>
                    </Routes>
                </Paper>
            </BrowserRouter>
        </ThemeProvider>
    );
}

export default App;
