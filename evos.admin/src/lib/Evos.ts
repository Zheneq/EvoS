import axios from "axios";

export interface LoginResponse {
    handle: string;
    token: string;
    banner: number;
}

export interface Status {
    players: PlayerData[];
    groups: GroupData[];
    queues: QueueData[];
    servers: ServerData[];
    games: GameData[];
}

export interface PlayerData {
    accountId: number;
    handle: string;
    bannerBg: number;
    bannerFg: number;
    status: string;
    titleId: number;
    factionData: PlayerFactionData;
    buildVersion: string;
}

export interface PlayerFactionData {
    factions: number[];
    selectedRibbonID: number;
}

export interface PlayerDetails {
    player: PlayerData;
    bannedUntil?: string;
    mutedUntil?: string;
}

export interface GroupData {
    groupId: number;
    accountIds: number[];
}

export interface QueueData {
    type: string;
    subtype: string;
    groupIds: number[];
}

export interface ServerData {
    id: string;
    name: string;
}

export interface GameData {
    id: string;
    ts: string;
    server: string;
    teamA: GamePlayerData[];
    teamB: GamePlayerData[];
    map: MapType;
    status: string;
    turn: number;
    teamAScore: number;
    teamBScore: number;
    gameType: string;
    gameSubType: string;
}

export interface GamePlayerData {
    accountId: number;
    characterType: CharacterType;
}

export interface PenaltyInfo {
    accountId: number;
    durationMinutes: number;
    description: string
}

export interface RegistrationCodeRequest {
    issueFor: string;
}

export interface RegistrationCodeResponse {
    code: string;
}

export interface RegistrationCodeEntry {
    code: string;
    issuedBy: number;
    issuedByHandle: string;
    issuedTo: number;
    issuedToHandle: string;
    issuedAt: string;
    expiresAt: string;
    usedAt: string;
}

export interface RegistrationCodesResponse {
    entries: RegistrationCodeEntry[];
}

export interface SearchResults {
    players: PlayerData[];
}

export interface AdminMessage {
    from: number;
    fromHandle: string;
    text: string;
    sentAt: string;
    viewedAt: string;
}

export interface AdminMessagesResponse {
    entries: AdminMessage[];
}

export enum CharacterType {
    None = 'None',
    BattleMonk = 'BattleMonk',
    BazookaGirl = 'BazookaGirl',
    DigitalSorceress = 'DigitalSorceress',
    Gremlins = 'Gremlins',
    NanoSmith = 'NanoSmith',
    RageBeast = 'RageBeast',
    RobotAnimal = 'RobotAnimal',
    Scoundrel = 'Scoundrel',
    Sniper = 'Sniper',
    SpaceMarine = 'SpaceMarine',
    Spark = 'Spark',
    TeleportingNinja = 'TeleportingNinja',
    Thief = 'Thief',
    Tracker = 'Tracker',
    Trickster = 'Trickster',
    PunchingDummy = 'PunchingDummy',
    Rampart = 'Rampart',
    Claymore = 'Claymore',
    Blaster = 'Blaster',
    FishMan = 'FishMan',
    Exo = 'Exo',
    Soldier = 'Soldier',
    Martyr = 'Martyr',
    Sensei = 'Sensei',
    PendingWillFill = 'PendingWillFill',
    Manta = 'Manta',
    Valkyrie = 'Valkyrie',
    Archer = 'Archer',
    TestFreelancer1 = 'TestFreelancer1',
    TestFreelancer2 = 'TestFreelancer2',
    Samurai = 'Samurai',
    Gryd = 'Gryd',
    Cleric = 'Cleric',
    Neko = 'Neko',
    Scamp = 'Scamp',
    FemaleWillFill = 'FemaleWillFill',
    Dino = 'Dino',
    Iceborg = 'Iceborg',
    Fireborg = 'Fireborg',
    Last = 'Last',
}

