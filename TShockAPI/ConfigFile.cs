/*
TShock, a server mod for Terraria
Copyright (C) 2011-2018 Pryaxis & TShock Contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Rests;

namespace TShockAPI
{
	/// <summary>ConfigFile - The config file class, which contains the configuration for a server that is serialized into JSON and deserialized on load.</summary>
	public class ConfigFile
	{
		/// <summary>InvasionMultiplier - The equation for calculating invasion size = 100 + (multiplier * (number of active players > 200 hp)).</summary>
		[Description(
			"计算入侵规模: 100 + (乘值 * (血量200以上玩家数量))."
			)]
		[JsonProperty("入侵规模")]
		public int InvasionMultiplier = 1;

		/// <summary>DefaultMaximumSpawns - The default max spawns per wave.</summary>
		[Description("默认每波入侵生成怪物量. 值/怪物数量正相关.")]
		[JsonProperty("默认入侵刷怪量")]
		public int DefaultMaximumSpawns = 5;

		/// <summary>DefaultSpawnRate - The default spawn rate.</summary>
		[Description("默认每波刷怪间空隙时间. 值/怪物数量负相关.")]
		[JsonProperty("默认刷怪率")]
		public int DefaultSpawnRate = 600;

		/// <summary>ServerPort - The configured server port.</summary>
		[Description("服务器默认监听运行的端口.")]
		[JsonProperty("服务器端口")]
		public int ServerPort = 7777;

		/// <summary>EnableWhitelist - boolean if the whitelist functionality should be turned on.</summary>
		[Description("开启whitelist.txt中的白名单模式. (使用IP)")]
		[JsonProperty("启用白名单")]
		public bool EnableWhitelist;

		/// <summary>InfiniteInvasion - Whether or not infinite invasion mode should be on.</summary>
		[Description(
			"开启永不停歇的刷怪. 不要忘记使用 /invade, 而且该选项会加入2亿+的哥布林到地图的生成队列."
			)]
		[JsonProperty("无限入侵")]
		public bool InfiniteInvasion;

		/// <summary>PvPMode - The server PvP mode (normal, always, or disabled).</summary>
		[Description("默认PvP模式. 可用模式: \"normal\", \"always\", \"disabled\".")]
		[JsonProperty("PvP模式")]
		public string PvPMode = "normal";

		/// <summary>SpawnProtection - Enables the spawn protection system.</summary>
		[Description("设置是否启用按照\"SpawnProtectionRadius(出生点保护半径)\"设定的出生点半径保护.")]
		[JsonProperty("出生点保护")]
		public bool SpawnProtection = true;

		/// <summary>SpawnProtectionRadius - The spawn protection tile radius.</summary>
		[Description("出生点保护的半径.")]
		[JsonProperty("出生点保护半径")]
		public int SpawnProtectionRadius = 10;

		/// <summary>MaxSlots - The server's max slots.</summary>
		[Description(
			"服务器最大容载玩家数. 若满员, 新来的玩家会收到\"服务器已满.\"的通知."
			)]
		[JsonProperty("最大玩家数")]
		public int MaxSlots = 8;

		/// <summary>RangeChecks - Whether or not the anti-grief system based on range should be enabled.</summary>
		[Description("全局保护模块, 监测物块操作距离.")]
		[JsonProperty("范围监测")]
		public bool RangeChecks = true;

		/// <summary>DisableBuild - Whether or not building should be enabled.</summary>
		[Description("禁止玩家建筑.")]
		[JsonProperty("禁止建筑")]
		public bool DisableBuild;

		/// <summary>SuperAdminChatRGB - The chat color for the superadmin group.</summary>
		[Description("#.#.# => R/G/B - superadmin组聊天颜色。单项最大值：255。")]
		[JsonProperty("超级管理聊天颜色")]
		public int[] SuperAdminChatRGB = { 255, 255, 255 };

		/// <summary>SuperAdminChatPrefix - The superadmin chat prefix.</summary>
		[Description("superadmin组前缀.")]
		[JsonProperty("超级管理聊天前缀")]
		public string SuperAdminChatPrefix = "[管理] ";

		/// <summary>SuperAdminChatSuffix - The superadmin chat suffix.</summary>
		[Description("superadmin组后缀.")]
		[JsonProperty("超级管理聊天后缀")]
		public string SuperAdminChatSuffix = "";

		/// <summary>BackupInterval - The backup frequency in minutes.</summary>
		[Description(
			"Backup frequency in minutes. So, a value of 60 = 60 minutes. Backups are stored in the \\tshock\\backups folder.")]
		[JsonProperty("备份间隔")]
		public int BackupInterval;

		/// <summary>BackupKeepFor - Backup max age in minutes.</summary>
		[Description("How long backups are kept in minutes. 2880 = 2 days.")]
		[JsonProperty("备份保留时间")]
		public int BackupKeepFor = 60;

		/// <summary>RememberLeavePos - Whether or not to remember where an IP player was when they left.</summary>
		[Description(
			"Remembers where a player left off. It works by remembering the IP, NOT the character.  \neg. When you try to disconnect, and reconnect to be automatically placed at spawn, you'll be at your last location. Note: Won't save after server restarts."
			)]
		[JsonProperty("保存玩家上次坐标")]
		public bool RememberLeavePos;

		/// <summary>HardcoreOnly - Whether or not HardcoreOnly should be enabled.</summary>
		[Description("Hardcore players ONLY. This means softcore players cannot join.")]
		[JsonProperty("仅限困难模式玩家")]
		public bool HardcoreOnly;

		/// <summary>MediumcoreOnly - Whether or not MediumCore only players should be enabled.</summary>
		[Description("Mediumcore players ONLY. This means softcore players cannot join.")]
		[JsonProperty("仅限中等难度玩家")]
		public bool MediumcoreOnly;

		/// <summary>KickOnMediumcoreDeath - Whether or not to kick mediumcore players on death.</summary>
		[Description("Kicks a mediumcore player on death.")]
		[JsonProperty("中等难度死亡时驱逐")]
		public bool KickOnMediumcoreDeath;

		/// <summary>BanOnMediumcoreDeath - Whether or not to ban mediumcore players on death.</summary>
		[Description("Bans a mediumcore player on death.")]
		[JsonProperty("中等难度死亡时封禁")]
		public bool BanOnMediumcoreDeath;

		[Description("Enable/disable Terraria's built in auto save.")]
		[JsonProperty("自动保存")]
		public bool AutoSave = true;
		[Description("Enable/disable save announcements.")]
		[JsonProperty("保存时通知")]
		public bool AnnounceSave = true;

		[Description("Number of failed login attempts before kicking the player.")]
		[JsonProperty("最多次失败登录尝试")]
		public int MaximumLoginAttempts = 3;

		[Description("Used when replying to a rest /status request or sent to the client when UseServerName is true.")]
		[JsonProperty("服务器名")]
		public string ServerName = "";
		[Description("Sends ServerName in place of the world name to clients.")]
		[JsonProperty("使用服务器名")]
		public bool UseServerName = false;

		[Description("Valid types are \"sqlite\" and \"mysql\"")]
		[JsonProperty("数据库类型")]
		public string StorageType = "sqlite";

		[Description("The MySQL hostname and port to direct connections to")]
		[JsonProperty("MySql服务器地址")]
		public string MySqlHost = "localhost:3306";
		[Description("Database name to connect to")]
		[JsonProperty("MySql数据库名")]
		public string MySqlDbName = "";
		[Description("Database username to connect with")]
		[JsonProperty("MySql连接账户名")]
		public string MySqlUsername = "";
		[Description("Database password to connect with")]
		[JsonProperty("MySql连接账户密码")]
		public string MySqlPassword = "";

		[Description("Bans a mediumcore player on death.")]
		[JsonProperty("中等难度玩家封禁原因")]
		public string MediumcoreBanReason = "玩家死亡后封禁";
		[Description("Kicks a mediumcore player on death.")]
		[JsonProperty("中等难度玩家驱逐原因")]
		public string MediumcoreKickReason = "玩家死亡后驱逐";

		/// <summary>EnableIPBans - Whether or not to kick players on join that match a banned IP address.</summary>
		[Description("Enables kicking of banned users by matching their IP Address.")]
		[JsonProperty("启用IP封禁")]
		public bool EnableIPBans = true;

		[Description("Enables kicking of banned users by matching their client UUID.")]
		[JsonProperty("启用UUID码封禁")]
		public bool EnableUUIDBans = true;

		[Description("Enables kicking of banned users by matching their Character Name.")]
		[JsonProperty("启用玩家名封禁")]
		public bool EnableBanOnUsernames;

		[Description("Selects the default group name to place new registrants under.")]
		[JsonProperty("注册后分组")]
		public string DefaultRegistrationGroupName = "default";

		[Description("Selects the default group name to place non registered users under")]
		[JsonProperty("未登录玩家分组")]
		public string DefaultGuestGroupName = "guest";

		[Description("Force-disable printing logs to players with the log permission.")]
		[JsonProperty("关闭日志显示")]
		public bool DisableSpewLogs = true;

		[Description("Prevents OnSecondUpdate checks from writing to the log file")]
		[JsonProperty("禁止以秒刷新检查写入日志")]
		public bool DisableSecondUpdateLogs = false;

		[Description("Valid types are \"sha512\", \"sha256\", \"md5\", append with \"-xp\" for the xp supported algorithms.")]
		[JsonProperty("哈希算法")]
		public string HashAlgorithm = "sha512";

		/// <summary>ServerFullReason - The reason given when kicking players when the server is full.</summary>
		[Description("String that is used when kicking people when the server is full.")]
		[JsonProperty("服务器人满驱逐原因")]
		public string ServerFullReason = "服务器已满! 请稍后再进入.";

		[Description("String that is used when a user is kicked due to not being on the whitelist.")]
		[JsonProperty("非白名单玩家驱逐原因")]
		public string WhitelistKickReason = "需要加入白名单以进入游戏.";

		[Description("String that is used when kicking people when the server is full with no reserved slots.")]
		[JsonProperty("服无预留位驱逐原因")]
		public string ServerFullNoReservedReason = "服务器已满, 且没有预留位.";

		[Description("This will save the world if Terraria crashes from an unhandled exception.")]
		[JsonProperty("崩溃时保存地图")]
		public bool SaveWorldOnCrash = true;

		[Description("This will announce a player's location on join")]
		[JsonProperty("宣布IP区域")]
		public bool EnableGeoIP;

		[Description("This will turn on token requirement for the public REST API endpoints.")]
		[JsonProperty("启用REST密钥验证")]
		public bool EnableTokenEndpointAuthentication;

		[Description("Enable/disable the rest api.")]
		[JsonProperty("启用REST服务")]
		public bool RestApiEnabled;

		[Description("This is the port which the rest api will listen on.")]
		[JsonProperty("REST服务端口")]
		public int RestApiPort = 7878;

		[Description("Disable tombstones for all players.")]
		[JsonProperty("禁止墓碑")]
		public bool DisableTombstones = true;

		[Description("Displays a player's IP on join to everyone who has the log permission.")]
		[JsonProperty("显示玩家IP至管理")]
		public bool DisplayIPToAdmins;

		[Description("Kicks users using a proxy as identified with the GeoIP database.")]
		[JsonProperty("驱逐代理玩家")]
		public bool KickProxyUsers = true;

		[Description("Disables hardmode, can't never be activated. Overrides /starthardmode.")]
		[JsonProperty("禁用肉山后")]
		public bool DisableHardmode;

		[Description("Disables the dungeon guardian from being spawned by player packets, this will instead force a respawn.")]
		[JsonProperty("禁用地牢守卫者")]
		public bool DisableDungeonGuardian;

		[Description("Disables clown bomb projectiles from spawning.")]
		[JsonProperty("禁用小丑炸弹")]
		public bool DisableClownBombs;

		[Description("Disables snow ball projectiles from spawning.")]
		[JsonProperty("禁用雪球")]
		public bool DisableSnowBalls;

		[Description(
			"Changes ingame chat format: {0} = Group Name, {1} = Group Prefix, {2} = Player Name, {3} = Group Suffix, {4} = Chat Message"
			)]
		[JsonProperty("聊天文本格式")]
		public string ChatFormat = "{1}{2}{3}: {4}";

		[Description("Change the player name when using chat above heads. This begins with a player name wrapped in brackets, as per Terraria's formatting. Same formatting as ChatFormat(minus the text aka {4}).")]
		[JsonProperty("头顶聊天文本格式")]
		public string ChatAboveHeadsFormat = "{2}";

		[Description("Force the world time to be normal, day, or night.")]
		[JsonProperty("锁定时间")]
		public string ForceTime = "normal";

		[Description("Disables/reverts a player if this number of tile kills is exceeded within 1 second.")]
		[JsonProperty("破坏物块速率上限")]
		public int TileKillThreshold = 60;

		[Description("Disables/reverts a player if this number of tile places is exceeded within 1 second.")]
		[JsonProperty("放置物块速率上限")]
		public int TilePlaceThreshold = 20;

		[Description("Disables a player if this number of liquid sets is exceeded within 1 second.")]
		[JsonProperty("倾倒液体速率上限")]
		public int TileLiquidThreshold = 15;

		[Description("Disable a player if this number of projectiles is created within 1 second.")]
		[JsonProperty("发射抛射体速率上限")]
		public int ProjectileThreshold = 50;

		/// <summary>HealOtherThreshold - Disables a player if this number of HealOtherPlayer packets is sent within 1 second.</summary>
		[Description("Disables a player if this number of HealOtherPlayer packets is sent within 1 second.")]
		[JsonProperty("玩家治疗速率上限")]
		public int HealOtherThreshold = 50;

		/// <summary>ProjIgnoreShrapnel - Whether or not to ignore shrapnel from crystal bullets for the projectile threshold count.</summary>
		[Description("Ignore shrapnel from crystal bullets for projectile threshold.")]
		[JsonProperty("忽略抛射体碎片计算")]
		public bool ProjIgnoreShrapnel = true;

		[Description("Requires all players to register or login before being allowed to play.")]
		[JsonProperty("强制玩家登录")]
		public bool RequireLogin;

		[Description(
			"Disables invisibility potions from being used in PvP (Note, can be used in the client, but the effect isn't sent to the rest of the server)."
			)]
		[JsonProperty("PvP禁用隐身药剂")]
		public bool DisableInvisPvP;

		[Description("The maximum distance players disabled for various reasons can move from.")]
		[JsonProperty("最大单次移动范围")]
		public int MaxRangeForDisabled = 10;

		[Description("Server password required to join the server.")]
		[JsonProperty("服务器密码")]
		public string ServerPassword = "";

		[Description("Protect chests with region and build permissions.")]
		[JsonProperty("保护区域内箱子")]
		public bool RegionProtectChests;

		/// <summary>RegionProtectGemLocks - Whether or not region protection should apply to gem locks.</summary>
		[Description("Protect gem locks with region and build permissions.")]
		[JsonProperty("保护区域内宝石锁")]
		public bool RegionProtectGemLocks = true;
		[Description("Disable users from being able to login with account password when joining.")]
		[JsonProperty("禁止进入游戏前登录")]
		public bool DisableLoginBeforeJoin;

		[Description("Disable users from being able to login with their client UUID.")]
		[JsonProperty("禁止UUID登录")]
		public bool DisableUUIDLogin;

		[Description("Kick clients that don't send a UUID to the server.")]
		[JsonProperty("驱逐空UUID玩家")]
		public bool KickEmptyUUID;

		[Description("Allows users to register any username with /register.")]
		[JsonProperty("允许注册任何用户名")]
		public bool AllowRegisterAnyUsername;

		[Description("Allows users to login with any username with /login.")]
		[JsonProperty("允许以任何用户名登录")]
		public bool AllowLoginAnyUsername = true;

		[Description("The maximum damage a player/npc can inflict.")]
		[JsonProperty("最大攻击数值")]
		public int MaxDamage = 1175;

		[Description("The maximum damage a projectile can inflict.")]
		[JsonProperty("最大抛射体攻击数值")]
		public int MaxProjDamage = 1175;

		[Description("Kicks a user if set to true, if they inflict more damage then the max damage.")]
		[JsonProperty("驱逐超过攻击上限的玩家")]
		public bool KickOnDamageThresholdBroken = false;

		[Description("Ignores checking to see if player 'can' update a projectile.")]
		[JsonProperty("忽略玩家更新抛射体")]
		public bool IgnoreProjUpdate = false;

		[Description("Ignores checking to see if player 'can' kill a projectile.")]
		[JsonProperty("忽略玩家破坏抛射体")]
		public bool IgnoreProjKill = false;

		/// <summary>AlllowIce - Allows ice placement even where a user cannot usually build.</summary>
		[Description("Allow ice placement even when user does not have canbuild.")]
		[JsonProperty("允许放置冰")]
		public bool AllowIce = false;

		[Description("Allows crimson to spread when a world is hardmode.")]
		[JsonProperty("允许血腥扩散")]
		public bool AllowCrimsonCreep = true;

		[Description("Allows corruption to spread when a world is hardmode.")]
		[JsonProperty("允许腐化扩散")]
		public bool AllowCorruptionCreep = true;

		[Description("Allows hallow to spread when a world is hardmode.")]
		[JsonProperty("允许神圣扩散")]
		public bool AllowHallowCreep = true;

		[Description("How many things a statue can spawn within 200 pixels(?) before it stops spawning. Default = 3")]
		[JsonProperty("200像素内雕像刷怪数量")]
		public int StatueSpawn200 = 3;

		[Description("How many things a statue can spawn within 600 pixels(?) before it stops spawning. Default = 6")]
		[JsonProperty("600像素内雕像刷怪数量")]
		public int StatueSpawn600 = 6;

		[Description("How many things a statue spawns can exist in the world before it stops spawning. Default = 10")]
		[JsonProperty("最多雕像刷怪数量")]
		public int StatueSpawnWorld = 10;

		[Description("Prevent banned items from being /i or /give.")]
		[JsonProperty("禁止生成禁用物")]
		public bool PreventBannedItemSpawn = false;

		[Description("Prevent players from interacting with the world if dead.")]
		[JsonProperty("禁止死亡玩家修改世界")]
		public bool PreventDeadModification = true;

		[Description("Displays chat messages above players' heads, but will disable chat prefixes to compensate.")]
		[JsonProperty("允许头顶聊天文本")]
		public bool EnableChatAboveHeads = false;

		[Description("Force Christmas-only events to occur all year.")]
		[JsonProperty("强制圣诞事件")]
		public bool ForceXmas = false;

		[Description("Allows groups on the banned item allowed list to spawn banned items.")]
		[JsonProperty("允许特定组生成禁用物")]
		public bool AllowAllowedGroupsToSpawnBannedItems = false;

		[Description("Allows stacks in chests to be beyond the stack limit")]
		[JsonProperty("忽略箱子物品堆栈检测")]
		public bool IgnoreChestStacksOnLoad = false;

		[Description("The path of the directory where logs should be written into.")]
		[JsonProperty("日志保存路径")]
		public string LogPath = "logs";

		[Description("Save logs to an SQL database instead of a text file. Default = false")]
		[JsonProperty("数据库日志")]
		public bool UseSqlLogs = false;

		[Description("Number of times the SQL log must fail to insert logs before falling back to the text log")]
		[JsonProperty("Sql日志失败次数")]
		public int RevertToTextLogsOnSqlFailures = 10;

		[Description("Prevents players from placing tiles with an invalid style.")]
		[JsonProperty("阻止无效放置模式")]
		public bool PreventInvalidPlaceStyle = true;

		[Description("#.#.#. = Red/Blue/Green - RGB Colors for broadcasts. Max value: 255.")]
		[JsonProperty("服务器通知颜色")]
		public int[] BroadcastRGB = { 127, 255, 212 };

		/// <summary>ApplicationRestTokens - A dictionary of REST tokens that external applications may use to make queries to your server.</summary>
		[Description("A dictionary of REST tokens that external applications may use to make queries to your server.")]
		[JsonProperty("应用的REST密钥")]
		public Dictionary<string, SecureRest.TokenData> ApplicationRestTokens = new Dictionary<string, SecureRest.TokenData>();

		[Description("The number of reserved slots past your max server slot that can be joined by reserved players")]
		[JsonProperty("服务器预留位")]
		public int ReservedSlots = 20;

		[Description("The number of reserved slots past your max server slot that can be joined by reserved players")]
		[JsonProperty("记录REST日志")]
		public bool LogRest = false;

		[Description("The number of seconds a player must wait before being respawned.")]
		[JsonProperty("玩家重生秒数")]
		public int RespawnSeconds = 5;

		[Description("The number of seconds a player must wait before being respawned if there is a boss nearby.")]
		[JsonProperty("Boss附近时玩家重生秒数")]
		public int RespawnBossSeconds = 10;

		[Description("Disables a player if this number of tiles is painted within 1 second.")]
		[JsonProperty("物块上色速率上限")]
		public int TilePaintThreshold = 15;

		/// <summary>ForceHalloween - Forces Halloween-only events to occur all year.</summary>
		[Description("Forces your world to be in Halloween mode regardless of the data.")]
		[JsonProperty("强制万圣节模式")]
		public bool ForceHalloween = false;

		[Description("Allows anyone to break grass, pots, etc.")]
		[JsonProperty("允许破坏特定物块")]
		public bool AllowCutTilesAndBreakables = false;

		[Description("Specifies which string starts a command.")]
		[JsonProperty("指令分隔符")]
		public string CommandSpecifier = "/";

		[Description("Specifies which string starts a command silently.")]
		[JsonProperty("静默指令分隔符")]
		public string CommandSilentSpecifier = ".";
		
		[Description("Kicks a hardcore player on death.")]
		[JsonProperty("困难模式死亡时驱逐")]
		public bool KickOnHardcoreDeath;
		
		[Description("Bans a hardcore player on death.")]
		[JsonProperty("困难模式死亡时封禁")]
		public bool BanOnHardcoreDeath;
		
		[Description("Bans a hardcore player on death.")]
		[JsonProperty("困难模式玩家封禁原因")]
		public string HardcoreBanReason = "玩家死亡后封禁";
		
		[Description("Kicks a hardcore player on death.")]
		[JsonProperty("困难模式玩家驱逐原因")]
		public string HardcoreKickReason = "玩家死亡后驱逐";

		[Description("Whether bosses or invasions should be anonymously spawned.")]
		[JsonProperty("匿名生成Boss入侵")]
		public bool AnonymousBossInvasions = true;

		[Description("The maximum allowable HP, before equipment buffs.")]
		[JsonProperty("玩家最大生命值")]
		public int MaxHP = 500;

		[Description("The maximum allowable MP, before equipment buffs.")]
		[JsonProperty("玩家最大魔法值")]
		public int MaxMP = 200;

		[Description("Determines if the server should save the world if the last player exits.")]
		[JsonProperty("最后玩家离开时保存")]
		public bool SaveWorldOnLastPlayerExit = true;

		[Description("Determines the BCrypt work factor to use. If increased, all passwords will be upgraded to new work-factor on verify. The number of computational rounds is 2^n. Increase with caution. Range: 5-31.")]
		[JsonProperty("BCrypt工作因子")]
		public int BCryptWorkFactor = 7;

		[Description("The minimum password length for new user accounts. Minimum value is 4.")]
		[JsonProperty("最短密码位数")]
		public int MinimumPasswordLength = 4;

		[Description("The maximum REST requests in the bucket before denying requests. Minimum value is 5.")]
		[JsonProperty("REST间隔内最多请求数")]
		public int RESTMaximumRequestsPerInterval = 5;

		[Description("How often in minutes the REST requests bucket is decreased by one. Minimum value is 1 minute.")]
		[JsonProperty("REST统计减少间隔分钟")]
		public int RESTRequestBucketDecreaseIntervalMinutes = 1;

		/// <summary>ShowBackupAutosaveMessages - Whether or not to show backup auto save messages.</summary>
		[JsonProperty("显示自动备份提示")]
		[Description("Show backup autosave messages.")]
		public bool ShowBackupAutosaveMessages = true;

		/// <summary>
		/// Reads a configuration file from a given path
		/// </summary>
		/// <param name="path">string path</param>
		/// <returns>ConfigFile object</returns>
		public static ConfigFile Read(string path)
		{
			if (!File.Exists(path))
				return new ConfigFile();
			using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				return Read(fs);
			}
		}

		/// <summary>
		/// Reads the configuration file from a stream
		/// </summary>
		/// <param name="stream">stream</param>
		/// <returns>ConfigFile object</returns>
		public static ConfigFile Read(Stream stream)
		{
			using (var sr = new StreamReader(stream))
			{
				var cf = JsonConvert.DeserializeObject<ConfigFile>(sr.ReadToEnd());
				if (ConfigRead != null)
					ConfigRead(cf);
				return cf;
			}
		}

		/// <summary>
		/// Writes the configuration to a given path
		/// </summary>
		/// <param name="path">string path - Location to put the config file</param>
		public void Write(string path)
		{
			using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write))
			{
				Write(fs);
			}
		}

		/// <summary>
		/// Writes the configuration to a stream
		/// </summary>
		/// <param name="stream">stream</param>
		public void Write(Stream stream)
		{
			var str = JsonConvert.SerializeObject(this, Formatting.Indented);
			using (var sw = new StreamWriter(stream))
			{
				sw.Write(str);
			}
		}

		/// <summary>
		/// On config read hook
		/// </summary>
		public static Action<ConfigFile> ConfigRead;

		/// <summary>
		/// Dumps all configuration options to a text file in Markdown format
		/// </summary>
		public static void DumpDescriptions()
		{
			var sb = new StringBuilder();
			var defaults = new ConfigFile();

			foreach (var field in defaults.GetType().GetFields().OrderBy(f => f.Name))
			{
				if (field.IsStatic)
					continue;

				var nameattr =
					field.GetCustomAttributes(false).FirstOrDefault(o => o is JsonPropertyAttribute) as JsonPropertyAttribute;
				var name = nameattr != null && !string.IsNullOrWhiteSpace(nameattr.PropertyName) ? nameattr.PropertyName : field.Name;
				var type = field.FieldType.Name;

				var descattr =
					field.GetCustomAttributes(false).FirstOrDefault(o => o is DescriptionAttribute) as DescriptionAttribute;
				var desc = descattr != null && !string.IsNullOrWhiteSpace(descattr.Description) ? descattr.Description : "无";

				var def = field.GetValue(defaults);

				sb.AppendLine("{0}  ".SFormat(name));
				sb.AppendLine("字段类型: {0}  ".SFormat(type));
				sb.AppendLine("说明: {0}  ".SFormat(desc));
				sb.AppendLine("默认值: \"{0}\"  ".SFormat(def));
				sb.AppendLine();
			}

			File.WriteAllText("TShock配置说明.txt", sb.ToString());
		}
	}
}