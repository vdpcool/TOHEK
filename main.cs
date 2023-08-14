using AmongUs.GameOptions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TOHE.Roles.Neutral;
using UnityEngine;

[assembly: AssemblyFileVersion(TOHE.Main.PluginVersion)]
[assembly: AssemblyInformationalVersion(TOHE.Main.PluginVersion)]
[assembly: AssemblyVersion(TOHE.Main.PluginVersion)]
namespace TOHE;

[BepInPlugin(PluginGuid, "TOHE", PluginVersion)]
[BepInIncompatibility("jp.ykundesu.supernewroles")]
[BepInProcess("Among Us.exe")]
public class Main : BasePlugin
{
    // == プログラム設定 / Program Config ==
    public static readonly string ModName = "TOHE-K";
    public static readonly string ModColor = "#ffc0cb";
    public static readonly bool AllowPublicRoom = true;
    public static readonly string ForkId = "TOHEK";
    public const string OriginalForkId = "OriginalTOH";
    public static HashAuth DebugKeyAuth { get; private set; }
    public const string DebugKeyHash = "c0fd562955ba56af3ae20d7ec9e64c664f0facecef4b3e366e109306adeae29d";
    public const string DebugKeySalt = "59687b";
    public static ConfigEntry<string> DebugKeyInput { get; private set; }
    public static readonly string MainMenuText = " ";
    public const string PluginGuid = "com.k.tohek";
    public const string PluginVersion = "2.5.1";
    public const string CanaryPluginVersion = "Canary Version";
    public const int PluginCreate = 3;
    public const bool Canary = true;

    public static readonly bool ShowQQButton = true;
    public static readonly string QQInviteUrl = "https://jq.qq.com/?_wv=1027&k=2RpigaN6";
    public static readonly bool ShowDiscordButton = true;
    public static readonly string DiscordInviteUrl = "https://discord.gg/tohe";

    public Harmony Harmony { get; } = new Harmony(PluginGuid);
    public static Version version = Version.Parse(PluginVersion);
    public static BepInEx.Logging.ManualLogSource Logger;
    public static bool hasArgumentException = false;
    public static string ExceptionMessage;
    public static bool ExceptionMessageIsShown = false;
    public static bool AlreadyShowMsgBox = false;
    public static string credentialsText;
    public static NormalGameOptionsV07 NormalOptions => GameOptionsManager.Instance.currentNormalGameOptions;
    //Client Options
    public static ConfigEntry<string> HideName { get; private set; }
    public static ConfigEntry<string> HideColor { get; private set; }
    public static ConfigEntry<int> MessageWait { get; private set; }
    public static ConfigEntry<bool> UnlockFPS { get; private set; }
    public static ConfigEntry<bool> AutoStart { get; private set; }
    public static ConfigEntry<bool> AutoPlayAgain { get; private set; }
    public static ConfigEntry<bool> ForceOwnLanguage { get; private set; }
    public static ConfigEntry<bool> ForceOwnLanguageRoleName { get; private set; }
    public static ConfigEntry<bool> EnableCustomButton { get; private set; }
    public static ConfigEntry<bool> EnableCustomSoundEffect { get; private set; }
    public static ConfigEntry<bool> SwitchVanilla { get; private set; }
    public static ConfigEntry<bool> DumpLog { get; private set; }
    public static ConfigEntry<bool> CanPublic { get; private set; }
    public static ConfigEntry<bool> ModeForSmallScreen { get; private set; }
    public static ConfigEntry<bool> ShowLobbyCode { get; private set; }
    public static ConfigEntry<bool> VersionCheat { get; private set; }
    public static ConfigEntry<bool> GodMode { get; private set; }

