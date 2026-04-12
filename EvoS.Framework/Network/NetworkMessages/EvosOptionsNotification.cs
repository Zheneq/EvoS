using System;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.WebSocket;

namespace EvoS.Framework.Network.NetworkMessages
{
    [Serializable]
    [EvosMessage(799)]
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
        public bool EnableGamepadControls;
        public bool EnableUniqueStatusEffectIcons;

        public static EvosOptionsNotification Of(OptionsNotification notify)
        {
	        return new EvosOptionsNotification
	        {
		        UserDialog = notify.UserDialog,
		        DeviceIdentifier = notify.DeviceIdentifier,

		        GraphicsQuality = notify.GraphicsQuality,
		        WindowMode = notify.WindowMode,
		        ResolutionWidth = notify.ResolutionWidth,
		        ResolutionHeight = notify.ResolutionHeight,
		        GameWindowMode = notify.GameWindowMode,
		        GameResolutionWidth = notify.GameResolutionWidth,
		        GameResolutionHeight = notify.GameResolutionHeight,
		        LockWindowSize = notify.LockWindowSize,

		        MasterVolume = notify.MasterVolume,
		        MusicVolume = notify.MusicVolume,
		        AmbianceVolume = notify.AmbianceVolume,

		        LockCursorMode = notify.LockCursorMode,
		        EnableChatter = notify.EnableChatter,
		        RightClickingConfirmsAbilityTargets = notify.RightClickingConfirmsAbilityTargets,
		        ShiftClickForMovementWaypoints = notify.ShiftClickForMovementWaypoints,

		        ShowGlobalChat = notify.ShowGlobalChat,
		        ShowAllChat = notify.ShowAllChat,
		        EnableProfanityFilter = notify.EnableProfanityFilter,
		        AutoJoinDiscord = notify.AutoJoinDiscord,
		        VoicePushToTalk = notify.VoicePushToTalk,
		        VoiceMute = notify.VoiceMute,
		        VoiceVolume = notify.VoiceVolume,
		        MicVolume = notify.MicVolume,
		        GameModeVoiceChat = notify.GameModeVoiceChat,

		        HideTutorialVideos = notify.HideTutorialVideos,
		        AllowCancelActionWhileConfirmed = notify.AllowCancelActionWhileConfirmed,

		        Region = notify.Region,
		        OverrideGlyphLanguageCode = notify.OverrideGlyphLanguageCode,

		        // Evos options
		        AllowResettingWaypoints = false,
		        ExtendedCooldownView = false,
		        EnableGamepadControls = true,
		        EnableUniqueStatusEffectIcons = false,
	        };
        }
    }
}