export enum MapType {
    CargoShip_Deathmatch = 'CargoShip_Deathmatch',
    Casino01_Deathmatch = 'Casino01_Deathmatch',
    EvosLab_Deathmatch = 'EvosLab_Deathmatch',
    Oblivion_Deathmatch = 'Oblivion_Deathmatch',
    Reactor_Deathmatch = 'Reactor_Deathmatch',
    RobotFactory_Deathmatch = 'RobotFactory_Deathmatch',
    Skyway_Deathmatch = 'Skyway_Deathmatch',
}

export enum PendingShutdownType {
    None = 'None',
    Now = 'Now',
    WaitForGamesToEnd = 'WaitForGamesToEnd',
    WaitForPlayersToLeave = 'WaitForPlayersToLeave',
}

export interface ServerMessageData {
    msg: any;
    severity: EvosServerMessageSeverity;
}

export enum Language {
    en = 'en',
    fr = 'fr',
    de = 'de',
    ru = 'ru',
    es = 'es',
    it = 'it',
    pl = 'pl',
    pt = 'pt',
    ko = 'ko',
    zh = 'zh',
    nl = 'nl',
    br = 'br',
}

export enum EvosServerMessageType {
    MessageOfTheDay = 'MessageOfTheDay',
    MessageOfTheDayPopup = 'MessageOfTheDayPopup',
    LauncherMessageOfTheDay = 'LauncherMessageOfTheDay',
    LauncherNotification = 'LauncherNotification',
}

export enum EvosServerMessageSeverity {
    Warning = 'Warning',
    Success = 'Success',
    Error = 'Error',
    Info = 'Info',
}

export const MessagesWithMetadata = new Set([
    EvosServerMessageType.LauncherMessageOfTheDay,
    EvosServerMessageType.LauncherNotification
]);

export function asDate(date?: string): Date | undefined {
    return date ? new Date(date) : undefined;
}

export function formatDate(ts: string): string {
    return ts ? new Date(ts).toLocaleString() : "N/A";
}

export function cap(txt: string): string {
    return txt.charAt(0).toUpperCase() + txt.slice(1);
}

export function toMap<I, K, V>(input: I[], keyMapper: (i: I) => K, valueMapper: (i: I) => V): Map<K, V> {
    const res = new Map<K, V>();
    input.forEach(i => res.set(keyMapper(i), valueMapper(i)));
    return res;
}

export function makeServerMsgData(msgMap: Map<Language, string>, severity: EvosServerMessageSeverity): ServerMessageData {
    return {
        msg: Object.fromEntries(msgMap),
        severity: severity,
    }
}

const baseUrl = ""

export function login(abort: AbortController, username: string, password: string) {
    return axios.post<LoginResponse>(
        baseUrl + "/api/admin/login",
        { UserName: username, Password: password },
        { signal: abort.signal });
}

export function getStatus(authHeader: string) {
    return axios.get<Status>(
        baseUrl + "/api/admin/lobby/status",
        { headers: { 'Authorization': authHeader } });
}

export function findPlayers(abort: AbortController, authHeader: string, query: string) {
    return axios.get<SearchResults>(
        baseUrl + "/api/admin/player/find",
        { params: { query: query }, headers: { 'Authorization': authHeader }, signal: abort.signal });
}

export function getPlayer(abort: AbortController, authHeader: string, accountId: number) {
    return axios.get<PlayerDetails>(
        baseUrl + "/api/admin/player/details",
        { params: { AccountId: accountId }, headers: { 'Authorization': authHeader }, signal: abort.signal });
}

export function getPlayers(abort: AbortController, authHeader: string, accountIds: number[]) {
    return axios.post<SearchResults>(
        baseUrl + "/api/admin/player/details",
        { accountIds: accountIds },
        { headers: { 'Authorization': authHeader }, signal: abort.signal });
}

export function pauseQueue(abort: AbortController, authHeader: string, paused: boolean) {
    return axios.put(
        baseUrl + "/api/admin/queue/paused",
        { Paused: paused },
        { headers: { 'Authorization': authHeader }, signal: abort.signal });
}