    public static Dictionary<byte, PlayerVersion> playerVersion = new();
    //Preset Name Options
    public static ConfigEntry<string> Preset1 { get; private set; }
    public static ConfigEntry<string> Preset2 { get; private set; }
    public static ConfigEntry<string> Preset3 { get; private set; }
    public static ConfigEntry<string> Preset4 { get; private set; }
    public static ConfigEntry<string> Preset5 { get; private set; }
    //Other Configs
    public static ConfigEntry<string> WebhookURL { get; private set; }
    public static ConfigEntry<string> BetaBuildURL { get; private set; }
    public static ConfigEntry<float> LastKillCooldown { get; private set; }
    public static ConfigEntry<float> LastShapeshifterCooldown { get; private set; }
    public static OptionBackupData RealOptionsData;
    public static Dictionary<byte, PlayerState> PlayerStates = new();
    public static Dictionary<byte, string> AllPlayerNames = new();
    public static Dictionary<byte, CustomRoles> AllPlayerCustomRoles;
    public static Dictionary<(byte, byte), string> LastNotifyNames;
    public static Dictionary<byte, Color32> PlayerColors = new();
    public static Dictionary<byte, PlayerState.DeathReason> AfterMeetingDeathPlayers = new();
    public static Dictionary<CustomRoles, string> roleColors;
    public static bool IsFixedCooldown => CustomRoles.Vampire.IsEnable() || CustomRoles.Poisoner.IsEnable();
    public static float RefixCooldownDelay = 0f;
    public static GameData.PlayerInfo LastVotedPlayerInfo;
    public static string LastVotedPlayer;
    public static List<byte> ResetCamPlayerList = new();
    public static List<byte> winnerList = new();
    public static List<string> winnerNameList = new();
    public static List<int> clientIdList = new();
    public static List<(string, byte, string)> MessagesToSend = new();
    public static bool isChatCommand = false;
    public static List<PlayerControl> LoversPlayers = new();
    public static List<PlayerControl> seniormanagementPlayers = new();
    public static bool isLoversDead = true;
    public static bool isseniormanagementDead = true;
    public static Dictionary<byte, float> AllPlayerKillCooldown = new();
    public static Dictionary<byte, Vent> LastEnteredVent = new();
    public static Dictionary<byte, Vector2> LastEnteredVentLocation = new();
    public static List<byte> CyberStarDead = new();
    public static List<byte> CaptainDead = new();
    public static List<byte> WorkaholicAlive = new();
    public static List<byte> BaitAlive = new();
    public static List<byte> BoobyTrapBody = new();
    public static List<byte> BoobyTrapKiller = new();
    public static Dictionary<byte, byte> KillerOfBoobyTrapBody = new();
    public static Dictionary<byte, string> DetectiveNotify = new();
    public static Dictionary<byte, string> VirusNotify = new();
    public static List<byte> OverDeadPlayerList = new();
    public static bool DoBlockNameChange = false;
    public static int updateTime;
    public static bool newLobby = false;
    public static Dictionary<int, int> SayStartTimes = new();
    public static Dictionary<int, int> SayBanwordsTimes = new();
    public static Dictionary<byte, float> AllPlayerSpeed = new();
    public const float MinSpeed = 0.0001f;
    public static List<byte> CleanerBodies = new();
    public static List<byte> MedusaBodies = new();
    public static List<byte> JanitorBodies = new();
    public static List<byte> ForHotPotato = new();
    public static List<byte> ForHotCold = new();
    public static List<byte> ForExorcist = new();
    public static Dictionary<byte, int> ExorcistMax = new();
    public static List<byte> InfectedBodies = new();
    public static List<byte> BrakarVoteFor = new();
    public static Dictionary<byte, int> ChairmanUsedButtonCount = new();
    public static Dictionary<byte, int> LoveCutterKilledCount = new();
    public static Dictionary<byte, (byte, float)> BitPlayers = new();
    public static Dictionary<byte, float> WarlockTimer = new();
    public static Dictionary<byte, float> AssassinTimer = new();
    public static Dictionary<byte, PlayerControl> CursedPlayers = new();
    public static Dictionary<byte, bool> isCurseAndKill = new();
    public static Dictionary<byte, int> MafiaRevenged = new();
    public static Dictionary<byte, int> RetributionistRevenged = new();
    public static Dictionary<byte, int> GuesserGuessed = new();
    public static Dictionary<byte, int> CapitalismAddTask = new();
    public static Dictionary<byte, int> CapitalismAssignTask = new();
    public static Dictionary<byte, Vector2> TimeMasterLocation = new();
    public static Dictionary<byte, long> TimeMasterInProtect = new();
    public static Dictionary<byte, int> TimeMasterNum = new();
    public static Dictionary<byte, Vector2> TimeMasterbacktrack = new();
    public static Dictionary<byte, long> TimeStopsInProtect = new();
    public static List<byte> TimeStopsstop = new();
    public static Dictionary<byte, int> JealousyMax = new();
    public static List<byte> ForJealousy = new();
    public static Dictionary<byte, int> YinLangKillMax = new();
    public static Dictionary<byte, int> SlaveownerMax = new();
    public static List<byte> ForSlaveowner = new();
    public static List<byte> ForET = new();
    public static Dictionary<byte, long> RudepeopleInProtect = new();
    public static Dictionary<byte, int> RudepeopleNumOfUsed = new();
    public static List<byte> ForKing = new();
    public static List<byte> KingCanpc = new(); 
    public static List<byte> KingCanKill = new();
    public static List<byte> ForSourcePlague = new();
    public static Dictionary<byte, int> DisorderKillCooldownMax = new();
    public static Dictionary<byte, int> KilledAntidote = new();
    public static List<byte> KilledDiseased = new();
    public static Dictionary<byte, int> ScoutImpotors = new();
    public static Dictionary<byte, int> ScoutCrewmate = new();
    public static Dictionary<byte, int> ScoutNeutral = new();
    public static List<byte> QSRInProtect = new();
    public static Dictionary<byte, int> MasochismKillMax = new();
    public static Dictionary<byte, byte> CovenLeaderList = new();
    public static List<byte> DemolitionManiacKill = new();
    public static Dictionary<byte, long> InBoom = new();
    public static List<byte> ForDemolition = new();
    public static Dictionary<byte, int> MaxCowboy = new();
    public static Dictionary<byte, int> GuideMax = new();
    public static Dictionary<byte, int> ManipulatorImpotors = new();
    public static Dictionary<byte, int> ManipulatorCrewmate = new();
    public static Dictionary<byte, int> ManipulatorNeutral = new();
    public static Dictionary<byte, string> ManipulatorNotify = new();
    public static Dictionary<byte, int> CrushMax = new();
    public static Dictionary<(byte, byte), bool> isDoused = new();
    public static Dictionary<(byte, byte), bool> isDraw = new();
    public static Dictionary<(byte, byte), bool> isRevealed = new();
    public static Dictionary<byte, (PlayerControl, float)> ArsonistTimer = new();
    public static Dictionary<byte, (PlayerControl, float)> RevolutionistTimer = new();
    public static Dictionary<byte, (PlayerControl, float)> FarseerTimer = new();
    public static Dictionary<byte, long> RevolutionistStart = new();
    public static Dictionary<byte, long> RevolutionistLastTime = new();
    public static Dictionary<byte, int> RevolutionistCountdown = new();
    public static Dictionary<byte, byte> PuppeteerList = new();
    public static Dictionary<byte, byte> TaglockedList = new();
    public static Dictionary<byte, byte> SpeedBoostTarget = new();
    public static Dictionary<byte, int> MayorUsedButtonCount = new();
    public static Dictionary<byte, int> SnitchUsedButtonCount = new();
    public static Dictionary<byte, int> ParaUsedButtonCount = new();
    public static Dictionary<byte, int> MarioVentCount = new();
    public static Dictionary<byte, long> VeteranInProtect = new();
    public static Dictionary<byte, int> VeteranNumOfUsed = new();
    public static Dictionary<byte, long> GrenadierBlinding = new();
    public static Dictionary<byte, long> MadGrenadierBlinding = new();
    public static Dictionary<byte, int> CursedWolfSpellCount = new();
    public static Dictionary<byte, int> JinxSpellCount = new();
    public static int AliveImpostorCount;
    public static bool isCursed;
    public static Dictionary<byte, bool> CheckShapeshift = new();
    public static Dictionary<byte, byte> ShapeshiftTarget = new();
    public static Dictionary<(byte, byte), string> targetArrows = new();
    public static Dictionary<byte, Vector2> EscapeeLocation = new();
    public static bool VisibleTasksCount = false;
    public static string nickName = "";
    public static bool introDestroyed = false;
    public static int DiscussionTime;
    public static int VotingTime;
    public static byte currentDousingTarget = byte.MaxValue;
    public static byte currentDrawTarget = byte.MaxValue;
    public static float DefaultCrewmateVision;
    public static float DefaultImpostorVision;
    public static bool IsInitialRelease = DateTime.Now.Month == 1 && DateTime.Now.Day is 17;
    public static bool IsAprilFools = DateTime.Now.Month == 4 && DateTime.Now.Day is 1;
    public static bool ResetOptions = true;
    public static byte FirstDied = byte.MaxValue;
    public static byte ShieldPlayer = byte.MaxValue;
    public static int MadmateNum = 0;
    public static int BardCreations = 0;
    public static Dictionary<byte, byte> Provoked = new();
    public static Dictionary<byte, int> DovesOfNeaceNumOfUsed = new();

