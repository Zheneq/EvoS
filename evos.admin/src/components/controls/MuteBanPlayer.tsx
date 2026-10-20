import {
    Alert,
    Box,
    Button,
    Card,
    FormControl,
    InputLabel,
    LinearProgress,
    MenuItem,
    Select,
    SelectChangeEvent,
    Stack,
    TextField,
    Typography
} from "@mui/material";
import React, {useState} from "react";
import {processError} from "../../lib/Error";
import {useAuthHeader} from "react-auth-kit";
import BaseDialog from "../generic/BaseDialog";
import {useNavigate} from "react-router-dom";
import {AxiosResponse} from "axios";
import {cap, PenaltyInfo} from "../../lib/Evos";
import VolumeOffRoundedIcon from "@mui/icons-material/VolumeOffRounded";
import GavelRoundedIcon from "@mui/icons-material/GavelRounded";

interface MutePlayerProps {
    disabled: boolean;
    deadline?: Date;
    accountId: number;
    action: (authHeader: string, penaltyInfo: PenaltyInfo) => Promise<AxiosResponse>;
    handle: string;
    actionText: string;
    doneText: string;
    onCommit: () => void;
}

const DEFAULT = 30;

export default function MuteBanPlayer({disabled, deadline, accountId, action, handle, actionText, doneText, onCommit}: MutePlayerProps) {
    const [durationMinutes, setDurationMinutes] = useState<number>(DEFAULT);
    const [description, setDescription] = useState<string>("");
    const [processing, setProcessing] = useState(false);
    const [msg, setMsg] = useState<string>();

    const authHeader = useAuthHeader();
    const navigate = useNavigate();

    const isBan = actionText.toLowerCase() === 'ban';
    const isCurrentlyActive = !!deadline && deadline > new Date();

    const handleSubmit = (event: React.FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        const data = new FormData(event.currentTarget);
        const description = data.get('description') as string;

        if (!description) {
            return;
        }

        setProcessing(true);
        const penaltyInfo: PenaltyInfo = {
            accountId: accountId,
            durationMinutes: durationMinutes,
            description: description,
        };
        action(authHeader(), penaltyInfo)
            .then(() => {
                setMsg(`${handle} has been ${durationMinutes ? "" : "un"}${doneText}`);
                setDescription("");
                if (!durationMinutes) {
                    setDurationMinutes(DEFAULT);
                }
            })
            .catch(e => processError(e, err => setMsg(err.text), navigate))
            .then(() => setProcessing(false));
    };

    const handleDismiss = () => {
        setMsg(undefined);
        onCommit();
    }

    const handleUpdateDuration = (event: SelectChangeEvent) => {
        setDurationMinutes(parseInt(event.target.value));
    };

    return (
        <Card variant="outlined">
            <Stack spacing={2} sx={{ width: '100%' }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    {isBan ? (
                        <GavelRoundedIcon color={isCurrentlyActive ? "error" : "primary"} />
                    ) : (
                        <VolumeOffRoundedIcon color={isCurrentlyActive ? "warning" : "primary"} />
                    )}
                    <Typography variant="h6" sx={{ fontWeight: 600, textTransform: 'capitalize' }}>
                        {actionText} Player
                    </Typography>
                </Box>

                {deadline && (
                    <Alert
                        severity={isBan ? "error" : "warning"}
                        icon={isBan ? <GavelRoundedIcon /> : <VolumeOffRoundedIcon />}
                        sx={{ py: 0.5 }}
                    >
                        <Typography variant="body2" sx={{ fontWeight: 600 }}>
                            {`${cap(doneText)} until ${deadline.toLocaleString()}`}
                        </Typography>
                        <Typography variant="caption" color="text.secondary">
                            {`Select "Un${actionText}" below to immediately lift this restriction.`}
                        </Typography>
                    </Alert>
                )}

                <Box component="form" onSubmit={handleSubmit} noValidate sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                    <BaseDialog title={msg} onDismiss={handleDismiss} />

                    <FormControl fullWidth size="small">
                        <InputLabel id={`${actionText}-duration-label`}>
                            {`${cap(actionText)} Duration`}
                        </InputLabel>
                        <Select
                            id={`${actionText}-duration`}
                            labelId={`${actionText}-duration-label`}
                            value={`${durationMinutes}`}
                            label={`${cap(actionText)} Duration`}
                            onChange={handleUpdateDuration}
                        >
                            {deadline && <MenuItem value={0}><strong>{`Un${actionText} (Lift penalty)`}</strong></MenuItem>}
                            <MenuItem value={15}>15 minutes</MenuItem>
                            <MenuItem value={30}>30 minutes</MenuItem>
                            <MenuItem value={60}>1 hour</MenuItem>
                            <MenuItem value={180}>3 hours</MenuItem>
                            <MenuItem value={720}>12 hours</MenuItem>
                            <MenuItem value={1440}>1 day</MenuItem>
                            <MenuItem value={4320}>3 days</MenuItem>
                            <MenuItem value={10080}>1 week</MenuItem>
                            <MenuItem value={43200}>1 month</MenuItem>
                            <MenuItem value={525600}>1 year</MenuItem>
                            <MenuItem value={52596000}>1 century (Permanent)</MenuItem>
                        </Select>
                    </FormControl>

                    <TextField
                        size="small"
                        required
                        fullWidth
                        id={`description-${actionText}`}
                        label="Reason / Description"
                        placeholder={`Reason for ${actionText}...`}
                        name="description"
                        value={description}
                        onChange={(e) => setDescription(e.target.value)}
                    />

                    <Button
                        type="submit"
                        fullWidth
                        variant={durationMinutes === 0 ? "outlined" : "contained"}
                        color={durationMinutes === 0 ? "success" : (isBan ? "error" : "warning")}
                        startIcon={isBan ? <GavelRoundedIcon /> : <VolumeOffRoundedIcon />}
                        disabled={disabled || processing || !description || !!msg}
                    >
                        {`${durationMinutes ? "" : "Un"}${cap(actionText)} ${handle}`}
                    </Button>

                    {processing && <LinearProgress sx={{ borderRadius: 1 }} />}
                </Box>
            </Stack>
        </Card>
    );
}