export function scheduleShutdown(abort: AbortController, authHeader: string, type: PendingShutdownType) {
    return axios.put(
        baseUrl + "/api/admin/server/shutdown",
        { Type: type },
        { headers: { 'Authorization': authHeader }, signal: abort.signal });
}

export function broadcast(abort: AbortController, authHeader: string, message: string) {
    return axios.post(
        baseUrl + "/api/admin/lobby/broadcast",
        { Msg: message },
        { headers: { 'Authorization': authHeader }, signal: abort.signal });
}

export function mute(authHeader: string, penaltyInfo: PenaltyInfo) {
    return axios.post(
        baseUrl + "/api/admin/player/muted",
        penaltyInfo,
        { headers: { 'Authorization': authHeader } });
}

export function ban(authHeader: string, penaltyInfo: PenaltyInfo) {
    return axios.post(
        baseUrl + "/api/admin/player/banned",
        penaltyInfo,
        { headers: { 'Authorization': authHeader } });
}

export function sendAdminMessage(abort: AbortController, authHeader: string, accountId: number, msg: string) {
    return axios.post(
        baseUrl + "/api/admin/player/adminMessage",
        { accountId: accountId, description: msg },
        { headers: { 'Authorization': authHeader }, signal: abort.signal });
}

export function getAdminMessages(abort: AbortController, authHeader: string, accountId: number) {
    return axios.get<AdminMessagesResponse>(
        baseUrl + "/api/admin/player/adminMessage",
        { params: { accountId: accountId }, headers: { 'Authorization': authHeader }, signal: abort.signal });
}

export function issueRegistrationCode(abort: AbortController, authHeader: string, data: RegistrationCodeRequest) {
    return axios.post<RegistrationCodeResponse>(
        baseUrl + "/api/admin/player/registrationCode",
        data,
        { headers: { 'Authorization': authHeader }, signal: abort.signal });
}

export function getRegistrationCodes(abort: AbortController, authHeader: string, before: Date) {
    return axios.get<RegistrationCodesResponse>(
        baseUrl + "/api/admin/player/registrationCode",
        { params: { before: Math.floor(before.getTime() / 1000) }, headers: { 'Authorization': authHeader }, signal: abort.signal });
}

export function generateTempPassword(abort: AbortController, authHeader: string, accountId: number) {
    return axios.post<RegistrationCodeResponse>(
        baseUrl + "/api/admin/player/generateTempPassword",
        { accountId: accountId },
        { headers: { 'Authorization': authHeader }, signal: abort.signal });
}

export function getMotd(abort: AbortController, type: EvosServerMessageType) {
    return axios.get<ServerMessageData>(
        baseUrl + "/api/admin/lobby/motd/" + type,
        { signal: abort.signal });
}

export function setMotd(abort: AbortController, authHeader: string, type: EvosServerMessageType, msg: ServerMessageData) {
    return axios.put(
        baseUrl + "/api/admin/lobby/motd/" + type,
        msg,
        { headers: { 'Authorization': authHeader }, signal: abort.signal });
}

export function shutdownServer(abort: AbortController, authHeader: string, processCode: string) {
    return axios.post(
        baseUrl + "/api/admin/server/shutdowngame",
        { processCode: processCode},
        { headers: { 'Authorization': authHeader }, signal: abort.signal });
}

export function reloadProxyConfig(abort: AbortController, authHeader: string) {
    return axios.post(
        baseUrl + "/api/admin/proxy/reload",
        {},
        { headers: { 'Authorization': authHeader }, signal: abort.signal });
}

export interface ChatMessage {
    message: string;
    senderId: number;
    senderHandle: string;
    game: string;
    time: string;
    character: CharacterType;
    team: Team;
    isMuted: boolean;
    recipients: number[];
    blockedRecipients: number[];
    type: ChatType;
}

export interface ChatHistoryResponse {
    messages: ChatMessage[];
}

export enum Team {
    Invalid = 'Invalid',
    TeamA = 'TeamA',
    TeamB = 'TeamB',
    Objects = 'Objects',
    Spectator = 'Spectator'
}

