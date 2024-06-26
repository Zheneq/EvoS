import { useState } from "react";
import {
  GameData,
  PlayerData,
  ServerData,
  shutdownServer,
} from "../../lib/Evos";
import {
  Box,
  Button,
  SelectChangeEvent,
  Tooltip,
  Typography,
} from "@mui/material";
import Game from "./Game";
import { useAuthHeader } from "react-auth-kit";
import BaseDialog from "../generic/BaseDialog";
import { processError } from "../../lib/Error";
import { useNavigate } from "react-router-dom";

interface Props {
  info: ServerData;
  game?: GameData;
  playerData: Map<number, PlayerData>;
}

function buildTitle(info: ServerData, game?: GameData) {
    let suffix = "";
    if (game) {
        const subType = game.gameSubType.split('@')[0];
        suffix = ` - ${game.gameType} ${subType}`
    }
    return info.name + suffix;
}

export default function Server({ info, game, playerData }: Props) {
  const [msg, setMsg] = useState<string>();
  const [resultAction, setResultAction] = useState<string>("TieGame");
  const [processing, setProcessing] = useState<boolean>();
  const authHeader = useAuthHeader();
  const navigate = useNavigate();

  const handleChange = (event: SelectChangeEvent<typeof resultAction>) => {
    const {
      target: { value },
    } = event;
    setResultAction(value);
  };

  const shutdownServerHandler = () => {
    setProcessing(true);

    const abort = new AbortController();
    shutdownServer(abort, authHeader(), info.id)
      .then(() => setMsg("Server shutdown initiated."))
      .catch((e) => processError(e, (err) => setMsg(err.text), navigate))

    return () => abort.abort();
  };

  return (
    <>
      <Box
        display="flex"
        alignItems="center"
        gap={2}
        justifyContent="center"
        sx={{ p: 2 }}
      >
        <BaseDialog title={msg} onDismiss={() => setMsg(undefined)} />
        <Tooltip arrow title={info.id}>
          <Typography variant="h3">{buildTitle(info, game)}</Typography>
        </Tooltip>
        {game && info.name !== "Custom game" && (
            <Button
              disabled={processing}
              variant="contained"
              color="primary"
              onClick={shutdownServerHandler}
            >
              Shutdown Server
            </Button>
        )}
      </Box>
      {game && <Game info={game} playerData={playerData} expanded />}
    </>
  );
}
