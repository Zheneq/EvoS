import {Button, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle, IconButton} from "@mui/material";
import {DialogProps} from "@mui/material/Dialog/Dialog";
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import {copyToClipboard} from "../../lib/Lib";

interface BaseDialogProps {
    props?: DialogProps;
    title?: string;
    content?: string;
    dismissText?: string;
    acceptText?: string;
    onDismiss: () => void;
    onAccept?: () => void;
    copyTitle?: boolean;
}

export default function BaseDialog(
    {
        props,
        title,
        content,
        dismissText,
        acceptText,
        onDismiss,
        onAccept,
        copyTitle = false
    }: BaseDialogProps) {
    const dialogProps = props ?? {};

    const handleCopyTitle = () => {
        if (title) {
            copyToClipboard(title);
        }
    };

    const dismissCaption = dismissText ?? (onAccept ? "Cancel" : "Ok");

    return (
        <Dialog open={!!title} {...dialogProps}>
            <DialogTitle id="alert-dialog-title">
                {title}
                {copyTitle && title && (
                    <IconButton
                        aria-label="copy title"
                        onClick={handleCopyTitle}
                    >
                        <ContentCopyIcon sx={{ fontSize: '1rem' }} />
                    </IconButton>
                )}
            </DialogTitle>
            {content && <DialogContent>
                <DialogContentText id="alert-dialog-description">{content}</DialogContentText>
            </DialogContent>}
            <DialogActions>
                {onAccept && (
                    <Button onClick={onAccept} autoFocus>{acceptText ?? "Accept"}</Button>
                )}
                <Button onClick={onDismiss}>{dismissCaption}</Button>
            </DialogActions>
        </Dialog>
    );
}