    public static Dictionary<byte, CustomRoles> DevRole = new();
    public static byte ShamanTarget = byte.MaxValue;

    public static IEnumerable<PlayerControl> AllPlayerControls => PlayerControl.AllPlayerControls.ToArray().Where(p => p != null);
    public static IEnumerable<PlayerControl> AllAlivePlayerControls => PlayerControl.AllPlayerControls.ToArray().Where(p => p != null && p.IsAlive() && !p.Data.Disconnected && !Pelican.IsEaten(p.PlayerId));

    public static Main Instance;

    //一些很新的东东

    public static string OverrideWelcomeMsg = "";
    public static int HostClientId;

    public static List<string> TName_Snacks_CN = new() { "冰激凌", "奶茶", "巧克力", "蛋糕", "甜甜圈", "可乐", "柠檬水", "冰糖葫芦", "果冻", "糖果", "牛奶", "抹茶", "烧仙草", "菠萝包", "布丁", "椰子冻", "曲奇", "红豆土司", "三彩团子", "艾草团子", "泡芙", "可丽饼", "桃酥", "麻薯", "鸡蛋仔", "马卡龙", "雪梅娘", "炒酸奶", "蛋挞", "松饼", "西米露", "奶冻", "奶酥", "可颂", "奶糖" };
    public static List<string> TName_Snacks_EN = new() { "Ice cream", "Milk tea", "Chocolate", "Cake", "Donut", "Coke", "Lemonade", "Candied haws", "Jelly", "Candy", "Milk", "Matcha", "Burning Grass Jelly", "Pineapple Bun", "Pudding", "Coconut Jelly", "Cookies", "Red Bean Toast", "Three Color Dumplings", "Wormwood Dumplings", "Puffs", "Can be Crepe", "Peach Crisp", "Mochi", "Egg Waffle", "Macaron", "Snow Plum Niang", "Fried Yogurt", "Egg Tart", "Muffin", "Sago Dew", "panna cotta", "soufflé", "croissant", "toffee" };
    public static string Get_TName_Snacks => TranslationController.Instance.currentLanguage.languageID is SupportedLangs.SChinese or SupportedLangs.TChinese ?
        TName_Snacks_CN[IRandom.Instance.Next(0, TName_Snacks_CN.Count)] :
        TName_Snacks_EN[IRandom.Instance.Next(0, TName_Snacks_EN.Count)];

