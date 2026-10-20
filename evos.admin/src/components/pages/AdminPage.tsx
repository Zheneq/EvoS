import React, {useState} from 'react';
import {
    Box,
    Card,
    Chip,
    Divider,
    Grid,
    Tab,
    Tabs,
    Typography
} from "@mui/material";
import PauseQueue from "../controls/PauseQueue";
import Broadcast from "../controls/Broadcast";
import Shutdown from "../controls/Shutdown";
import ServerMessage from "../controls/ServerMessage";
import Proxy from "../controls/Proxy";
import {EvosServerMessageType} from "../../lib/Evos";
import AdminPanelSettingsRoundedIcon from "@mui/icons-material/AdminPanelSettingsRounded";
import NotificationsActiveRoundedIcon from "@mui/icons-material/NotificationsActiveRounded";
import ChatBubbleOutlineRoundedIcon from "@mui/icons-material/ChatBubbleOutlineRounded";
import AnnouncementRoundedIcon from "@mui/icons-material/AnnouncementRounded";
import WebAssetRoundedIcon from "@mui/icons-material/WebAssetRounded";
import NotificationsRoundedIcon from "@mui/icons-material/NotificationsRounded";

interface MessageTabDef {
    type: EvosServerMessageType;
    label: string;
    icon: React.ReactElement;
    description: string;
}

const MESSAGE_TABS: MessageTabDef[] = [
    {
        type: EvosServerMessageType.MessageOfTheDay,
        label: "Message of the Day",
        icon: <ChatBubbleOutlineRoundedIcon fontSize="small" />,
        description: "Displayed in the game lobby chat and client status area for all players upon connection.",
    },
    {
        type: EvosServerMessageType.MessageOfTheDayPopup,
        label: "Login Popup",
        icon: <AnnouncementRoundedIcon fontSize="small" />,
        description: "Appears as an interactive modal dialog popup immediately when a player logs into the client.",
    },
    {
        type: EvosServerMessageType.LauncherMessageOfTheDay,
        label: "Launcher News",
        icon: <WebAssetRoundedIcon fontSize="small" />,
        description: "Rendered as the featured announcement card in the EvoS game launcher client.",
    },
    {
        type: EvosServerMessageType.LauncherNotification,
        label: "Launcher Banner",
        icon: <NotificationsRoundedIcon fontSize="small" />,
        description: "Rendered as a high-visibility alert banner across the top of the launcher window.",
    },
];

export default function AdminPage() {
    const [selectedTab, setSelectedTab] = useState<number>(0);

    const currentTabDef = MESSAGE_TABS[selectedTab];

    return (
        <Box sx={{ width: '100%', display: 'flex', flexDirection: 'column', gap: 3 }}>
            {/* Page Header */}
            <Box
                sx={{
                    display: 'flex',
                    flexDirection: { xs: 'column', md: 'row' },
                    justifyContent: 'space-between',
                    alignItems: { xs: 'flex-start', md: 'center' },
                    gap: 2,
                    pb: 2,
                    borderBottom: '1px solid',
                    borderColor: 'divider',
                }}
            >
                <Box>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap', mb: 0.5 }}>
                        <AdminPanelSettingsRoundedIcon color="primary" sx={{ fontSize: 32 }} />
                        <Typography variant="h4" component="h1" sx={{ fontWeight: 700, letterSpacing: '-0.5px' }}>
                            Server Administration
                        </Typography>
                        <Chip
                            label="System Master"
                            color="primary"
                            variant="outlined"
                            size="small"
                            sx={{ fontWeight: 600 }}
                        />
                    </Box>
                    <Typography variant="body2" color="text.secondary">
                        Manage matchmaking queues, server lifecycle, routing rules, and player announcements.
                    </Typography>
                </Box>
            </Box>

            {/* Server Operational Controls (2x2 Grid) */}
            <Grid container spacing={3}>
                <Grid size={{ xs: 12, md: 6 }}>
                    <PauseQueue />
                </Grid>
                <Grid size={{ xs: 12, md: 6 }}>
                    <Proxy />
                </Grid>
                <Grid size={{ xs: 12, md: 6 }}>
                    <Broadcast />
                </Grid>
                <Grid size={{ xs: 12, md: 6 }}>
                    <Shutdown />
                </Grid>
            </Grid>

            {/* Server Announcements & MOTD Section */}
            <Card variant="outlined">
                <Box sx={{ p: 2.5, pb: 1.5 }}>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25, mb: 0.5 }}>
                        <NotificationsActiveRoundedIcon color="primary" />
                        <Typography variant="h6" sx={{ fontWeight: 600 }}>
                            Server Announcements & MOTD
                        </Typography>
                    </Box>
                    <Typography variant="body2" color="text.secondary">
                        Configure in-game message of the day, login modal popups, and launcher announcements.
                    </Typography>
                </Box>

                <Divider />

                {/* Tabs for Message Types */}
                <Box sx={{ borderBottom: 1, borderColor: 'divider', px: 2 }}>
                    <Tabs
                        value={selectedTab}
                        onChange={(e, val) => setSelectedTab(val)}
                        variant="scrollable"
                        scrollButtons="auto"
                    >
                        {MESSAGE_TABS.map((tab, idx) => (
                            <Tab
                                key={tab.type}
                                icon={tab.icon}
                                iconPosition="start"
                                label={tab.label}
                                id={`server-msg-tab-${idx}`}
                                sx={{ minHeight: 48, textTransform: 'none', fontWeight: 600 }}
                            />
                        ))}
                    </Tabs>
                </Box>

                {/* Tab Content */}
                <Box sx={{ p: 2.5 }}>
                    <Box sx={{ mb: 2.5 }}>
                        <Typography variant="subtitle2" color="text.primary" sx={{ fontWeight: 600 }}>
                            {currentTabDef.label}
                        </Typography>
                        <Typography variant="caption" color="text.secondary">
                            {currentTabDef.description}
                        </Typography>
                    </Box>

                    <ServerMessage key={currentTabDef.type} type={currentTabDef.type} />
                </Box>
            </Card>
        </Box>
    );
}
