using System;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.WebSocket;

namespace EvoS.Framework.Network.NetworkMessages
{
    [Serializable]
    [EvosMessage(798)]
    public class EvosOptionsNotificationLegacy : WebSocketMessage
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

        public EvosOptionsNotification ToCurrent()
        {
	        return new EvosOptionsNotification
	        {
		        UserDialog = UserDialog,
		        DeviceIdentifier = DeviceIdentifier,

		        GraphicsQuality = GraphicsQuality,
		        WindowMode = WindowMode,
		        ResolutionWidth = ResolutionWidth,
		        ResolutionHeight = ResolutionHeight,
		        GameWindowMode = GameWindowMode,
		        GameResolutionWidth = GameResolutionWidth,
		        GameResolutionHeight = GameResolutionHeight,
		        LockWindowSize = LockWindowSize,

		        MasterVolume = MasterVolume,
		        MusicVolume = MusicVolume,
		        AmbianceVolume = AmbianceVolume,

		        LockCursorMode = LockCursorMode,
		        EnableChatter = EnableChatter,
		        RightClickingConfirmsAbilityTargets = RightClickingConfirmsAbilityTargets,
		        ShiftClickForMovementWaypoints = ShiftClickForMovementWaypoints,

		        ShowGlobalChat = ShowGlobalChat,
		        ShowAllChat = ShowAllChat,
		        EnableProfanityFilter = EnableProfanityFilter,
		        AutoJoinDiscord = AutoJoinDiscord,
		        VoicePushToTalk = VoicePushToTalk,
		        VoiceMute = VoiceMute,
		        VoiceVolume = VoiceVolume,
		        MicVolume = MicVolume,
		        GameModeVoiceChat = GameModeVoiceChat,

		        HideTutorialVideos = HideTutorialVideos,
		        AllowCancelActionWhileConfirmed = AllowCancelActionWhileConfirmed,

		        Region = Region,
		        OverrideGlyphLanguageCode = OverrideGlyphLanguageCode,

		        // Evos options
		        AllowResettingWaypoints = AllowResettingWaypoints,
		        ExtendedCooldownView = ExtendedCooldownView,
		        EnableGamepadControls = true,
		        EnableUniqueStatusEffectIcons = false,
	        };
        }
    }
}
