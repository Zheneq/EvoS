import React, { useEffect } from 'react';
import './App.css';
import StatusPage from './components/pages/StatusPage';
import { BrowserRouter, Route, Routes } from 'react-router-dom';
import LoginPage from './components/pages/LoginPage';
import AdminPage from './components/pages/AdminPage';
import ProfilePage from './components/pages/ProfilePage';
import { CssBaseline } from '@mui/material';
import ProfileSearchPage from './components/pages/ProfileSearchPage';
import CodesPage from './components/pages/CodesPage';
import ChatHistoryPage from './components/pages/ChatHistoryPage';
import MatchPage from './components/pages/MatchPage';
import MatchHistoryPage from './components/pages/MatchHistoryPage';
import ReportHistoryPage from './components/pages/ReportHistoryPage';
import DebugPage from './components/pages/DebugPage';
import GameServersPage from './components/pages/GameServersPage';
import AppTheme from './theme/AppTheme';
import { DashboardLayout } from './components/layout';

interface PageProps {
  title: string;
  children?: React.ReactNode;
}

function Page(props: PageProps) {
  useEffect(() => {
    document.title = props.title ? `${props.title} - Atlas Reactor` : 'Atlas Reactor';
  }, [props.title]);
  return <>{props.children}</>;
}

const page = (title: string, content: React.ReactNode) => {
  return <Page title={title}>{content}</Page>;
};

function App() {
  return (
    <AppTheme>
      <CssBaseline enableColorScheme />
      <BrowserRouter>
        <DashboardLayout>
          <Routes>
            <Route path="/" element={page('Lobby Status', <StatusPage />)} />
            <Route path="/login" element={page('Login', <LoginPage />)} />
            <Route path="/admin" element={page('Admin Panel', <AdminPage />)} />
            <Route path="/codes" element={page('Codes', <CodesPage />)} />
            <Route path="/account" element={page('Search', <ProfileSearchPage />)} />
            <Route path="/account/:accountId" element={page('Account Profile', <ProfilePage />)} />
            <Route path="/account/:accountId/chat" element={page('Chat History', <ChatHistoryPage />)} />
            <Route path="/account/:accountId/matches/:matchId" element={page('Match Details', <MatchPage />)} />
            <Route path="/account/:accountId/matches" element={page('Match History', <MatchHistoryPage />)} />
            <Route path="/account/:accountId/feedback" element={page('Report History', <ReportHistoryPage />)} />
            <Route path="/debug" element={page('Debug Tools', <DebugPage />)} />
            <Route path="/game-servers" element={page('Game Servers', <GameServersPage />)} />
          </Routes>
        </DashboardLayout>
      </BrowserRouter>
    </AppTheme>
  );
}

export default App;