export enum ChatType {
    GlobalChat = 'GlobalChat',
    GameChat = 'GameChat',
    TeamChat = 'TeamChat',
    GroupChat = 'GroupChat',
    WhisperChat = 'WhisperChat',
    CombatLog = 'CombatLog',
    SystemMessage = 'SystemMessage',
    Error = 'Error',
    Exception = 'Exception',
    BroadcastMessage = 'BroadcastMessage',
    PingChat = 'PingChat',
    ScriptedChat = 'ScriptedChat',
    NUM_VALUES = 'NUM_VALUES'
}

export const chatTypeColors = new Map<ChatType, string>([
    [ChatType.GlobalChat, 'rgba(80,80,80,0)'],
    [ChatType.GameChat, '#655a01'],
    [ChatType.TeamChat, '#004f6a'],
    [ChatType.GroupChat, '#146700'],
    [ChatType.WhisperChat, '#5a015a'],
    [ChatType.CombatLog, '#006053'],
    [ChatType.SystemMessage, '#006053'],
    [ChatType.Error, '#006053'],
    [ChatType.Exception, '#006053'],
    [ChatType.BroadcastMessage, '#006053'],
    [ChatType.PingChat, '#006053'],
    [ChatType.ScriptedChat, '#006053'],
]);

export interface UserFeedback {
    accountId: number;
    time: string;
    context: string;
    message: string;
    reason: FeedbackReason;
    reportedPlayerAccountId: number;
    reportedPlayerHandle: string;
}

export interface UserFeedbackResponse {
    feedback: UserFeedback[];
}

export enum FeedbackReason {
    None = 'None',
    Suggestion = 'Suggestion',
    Bug = 'Bug',
    UnsportsmanlikeConduct = 'UnsportsmanlikeConduct',
    VerbalHarassment = 'VerbalHarassment',
    LeavingTheGameAFK = 'LeavingTheGameAFK',
    HateSpeech = 'HateSpeech',
    IntentionallyFeeding = 'IntentionallyFeeding',
    SpammingAdvertising = 'SpammingAdvertising',
    OffensiveName = 'OffensiveName',
    Other = 'Other',
    Botting = 'Botting'
}

export function getChatHistory(
    abort: AbortController, 
    authHeader: string, 
    accountId: number, 
    date: number,
    before: boolean,
    includeBlocked?: boolean,
    includeGeneral?: boolean,
    limit?: number
) {
    return axios.get<ChatHistoryResponse>(
        baseUrl + "/api/admin/moderation/chatHistory",
        { 
            params: { 
                accountId: accountId,
                after: before ? undefined : date,
                before: before ? date : undefined,
                includeBlocked: includeBlocked,
                includeGeneral: includeGeneral,
                limit: limit
            }, 
            headers: { 'Authorization': authHeader }, 
            signal: abort.signal 
        }
    );
}

export function getSentFeedback(
    abort: AbortController,
    authHeader: string,
    accountId: number
) {
    return axios.get<UserFeedbackResponse>(
        baseUrl + "/api/admin/moderation/sentFeedback",
        {
            params: { accountId: accountId },
            headers: { 'Authorization': authHeader },
            signal: abort.signal
        }
    );
}

export function getReceivedFeedback(
    abort: AbortController,
    authHeader: string,
    accountId: number
) {
    return axios.get<UserFeedbackResponse>(
        baseUrl + "/api/admin/moderation/receivedFeedback",
        {
            params: { accountId: accountId },
            headers: { 'Authorization': authHeader },
            signal: abort.signal
        }
    );
}

export enum PlayerGameResult {
    NoResult = 'NoResult',
    Tie = 'Tie',
    Win = 'Win',
    Lose = 'Lose'
}

export const resultColors = new Map<PlayerGameResult, string>([
    [PlayerGameResult.NoResult, 'rgba(0,0,0,0)'],
    [PlayerGameResult.Tie, 'rgba(255,255,0,0.16)'],
    [PlayerGameResult.Win, 'rgba(0,255,0,0.1)'],
    [PlayerGameResult.Lose, 'rgba(255,0,0,0.1)'],
]);