    public override void Load()
    {
        Instance = this;

        //Client Options
        HideName = Config.Bind("Client Options", "Hide Game Code Name", "TOHE-K");
        HideColor = Config.Bind("Client Options", "Hide Game Code Color", $"{ModColor}");
        DebugKeyInput = Config.Bind("Authentication", "Debug Key", "");
        AutoStart = Config.Bind("Client Options", "AutoStart", true);
        AutoPlayAgain = Config.Bind("Client Options", "AutoPlayAgain", true);
        UnlockFPS = Config.Bind("Client Options", "UnlockFPS", true);
        ForceOwnLanguage = Config.Bind("Client Options", "ForceOwnLanguage", true);
        ForceOwnLanguageRoleName = Config.Bind("Client Options", "ForceOwnLanguageRoleName", true);
        EnableCustomButton = Config.Bind("Client Options", "EnableCustomButton", true);
        EnableCustomSoundEffect = Config.Bind("Client Options", "EnableCustomSoundEffect", true);
        SwitchVanilla = Config.Bind("Client Options", "SwitchVanilla", false);
        DumpLog = Config.Bind("Client Options", "DumpLog", false);
        CanPublic = Config.Bind("Client Options", "CanPublic", false);
        ModeForSmallScreen = Config.Bind("Client Options", "ModeForSmallScreen", false);
        ShowLobbyCode = Config.Bind("Client Options", "ShowLobbyCode", false);
        VersionCheat = Config.Bind("Client Options", "VersionCheat", false);
        GodMode = Config.Bind("Client Options", "GodMode", false);

        Logger = BepInEx.Logging.Logger.CreateLogSource("TOHEK");
        TOHE.Logger.Enable();
        TOHE.Logger.Disable("NotifyRoles");
        TOHE.Logger.Disable("SwitchSystem");
        TOHE.Logger.Disable("ModNews");
        if (!DebugModeManager.AmDebugger)
        {
            TOHE.Logger.Disable("2018k");
            TOHE.Logger.Disable("Github");
            TOHE.Logger.Disable("CustomRpcSender");
            //TOHE.Logger.Disable("ReceiveRPC");
            TOHE.Logger.Disable("SendRPC");
            TOHE.Logger.Disable("SetRole");
            TOHE.Logger.Disable("Info.Role");
            TOHE.Logger.Disable("TaskState.Init");
            //TOHE.Logger.Disable("Vote");
            TOHE.Logger.Disable("RpcSetNamePrivate");
            //TOHE.Logger.Disable("SendChat");
            TOHE.Logger.Disable("SetName");
            //TOHE.Logger.Disable("AssignRoles");
            //TOHE.Logger.Disable("RepairSystem");
            //TOHE.Logger.Disable("MurderPlayer");
            //TOHE.Logger.Disable("CheckMurder");
            TOHE.Logger.Disable("PlayerControl.RpcSetRole");
            TOHE.Logger.Disable("SyncCustomSettings");
        }
        //TOHE.Logger.isDetail = true;

        // 認証関連-初期化
        DebugKeyAuth = new HashAuth(DebugKeyHash, DebugKeySalt);

        // 認証関連-認証
        DebugModeManager.Auth(DebugKeyAuth, DebugKeyInput.Value);

        Preset1 = Config.Bind("Preset Name Options", "Preset1", "Preset_1");
        Preset2 = Config.Bind("Preset Name Options", "Preset2", "Preset_2");
        Preset3 = Config.Bind("Preset Name Options", "Preset3", "Preset_3");
        Preset4 = Config.Bind("Preset Name Options", "Preset4", "Preset_4");
        Preset5 = Config.Bind("Preset Name Options", "Preset5", "Preset_5");
        WebhookURL = Config.Bind("Other", "WebhookURL", "none");
        BetaBuildURL = Config.Bind("Other", "BetaBuildURL", "");
        MessageWait = Config.Bind("Other", "MessageWait", 1);
        LastKillCooldown = Config.Bind("Other", "LastKillCooldown", (float)30);
        LastShapeshifterCooldown = Config.Bind("Other", "LastShapeshifterCooldown", (float)30);

        hasArgumentException = false;
        ExceptionMessage = "";
        try
        {
            roleColors = new Dictionary<CustomRoles, string>()
            {
                //バニラ役職
                {CustomRoles.Crewmate, "#8cffff"},
                {CustomRoles.Engineer, "#8cffff"},
                {CustomRoles.Scientist, "#8cffff"},
                {CustomRoles.GuardianAngel, "#ffffff"},
                // Vanilla Remakes
                {CustomRoles.CrewmateTOHE, "#8cffff"},
                {CustomRoles.EngineerTOHE, "#FF6A00"},
                {CustomRoles.ScientistTOHE, "#8ee98e"},
                {CustomRoles.GuardianAngelTOHE, "#77e6d1"},
                //特殊クルー役職
                {CustomRoles.Luckey, "#b8d7a3" },
                {CustomRoles.Needy, "#a4dffe"},
                {CustomRoles.SabotageMaster, "#3333ff"},
                {CustomRoles.Snitch, "#b8fb4f"},
                {CustomRoles.Marshall, "#5573aa"},
                {CustomRoles.Mayor, "#204d42"},
                {CustomRoles.Paranoia, "#c993f5"},
                {CustomRoles.Psychic, "#6F698C"},
                {CustomRoles.Sheriff, "#ffb347"},
                {CustomRoles.SillySheriff, "#f8cd46"},
                {CustomRoles.GrudgeSheriff, "#f8cd46"},
                {CustomRoles.Copycat, "#7FBFFF"},
                {CustomRoles.SuperStar, "#f6f657"},
                {CustomRoles.CyberStar, "#ee4a55" },
                {CustomRoles.SpeedBooster, "#00ffff"},
                {CustomRoles.Doctor, "#80ffdd"},
                {CustomRoles.Dictator, "#df9b00"},
                {CustomRoles.Detective, "#7160e8" },
                {CustomRoles.SwordsMan, "#f0e68c"},
                {CustomRoles.NiceGuesser, "#eede26"},
                {CustomRoles.Transporter, "#42D1FF"},
                {CustomRoles.TimeManager, "#6495ed"},
                {CustomRoles.Veteran, "#a77738"},
                {CustomRoles.Bodyguard, "#185abd"},
                {CustomRoles.Counterfeiter, "#BE29EC"},
                {CustomRoles.Grenadier, "#3c4a16"},
                {CustomRoles.Medic, "#00ff97"},
                {CustomRoles.Divinator, "#882c83"},
                {CustomRoles.Glitch, "#dcdcdc"},
                {CustomRoles.Judge, "#f8d85a"},
                {CustomRoles.Mortician, "#333c49"},
                {CustomRoles.Mediumshiper, "#a200ff"},
                {CustomRoles.Observer, "#a8e0fa"},
                {CustomRoles.DovesOfNeace, "#007FFF"},
                {CustomRoles.Monarch, "#FFA500"},
                {CustomRoles.Bloodhound, "#8B0000"},
                {CustomRoles.Tracker, "#3CB371"},
                {CustomRoles.Merchant, "#D27D2D"},
                {CustomRoles.Retributionist, "#228B22"},
                {CustomRoles.Deputy, "#df9026"},
                {CustomRoles.Guardian, "#2E8B57"},
                {CustomRoles.Addict, "#008000"},
                {CustomRoles.Tracefinder, "#0066CC"},
                {CustomRoles.Oracle, "#6666FF"},
                {CustomRoles.Spiritualist, "#669999"},
                {CustomRoles.TimeMaster,"#44baff" },
                {CustomRoles.TimeStops,"#0372FB" },
                {CustomRoles.SuperPowers,"#FF6600" },
                {CustomRoles.ET,"#cd44c8" },
                {CustomRoles.Chameleon, "#01C895"},
                {CustomRoles.ParityCop, "#0D57AF"},
                {CustomRoles.Captain,"#0089ff" },
                {CustomRoles.Solicited, "#0089ff" },
                {CustomRoles.HatarakiMan,"#6A5ACD" },
                {CustomRoles.XiaoMu,"#FFA500" },
                {CustomRoles.Wronged,"#999999" },
                {CustomRoles.Rudepeople, "#66CC00"},
                {CustomRoles.Nekomata, "#00ffff"},
                {CustomRoles.TaskManager, "#80ffdd"},
                {CustomRoles.Chairman, "#204d42"},
                {CustomRoles.Indomitable,"#808000" },
                {CustomRoles.GlennQuagmire,"#FF3333" },
                {CustomRoles.ChiefOfPolice,"#f8cd46" },
                {CustomRoles.CrewSchrodingerCat,"#ffffff" },
                {CustomRoles.Prophet,"#FFCC99" },
                {CustomRoles.Scout,"#6666CC" },
                {CustomRoles.ElectOfficials,"#99CCCC" },
                {CustomRoles.BSR,"#00ff55" },
                {CustomRoles.Cowboy,"#663333" },
                {CustomRoles.Undercover,"#ff1919" },
                {CustomRoles.Mascot,"#00ff66" },
                {CustomRoles.Manipulator,"#FF3300" },
                {CustomRoles.NBakery, "#b58428"},
                {CustomRoles.ONBakery, "#f19801"},
                //第三陣営役職
                {CustomRoles.Arsonist, "#ff6633"},
                {CustomRoles.Jester, "#ec62a5"},
                {CustomRoles.Terrorist, "#00e600"},
                {CustomRoles.Executioner, "#c0c0c0"},
                {CustomRoles.Lawyer, "#008080"},
                {CustomRoles.Swapper,"#565656" },
                {CustomRoles.Amnesiac,"#87CEFA" },
                {CustomRoles.PlagueBearer,"#e5f6b4"},
                {CustomRoles.Pestilence,"#343136"},
                {CustomRoles.Jealousy, "#996666"},
                {CustomRoles.YinLang,"#6A5ACD" },
                {CustomRoles.MengJiangGirl,"#778899" },
                {CustomRoles.Whoops,"#00b4eb" },
                {CustomRoles.Slaveowner,"#996600" },
                {CustomRoles.Exorcist,"#336666" },
                {CustomRoles.FreeMan, "#f6f657" },
                {CustomRoles.Shaman, "#50c878"},
                {CustomRoles.King,"#FFCC00" },
                {CustomRoles.Doomsayer, "#14f786"},
                {CustomRoles.Pirate,"#EDC240"},
                {CustomRoles.QSR, "#949e52"},
                {CustomRoles.Masochism ,"#663366" },
                {CustomRoles.CovenLeader, "#663399"},
                {CustomRoles.SourcePlague, "#CCCC33"},
                {CustomRoles.PlaguesGod, "#101010"},
                {CustomRoles.SchrodingerCat,"#666666" },
                {CustomRoles.GamerSchrodingerCat,"#68bc71" },
                {CustomRoles.BloodSchrodingerCat,"#630000" },
                {CustomRoles.JSchrodingerCat,"#00b4eb" },
                {CustomRoles.YLSchrodingerCat,"#6A5ACD" },
                {CustomRoles.PGSchrodingerCat, "#101010"},
                {CustomRoles.DHSchrodingerCat, "#483d8b"},
                {CustomRoles.OKSchrodingerCat, "#CC6600"},
                {CustomRoles.God, "#f96464"},
                {CustomRoles.Opportunist, "#4dff4d"},
                {CustomRoles.OpportunistKiller, "#CC6600"},
                {CustomRoles.Mario, "#ff6201"},
                {CustomRoles.Jackal, "#00b4eb"},
                {CustomRoles.Sidekick, "#00b4eb"},
                {CustomRoles.Innocent, "#8f815e"},
                {CustomRoles.Pelican, "#34c84b"},
                {CustomRoles.Revolutionist, "#ba4d06"},
                {CustomRoles.FFF, "#414b66"},
                {CustomRoles.Konan, "#4d4dff"},
                {CustomRoles.Gamer, "#68bc71"},
                {CustomRoles.DarkHide, "#483d8b"},
                {CustomRoles.LoveCutter, "#c71585"},
                {CustomRoles.PlatonicLover, "#ff6be4"},
                {CustomRoles.Crush,"#ff9ace" },
                {CustomRoles.Workaholic, "#008b8b"},
                {CustomRoles.Collector, "#9d8892"},
                {CustomRoles.Provocateur, "#74ba43"},
                {CustomRoles.Sunnyboy, "#ff9902"},
                {CustomRoles.Poisoner, "#478800"},
                {CustomRoles.NWitch, "#BF5FFF"},
                {CustomRoles.Totocalcio, "#ff9409"},
                {CustomRoles.Succubus, "#cf6acd"},
                {CustomRoles.HexMaster, "#ff00ff"},
                {CustomRoles.Wraith, "#4B0082"},
                {CustomRoles.NSerialKiller, "#233fcc"},
                {CustomRoles.BloodKnight, "#630000"},
                {CustomRoles.Juggernaut, "#A41342"},
                {CustomRoles.Parasite, "#ff1919"},
                {CustomRoles.Crewpostor, "#ff1919"},
                {CustomRoles.Infectious, "#7B8968"},
                {CustomRoles.Virus, "#2E8B57"},
                {CustomRoles.Farseer, "#BA55D3"},
                {CustomRoles.Pursuer, "#617218"},
                {CustomRoles.Phantom, "#662962"},
                {CustomRoles.Jinx, "#ed2f91"},
                {CustomRoles.Maverick, "#781717"},
                {CustomRoles.CursedSoul, "#531269"},
                {CustomRoles.Ritualist, "#663399"},
                {CustomRoles.Pickpocket, "#47008B"},
                {CustomRoles.Traitor, "#BA2E05"},
                {CustomRoles.Vulture, "#556B2F"},
                {CustomRoles.Medusa, "#9900CC"},
                {CustomRoles.Convict, "#ff1919"},
                {CustomRoles.Spiritcaller, "#003366"},
                {CustomRoles.EvilSpirit, "#003366"},
                // GM
                {CustomRoles.GM, "#ff5b70"},
                //サブ役職
                {CustomRoles.NotAssigned, "#ffffff"},
                {CustomRoles.LastImpostor, "#ff1919"},
                {CustomRoles.Lovers, "#ff9ace"},
                {CustomRoles.Ntr, "#00a4ff"},
                {CustomRoles.Madmate, "#ff1919"},
                {CustomRoles.Watcher, "#800080"},
                {CustomRoles.Flashman, "#ff8400"},
                {CustomRoles.Lighter, "#eee5be"},
                {CustomRoles.Sunglasses, "#883fd1"},
                {CustomRoles.Seer, "#61b26c"},
                {CustomRoles.Gravestone, "#2EA8E7"},
                {CustomRoles.Lazy, "#a4dffe"},
                {CustomRoles.Autopsy, "#80ffdd"},
                {CustomRoles.InfoPoor, "#556b2f"},
                {CustomRoles.Clumsy, "#696969"},
                {CustomRoles.VIP, "#ffff00"},
                {CustomRoles.Loyal, "#B71556"},
                {CustomRoles.Visionary, "#ff1919"},
                {CustomRoles.Bitch, "#333333"},
                {CustomRoles.OldThousand,"#f6f657" },
                {CustomRoles.Antidote,"#FF9876"},
                {CustomRoles.Diseased, "#AAAAAA"},
                {CustomRoles.Brakar, "#1447af"},
                {CustomRoles.Oblivious, "#424242"},
                {CustomRoles.Bewilder, "#c894f5"},
                {CustomRoles.Workhorse, "#00ffff"},
                {CustomRoles.Fool, "#e6e7ff"},
                {CustomRoles.Avanger, "#ffab1c"},
                {CustomRoles.Youtuber, "#fb749b"},
                {CustomRoles.Egoist, "#5600ff"},
                {CustomRoles.seniormanagement, "#0089ff"},
                {CustomRoles.Believer, "#000000"},
                {CustomRoles.TicketsStealer, "#ff1919"},
                {CustomRoles.DualPersonality, "#3a648f"},
                {CustomRoles.Mimic, "#ff1919"},
                {CustomRoles.Guesser, "#f8cd46"},
                {CustomRoles.Necroview, "#663399"},
                {CustomRoles.Reach, "#74ba43"},
                {CustomRoles.Charmed, "#cf6acd"},
                {CustomRoles.Bait, "#00f7ff"},
                {CustomRoles.Trapper, "#5a8fd0"},
                {CustomRoles.Infected, "#7B8968"},
                {CustomRoles.Onbound, "#BAAAE9"},
                {CustomRoles.Knighted, "#FFA500"},
                {CustomRoles.Contagious, "#2E8B57"},
                {CustomRoles.Unreportable, "#FF6347"},
                {CustomRoles.Rogue, "#696969"},
                {CustomRoles.Lucky, "#b8d7a3"},
                {CustomRoles.DoubleShot, "#19fa8d"},
                //{CustomRoles.Reflective, "#FFD700"},
                {CustomRoles.Rascal, "#990000"},
                {CustomRoles.Soulless, "#531269"},
                {CustomRoles.Swift, "#ff1919"},
                {CustomRoles.QL,"#66ff5c" },
                //SoloKombat
                {CustomRoles.KB_Normal, "#f55252"},
                //HotPotatto
                {CustomRoles.Hotpotato, "#ffa300" },
                {CustomRoles.Coldpotato, "#00efff" },
            };
            foreach (var role in Enum.GetValues(typeof(CustomRoles)).Cast<CustomRoles>())
            {
                switch (role.GetCustomRoleTypes())
                {
                    case CustomRoleTypes.Impostor:
                    case CustomRoleTypes.Madmate:
                        roleColors.TryAdd(role, "#ff1919");
                        break;
                    default:
                        break;
                }
            }
        }
        catch (ArgumentException ex)
        {
            TOHE.Logger.Error("错误：字典出现重复项", "LoadDictionary");
            TOHE.Logger.Exception(ex, "LoadDictionary");
            hasArgumentException = true;
            ExceptionMessage = ex.Message;
            ExceptionMessageIsShown = false;
        }

        CustomWinnerHolder.Reset();
        ServerAddManager.Init();
        Translator.Init();
        BanManager.Init();
        TemplateManager.Init();
        SpamManager.Init();
        DevManager.Init();
        Cloud.Init();

        IRandom.SetInstance(new NetRandomWrapper());

        TOHE.Logger.Info($"{Application.version}", "AmongUs Version");

        var handler = TOHE.Logger.Handler("GitVersion");
        handler.Info($"{nameof(ThisAssembly.Git.BaseTag)}: {ThisAssembly.Git.BaseTag}");
        handler.Info($"{nameof(ThisAssembly.Git.Commit)}: {ThisAssembly.Git.Commit}");
        handler.Info($"{nameof(ThisAssembly.Git.Commits)}: {ThisAssembly.Git.Commits}");
        handler.Info($"{nameof(ThisAssembly.Git.IsDirty)}: {ThisAssembly.Git.IsDirty}");
        handler.Info($"{nameof(ThisAssembly.Git.Sha)}: {ThisAssembly.Git.Sha}");
        handler.Info($"{nameof(ThisAssembly.Git.Tag)}: {ThisAssembly.Git.Tag}");

        ClassInjector.RegisterTypeInIl2Cpp<ErrorText>();

        Harmony.PatchAll();

        if (!DebugModeManager.AmDebugger) ConsoleManager.DetachConsole();
        else ConsoleManager.CreateConsole();

        TOHE.Logger.Msg("========= TOHEK loaded! =========", "Plugin Load");
    }
}
public enum CustomRoles
{
    //Default
    Crewmate = 0,
    //Impostor(Vanilla)
    Impostor,
    Shapeshifter,
    // Vanilla Remakes
    ImpostorTOHE,
    ShapeshifterTOHE,
    //Impostor
    BountyHunter,
    FireWorks,
    Mafia,
    SerialKiller,
    ShapeMaster,
    Wildling,
    EvilGuesser,
    Minimalism,
    Zombie,
    Sniper,
    Vampire,
    Witch,
    Warlock,
    Assassin,
    Vindicator,
    Hacker,
    Miner,
    Escapee,
    Mare,
    Inhibitor,
    Puppeteer,
    TimeThief,
    EvilTracker,
    AntiAdminer,
    Sans,
    Bomber,
    BoobyTrap,
    Scavenger,
    Capitalism,
    Gangster,
    Cleaner,
    Janitor,
    BallLightning,
    Greedier,
    CursedWolf,
    ImperiusCurse,
    QuickShooter,
    Eraser,
    OverKiller,
    Hangman,
    Bard,
    Trickster,
    Swooper,
    Crewpostor,
    Parasite,
    Disperser,
    Camouflager,
    Saboteur,
    Councillor,
    Dazzler,
    Deathpact,
    Devourer,
    EvilDiviner,
    Morphling,
    Twister,
    ImpostorSchrodingerCat,
    Vandalism,
    Disorder,
    DemolitionManiac,
    QX,
    Guide,
    Depressed,
    //Crewmate(Vanilla)
    Engineer,
    GuardianAngel,
    Scientist,
    // Vanilla Remakes
    CrewmateTOHE,
    EngineerTOHE,
    GuardianAngelTOHE,
    ScientistTOHE,
    //Crewmate
    Luckey,
    Needy,
    SuperStar,
    CyberStar,
    Mayor,
    Paranoia,
    Psychic,
    SabotageMaster,
    Sheriff,
    SillySheriff,
    GrudgeSheriff,
    Snitch,
    Marshall,
    SpeedBooster,
    Dictator,
    Doctor,
    Detective,
    SwordsMan,
    NiceGuesser,
    Transporter,
    TimeManager,
    Veteran,
    Bodyguard,
    Counterfeiter,
    Grenadier,
    Medic,
    Divinator,
    Glitch,
    Judge,
    Mortician,
    Mediumshiper,
    Observer,
    DovesOfNeace,
    Monarch,
    Copycat,
    Farseer,
    Bloodhound,
    Tracker,
    Merchant,
    Retributionist,
    Deputy,
    Guardian,
    Addict,
    Tracefinder,
    Oracle,
    Spiritualist,
    TimeMaster,
    TimeStops,
    SuperPowers,
    ET,
    Chameleon,
    ParityCop,
    Captain,
    Solicited,
    HatarakiMan,
    XiaoMu,
    Wronged,
    Rudepeople,
    Nekomata,
    TaskManager,
    Chairman,
    Indomitable,
    GlennQuagmire,
    ChiefOfPolice,
    CrewSchrodingerCat,
    Prophet,
    Scout,
    ElectOfficials,
    BSR,
    Cowboy,
    Undercover,
    Mascot,
    Manipulator,
    Bakery,
    NBakery,
    ONBakery,
    //Neutral
    Arsonist,
    HexMaster,
    Jester,
    God,
    Opportunist,
    OpportunistKiller,
    Mario,
    Terrorist,
    Executioner,
    Lawyer,
    Swapper,
    Amnesiac,
    YinLang,
    MengJiangGirl,
    Whoops,
    FreeMan,
    Shaman,
    King,
    Doomsayer,
    Pirate,
    QSR,
    Masochism,
    CovenLeader,
    SourcePlague,
    PlaguesGod,
    SchrodingerCat,
    GamerSchrodingerCat,
    BloodSchrodingerCat,
    JSchrodingerCat,
    YLSchrodingerCat,
    PGSchrodingerCat,
    DHSchrodingerCat,
    OKSchrodingerCat,
    Jackal,
    Poisoner,
    NWitch,
    Innocent,
    Pelican,
    Revolutionist,
    NSerialKiller,
    Juggernaut,
    Infectious,
    FFF,
    Konan,
    Gamer,
    DarkHide,
    LoveCutter,
    PlatonicLover,
    Crush,
    Workaholic,
    Collector,
    Provocateur,
    Sunnyboy,
    BloodKnight,
    Wraith,
    Totocalcio,
    Succubus,
    Virus,
    Pursuer,
    Phantom,
    Jinx,
    Maverick,
    CursedSoul,
    Ritualist,
    Pickpocket,
    Traitor,
    Vulture,
    PlagueBearer,
    Pestilence,
    Jealousy,
    Slaveowner,
    Exorcist,
    Medusa,
    Convict,
    Spiritcaller,

