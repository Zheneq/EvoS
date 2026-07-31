import {Avatar, Box, Stack, Typography} from "@mui/material";

interface DiscordUserProps {
    displayName?: string | null;
    userName?: string | null;
    userId?: string | null;
    avatarUrl?: string | null;
}

export default function DiscordUser({displayName, userName, userId, avatarUrl}: DiscordUserProps) {
    if (!userId) {
        return <>-</>;
    }
    return (
        <Stack direction="row" spacing={1} alignItems="center">
            <Avatar src={avatarUrl ?? undefined} sx={{ width: 28, height: 28 }} />
            <Box>
                <div>{displayName}</div>
                <Typography variant="caption" color="text.secondary">
                    {userName} · {userId}
                </Typography>
            </Box>
        </Stack>
    );
}