export interface MatchActor {
    character: CharacterType;
    team: Team;
    isPlayer: boolean;
}

export interface MatchComponent {
    matchTime: string;
    result: PlayerGameResult;
    kills: number;
    characterUsed: CharacterType;
    gameType: string;
    mapName: string;
    turnsPlayed: number;
    gameMode: string;
    participants: MatchActor[];
}

export interface MatchDetails {
    deaths: number;
    takedowns: number;
    damageDealt: number;
    damageTaken: number;
    healing: number;
    contribution: number;
    matchResults: MatchResultsStats;
    groupSize: number;
    tier: string;
    points: number;
}

export interface TeamStatline {
    player: PlayerIdentity;
    character: CharacterInfo;
    combatStats: CombatStats;
    performance: PerformanceStats;
    customization: PlayerCustomization;
    abilityMods: number[];
    unusedCatalysts: CatalystPhaseInfo;
}

export interface PlayerIdentity {
    playerId: number;
    accountId: number;
    displayName: string;
    isPerspectivePlayer: boolean;
    isAlly: boolean;
    playerType: string;
}

export interface CharacterInfo {
    type: CharacterType;
}

export interface CombatStats {
    kills: number;
    deaths: number;
    damageDealt: number;
    damageTaken: number;
    healing: number;
    assists: number;
    absorbedDamage: number;
}

export interface PerformanceStats {
    turnsPlayed: number;
    averageLockInTime: number;
    contributionScore: number;
}

export interface PlayerCustomization {
    titleId: number;
    titleLevel: number;
    bannerId: number;
    emblemId: number;
    ribbonId: number;
}

export interface CatalystPhaseInfo {
    hasPrepPhase: boolean;
    hasDashPhase: boolean;
    hasBlastPhase: boolean;
}

export interface Score {
    teamAScore: number;
    teamBScore: number;
}

export interface MatchResultsStats {
    friendlyTeamStats: TeamStatline[];
    enemyTeamStats: TeamStatline[];
    score: Score;
    victoryCondition: string;
    victoryConditionTurns: number;
    gameDuration: number;
}

export interface MatchFreelancerStats {
    characterType: CharacterType;
    totalAssists: number;
    totalDeaths: number;
    totalBadgePoints: number;
    energyGainPerTurn: number;
    damagePerTurn: number;
    damageEfficiency: number;
    killParticipation: number;
    supportPerTurn: number;
    damageTakenPerTurn: number;
    damageDonePerLife: number;
    mmr: number;
    teamMitigation: number;
    totalTurns: number;
}

export interface MatchData {
    createDate: string;
    updateDate: string;
    gameServerProcessCode: string;
    matchComponent: MatchComponent;
    matchDetailsComponent: MatchDetails;
    matchFreelancerStats: MatchFreelancerStats;
}

export function getMatch(abort: AbortController, authHeader: string, accountId: number, matchId: string) {
    return axios.get<MatchData>(
        baseUrl + "/api/admin/match",
        {
            params: {
                accountId: accountId,
                matchId: matchId
            },
            headers: { 'Authorization': authHeader },
            signal: abort.signal
        }
    );
}

export interface MatchHistoryEntry {
    matchId: string;
    matchTime: string;
    character: CharacterType;
    gameType: string;
    subType: string;
    mapName: string;
    numOfTurns: number;
    teamAScore: number;
    teamBScore: number;
    team: Team;
    result: PlayerGameResult;
}

export interface MatchHistoryResponse {
    matches: MatchHistoryEntry[];
}

export function getMatchHistory(
    abort: AbortController,
    authHeader: string,
    accountId: number,
    date: number,
    before: boolean,
    limit?: number
) {
    return axios.get<MatchHistoryResponse>(
        baseUrl + "/api/admin/player/matches",
        {
            params: {
                accountId: accountId,
                after: before ? undefined : date,
                before: before ? date : undefined,
                limit: limit
            },
            headers: { 'Authorization': authHeader },
            signal: abort.signal
        }
    );
}