    //SoloKombat
    KB_Normal,
    //烫手的山芋
    Hotpotato,
    Coldpotato,
    //GM
    GM,

    // Sub-role after 500
    NotAssigned = 500,
    LastImpostor,
    Lovers,
    Ntr,
    Madmate,
    Watcher,
    Flashman,
    Lighter,
    Sunglasses,
    Seer,
    Gravestone,
    Lazy,
    Autopsy,
    InfoPoor,
    Clumsy,
    VIP,
    Loyal,
    EvilSpirit,
    Visionary,
    Bitch,
    OldThousand,
    Antidote,
    Diseased,
    Brakar,
    Oblivious,
    Bewilder,
    Workhorse,
    Fool,
    Avanger,
    Sidekick,
    Youtuber,
    Egoist,
    seniormanagement,
    Believer,
    TicketsStealer,
    DualPersonality,
    Mimic,
    Guesser,
    Necroview,
    Reach,
    Charmed,
    Bait,
    Trapper,
    Infected,
    Onbound,
    Knighted,
    Contagious,
    Unreportable,
    Rogue,
    Lucky,
    DoubleShot,
   // Reflective,
    Rascal,
    Soulless,
    Swift,
    QL,
}
//WinData
public enum CustomWinner
{
    Draw = -1,
    Default = -2,
    None = -3,
    Error = -4,
    Neutrals = -5,
    Impostor = CustomRoles.Impostor,
    Crewmate = CustomRoles.Crewmate,
    Jester = CustomRoles.Jester,
    Terrorist = CustomRoles.Terrorist,
    Lovers = CustomRoles.Lovers,
    Executioner = CustomRoles.Executioner,
    Arsonist = CustomRoles.Arsonist,
    Glitch = CustomRoles.Glitch,
    Revolutionist = CustomRoles.Revolutionist,
    Jackal = CustomRoles.Jackal,
    Sidekick = CustomRoles.Sidekick,
    God = CustomRoles.God,
    Mario = CustomRoles.Mario,
    Innocent = CustomRoles.Innocent,
    Pelican = CustomRoles.Pelican,
    Youtuber = CustomRoles.Youtuber,
    Egoist = CustomRoles.Egoist,
    Gamer = CustomRoles.Gamer,
    DarkHide = CustomRoles.DarkHide,
    LoveCutter = CustomRoles.LoveCutter,
    NBakery = CustomRoles.NBakery,
    CP = CustomRoles.Coldpotato,
    Workaholic = CustomRoles.Workaholic,
    Collector = CustomRoles.Collector,
    BloodKnight = CustomRoles.BloodKnight,
    Poisoner = CustomRoles.Poisoner,
    HexMaster = CustomRoles.HexMaster,
    Succubus = CustomRoles.Succubus,
    Wraith = CustomRoles.Wraith,
    SerialKiller = CustomRoles.NSerialKiller,
    Witch = CustomRoles.NWitch,
    Juggernaut = CustomRoles.Juggernaut,
    Infectious = CustomRoles.Infectious,
    Virus = CustomRoles.Virus,
    Rogue = CustomRoles.Rogue,
    Phantom = CustomRoles.Phantom,
    Jinx = CustomRoles.Jinx,
    CursedSoul = CustomRoles.CursedSoul,
    Ritualist = CustomRoles.Ritualist,
    Pickpocket = CustomRoles.Pickpocket,
    Traitor = CustomRoles.Traitor,
    Vulture = CustomRoles.Vulture,
    Pestilence = CustomRoles.Pestilence,
    Medusa = CustomRoles.Medusa,
    Spiritcaller = CustomRoles.Spiritcaller,
    Jealousy = CustomRoles.Jealousy,
    YinLang = CustomRoles.YinLang,
    MengJiangGirl = CustomRoles.MengJiangGirl,
    Exorcist = CustomRoles.Exorcist,
    King = CustomRoles.King,
    Doomsayer = CustomRoles.Doomsayer,
    Pirate = CustomRoles.Pirate,
    Masochism = CustomRoles.Masochism,
    Coven = CustomRoles.CovenLeader,
    PlagueBearer = CustomRoles.PlagueBearer,
    PlaguesGod = CustomRoles.PlaguesGod
}
public enum AdditionalWinners
{
    None = -1,
    Lovers = CustomRoles.Lovers,
    Opportunist = CustomRoles.Opportunist,
    OpportunistKiller = CustomRoles.OpportunistKiller,
    FreeMan = CustomRoles.FreeMan,
    Shaman = CustomRoles.Shaman,
    QSR = CustomRoles.QSR,
    Executioner = CustomRoles.Executioner,
    Slaveowner = CustomRoles.Slaveowner,
    Lawyer = CustomRoles.Lawyer,
    FFF = CustomRoles.FFF,
    Provocateur = CustomRoles.Provocateur,
    Sunnyboy = CustomRoles.Sunnyboy,
    Witch = CustomRoles.NWitch,
    Totocalcio = CustomRoles.Totocalcio,
    Jackal = CustomRoles.Jackal,
    Sidekick = CustomRoles.Sidekick,
    Pursuer = CustomRoles.Pursuer,
    Phantom = CustomRoles.Phantom,
    Maverick = CustomRoles.Maverick,
    DHCat = CustomRoles.DHSchrodingerCat,
    OKC = CustomRoles.OKSchrodingerCat,
}
public enum SuffixModes
{
    None,
    TOHE,
    Streaming,
    Recording,
    RoomHost,
    OriginalName,
    Myseft,
    DoNotKillMePlz,
    NoAndroidorIOSPlz,
    AutoHost
}
public enum VoteMode
{
    Default,
    Suicide,
    SelfVote,
    Skip
}
public enum TieMode
{
    Default,
    All,
    Random
}
public enum SyncColorMode
{
    None,
    Clone,
    fif_fif,
    ThreeCornered,
    Twin,
}