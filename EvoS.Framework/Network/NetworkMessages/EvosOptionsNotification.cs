using System;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.WebSocket;

namespace EvoS.Framework.Network.NetworkMessages
{
    [Serializable]
    [EvosMessage(798)]
    public class EvosOptionsNotification : WebSocketMessage
    {
        [NonSerialized]
        public new static bool LogData = false;
        
        public bool UserDialog;
        public string DeviceIdentifier;
		
        public byte GraphicsQuality;
        public byte WindowMode;
        public short ResolutionWidth;
        public short ResolutionHeight;
        public byte GameWindowMode;
        public short GameResolutionWidth;
        public short GameResolutionHeight;
        public bool LockWindowSize;
		
        public byte MasterVolume;
        public byte MusicVolume;
        public byte AmbianceVolume;
		
        public byte LockCursorMode;
        public bool EnableChatter;
        public bool RightClickingConfirmsAbilityTargets;
        public bool ShiftClickForMovementWaypoints;
		
        public bool ShowGlobalChat;
        public bool ShowAllChat;
        public bool EnableProfanityFilter;
        public bool AutoJoinDiscord;
        public bool VoicePushToTalk;
        public bool VoiceMute;
        public float VoiceVolume;
        public float MicVolume;
        public byte GameModeVoiceChat;
		
        public bool HideTutorialVideos;
        public bool AllowCancelActionWhileConfirmed;
		
        public Region Region;
        public string OverrideGlyphLanguageCode;
		
        // Evos options
        public bool AllowResettingWaypoints;
        public bool ExtendedCooldownView;
    }
}
