/*
TShock, a server mod for Terraria
Copyright (C) 2011-2015 Nyx Studios (fka. The TShock Team)

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

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Terraria;
using Terraria.ID;
using TShockAPI.DB;
using TerrariaApi.Server;
using TShockAPI.Hooks;

namespace TShockAPI
{
	public delegate void CommandDelegate(CommandArgs args);

	public class CommandArgs : EventArgs
	{
		public string Message { get; private set; }
		public TSPlayer Player { get; private set; }
		public bool Silent { get; private set; }

		/// <summary>
		/// Parameters passed to the arguement. Does not include the command name.
		/// IE '/kick "jerk face"' will only have 1 argument
		/// </summary>
		public List<string> Parameters { get; private set; }

		public Player TPlayer
		{
			get { return Player.TPlayer; }
		}

		public CommandArgs(string message, TSPlayer ply, List<string> args)
		{
			Message = message;
			Player = ply;
			Parameters = args;
			Silent = false;
		}

		public CommandArgs(string message, bool silent, TSPlayer ply, List<string> args)
		{
			Message = message;
			Player = ply;
			Parameters = args;
			Silent = silent;
		}
	}

	public class Command
	{
		/// <summary>
		/// Gets or sets whether to allow non-players to use this command.
		/// </summary>
		public bool AllowServer { get; set; }
		/// <summary>
		/// Gets or sets whether to do logging of this command.
		/// </summary>
		public bool DoLog { get; set; }
		/// <summary>
		/// Gets or sets the help text of this command.
		/// </summary>
		public string HelpText { get; set; }
        /// <summary>
        /// Gets or sets an extended description of this command.
        /// </summary>
        public string[] HelpDesc { get; set; }
		/// <summary>
		/// Gets the name of the command.
		/// </summary>
		public string Name { get { return Names[0]; } }
		/// <summary>
		/// Gets the names of the command.
		/// </summary>
		public List<string> Names { get; protected set; }
		/// <summary>
		/// Gets the permissions of the command.
		/// </summary>
		public List<string> Permissions { get; protected set; }

		private CommandDelegate commandDelegate;
		public CommandDelegate CommandDelegate
		{
			get { return commandDelegate; }
			set
			{
				if (value == null)
					throw new ArgumentNullException();

				commandDelegate = value;
			}
	 	}

		public Command(List<string> permissions, CommandDelegate cmd, params string[] names)
			: this(cmd, names)
		{
			Permissions = permissions;
		}

		public Command(string permissions, CommandDelegate cmd, params string[] names)
			: this(cmd, names)
		{
			Permissions = new List<string> { permissions };
		}

		public Command(CommandDelegate cmd, params string[] names)
		{
			if (cmd == null)
				throw new ArgumentNullException("cmd");
			if (names == null || names.Length < 1)
				throw new ArgumentException("names");

			AllowServer = true;
			CommandDelegate = cmd;
			DoLog = true;
			HelpText = "该指令无可用帮助信息.";
            HelpDesc = null;
			Names = new List<string>(names);
			Permissions = new List<string>();
		}

		public bool Run(string msg, bool silent, TSPlayer ply, List<string> parms)
		{
			if (!CanRun(ply))
				return false;

			try
			{
				CommandDelegate(new CommandArgs(msg, silent, ply, parms));
			}
			catch (Exception e)
			{
				ply.SendErrorMessage("指令执行失败. 详细信息在日志文件.");
				TShock.Log.Error(e.ToString());
			}

			return true;
		}

		public bool Run(string msg, TSPlayer ply, List<string> parms)
		{
			return Run(msg, false, ply, parms);
		}

		public bool HasAlias(string name)
		{
			return Names.Contains(name);
		}

		public bool CanRun(TSPlayer ply)
		{
			if (Permissions == null || Permissions.Count < 1)
				return true;
			foreach (var Permission in Permissions)
			{
				if (ply.HasPermission(Permission))
					return true;
			}
			return false;
		}
	}

	public static class Commands
	{
		public static List<Command> ChatCommands = new List<Command>();
		public static ReadOnlyCollection<Command> TShockCommands = new ReadOnlyCollection<Command>(new List<Command>());

		public static string Specifier
		{
			get { return string.IsNullOrWhiteSpace(TShock.Config.CommandSpecifier) ? "/" : TShock.Config.CommandSpecifier; }
		}

		public static string SilentSpecifier
		{
			get { return string.IsNullOrWhiteSpace(TShock.Config.CommandSilentSpecifier) ? "." : TShock.Config.CommandSilentSpecifier; }
		}

		private delegate void AddChatCommand(string permission, CommandDelegate command, params string[] names);

		public static void InitCommands()
		{
			List<Command> tshockCommands = new List<Command>(100);
			Action<Command> add = (cmd) => 
			{
				tshockCommands.Add(cmd);
				ChatCommands.Add(cmd);
			};

			add(new Command(AuthToken, "auth", "验证")
			{
				AllowServer = false,
				HelpText = "首次登入游戏时验证超管."
			});
			add(new Command(Permissions.authverify, AuthVerify, "auth-verify", "完成验证")
			{
				HelpText = "关闭密钥验证超管."
			});
			add(new Command(Permissions.user, ManageUsers, "user", "用户")
			{
				DoLog = false,
				HelpText = "管理用户账户."
			});

			#region Account Commands
			add(new Command(Permissions.canlogin, AttemptLogin, "login", "登录")
			{
				AllowServer = false,
				DoLog = false,
				HelpText = "登入游戏."
			});
			add(new Command(Permissions.canlogout, Logout, "logout", "登出")
			{
				AllowServer = false,
				DoLog = false,
				HelpText = "退出登录账户."
			});
			add(new Command(Permissions.canchangepassword, PasswordUser, "password", "改密")
			{
				AllowServer = false,
				DoLog = false,
				HelpText = "更改账户密码."
			});
			add(new Command(Permissions.canregister, RegisterUser, "register", "注册")
			{
				AllowServer = false,
				DoLog = false,
				HelpText = "注册新账户."
			});
			#endregion
			#region Admin Commands
			add(new Command(Permissions.ban, Ban, "ban", "封禁")
			{
				HelpText = "管理玩家封禁"
			});
			add(new Command(Permissions.broadcast, Broadcast, "say", "广播", "bc","broadcast")
			{
				HelpText = "广播特定信息."
			});
			add(new Command(Permissions.logs, DisplayLogs, "displaylogs", "显示日志")
			{
				HelpText = "控制是否接收日志."
			});
			add(new Command(Permissions.managegroup, Group, "group", "组")
			{
				HelpText = "管理用户组."
			});
			add(new Command(Permissions.manageitem, ItemBan, "itemban", "禁用物品")
			{
				HelpText = "管理物品封禁."
			});
            add(new Command(Permissions.manageprojectile, ProjectileBan, "projban", "禁抛射体")
            {
                HelpText = "管理抛射体封禁."
            });
			add(new Command(Permissions.managetile, TileBan, "tileban", "禁用物块")
			{
				HelpText = "管理物块封禁."
			});
			add(new Command(Permissions.manageregion, Region, "region", "区域")
			{
				HelpText = "管理区域."
			});
			add(new Command(Permissions.kick, Kick, "kick", "驱逐")
			{
				HelpText = "从游戏中驱逐玩家."
			});
			add(new Command(Permissions.mute, Mute, "mute", "禁言", "unmute")
			{
				HelpText = "禁言某玩家."
			});
			add(new Command(Permissions.savessc, OverrideSSC, "ossc", "覆盖云存档", "overridessc")
			{
				HelpText = "暂时覆盖某玩家的云存档."
			});
			add(new Command(Permissions.savessc, SaveSSC, "savessc", "保存云存档")
			{
				HelpText = "保存所有玩家的云存档."
			});
			add(new Command(Permissions.settempgroup, TempGroup, "tempgroup", "临时组")
			{
				HelpText = "暂时更改用户组."
			});
			add(new Command(Permissions.userinfo, GrabUserUserInfo, "ui", "用户信息", "userinfo")
			{
				HelpText = "显示用户信息."
			});
			#endregion
			#region Annoy Commands
			add(new Command(Permissions.annoy, Annoy, "annoy", "骚扰")
			{
				HelpText = "骚扰玩家一段时间."
			});
			add(new Command(Permissions.annoy, Confuse, "confuse", "混乱")
			{
				HelpText = "使玩家在一定时间分不清方向."
			});
			add(new Command(Permissions.annoy, Rocket, "rocket", "上天")
			{
				HelpText = "SSC模式下让玩家飞."
			});
			add(new Command(Permissions.annoy, FireWork, "firework", "烟花")
			{
				HelpText = "在玩家所在位置爆炸."
			});
			#endregion
			#region Configuration Commands
			add(new Command(Permissions.maintenance, CheckUpdates, "checkupdates", "检查更新")
			{
				HelpText = "检查TShock更新."
			});
			add(new Command(Permissions.maintenance, Off, "off", "关服","exit")
			{
				HelpText = "关闭服务器."
			});
			add(new Command(Permissions.maintenance, OffNoSave, "off-nosave", "不保存关服","exit-nosave")
			{
				HelpText = "不保存地图的情况下关闭服务器."
			});
			add(new Command(Permissions.cfgreload, Reload, "reload", "加载配置")
			{
				HelpText = "重载服务器配置."
			});
			add(new Command(Permissions.maintenance, Restart, "restart", "重启")
			{
				HelpText = "重启服务器."
			});
			add(new Command(Permissions.cfgpassword, ServerPassword, "serverpassword", "服务器密码")
			{
				HelpText = "更改服务器登入密码."
			});
			add(new Command(Permissions.maintenance, GetVersion, "version", "版本")
			{
				HelpText = "查看TShock版本."
			});
			/* Does nothing atm.
			 * 
			 * add(new Command(Permissions.updateplugins, UpdatePlugins, "updateplugins")
			{
			});*/
			add(new Command(Permissions.whitelist, Whitelist, "whitelist", "白名单")
			{
				HelpText = "更改服务器白名单."
			});
			#endregion
			#region Item Commands
			add(new Command(Permissions.give, Give, "g", "给","give")
			{
				HelpText = "给别人特定物品."
			});
			add(new Command(Permissions.item, Item, "i", "物", "item")
			{
				AllowServer = false,
				HelpText = "生成特定物品."
			});
			#endregion
			#region NPC Commands
			add(new Command(Permissions.butcher, Butcher, "butcher", "清怪")
			{
				HelpText = "杀死特定NPC"
			});
			add(new Command(Permissions.renamenpc, RenameNPC, "renamenpc", "重命名NPC")
			{
				HelpText = "重命名特定NPC."
			});
			add(new Command(Permissions.invade, Invade, "invade", "入侵")
			{
				HelpText = "开启入侵."
			});
			add(new Command(Permissions.maxspawns, MaxSpawns, "maxspawns", "最大刷怪")
			{
				HelpText = "设定NPC最大数量."
			});
			add(new Command(Permissions.spawnboss, SpawnBoss, "sb", "刷BOSS","spawnboss")
			{
				AllowServer = false,
				HelpText = "在你周围生成BOSS."
			});
			add(new Command(Permissions.spawnmob, SpawnMob, "sm", "刷怪", "spawnmob")
			{
				AllowServer = false,
				HelpText = "在你周围生成小怪."
			});
			add(new Command(Permissions.spawnrate, SpawnRate, "spawnrate", "刷怪率")
			{
				HelpText = "设定刷怪率."
			});
			add(new Command(Permissions.clearangler, ClearAnglerQuests, "clearangler", "清空任务")
			{
				HelpText = "清除玩家渔夫任务完成情况."
			});
			#endregion
			#region TP Commands
			add(new Command(Permissions.home, Home, "home", "家")
			{
				AllowServer = false,
				HelpText = "传送至你的出生点."
			});
			add(new Command(Permissions.spawn, Spawn, "spawn", "出生点")
			{
				AllowServer = false,
				HelpText = "传送到世界出生点."
			});
			add(new Command(Permissions.tp, TP, "tp", "传")
			{
				AllowServer = false,
				HelpText = "传送到另外的玩家位置."
			});
			add(new Command(Permissions.tpothers, TPHere, "tphere", "传至")
			{
				AllowServer = false,
				HelpText = "传送其他玩家到你的位置处."
			});
			add(new Command(Permissions.tpnpc, TPNpc, "tpnpc", "传至NPC")
			{
				AllowServer = false,
				HelpText = "传送至其他的NPC位置."
			});
			add(new Command(Permissions.tppos, TPPos, "tppos", "传坐标")
			{
				AllowServer = false,
				HelpText = "传送至特定坐标点."
			});
			add(new Command(Permissions.getpos, GetPos, "pos", "坐标")
			{
				AllowServer = false,
				HelpText = "返回你的或某玩家的坐标."
			});
			add(new Command(Permissions.tpallow, TPAllow, "tpallow", "传送保护")
			{
				AllowServer = false,
				HelpText = "切换别人能不能传送到你那里."
			});
			#endregion
			#region World Commands
			add(new Command(Permissions.toggleexpert, ToggleExpert, "expert", "专家","expertmode")
			{
					HelpText = "转换专家模式."
			});
			add(new Command(Permissions.antibuild, ToggleAntiBuild, "antibuild", "禁建筑")
			{
				HelpText = "转换世界保护状态."
			});
			add(new Command(Permissions.bloodmoon, Bloodmoon, "bloodmoon", "血月")
			{
				HelpText = "切换血月模式."
			});
			add(new Command(Permissions.grow, Grow, "grow", "长")
			{
				AllowServer = false,
				HelpText = "在指定位置长植物."
			});
			add(new Command(Permissions.dropmeteor, DropMeteor, "dropmeteor", "陨石")
			{
				HelpText = "召唤陨石."
			});
			add(new Command(Permissions.eclipse, Eclipse, "eclipse", "日蚀")
			{
				HelpText = "切换日蚀事件."
			});
			add(new Command(Permissions.halloween, ForceHalloween, "forcehalloween", "万圣")
			{
				HelpText = "转换万圣节模式."
			});
			add(new Command(Permissions.xmas, ForceXmas, "forcexmas", "圣诞")
			{
				HelpText = "转换圣诞节模式."
			});
			add(new Command(Permissions.fullmoon, Fullmoon, "fullmoon", "满月")
			{
				HelpText = "满月模式."
			});
			add(new Command(Permissions.hardmode, Hardmode, "hardmode", "肉后")
			{
				HelpText = "转换世界的肉后状态."
			});
			add(new Command(Permissions.editspawn, ProtectSpawn, "protectspawn", "出生点保护")
			{
				HelpText = "开启出生点保护."
			});
			add(new Command(Permissions.rain, Rain, "rain", "雨")
			{
				HelpText = "更改下雨."
			});
			add(new Command(Permissions.worldsave, Save, "save", "保存")
			{
				HelpText = "保存世界文件."
			});
			add(new Command(Permissions.worldspawn, SetSpawn, "setspawn", "设置出生点")
			{
				AllowServer = false,
				HelpText = "设定世界出生点."
			});
			add(new Command(Permissions.dungeonposition, SetDungeon, "setdungeon", "设置地牢位置")
			{
				AllowServer = false,
				HelpText = "设定世界地牢位置."
			});
			add(new Command(Permissions.worldsettle, Settle, "settle")
			{
				HelpText = "强行更新液体状态."
			});
			add(new Command(Permissions.time, Time, "time", "时")
			{
				HelpText = "设定世界时间."
			});
			add(new Command(Permissions.wind, Wind, "wind", "风")
			{
				HelpText = "更改风速."
			});
			add(new Command(Permissions.worldinfo, WorldInfo, "world", "世")
			{
				HelpText = "显示当前世界的信息."
			});
			#endregion
			#region Other Commands
			add(new Command(Permissions.buff, Buff, "buff")
			{
				AllowServer = false,
				HelpText = "给自己上buff."
			});
			add(new Command(Permissions.clear, Clear, "clear", "清")
			{
				HelpText = "清理物品掉落/抛射体/NPC."
			});
			add(new Command(Permissions.buffplayer, GBuff, "gbuff", "给buff","buffplayer")
			{
				HelpText = "给其他玩家上buff."
			});
			add(new Command(Permissions.godmode, ToggleGodMode, "godmode", "无敌")
			{
				HelpText = "转换玩家的无敌模式."
			});
			add(new Command(Permissions.heal, Heal, "heal", "治愈")
			{
				HelpText = "治愈玩家生命/魔力."
			});
			add(new Command(Permissions.kill, Kill, "kill", "杀死")
			{
				HelpText = "强行杀死玩家."
			});
			add(new Command(Permissions.cantalkinthird, ThirdPerson, "me", "我", "第三人")
			{
				HelpText = "发送特殊消息."
			});
			add(new Command(Permissions.canpartychat, PartyChat, "p", "队","party")
			{
				AllowServer = false,
				HelpText = "队内聊天."
			});
			add(new Command(Permissions.whisper, Reply, "r", "回复","reply")
			{
				HelpText = "回复上一条私信."
			});
			add(new Command(Rests.RestPermissions.restmanage, ManageRest, "rest")
			{
				HelpText = "管理REST接口状态."
			});
			add(new Command(Permissions.slap, Slap, "slap", "打")
			{
				HelpText = "造成玩家伤害."
			});
			add(new Command(Permissions.serverinfo, ServerInfo, "serverinfo", "信息")
			{
				HelpText = "查看服务器信息."
			});
			add(new Command(Permissions.warp, Warp, "warp", "跳跃")
			{
				HelpText = "管理跳跃点."
			});
			add(new Command(Permissions.whisper, Whisper, "w", "私聊","whisper", "tell")
			{
				HelpText = "私信玩家."
			});
			#endregion

			add(new Command(Aliases, "aliases", "别名")
			{
				HelpText = "展示某指令的别名."
			});
			add(new Command(Help, "help", "帮助")
			{
				HelpText = "查看指令列表或获取指令帮助."
			});
			add(new Command(Motd, "motd", "公告")
			{
				HelpText = "查看本日公告."
			});
			add(new Command(ListConnectedPlayers, "who", "玩家","online", "playing")
			{
				HelpText = "查看在线玩家."
			});
			add(new Command(Rules, "rules", "规则")
			{
				HelpText = "查看服务器规则."
			});

			TShockCommands = new ReadOnlyCollection<Command>(tshockCommands);
		}

		public static bool HandleCommand(TSPlayer player, string text)
		{
			string cmdText = text.Remove(0, 1);
			string cmdPrefix = text[0].ToString();
			bool silent = false;

			if (cmdPrefix == SilentSpecifier)
				silent = true;

			var args = ParseParameters(cmdText);
			if (args.Count < 1)
				return false;

			string cmdName = args[0].ToLower();
			args.RemoveAt(0);

			IEnumerable<Command> cmds = ChatCommands.FindAll(c => c.HasAlias(cmdName));

			if (Hooks.PlayerHooks.OnPlayerCommand(player, cmdName, cmdText, args, ref cmds, cmdPrefix))
				return true;

			if (cmds.Count() == 0)
			{
				if (player.AwaitingResponse.ContainsKey(cmdName))
				{
					Action<CommandArgs> call = player.AwaitingResponse[cmdName];
					player.AwaitingResponse.Remove(cmdName);
					call(new CommandArgs(cmdText, player, args));
					return true;
				}
				player.SendErrorMessage("键入的指令无效. 使用 {0}help 查看有效指令.", Specifier);
				return true;
			}
			foreach (Command cmd in cmds)
			{
				if (!cmd.CanRun(player))
				{
					TShock.Utils.SendLogs(string.Format("{0} 尝试执行 {1}{2}.", player.Name, Specifier, cmdText), Color.PaleVioletRed, player);
					player.SendErrorMessage("缺少执行该指令的权限.");
				}
				else if (!cmd.AllowServer && !player.RealPlayer)
				{
					player.SendErrorMessage("你必须在游戏内执行该指令.");
				}
				else
				{
					if (cmd.DoLog)
						TShock.Utils.SendLogs(string.Format("{0} 执行: {1}{2}.", player.Name, silent ? SilentSpecifier : Specifier, cmdText), Color.PaleVioletRed, player);
					cmd.Run(cmdText, silent, player, args);
				}
			}
			return true;
		}

		/// <summary>
		/// Parses a string of parameters into a list. Handles quotes.
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		private static List<String> ParseParameters(string str)
		{
			var ret = new List<string>();
			var sb = new StringBuilder();
			bool instr = false;
			for (int i = 0; i < str.Length; i++)
			{
				char c = str[i];

				if (c == '\\' && ++i < str.Length)
				{
					if (str[i] != '"' && str[i] != ' ' && str[i] != '\\')
						sb.Append('\\');
					sb.Append(str[i]);
				}
				else if (c == '"')
				{
					instr = !instr;
					if (!instr)
					{
						ret.Add(sb.ToString());
						sb.Clear();
					}
					else if (sb.Length > 0)
					{
						ret.Add(sb.ToString());
						sb.Clear();
					}
				}
				else if (IsWhiteSpace(c) && !instr)
				{
					if (sb.Length > 0)
					{
						ret.Add(sb.ToString());
						sb.Clear();
					}
				}
				else
					sb.Append(c);
			}
			if (sb.Length > 0)
				ret.Add(sb.ToString());

			return ret;
		}

		private static bool IsWhiteSpace(char c)
		{
			return c == ' ' || c == '\t' || c == '\n';
		}

		#region Account commands

		private static void AttemptLogin(CommandArgs args)
		{
			if (args.Player.LoginAttempts > TShock.Config.MaximumLoginAttempts && (TShock.Config.MaximumLoginAttempts != -1))
			{
				TShock.Log.Warn(String.Format("{0} ({1}) 超出尝试登录上限({2}), 已被移除游戏.",
					args.Player.IP, args.Player.Name, TShock.Config.MaximumLoginAttempts));
				TShock.Utils.Kick(args.Player, "无效登录次数过多.");
				return;
			}

			if (args.Player.IsLoggedIn)
			{
				args.Player.SendErrorMessage("你已成功登录, 故无法再次登录.");
				return;
			}
            
			User user = TShock.Users.GetUserByName(args.Player.Name);
			string password = "";
			bool usingUUID = false;
			if (args.Parameters.Count == 0 && !TShock.Config.DisableUUIDLogin)
			{
				if (PlayerHooks.OnPlayerPreLogin(args.Player, args.Player.Name, ""))
					return;
				usingUUID = true;
			}
			else if (args.Parameters.Count == 1)
			{
				if (PlayerHooks.OnPlayerPreLogin(args.Player, args.Player.Name, args.Parameters[0]))
					return;
				password = args.Parameters[0];
			}
			else if (args.Parameters.Count == 2 && TShock.Config.AllowLoginAnyUsername)
			{
				if (String.IsNullOrEmpty(args.Parameters[0]))
				{
					args.Player.SendErrorMessage("无效登录尝试.");
					return;
				}

				if (PlayerHooks.OnPlayerPreLogin(args.Player, args.Parameters[0], args.Parameters[1]))
					return;

				user = TShock.Users.GetUserByName(args.Parameters[0]);
				password = args.Parameters[1];
			}
			else
			{
				args.Player.SendErrorMessage("语法: {0}login - 使用人物名和UUID验证登录.", Specifier);
				args.Player.SendErrorMessage("      {0}login <密码> - 使用人物名和密码验证登录.", Specifier);
				args.Player.SendErrorMessage("      {0}login <用户名> <密码> - 使用用户名和密码验证登录.", Specifier);
				args.Player.SendErrorMessage("如果你忘记密码, 我们无法恢复.");
				return;
			}
			try
			{
				if (user == null)
				{
					args.Player.SendErrorMessage("不存在相应的账户. 尝试注册?");
				}
				else if (user.VerifyPassword(password) ||
						(usingUUID && user.UUID == args.Player.UUID && !TShock.Config.DisableUUIDLogin &&
						!String.IsNullOrWhiteSpace(args.Player.UUID)))
				{
					args.Player.PlayerData = TShock.CharacterDB.GetPlayerData(args.Player, user.ID);

					var group = TShock.Utils.GetGroup(user.Group);

					args.Player.Group = group;
					args.Player.tempGroup = null;
					args.Player.User = user;
					args.Player.IsLoggedIn = true;
					args.Player.IgnoreActionsForInventory = "none";

					if (Main.ServerSideCharacter)
					{
						if (args.Player.HasPermission(Permissions.bypassssc))
						{
							args.Player.PlayerData.CopyCharacter(args.Player);
							TShock.CharacterDB.InsertPlayerData(args.Player);
						}
						args.Player.PlayerData.RestoreCharacter(args.Player);
					}
					args.Player.LoginFailsBySsi = false;

					if (args.Player.HasPermission(Permissions.ignorestackhackdetection))
						args.Player.IgnoreActionsForCheating = "none";

					if (args.Player.HasPermission(Permissions.usebanneditem))
						args.Player.IgnoreActionsForDisabledArmor = "none";

					args.Player.SendSuccessMessage($"已经验证 {user.Name} 登录完毕.");

					TShock.Log.ConsoleInfo(args.Player.Name + " 成功使用账户 " + user.Name + " 验证登录.");
					if ((args.Player.LoginHarassed) && (TShock.Config.RememberLeavePos))
					{
						if (TShock.RememberedPos.GetLeavePos(args.Player.Name, args.Player.IP) != Vector2.Zero)
						{
							Vector2 pos = TShock.RememberedPos.GetLeavePos(args.Player.Name, args.Player.IP);
							args.Player.Teleport((int) pos.X*16, (int) pos.Y*16);
						}
						args.Player.LoginHarassed = false;

					}
					TShock.Users.SetUserUUID(user, args.Player.UUID);

					Hooks.PlayerHooks.OnPlayerPostLogin(args.Player);
				}
				else
				{
					if (usingUUID && !TShock.Config.DisableUUIDLogin)
					{
						args.Player.SendErrorMessage("UUID记录不符, 无法自动登录!");
					}
					else
					{
						args.Player.SendErrorMessage("密码无效!");
					}
					TShock.Log.Warn(args.Player.IP + " 尝试使用账户 " + user.Name + " 登录失败.");
					args.Player.LoginAttempts++;
				}
			}
			catch (Exception ex)
			{
				args.Player.SendErrorMessage("处理数据出现异常, 请联系管理员.");
				TShock.Log.Error(ex.ToString());
			}
		}

		private static void Logout(CommandArgs args)
		{
			if (!args.Player.IsLoggedIn)
			{
				args.Player.SendErrorMessage("你没有登录游戏.");
				return;
			}

			PlayerHooks.OnPlayerLogout(args.Player);


			if (Main.ServerSideCharacter)
			{
				args.Player.IgnoreActionsForInventory = String.Format("云端存档/强制开荒 模式. 请使用 {0}register \\ {0}login 加入游戏!", Commands.Specifier);
				if (!args.Player.IgnoreActionsForClearingTrashCan && (!args.Player.Dead || args.Player.TPlayer.difficulty != 2))
				{
					args.Player.PlayerData.CopyCharacter(args.Player);
					TShock.CharacterDB.InsertPlayerData(args.Player);
				}
			}

			args.Player.PlayerData = new PlayerData(args.Player);
			args.Player.Group = TShock.Groups.GetGroupByName(TShock.Config.DefaultGuestGroupName);
			args.Player.tempGroup = null;
			if (args.Player.tempGroupTimer != null)
			{
				args.Player.tempGroupTimer.Stop();
			}
			args.Player.User = null;
			args.Player.IsLoggedIn = false;

			args.Player.SendSuccessMessage("成功登出游戏.");
			if (Main.ServerSideCharacter)
			{
				args.Player.SendWarningMessage("云端存档/强制开荒 模式. 你必须登录后才能进入游戏.");
			}
		}

		private static void PasswordUser(CommandArgs args)
		{
			try
			{
				if (args.Player.IsLoggedIn && args.Parameters.Count == 2)
				{
					string password = args.Parameters[0];
					if (args.Player.User.VerifyPassword(password))
					{
						try
						{
							args.Player.SendSuccessMessage("更改密码完毕!");
							TShock.Users.SetUserPassword(args.Player.User, args.Parameters[1]); // SetUserPassword will hash it for you.
							TShock.Log.ConsoleInfo($"玩家 {args.Player.Name}({args.Player.IP}) 更改了账户 {args.Player.User.Name} 的密码.");

                        }
						catch (ArgumentOutOfRangeException)
						{
							args.Player.SendErrorMessage("密码位数不能少于 " + TShock.Config.MinimumPasswordLength + " 个字符.");
						}
					}
					else
					{
						args.Player.SendErrorMessage("旧密码错误!");
						TShock.Log.ConsoleError($"玩家 {args.Player.Name}({args.Player.IP}) 未能更改账户 {args.Player.User.Name} 的密码.");
					}
				}
				else
				{
					args.Player.SendErrorMessage("未登录或语法无效! 正确语法: {0}password <旧密码> <新密码>", Specifier);
				}
			}
			catch (UserManagerException ex)
			{
				args.Player.SendErrorMessage("对不起, 出现了异常: " + ex.Message + ".");
				TShock.Log.ConsoleError("PasswordUser(更改密码) 方法出现异常: " + ex);
			}
		}

		private static void RegisterUser(CommandArgs args)
		{
			try
			{
				var user = new User();
				string echoPassword = "";
				if (args.Parameters.Count == 1)
				{
					user.Name = args.Player.Name;
					echoPassword = args.Parameters[0];
					try
					{
						user.CreateBCryptHash(args.Parameters[0]);
					}
					catch (ArgumentOutOfRangeException)
					{
						args.Player.SendErrorMessage("密码位数不能少于 " + TShock.Config.MinimumPasswordLength + " 个字符.");
						return;
					}
				}
				else if (args.Parameters.Count == 2 && TShock.Config.AllowRegisterAnyUsername)
				{
					user.Name = args.Parameters[0];
					echoPassword = args.Parameters[1];
					try
					{
						user.CreateBCryptHash(args.Parameters[1]);
					}
					catch (ArgumentOutOfRangeException)
					{
						args.Player.SendErrorMessage("密码位数不能少于 " + TShock.Config.MinimumPasswordLength + " 个字符.");
						return;
					}
				}
				else
				{
					args.Player.SendErrorMessage("语法无效! 正确语法: {0}register <密码>", Specifier);
					return;
				}

				user.Group = TShock.Config.DefaultRegistrationGroupName; // FIXME -- we should get this from the DB. --Why?
				user.UUID = args.Player.UUID;

                if (TShock.Users.GetUserByName(user.Name) == null && user.Name != TSServerPlayer.AccountName) // Cheap way of checking for existance of a user
				{
					args.Player.SendSuccessMessage("账户 {0} 注册成功.", user.Name);
					args.Player.SendSuccessMessage("你的密码是 {0}.", echoPassword);
					TShock.Users.AddUser(user);
					TShock.Log.ConsoleInfo("玩家 {0} 注册了新账户: {1}.", args.Player.Name, user.Name);
				}
				else
				{
					args.Player.SendErrorMessage("账户 " + user.Name + " 已经被注册了.");
					TShock.Log.ConsoleInfo(args.Player.Name + " 尝试注册一个存在的账户: " + user.Name);
				}
			}
			catch (UserManagerException ex)
			{
				args.Player.SendErrorMessage("对不起, 出现了异常: " + ex.Message + ".");
				TShock.Log.ConsoleError("RegisterUser 出现异常:: " + ex);
			}
		}

		private static void ManageUsers(CommandArgs args)
		{
			// This guy needs to be here so that people don't get exceptions when they type /user
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("语法无效! 使用 {0}user help 以获取使用方法.", Specifier);
				return;
			}

			string subcmd = args.Parameters[0];

			// Add requires a username, password, and a group specified.
			if (subcmd == "add"  && args.Parameters.Count == 4)
			{
				var user = new User();

				user.Name = args.Parameters[1];
				try
				{
					user.CreateBCryptHash(args.Parameters[2]);
				}
				catch (ArgumentOutOfRangeException)
				{
					args.Player.SendErrorMessage("密码位数不能少于 " + TShock.Config.MinimumPasswordLength + " 个字符.");
					return;
				}
				user.Group = args.Parameters[3];
				
				try
				{
					TShock.Users.AddUser(user);
					args.Player.SendSuccessMessage("添加账户 " + user.Name + " 至组 " + user.Group + " 完毕!");
					TShock.Log.ConsoleInfo(args.Player.Name + " 添加了账户 " + user.Name + " 至组 " + user.Group);
				}
				catch (GroupNotExistsException)
				{
					args.Player.SendErrorMessage("组 " + user.Group + " 不存在!");
				}
				catch (UserExistsException)
				{
					args.Player.SendErrorMessage("账户 " + user.Name + " 已存在!");
				}
				catch (UserManagerException e)
				{
					args.Player.SendErrorMessage("用户 " + user.Name + " 添加失败. 详细信息在控制台窗口.");
					TShock.Log.ConsoleError(e.ToString());
				}
			}
				// User deletion requires a username
			else if (subcmd == "del" && args.Parameters.Count == 2)
			{
				var user = new User();
				user.Name = args.Parameters[1];

				try
				{
					TShock.Users.RemoveUser(user);
					args.Player.SendSuccessMessage("成功移除账户.");
					TShock.Log.ConsoleInfo(args.Player.Name + " 删除了账户: " + args.Parameters[1] + ".");
				}
				catch (UserNotExistException)
				{
					args.Player.SendErrorMessage("账户 " + user.Name + " 不存在!");
				}
				catch (UserManagerException ex)
				{
					args.Player.SendErrorMessage(ex.Message);
					TShock.Log.ConsoleError(ex.ToString());
				}
			}
			
			// Password changing requires a username, and a new password to set
			else if (subcmd == "password" && args.Parameters.Count == 3)
			{
				var user = new User();
				user.Name = args.Parameters[1];

				try
				{
					TShock.Users.SetUserPassword(user, args.Parameters[2]);
					TShock.Log.ConsoleInfo(args.Player.Name + " 更改了账户 " + user.Name + "的密码");
					args.Player.SendSuccessMessage("成功更改账户 " + user.Name + " 的密码.");
				}
				catch (UserNotExistException)
				{
					args.Player.SendErrorMessage("账户 " + user.Name + " 不存在!");
				}
				catch (UserManagerException e)
				{
					args.Player.SendErrorMessage("账户 " + user.Name + " 更改密码失败! 请检查控制台异常消息.");
					TShock.Log.ConsoleError(e.ToString());
				}
				catch (ArgumentOutOfRangeException)
				{
					args.Player.SendErrorMessage("密码位数不能少于 " + TShock.Config.MinimumPasswordLength + " 个字符.");
				}
			}
			// Group changing requires a username or IP address, and a new group to set
			else if (subcmd == "group" && args.Parameters.Count == 3)
			{
				var user = new User();
				user.Name = args.Parameters[1];

				try
				{
					TShock.Users.SetUserGroup(user, args.Parameters[2]);
					TShock.Log.ConsoleInfo(args.Player.Name + " 更改了用户 " + user.Name + " 的用户组至 " + args.Parameters[2] + ".");
					args.Player.SendSuccessMessage("用户 " + user.Name + " 的用户组被调整至 " + args.Parameters[2] + "!");
				}
				catch (GroupNotExistsException)
				{
					args.Player.SendErrorMessage("不存在该用户组!");
				}
				catch (UserNotExistException)
				{
					args.Player.SendErrorMessage("用户 " + user.Name + " 不存在!");
				}
				catch (UserManagerException e)
				{
					args.Player.SendErrorMessage("用户 " + user.Name + " 无法被修改. 请检查控制台的异常消息!");
					TShock.Log.ConsoleError(e.ToString());
				}
			}
			else if (subcmd == "help")
			{
				args.Player.SendInfoMessage("用户管理子指令帮助:");
				args.Player.SendInfoMessage("{0}user add <账户名> <密码> <组> -- 添加账户", Specifier);
				args.Player.SendInfoMessage("{0}user del <账户名> -- 删除指定账户", Specifier);
				args.Player.SendInfoMessage("{0}user password <帐户名> <新密码> -- 更改特定账户的密码", Specifier);
				args.Player.SendInfoMessage("{0}user group <账户名> <组> -- 改变某账户的用户组", Specifier);
			}
			else
			{
				args.Player.SendErrorMessage("语法无效! 使用 {0}user help 以获取使用方法.", Specifier);
			}
		}

		#endregion

		#region Stupid commands

		private static void ServerInfo(CommandArgs args)
		{
			args.Player.SendInfoMessage("内存占用: " + Process.GetCurrentProcess().WorkingSet64);
			args.Player.SendInfoMessage("虚拟内存占用: " + Process.GetCurrentProcess().VirtualMemorySize64);
			args.Player.SendInfoMessage("处理器时间: " + Process.GetCurrentProcess().TotalProcessorTime);
			args.Player.SendInfoMessage("系统版本: " + Environment.OSVersion);
			args.Player.SendInfoMessage("处理器数目: " + Environment.ProcessorCount);
			args.Player.SendInfoMessage("主机名: " + Environment.MachineName);
		}

		private static void WorldInfo(CommandArgs args)
		{
			args.Player.SendInfoMessage("世界名: " + (TShock.Config.UseServerName ? TShock.Config.ServerName : Main.worldName));
			args.Player.SendInfoMessage("大小: {0}x{1}", Main.maxTilesX, Main.maxTilesY);
			args.Player.SendInfoMessage("世界ID: " + Main.worldID);
		}

		#endregion

		#region Player Management Commands

		private static void GrabUserUserInfo(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}userinfo <玩家名>", Specifier);
				return;
			}

			var players = TShock.Utils.FindPlayer(args.Parameters[0]);
			if (players.Count < 1)
				args.Player.SendErrorMessage("未找到玩家.");
			else if (players.Count > 1)
				TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
			else
			{
				var message = new StringBuilder();
				message.Append("IP地址: ").Append(players[0].IP);
				if (players[0].User != null && players[0].IsLoggedIn)
					message.Append(" | 用户名: ").Append(players[0].User.Name).Append(" | 用户组: ").Append(players[0].Group.Name);
				args.Player.SendSuccessMessage(message.ToString());
			}
		}

		private static void Kick(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}kick <玩家名> [原因]", Specifier);
				return;
			}
			if (args.Parameters[0].Length == 0)
			{
				args.Player.SendErrorMessage("缺少玩家名.");
				return;
			}

			string plStr = args.Parameters[0];
			var players = TShock.Utils.FindPlayer(plStr);
			if (players.Count == 0)
			{
				args.Player.SendErrorMessage("指定玩家无效!");
			}
			else if (players.Count > 1)
			{
				TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
			}
			else
			{
				string reason = args.Parameters.Count > 1
									? String.Join(" ", args.Parameters.GetRange(1, args.Parameters.Count - 1))
									: "不当行为";
				if (!TShock.Utils.Kick(players[0], reason, !args.Player.RealPlayer, false, args.Player.Name))
				{
					args.Player.SendErrorMessage("你无法驱逐其他管理!");
				}
			}
		}

		private static void Ban(CommandArgs args)
		{
			string subcmd = args.Parameters.Count == 0 ? "help" : args.Parameters[0].ToLower();
			switch (subcmd)
			{
				case "add":
                case "禁":
					#region Add ban
					{
						if (args.Parameters.Count < 2)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}ban add <玩家名> [原因]", Specifier);
							return;
						}

						List<TSPlayer> players = TShock.Utils.FindPlayer(args.Parameters[1]);
						string reason = args.Parameters.Count > 2 ? String.Join(" ", args.Parameters.Skip(2)) : "不当行为.";
						if (players.Count == 0)
						{
							var user = TShock.Users.GetUserByName(args.Parameters[1]);
							if (user != null)
							{
								bool force = !args.Player.RealPlayer;

								if (user.Name == args.Player.Name && !force)
								{
									args.Player.SendErrorMessage("你无法封禁你自己!");
									return;
								}

								if (TShock.Groups.GetGroupByName(user.Group).HasPermission(Permissions.immunetoban) && !force)
									args.Player.SendErrorMessage("你无法封禁玩家 {0}!", user.Name);
								else
								{
									if (user.KnownIps == null)
									{
										args.Player.SendErrorMessage("无法封禁玩家 {0} 因为缺少历史IP信息.", user.Name);
										return;
									}
									var knownIps = JsonConvert.DeserializeObject<List<string>>(user.KnownIps);
									TShock.Bans.AddBan(knownIps.Last(), user.Name, user.UUID, reason, false, args.Player.User.Name);
									if (String.IsNullOrWhiteSpace(args.Player.User.Name))
									{
										if (args.Silent)
										{
											args.Player.SendInfoMessage("{0} 被{1}封禁. 原因: {2}.", user.Name, force ? "强制" : "", reason);
										} 
										else 
										{
											TSPlayer.All.SendInfoMessage("{0} 被{1}封禁. 原因: {2}.", user.Name, force ? "强制" : "", reason);
										}
									}
									else
									{
										if (args.Silent)
										{
											args.Player.SendInfoMessage("{1} 被{0}封禁了, 原因: {2}.", force ? "强制" : "", user.Name, reason);
										}
										else
										{
											TSPlayer.All.SendInfoMessage("{0} {1}封禁了 {2}. 原因: {3}.", args.Player.Name, force ? "强制" : "",user.Name,reason);
                                        }
									}
								}
							}
							else
								args.Player.SendErrorMessage("无效玩家/玩家账户!");
						}
						else if (players.Count > 1)
							TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
						else
						{
							if (!TShock.Utils.Ban(players[0], reason, !args.Player.RealPlayer, args.Player.User.Name))
								args.Player.SendErrorMessage("你无法封禁玩家 {0}!", players[0].Name);
						}
					}
					#endregion
					return;
				case "addip":
                case "禁ip":
					#region Add IP ban
					{
						if (args.Parameters.Count < 2)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}ban addip <ip> [原因]", Specifier);
							return;
						}

						string ip = args.Parameters[1];
						string reason = args.Parameters.Count > 2
											? String.Join(" ", args.Parameters.GetRange(2, args.Parameters.Count - 2))
											: "手动添加IP地址封禁.";
						TShock.Bans.AddBan(ip, "", "", reason, false, args.Player.User.Name);
						args.Player.SendSuccessMessage("成功封禁 IP {0}.", ip);
					}
					#endregion
					return;
				case "addtemp":
                case "临时禁":
                    #region Add temp ban
                    {
						if (args.Parameters.Count < 3)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}ban addtemp <玩家名> <时间> [原因]", Specifier);
							return;
						}

						int time;
						if (!TShock.Utils.TryParseTime(args.Parameters[2], out time))
						{
							args.Player.SendErrorMessage("时间格式无效! 正确格式: _d_h_m_s(至少一个, 如5d).");
							args.Player.SendErrorMessage("有效的格式: 1d \\ 10h-30m+2m, 无效: 2 \\ 5.");
							return;
						}

						string reason = args.Parameters.Count > 3
											? String.Join(" ", args.Parameters.Skip(3))
											: "不当行为.";

						List<TSPlayer> players = TShock.Utils.FindPlayer(args.Parameters[1]);
						if (players.Count == 0)
						{
							var user = TShock.Users.GetUserByName(args.Parameters[1]);
							if (user != null)
							{
								bool force = !args.Player.RealPlayer;
								if (TShock.Groups.GetGroupByName(user.Group).HasPermission(Permissions.immunetoban) && !force)
									args.Player.SendErrorMessage("你无法封禁玩家 {0}!", user.Name);
								else
								{
									var knownIps = JsonConvert.DeserializeObject<List<string>>(user.KnownIps);
									TShock.Bans.AddBan(knownIps.Last(), user.Name, user.UUID, reason, false, args.Player.User.Name, DateTime.UtcNow.AddSeconds(time).ToString("s"));
									if (String.IsNullOrWhiteSpace(args.Player.User.Name))
									{
										if (args.Silent)
										{
                                            args.Player.SendSuccessMessage("{0} 被{1}封禁. 原因: {2}.",user.Name,force ? "强制" : "",reason);
                                        }
										else
										{
											TSPlayer.All.SendInfoMessage("{0} 被{1}封禁. 原因: {2}.", user.Name, force ? "强制 " : "", reason);
										}
									}
									else
									{
									    if (args.Silent) 
                                        {
                                            args.Player.SendSuccessMessage("{0} 被{1}封禁. 原因: {2}.", players[0].Name,force ? "强制" : "", reason);
									    }
									    else 
                                        {
									        TSPlayer.All.SendSuccessMessage("{0} {1}封禁了 {2}. 原因: {3}.", args.Player.Name,force ? "强制" : "", players[0].Name,
									            reason);
									    }
									}
								}
							}
							else
							{
								args.Player.SendErrorMessage("无效玩家/玩家账户!");
							}
						}
						else if (players.Count > 1)
							TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
						else
						{
							if (args.Player.RealPlayer && players[0].HasPermission(Permissions.immunetoban))
							{
								args.Player.SendErrorMessage("你无法封禁玩家 {0}!", players[0].Name);
								return;
							}

							if (TShock.Bans.AddBan(players[0].IP, players[0].Name, players[0].UUID, reason,
								false, args.Player.Name, DateTime.UtcNow.AddSeconds(time).ToString("s")))
							{
								players[0].Disconnect(String.Format("你被封禁: {0}", reason));
								string verb = args.Player.RealPlayer ? "强制" : "";
								if (args.Player.RealPlayer)
									if (args.Silent)
									{
										args.Player.SendSuccessMessage("{0}封禁 {1} 完毕. 原因: {2}", verb, players[0].Name, reason);
									}
									else
									{
										TSPlayer.All.SendSuccessMessage("{0} {1}封禁了 {2}. 原因: {3}", args.Player.Name, verb, players[0].Name, reason);
									}
								else
								{
									if (args.Silent) 
									{
										args.Player.SendSuccessMessage("{0}封禁 {1} 完毕. 原因: {2}", verb, players[0].Name, reason);
									}
									else
									{
										TSPlayer.All.SendSuccessMessage("{0} 被{1}封禁. 原因: {2}.", players[0].Name, verb, reason);
									}
								}
							}
							else
								args.Player.SendErrorMessage("封禁玩家 {0} 失败, 请检查日志.", players[0].Name);
						}
					}
					#endregion
					return;
				case "del":
                case "解禁":
                    #region Delete ban
                    {
						if (args.Parameters.Count != 2)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}ban del <玩家名>", Specifier);
							return;
						}

						string plStr = args.Parameters[1];
						Ban ban = TShock.Bans.GetBanByName(plStr, false);
						if (ban != null)
						{
							if (TShock.Bans.RemoveBan(ban.Name, true))
								args.Player.SendSuccessMessage("成功解禁 {0} ({1}).", ban.Name, ban.IP);
							else
								args.Player.SendErrorMessage("无法解禁 {0} ({1}), 请检查日志.", ban.Name, ban.IP);
						}
						else
							args.Player.SendErrorMessage("玩家 {0} 未被封禁.", plStr);
					}
					#endregion
					return;
				case "delip":
                case "解禁ip":
                    #region Delete IP ban
                    {
						if (args.Parameters.Count != 2)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}ban delip <ip>", Specifier);
							return;
						}

						string ip = args.Parameters[1];
						Ban ban = TShock.Bans.GetBanByIp(ip);
						if (ban != null)
						{
							if (TShock.Bans.RemoveBan(ban.IP, false))
								args.Player.SendSuccessMessage("成功解禁 IP {0} ({1}).", ban.IP, ban.Name);
							else
								args.Player.SendErrorMessage("解禁IP {0} ({1}) 失败, 请检查日志.", ban.IP, ban.Name);
						}
						else
							args.Player.SendErrorMessage("IP {0} 没有被禁止.", ip);
					}
					#endregion
					return;
				case "help":
                case "帮助":
					#region Help
					{
						int pageNumber;
						if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
							return;

						var lines = new List<string>
						{
							"禁(add) <玩家名> [原因] - 封禁在线用户或离线账户.",
							"禁IP(addip) <ip> [原因] - 封禁指定IP.",
							"临时禁(addtemp) <玩家名> <时间> [原因] - 临时封禁某玩家.",
							"解禁(del) <玩家名> - 解除对玩家的封禁.",
							"解禁IP(delip) <ip> - 解除对特定IP的封禁.",
							"列表(list) [页码] - 显示所有被封禁的玩家.",
							"IP列表(listip) [页码] - 显示所有被封禁的IP."
                        };
						
						PaginationTools.SendPage(args.Player, pageNumber, lines,
							new PaginationTools.Settings
							{
								HeaderFormat = "封禁管理子指令 ({0}/{1}):",
								FooterFormat = "键入 {0}ban help {{0}} 以获取更多子指令.".SFormat(Specifier)
							}
						);
					}
					#endregion
					return;
				case "list":
                case "列表":
					#region List bans
					{
						int pageNumber;
						if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
						{
							return;
						}

						List<Ban> bans = TShock.Bans.GetBans();

						var nameBans = from ban in bans
									   where !String.IsNullOrEmpty(ban.Name)
									   select ban.Name;

						PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(nameBans),
							new PaginationTools.Settings
							{
								HeaderFormat = "玩家封禁列表 ({0}/{1}):",
								FooterFormat = "键入 {0}ban list {{0}} 以获取下一页列表.".SFormat(Specifier),
								NothingToDisplayString = "当前没有被封禁的玩家."
							});
					}
					#endregion
					return;
				case "listip":
                case "ip列表":
                    #region List IP bans
                    {
						int pageNumber;
						if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
						{
							return;
						}

						List<Ban> bans = TShock.Bans.GetBans();

						var ipBans = from ban in bans
									 where String.IsNullOrEmpty(ban.Name)
									 select ban.IP;

						PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(ipBans),
							new PaginationTools.Settings
							{
								HeaderFormat = "封禁IP列表 ({0}/{1}):",
								FooterFormat = "键入 {0}ban listip {{0}} 以获取下一页列表.".SFormat(Specifier),
								NothingToDisplayString = "当前没有被封禁的IP."
                            });
					}
					#endregion
					return;
				default:
					args.Player.SendErrorMessage("子指令无效! 键入 {0}ban help 以获取更多用法/子指令.", Specifier);
					return;
			}
		}

		private static void Whitelist(CommandArgs args)
		{
			if (args.Parameters.Count == 1)
			{
				using (var tw = new StreamWriter(FileTools.WhitelistPath, true))
				{
					tw.WriteLine(args.Parameters[0]);
				}
				args.Player.SendSuccessMessage("添加了 " + args.Parameters[0] + " 至白名单.");
			}
		}

		private static void DisplayLogs(CommandArgs args)
		{
			args.Player.DisplayLogs = (!args.Player.DisplayLogs);
			args.Player.SendSuccessMessage("你将会 " + (args.Player.DisplayLogs ? "开始" : "停止") + " 接收日志显示.");
		}

		private static void SaveSSC(CommandArgs args)
		{
			if (Main.ServerSideCharacter)
			{
				args.Player.SendSuccessMessage("云存档保存完毕.");
				foreach (TSPlayer player in TShock.Players)
				{
					if (player != null && player.IsLoggedIn && !player.IgnoreActionsForClearingTrashCan)
					{
						TShock.CharacterDB.InsertPlayerData(player);
					}
				}
			}
		}

		private static void OverrideSSC(CommandArgs args)
		{
			if (!Main.ServerSideCharacter)
			{
				args.Player.SendErrorMessage("当前非云存档模式.");
				return;
			}
			if( args.Parameters.Count < 1 )
			{
				args.Player.SendErrorMessage("正确用法: {0}overridessc|{0}ossc <玩家名>", Specifier);
				return;
			}

			string playerNameToMatch = string.Join(" ", args.Parameters);
			var matchedPlayers = TShock.Utils.FindPlayer(playerNameToMatch);
			if( matchedPlayers.Count < 1 )
			{
				args.Player.SendErrorMessage("未找到玩家 {0}.", playerNameToMatch);
				return;
			}
			else if( matchedPlayers.Count > 1 )
			{
				TShock.Utils.SendMultipleMatchError(args.Player, matchedPlayers.Select(p => p.Name));
				return;
			}

			TSPlayer matchedPlayer = matchedPlayers[0];
			if (matchedPlayer.IsLoggedIn)
			{
				args.Player.SendErrorMessage("玩家 {0} 已登录.", matchedPlayer.Name);
				return;
			}
			if (!matchedPlayer.LoginFailsBySsi)
			{
				args.Player.SendErrorMessage("玩家 {0} 需要先执行一次 /login 尝试.", matchedPlayer.Name);
				return;
			}
			if (matchedPlayer.IgnoreActionsForClearingTrashCan)
			{
				args.Player.SendErrorMessage("玩家 {0} 首先需要重新连接服务器.", matchedPlayer.Name);
				return;
			}

			TShock.CharacterDB.InsertPlayerData(matchedPlayer);
			args.Player.SendSuccessMessage("玩家 {0} 的云存档已被覆盖保存.", matchedPlayer.Name);
		}

		private static void ForceHalloween(CommandArgs args)
		{
			TShock.Config.ForceHalloween = !TShock.Config.ForceHalloween;
			Main.checkHalloween();
			if (args.Silent) 
				args.Player.SendInfoMessage("{0}了圣诞模式!", (TShock.Config.ForceHalloween ? "开启" : "关闭"));
			else
				TSPlayer.All.SendInfoMessage("{0} {1}了圣诞模式!", args.Player.Name, (TShock.Config.ForceHalloween ? "开启" : "关闭"));
		}

		private static void ForceXmas(CommandArgs args)
		{
			TShock.Config.ForceXmas = !TShock.Config.ForceXmas;
			Main.checkXMas();
			if (args.Silent)
				args.Player.SendInfoMessage("{0}了圣诞模式!", (TShock.Config.ForceXmas ? "开启" : "关闭"));
			else
				TSPlayer.All.SendInfoMessage("{0} {1}了圣诞模式!", args.Player.Name, (TShock.Config.ForceXmas ? "开启" : "关闭"));
		}

		private static void TempGroup(CommandArgs args)
		{
			if (args.Parameters.Count < 2)
			{
				args.Player.SendInfoMessage("用法无效!");
				args.Player.SendInfoMessage("用法: {0}tempgroup <用户名> <新用户组> [时间]", Specifier);
				return;
			}

			List<TSPlayer> ply = TShock.Utils.FindPlayer(args.Parameters[0]);
			if (ply.Count < 1)
			{
				args.Player.SendErrorMessage("无法找到玩家 {0}.", args.Parameters[0]);
				return;
			}

			if (ply.Count > 1)
			{
				TShock.Utils.SendMultipleMatchError(args.Player, ply.Select(p => p.User.Name));
			}

			if (!TShock.Groups.GroupExists(args.Parameters[1]))
			{
				args.Player.SendErrorMessage("无法找到组 {0}.", args.Parameters[1]);
				return;
			}

			if (args.Parameters.Count > 2)
			{
				int time;
				if (!TShock.Utils.TryParseTime(args.Parameters[2], out time))
				{
                    args.Player.SendErrorMessage("时间格式无效! 正确格式: _d_h_m_s(至少一个, 如5d).");
                    args.Player.SendErrorMessage("有效的格式: 1d \\ 10h-30m+2m, 无效: 2 \\ 5.");
                    return;
				}

				ply[0].tempGroupTimer = new System.Timers.Timer(time * 1000);
				ply[0].tempGroupTimer.Elapsed += ply[0].TempGroupTimerElapsed;
				ply[0].tempGroupTimer.Start();
			}

			Group g = TShock.Utils.GetGroup(args.Parameters[1]);

			ply[0].tempGroup = g;

			if (args.Parameters.Count < 3)
			{
				args.Player.SendSuccessMessage("成功更改玩家 {0} 的用户组至 {1}", ply[0].Name, g.Name);
				ply[0].SendSuccessMessage("你的用户组被临时更改至 {0}", g.Name);
			}
			else
			{
				args.Player.SendSuccessMessage("成功更改玩家 {0} 的用户组至 {1}. ({2}后过期)",
					ply[0].Name, g.Name, args.Parameters[2]);
				ply[0].SendSuccessMessage("你的临时用户组被更改至 {0}. ({1}后过期)",
					g.Name, args.Parameters[2]);
			}
		}

		#endregion Player Management Commands

		#region Server Maintenence Commands

		private static void Broadcast(CommandArgs args)
		{
			string message = string.Join(" ", args.Parameters);

			TShock.Utils.Broadcast(
				"(服务器通知) " + message, 
				Convert.ToByte(TShock.Config.BroadcastRGB[0]), Convert.ToByte(TShock.Config.BroadcastRGB[1]), 
				Convert.ToByte(TShock.Config.BroadcastRGB[2]));
		}

		private static void Off(CommandArgs args)
		{

			if (Main.ServerSideCharacter)
			{
				foreach (TSPlayer player in TShock.Players)
				{
					if (player != null && player.IsLoggedIn && !player.IgnoreActionsForClearingTrashCan)
					{
						player.SaveServerCharacter();
					}
				}
			}

			string reason = ((args.Parameters.Count > 0) ? "关服: " + String.Join(" ", args.Parameters) : "服务器已关闭!");
			TShock.Utils.StopServer(true, reason);
		}
		
		private static void Restart(CommandArgs args)
		{
			if (TShock.NoRestart)
			{
				args.Player.SendErrorMessage("该指令被禁用.");
				return;
			}

			if (ServerApi.RunningMono)
			{
				TShock.Log.ConsoleInfo("该指令无法在Mono环境下使用.");
			}
			else
			{
				string reason = ((args.Parameters.Count > 0) ? "关服: " + String.Join(" ", args.Parameters) : "服务器已关闭!");
				TShock.Utils.RestartServer(true, reason);
			}
		}

		private static void OffNoSave(CommandArgs args)
		{
			string reason = ((args.Parameters.Count > 0) ? "关服: " + String.Join(" ", args.Parameters) : "服务器已关闭!");
			TShock.Utils.StopServer(false, reason);
		}

		private static void CheckUpdates(CommandArgs args)
		{
			args.Player.SendInfoMessage("尝试检查更新...");
			try
			{
				TShock.UpdateManager.UpdateCheck(null);
			}
			catch (Exception)
			{
				//swallow the exception
				return;
			}
		}

		private static void ManageRest(CommandArgs args)
		{
			string subCommand = "help";
			if (args.Parameters.Count > 0)
				subCommand = args.Parameters[0];

			switch(subCommand.ToLower())
			{
				case "listusers":
				{
					int pageNumber;
					if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
						return;

					Dictionary<string,int> restUsersTokens = new Dictionary<string,int>();
					foreach (Rests.SecureRest.TokenData tokenData in TShock.RestApi.Tokens.Values)
					{
						if (restUsersTokens.ContainsKey(tokenData.Username))
							restUsersTokens[tokenData.Username]++;
						else
							restUsersTokens.Add(tokenData.Username, 1);
					}

					List<string> restUsers = new List<string>(
						restUsersTokens.Select(ut => string.Format("{0} ({1} 密钥)", ut.Key, ut.Value)));

					PaginationTools.SendPage(
						args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(restUsers), new PaginationTools.Settings {
							NothingToDisplayString = "当前没有活动的REST用户.",
							HeaderFormat = "活动的REST接口用户 ({0}/{1}):",
							FooterFormat = "键入 {0}rest listusers {{0}} 以获取更多信息.".SFormat(Specifier)
						}
					);

					break;
				}
				case "destroytokens":
				{
					TShock.RestApi.Tokens.Clear();
					args.Player.SendSuccessMessage("清空所有的REST密钥完毕.");
					break;
				}
				default:
				{
					args.Player.SendInfoMessage("可用的REST管理子指令:");
					args.Player.SendMessage("listusers - 查看所有REST用户及密钥.", Color.White);
					args.Player.SendMessage("destroytokens - 清空当前所有REST密钥.", Color.White);
					break;
				}
			}
		}

		#endregion Server Maintenence Commands

        #region Cause Events and Spawn Monsters Commands

		private static void DropMeteor(CommandArgs args)
		{
			WorldGen.spawnMeteor = false;
			WorldGen.dropMeteor();
			if (args.Silent)
			{
				args.Player.SendInfoMessage("召唤陨石落下.");
			}
			else
			{
				TSPlayer.All.SendInfoMessage("{0} 召唤了一颗陨石.", args.Player.Name);
			}
		}

		private static void Fullmoon(CommandArgs args)
		{
			TSPlayer.Server.SetFullMoon();
			if (args.Silent)
			{
				args.Player.SendInfoMessage("开启满月进程.");
			}
			else
			{
				TSPlayer.All.SendInfoMessage("{0} 开启了满月事件.", args.Player.Name);
			}
		}

		private static void Bloodmoon(CommandArgs args)
		{
			TSPlayer.Server.SetBloodMoon(!Main.bloodMoon);
			if (args.Silent)
			{
				args.Player.SendInfoMessage("成功{0}血月事件.", Main.bloodMoon ? "开始" : "停止");
			}
			else
			{
				TSPlayer.All.SendInfoMessage("{0} {1}了血月事件.", args.Player.Name, Main.bloodMoon ? "开始" : "停止");
			}
		}

		private static void Eclipse(CommandArgs args)
		{
			TSPlayer.Server.SetEclipse(!Main.eclipse);
			if (args.Silent)
			{
				args.Player.SendInfoMessage("成功{0}日蚀事件.", Main.eclipse ? "开始" : "停止");
			}
			else
			{
				TSPlayer.All.SendInfoMessage("{0} 了{1}日蚀事件.", args.Player.Name, Main.eclipse ? "开始" : "停止");
			}
		}

		private static void Invade(CommandArgs args)
		{
			if (Main.invasionSize <= 0)
			{
				if (args.Parameters.Count < 1)
				{
					args.Player.SendErrorMessage("语法无效! 正确语法: {0}invade <入侵种类> [波]", Specifier);
					return;
				}

				int wave = 1;
				switch (args.Parameters[0].ToLower())
				{
					case "goblin":
					case "goblins":
						TSPlayer.All.SendInfoMessage("{0} 开始了哥布林入侵.", args.Player.Name);
						TShock.StartInvasion(1);
						break;

					case "snowman":
					case "snowmen":
						TSPlayer.All.SendInfoMessage("{0} 开始了雪人军团入侵.", args.Player.Name);
						TShock.StartInvasion(2);
						break;

					case "pirate":
					case "pirates":
						TSPlayer.All.SendInfoMessage("{0} 开始了海盗入侵.", args.Player.Name);
						TShock.StartInvasion(3);
						break;

					case "pumpkin":
					case "pumpkinmoon":
						if (args.Parameters.Count > 1)
						{
							if (!int.TryParse(args.Parameters[1], out wave) || wave <= 0)
							{
								args.Player.SendErrorMessage("入侵进度无效!");
								break;
							}
						}

						TSPlayer.Server.SetPumpkinMoon(true);
						Main.bloodMoon = false;
						NPC.waveKills = 0f;
						NPC.waveCount = wave;
						TSPlayer.All.SendInfoMessage("{0} 开始了南瓜狂欢夜. 当前回合: {1}!", args.Player.Name, wave);
						break;

					case "frost":
					case "frostmoon":
						if (args.Parameters.Count > 1)
						{
							if (!int.TryParse(args.Parameters[1], out wave) || wave <= 0)
							{
								args.Player.SendErrorMessage("入侵进度无效!");
								return;
							}
						}

						TSPlayer.Server.SetFrostMoon(true);
						Main.bloodMoon = false;
						NPC.waveKills = 0f;
						NPC.waveCount = wave;
						TSPlayer.All.SendInfoMessage("{0} 开始了冰霜之月. 当前回合: {1}!", args.Player.Name, wave);
						break;

					case "martian":
					case "martians":
						TSPlayer.All.SendInfoMessage("{0} 开始了火星入侵.", args.Player.Name);
						TShock.StartInvasion(4);
						break;
				}
			}
			else
			{
				TSPlayer.All.SendInfoMessage("{0} 停止了入侵事件.", args.Player.Name);
				Main.invasionSize = 0;
			}
		}

		private static void ClearAnglerQuests(CommandArgs args)
		{
			if (args.Parameters.Count > 0)
			{
				var result = Main.anglerWhoFinishedToday.RemoveAll(s => s.ToLower().Equals(args.Parameters[0].ToLower()));
				if (result > 0)
				{
					args.Player.SendSuccessMessage("移去了 {0} 名玩家的今日渔夫任务完成情况.", result);
					foreach (TSPlayer ply in TShock.Players.Where(p => p!= null && p.Active && p.TPlayer.name.ToLower().Equals(args.Parameters[0].ToLower())))
					{
						//this will always tell the client that they have not done the quest today.
						ply.SendData((PacketTypes)74, "");
					}
				}
				else
					args.Player.SendErrorMessage("在完成钓鱼任务列表找不到玩家.");

			}
			else
			{
				Main.anglerWhoFinishedToday.Clear();
				NetMessage.SendAnglerQuest();
				args.Player.SendSuccessMessage("清空了所有玩家今日渔夫任务完成情况.");
			}
		}

		private static void ToggleExpert(CommandArgs args)
		{
			Main.expertMode = !Main.expertMode;
			TSPlayer.All.SendData(PacketTypes.WorldInfo);
			args.Player.SendSuccessMessage("专家模式被{0}.", Main.expertMode ? "开启" : "关闭");
		}

		private static void Hardmode(CommandArgs args)
		{
			if (Main.hardMode)
			{
				Main.hardMode = false;
				TSPlayer.All.SendData(PacketTypes.WorldInfo);
				args.Player.SendSuccessMessage("肉后模式关闭.");
			}
			else if (!TShock.Config.DisableHardmode)
			{
				WorldGen.StartHardmode();
				args.Player.SendSuccessMessage("肉后模式开启.");
			}
			else
			{
				args.Player.SendErrorMessage("配置中禁用了肉山后模式.");
			}
		}

		private static void SpawnBoss(CommandArgs args)
		{
			if (args.Parameters.Count < 1 || args.Parameters.Count > 2)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}spawnboss <boss种类> [数量]", Specifier);
				return;
			}

			int amount = 1;
			if (args.Parameters.Count == 2 && (!int.TryParse(args.Parameters[1], out amount) || amount <= 0))
			{
				args.Player.SendErrorMessage("数量无效!");
				return;
			}

			NPC npc = new NPC();
			switch (args.Parameters[0].ToLower())
			{
				case "*":
				case "all":
					int[] npcIds = { 4, 13, 35, 50, 125, 126, 127, 134, 222, 245, 262, 266, 370, 398 };
					TSPlayer.Server.SetTime(false, 0.0);
					foreach (int i in npcIds)
					{
						npc.SetDefaults(i);
						TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					}
					TSPlayer.All.SendSuccessMessage("{0} 生成了 {1} 次全部BOSS.", args.Player.Name, amount);
					return;
				case "brain":
				case "brain of cthulhu":
					npc.SetDefaults(266);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} 生成了 {1} 只 克苏鲁之脑.", args.Player.Name, amount);
					return;
				case "destroyer":
					npc.SetDefaults(134);
					TSPlayer.Server.SetTime(false, 0.0);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} 生成了 {1} 只 机械破坏者.", args.Player.Name, amount);
					return;
				case "duke":
				case "duke fishron":
				case "fishron":
					npc.SetDefaults(370);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} 生成了 {1} 只 猪鲨公爵.", args.Player.Name, amount);
					return;
				case "eater":
				case "eater of worlds":
					npc.SetDefaults(13);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} 生成了 {1} 只 世界吞噬者.", args.Player.Name, amount);
					return;
				case "eye":
				case "eye of cthulhu":
					npc.SetDefaults(4);
					TSPlayer.Server.SetTime(false, 0.0);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} 生成了 {1} 只 克苏鲁之眼.", args.Player.Name, amount);
					return;
				case "golem":
					npc.SetDefaults(245);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} 生成了 {1} 只 石巨人.", args.Player.Name, amount);
					return;
				case "king":
				case "king slime":
					npc.SetDefaults(50);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} 生成了 {1} 只 史莱姆王.", args.Player.Name, amount);
					return;
				case "plantera":
					npc.SetDefaults(262);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} 生成了 {1} 只 世纪之花.", args.Player.Name, amount);
					return;
				case "prime":
				case "skeletron prime":
					npc.SetDefaults(127);
					TSPlayer.Server.SetTime(false, 0.0);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} 生成了 {1} 只 机械骷髅王.", args.Player.Name, amount);
					return;
				case "queen":
				case "queen bee":
					npc.SetDefaults(222);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} 生成了 {1} 只 蜂后.", args.Player.Name, amount);
					return;
				case "skeletron":
					npc.SetDefaults(35);
					TSPlayer.Server.SetTime(false, 0.0);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} 生成了 {1} 只 骷髅王.", args.Player.Name, amount);
					return;
				case "twins":
					TSPlayer.Server.SetTime(false, 0.0);
					npc.SetDefaults(125);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					npc.SetDefaults(126);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} 生成了 {1} 只 双子眼球.", args.Player.Name, amount);
					return;
				case "wof":
				case "wall of flesh":
					if (Main.wof >= 0)
					{
						args.Player.SendErrorMessage("已经存在肉山!");
						return;
					}
					if (args.Player.Y / 16f < Main.maxTilesY - 205)
					{
						args.Player.SendErrorMessage("你必须在地狱内生成肉山!");
						return;
					}
					NPC.SpawnWOF(new Vector2(args.Player.X, args.Player.Y));
					TSPlayer.All.SendSuccessMessage("{0} 生成了 {1} 只 肉山.", args.Player.Name);
					return;
				case "moon":
				case "moon lord":
					npc.SetDefaults(398);
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY);
					TSPlayer.All.SendSuccessMessage("{0} 生成了 {1} 只 月主.", args.Player.Name, amount);
					return;
				default:
					args.Player.SendErrorMessage("BOSS种类无效!");
					return;
			}
		}

		private static void SpawnMob(CommandArgs args)
		{
			if (args.Parameters.Count < 1 || args.Parameters.Count > 2)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}spawnmob <怪物种类> [数量]", Specifier);
				return;
			}
			if (args.Parameters[0].Length == 0)
			{
				args.Player.SendErrorMessage("怪物种类无效!!");
				return;
			}

			int amount = 1;
			if (args.Parameters.Count == 2 && !int.TryParse(args.Parameters[1], out amount))
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}spawnmob <怪物种类> [数量]", Specifier);
				return;
			}

			amount = Math.Min(amount, Main.maxNPCs);

			var npcs = TShock.Utils.GetNPCByIdOrName(args.Parameters[0]);
			if (npcs.Count == 0)
			{
				args.Player.SendErrorMessage("怪物种类无效!!");
			}
			else if (npcs.Count > 1)
			{
				TShock.Utils.SendMultipleMatchError(args.Player, npcs.Select(n => n.name));
			}
			else
			{
				var npc = npcs[0];
				if (npc.type >= 1 && npc.type < Main.maxNPCTypes && npc.type != 113)
				{
					TSPlayer.Server.SpawnNPC(npc.type, npc.name, amount, args.Player.TileX, args.Player.TileY, 50, 20);
					if (args.Silent)
					{
						args.Player.SendSuccessMessage("成功生成 {1} 只 {0}.", npc.name, amount);
					}
					else
					{
						TSPlayer.All.SendSuccessMessage("{0} 生成了 {2} 只 {1}.", args.Player.Name, npc.name, amount);
					}
				}
				else if (npc.type == 113)
				{
					if (Main.wof >= 0 || (args.Player.Y / 16f < (Main.maxTilesY - 205)))
					{
						args.Player.SendErrorMessage("无法召唤肉山!");
						return;
					}
					NPC.SpawnWOF(new Vector2(args.Player.X, args.Player.Y));
					if (args.Silent)
					{
						args.Player.SendSuccessMessage("成功召唤肉山!");
					}
					else
					{
						TSPlayer.All.SendSuccessMessage("{0} 召唤了肉山!", args.Player.Name);
					}
				}
				else
				{
					args.Player.SendErrorMessage("怪物种类无效!!");
				}
			}
		}

		#endregion Cause Events and Spawn Monsters Commands

		#region Teleport Commands

		private static void Home(CommandArgs args)
		{
			args.Player.Spawn();
			args.Player.SendSuccessMessage("传送至你的出生点.");
		}

		private static void Spawn(CommandArgs args)
		{
			if (args.Player.Teleport(Main.spawnTileX*16, (Main.spawnTileY*16) -48))
				args.Player.SendSuccessMessage("传送至地图的出生点.");
		}

		private static void TP(CommandArgs args)
		{
			if (args.Parameters.Count != 1 && args.Parameters.Count != 2)
			{
				if (args.Player.HasPermission(Permissions.tpothers))
					args.Player.SendErrorMessage("语法无效! 正确语法: {0}tp <玩家名> [玩家2]", Specifier);
				else
					args.Player.SendErrorMessage("语法无效! 正确语法: {0}tp <玩家名>", Specifier);
				return;
			}

			if (args.Parameters.Count == 1)
			{
				var players = TShock.Utils.FindPlayer(args.Parameters[0]);
				if (players.Count == 0)
					args.Player.SendErrorMessage("指定玩家无效!");
				else if (players.Count > 1)
					TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
				else
				{
					var target = players[0];
					if (!target.TPAllow && !args.Player.HasPermission(Permissions.tpoverride))
					{
						args.Player.SendErrorMessage("{0} 已经禁止别人传送至其位置.", target.Name);
						return;
					}
					if (args.Player.Teleport(target.TPlayer.position.X, target.TPlayer.position.Y))
					{
						args.Player.SendSuccessMessage("传送至 {0}.", target.Name);
						if (!args.Player.HasPermission(Permissions.tpsilent))
							target.SendInfoMessage("{0} 传送至你所在的位置.", args.Player.Name);
					}
				}
			}
			else
			{
				if (!args.Player.HasPermission(Permissions.tpothers))
				{
					args.Player.SendErrorMessage("缺少执行该指令的权限.");
					return;
				}

				var players1 = TShock.Utils.FindPlayer(args.Parameters[0]);
				var players2 = TShock.Utils.FindPlayer(args.Parameters[1]);

				if (players2.Count == 0)
					args.Player.SendErrorMessage("指定玩家无效!");
				else if (players2.Count > 1)
					TShock.Utils.SendMultipleMatchError(args.Player, players2.Select(p => p.Name));
				else if (players1.Count == 0)
				{
					if (args.Parameters[0] == "*")
					{
						if (!args.Player.HasPermission(Permissions.tpallothers))
						{
							args.Player.SendErrorMessage("缺少执行该指令的权限.");
							return;
						}

						var target = players2[0];
						foreach (var source in TShock.Players.Where(p => p != null && p != args.Player))
						{
							if (!target.TPAllow && !args.Player.HasPermission(Permissions.tpoverride))
								continue;
							if (source.Teleport(target.TPlayer.position.X, target.TPlayer.position.Y))
							{
								if (args.Player != source)
								{
									if (args.Player.HasPermission(Permissions.tpsilent))
										source.SendSuccessMessage("你被传送至 {0}.", target.Name);
									else
										source.SendSuccessMessage("{0} 传送你到 {1} 的位置.", args.Player.Name, target.Name);
								}
								if (args.Player != target)
								{
									if (args.Player.HasPermission(Permissions.tpsilent))
										target.SendInfoMessage("{0} 被传送到你的位置.", source.Name);
									if (!args.Player.HasPermission(Permissions.tpsilent))
										target.SendInfoMessage("{0} 传送了 {1} 至你所在地.", args.Player.Name, source.Name);
								}
							}
						}
						args.Player.SendSuccessMessage("传送所有玩家至 {0}.", target.Name);
					}
					else
						args.Player.SendErrorMessage("指定玩家无效!");
				}
				else if (players1.Count > 1)
					TShock.Utils.SendMultipleMatchError(args.Player, players1.Select(p => p.Name));
				else
				{
					var source = players1[0];
					if (!source.TPAllow && !args.Player.HasPermission(Permissions.tpoverride))
					{
						args.Player.SendErrorMessage("{0} 禁止了玩家传送到其所在位置.", source.Name);
						return;
					}
					var target = players2[0];
					if (!target.TPAllow && !args.Player.HasPermission(Permissions.tpoverride))
					{
						args.Player.SendErrorMessage("{0} 禁止了玩家传送到其所在位置.", target.Name);
						return;
					}
					args.Player.SendSuccessMessage("传送了玩家 {0} 至 {1} 的位置.", source.Name, target.Name);
					if (source.Teleport(target.TPlayer.position.X, target.TPlayer.position.Y))
					{
						if (args.Player != source)
						{
							if (args.Player.HasPermission(Permissions.tpsilent))
								source.SendSuccessMessage("你被传送至 {0} 所在位置.", target.Name);
							else
								source.SendSuccessMessage("{0} 传送你至 {1} 所在的位置.", args.Player.Name, target.Name);
						}
						if (args.Player != target)
						{
							if (args.Player.HasPermission(Permissions.tpsilent))
								target.SendInfoMessage("{0} 被传送到你所在的位置.", source.Name);
							if (!args.Player.HasPermission(Permissions.tpsilent))
								target.SendInfoMessage("{0} 传送了 {1} 至你所在的位置.", args.Player.Name, source.Name);
						}
					}
				}
			}
		}

		private static void TPHere(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				if (args.Player.HasPermission(Permissions.tpallothers))
					args.Player.SendErrorMessage("语法无效! 正确语法: {0}tphere <player|*>", Specifier);
				else
					args.Player.SendErrorMessage("语法无效! 正确语法: {0}tphere <玩家名>", Specifier);
				return;
			}

			string playerName = String.Join(" ", args.Parameters);
			var players = TShock.Utils.FindPlayer(playerName);
			if (players.Count == 0)
			{
				if (playerName == "*")
				{
					if (!args.Player.HasPermission(Permissions.tpallothers))
					{
						args.Player.SendErrorMessage("缺少权限.");
						return;
					}
					for (int i = 0; i < Main.maxPlayers; i++)
					{
						if (Main.player[i].active && (Main.player[i] != args.TPlayer))
						{
							if (TShock.Players[i].Teleport(args.TPlayer.position.X, args.TPlayer.position.Y))
								TShock.Players[i].SendSuccessMessage("你被传送至 {0}.", args.Player.Name);
						}
					}
					args.Player.SendSuccessMessage("传送所有人至你所在地.");
				}
				else
					args.Player.SendErrorMessage("指定玩家无效!");
			}
			else if (players.Count > 1)
				TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
			else
			{
				var plr = players[0];
				if (plr.Teleport(args.TPlayer.position.X, args.TPlayer.position.Y))
				{
					plr.SendInfoMessage("你被传送至 {0}.", args.Player.Name);
					args.Player.SendSuccessMessage("传送了 {0} 至你所在位置.", plr.Name);
				}
			}
		}

		private static void TPNpc(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}tpnpc <NPC>", Specifier);
				return;
			}

			var npcStr = string.Join(" ", args.Parameters);
			var matches = new List<NPC>();
			foreach (var npc in Main.npc.Where(npc => npc.active))
			{
				if (string.Equals(npc.name, npcStr, StringComparison.CurrentCultureIgnoreCase))
				{
					matches = new List<NPC> { npc };
					break;
				}
				if (npc.name.ToLower().StartsWith(npcStr.ToLower()))
					matches.Add(npc);
			}

			if (matches.Count > 1)
			{
				TShock.Utils.SendMultipleMatchError(args.Player, matches.Select(n => n.name));
				return;
			}
			if (matches.Count == 0)
			{
				args.Player.SendErrorMessage("NPC无效!");
				return;
			}

			var target = matches[0];
			args.Player.Teleport(target.position.X, target.position.Y);
			args.Player.SendSuccessMessage("传送至NPC {0} 的位置.", target.name);
		}

		private static void GetPos(CommandArgs args)
		{
			var player = args.Player.Name;
			if (args.Parameters.Count > 0)
			{
				player = String.Join(" ", args.Parameters);
			}

			var players = TShock.Utils.FindPlayer(player);
			if (players.Count == 0)
			{
				args.Player.SendErrorMessage("指定玩家无效!");
			}
			else if (players.Count > 1)
			{
				TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
			}
			else
			{
				args.Player.SendSuccessMessage("玩家 {0} 的坐标为 ({1}, {2}).", players[0].Name, players[0].TileX, players[0].TileY);
			}
		}

		private static void TPPos(CommandArgs args)
		{
			if (args.Parameters.Count != 2)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}tppos <物块坐标x> <物块坐标y>", Specifier);
				return;
			}

			int x, y;
			if (!int.TryParse(args.Parameters[0], out x) || !int.TryParse(args.Parameters[1], out y))
			{
				args.Player.SendErrorMessage("坐标无效!");
				return;
			}
			x = Math.Max(0, x);
			y = Math.Max(0, y);
			x = Math.Min(x, Main.maxTilesX - 1);
			y = Math.Min(y, Main.maxTilesY - 1);

			args.Player.Teleport(16 * x, 16 * y);
			args.Player.SendSuccessMessage("传送至 {0}, {1}!", x, y);
		}

		private static void TPAllow(CommandArgs args)
		{
			if (!args.Player.TPAllow)
				args.Player.SendSuccessMessage("关闭传送保护.");
			if (args.Player.TPAllow)
                args.Player.SendSuccessMessage("开启传送保护.");
			args.Player.TPAllow = !args.Player.TPAllow;
		}

		private static void Warp(CommandArgs args)
		{
		    bool hasManageWarpPermission = args.Player.HasPermission(Permissions.managewarp);
            if (args.Parameters.Count < 1)
            {
                if (hasManageWarpPermission)
                {
                    args.Player.SendInfoMessage("语法无效! 正确语法: {0}warp [子指令] [参数]", Specifier);
                    args.Player.SendInfoMessage("子指令列表: add, del, hide, list, send, [跳跃点名]");
                    args.Player.SendInfoMessage("子指令用法: add [跳跃点名], del [跳跃点名], list [页码]");
                    args.Player.SendInfoMessage("         : send [玩家名] [跳跃点名], hide [跳跃点名] [true/false]");
                    args.Player.SendInfoMessage("子指令示范: {0}warp add foobar, {0}warp hide foobar true, {0}warp foobar", Specifier);
                    return;
                }
                else
                {
                    args.Player.SendErrorMessage("语法无效! 正确语法: {0}warp [跳跃点名] 或 {0}warp list [页码]", Specifier);
                    return;
                }
            }

			if (args.Parameters[0].Equals("list"))
            {
                #region List warps
				int pageNumber;
				if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
					return;
				IEnumerable<string> warpNames = from warp in TShock.Warps.Warps
												where !warp.IsPrivate
												select warp.Name;
				PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(warpNames),
					new PaginationTools.Settings
					{
						HeaderFormat = "跳跃点 ({0}/{1}):",
						FooterFormat = "键入 {0}warp list {{0}} 以获取下一页跳跃点.".SFormat(Specifier),
						NothingToDisplayString = "当前没有可用跳跃点."
					});
                #endregion
            }
            else if (args.Parameters[0].ToLower() == "add" && hasManageWarpPermission)
            {
                #region Add warp
                if (args.Parameters.Count == 2)
                {
                    string warpName = args.Parameters[1];
                    if (warpName == "list" || warpName == "hide" || warpName == "del" || warpName == "add")
                    {
                        args.Player.SendErrorMessage("不可以使用该特殊名称定义.");
                    }
                    else if (TShock.Warps.Add(args.Player.TileX, args.Player.TileY, warpName))
                    {
                        args.Player.SendSuccessMessage("跳跃点已添加: " + warpName);
                    }
                    else
                    {
                        args.Player.SendErrorMessage("跳跃点 " + warpName + " 已经存在.");
                    }
                }
                else
                    args.Player.SendErrorMessage("语法无效! 正确语法: {0}warp add [跳跃点]", Specifier);
                #endregion
            }
            else if (args.Parameters[0].ToLower() == "del" && hasManageWarpPermission)
            {
                #region Del warp
                if (args.Parameters.Count == 2)
                {
                    string warpName = args.Parameters[1];
					if (TShock.Warps.Remove(warpName))
					{
						args.Player.SendSuccessMessage("删除跳跃点: " + warpName);
					}
					else
						args.Player.SendErrorMessage("未能找到指定的跳跃点.");
                }
                else
                    args.Player.SendErrorMessage("语法无效! 正确语法: {0}warp del [跳跃点]", Specifier);
                #endregion
            }
            else if (args.Parameters[0].ToLower() == "hide" && hasManageWarpPermission)
            {
                #region Hide warp
                if (args.Parameters.Count == 3)
                {
                    string warpName = args.Parameters[1];
                    bool state = false;
                    if (Boolean.TryParse(args.Parameters[2], out state))
                    {
                        if (TShock.Warps.Hide(args.Parameters[1], state))
                        {
                            if (state)
                                args.Player.SendSuccessMessage("开始隐藏跳跃点 " + warpName);
                            else
                                args.Player.SendSuccessMessage("停止隐藏跳跃点 " + warpName);
                        }
                        else
                            args.Player.SendErrorMessage("未能找到指定的跳跃点.");
                    }
                    else
                        args.Player.SendErrorMessage("语法无效! 正确语法: {0}warp hide [跳跃点] <true/false>", Specifier);
                }
                else
                    args.Player.SendErrorMessage("语法无效! 正确语法: {0}warp hide [跳跃点] <true/false>", Specifier);
                #endregion
            }
            else if (args.Parameters[0].ToLower() == "send" && args.Player.HasPermission(Permissions.tpothers))
            {
                #region Warp send
                if (args.Parameters.Count < 3)
                {
                    args.Player.SendErrorMessage("语法无效! 正确语法: {0}warp send [玩家名] [传送点]", Specifier);
                    return;
                }

                var foundplr = TShock.Utils.FindPlayer(args.Parameters[1]);
                if (foundplr.Count == 0)
                {
                    args.Player.SendErrorMessage("指定玩家无效!");
                    return;
                }
                else if (foundplr.Count > 1)
                {
					TShock.Utils.SendMultipleMatchError(args.Player, foundplr.Select(p => p.Name));
                    return;
                }

                string warpName = args.Parameters[2];
                var warp = TShock.Warps.Find(warpName);
                var plr = foundplr[0];
				if (warp.Position != Point.Zero)
				{
					if (plr.Teleport(warp.Position.X * 16, warp.Position.Y * 16))
					{
						plr.SendSuccessMessage("{0} 传送你至跳跃点 {1}.", args.Player.Name, warpName);
						args.Player.SendSuccessMessage("成功将玩家 {0} 传送至 {1}.", plr.Name, warpName);
					}
				}
				else
				{
					args.Player.SendErrorMessage("未能找到指定的跳跃点.");
				}
                #endregion
            }
            else
            {
                string warpName = String.Join(" ", args.Parameters);
                var warp = TShock.Warps.Find(warpName);
                if (warp != null)
                {
					if (args.Player.Teleport(warp.Position.X * 16, warp.Position.Y * 16))
                        args.Player.SendSuccessMessage("到达跳跃点 " + warpName + ".");
                }
                else
                {
                    args.Player.SendErrorMessage("指定跳跃点未找到.");
                }
            }
		}

		#endregion Teleport Commands

		#region Group Management

		private static void Group(CommandArgs args)
		{
			string subCmd = args.Parameters.Count == 0 ? "help" : args.Parameters[0].ToLower();

			switch (subCmd)
			{
				case "add":
					#region Add group
					{
						if (args.Parameters.Count < 2)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}group add <组名> [权限]", Specifier);
							return;
						}

						string groupName = args.Parameters[1];
						args.Parameters.RemoveRange(0, 2);
						string permissions = String.Join(",", args.Parameters);

						try
						{
							TShock.Groups.AddGroup(groupName, null, permissions, TShockAPI.Group.defaultChatColor);
							args.Player.SendSuccessMessage("分组添加成功!");
						}
						catch (GroupExistsException)
						{
							args.Player.SendErrorMessage("该分组已存在!");
						}
						catch (GroupManagerException ex)
						{
							args.Player.SendErrorMessage(ex.ToString());
						}
					}
					#endregion
					return;
				case "addperm":
					#region Add permissions
					{
						if (args.Parameters.Count < 3)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}group addperm <组名> <权限...>", Specifier);
							return;
						}

						string groupName = args.Parameters[1];
						args.Parameters.RemoveRange(0, 2);
						if (groupName == "*")
						{
							foreach (Group g in TShock.Groups)
							{
								TShock.Groups.AddPermissions(g.Name, args.Parameters);
							}
							args.Player.SendSuccessMessage("修改全部组权限完毕.");
							return;
						}
						try
						{
							string response = TShock.Groups.AddPermissions(groupName, args.Parameters);
							if (response.Length > 0)
							{
								args.Player.SendSuccessMessage(response);
							}
							return;
						}
						catch (GroupManagerException ex)
						{
							args.Player.SendErrorMessage(ex.ToString());
						}
					}
					#endregion
					return;
				case "help":
					#region Help
					{
						int pageNumber;
						if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
							return;

						var lines = new List<string>
						{
							"add <名称> <权限...> - 添加新用户组.",
							"addperm <组名> <权限...> - 给指定组添加权限.",
							"color <组名> <rrr,ggg,bbb> - 改变分组的对话颜色.",
							"del <组名> - 删除分组.",
							"delperm <组名> <权限...> - 移除指定组的权限.",
							"list [页码] - 显示当前组列表.",
							"listperm <组名> [页码] - 显示指定组的所有权限.",
							"parent <组名> <父组> - 改变指定组的父组.",
							"prefix <组名> <前缀> - 改变指定组的前缀.",
                            "suffix <组名> <后缀> - 改变指定组的后缀."
                        };

						PaginationTools.SendPage(args.Player, pageNumber, lines,
							new PaginationTools.Settings
							{
								HeaderFormat = "分组管理子指令 ({0}/{1}):",
								FooterFormat = "键入 {0}group help {{0}} 以获取下一页指令.".SFormat(Specifier)
							}
						);
					}
					#endregion
					return;
				case "parent":
					#region Parent
					{
						if (args.Parameters.Count < 2)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}group parent <组名> [新父组名]", Specifier);
							return;
						}

						string groupName = args.Parameters[1];
						Group group = TShock.Groups.GetGroupByName(groupName);
						if (group == null)
						{
							args.Player.SendErrorMessage("组 {0} 不存在.", groupName);
							return;
						}

						if (args.Parameters.Count > 2)
						{
							string newParentGroupName = string.Join(" ", args.Parameters.Skip(2));
							if (!string.IsNullOrWhiteSpace(newParentGroupName) && !TShock.Groups.GroupExists(newParentGroupName))
							{
								args.Player.SendErrorMessage("组 {0} 不存在.", newParentGroupName);
								return;
							}

							try
							{
								TShock.Groups.UpdateGroup(groupName, newParentGroupName, group.Permissions, group.ChatColor, group.Suffix, group.Prefix);

								if (!string.IsNullOrWhiteSpace(newParentGroupName))
									args.Player.SendSuccessMessage("设定组 {0} 的父组为组 {1} .", groupName, newParentGroupName);
								else
									args.Player.SendSuccessMessage("去除组 {0} 的父组.", groupName);
							}
							catch (GroupManagerException ex)
							{
								args.Player.SendErrorMessage(ex.Message);
							}
						}
						else
						{
							if (group.Parent != null)
								args.Player.SendSuccessMessage("组 {0} 的父组是组 {1} .", group.Name, group.Parent.Name);
							else
								args.Player.SendSuccessMessage("组 {0} 无父组.", group.Name);
						}
					}
					#endregion
					return;
				case "suffix":
					#region Suffix
					{
						if (args.Parameters.Count < 2)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}group suffix <组名> [new suffix]", Specifier);
							return;
						}

						string groupName = args.Parameters[1];
						Group group = TShock.Groups.GetGroupByName(groupName);
						if (group == null)
						{
							args.Player.SendErrorMessage("组 {0} 不存在.", groupName);
							return;
						}

						if (args.Parameters.Count > 2)
						{
							string newSuffix = string.Join(" ", args.Parameters.Skip(2));

							try
							{
								TShock.Groups.UpdateGroup(groupName, group.ParentName, group.Permissions, group.ChatColor, newSuffix, group.Prefix);

								if (!string.IsNullOrWhiteSpace(newSuffix))
									args.Player.SendSuccessMessage("设定组 {0} 的后缀为 {1} .", groupName, newSuffix);
								else
									args.Player.SendSuccessMessage("移除组 {0} 的后缀.", groupName);
							}
							catch (GroupManagerException ex)
							{
								args.Player.SendErrorMessage(ex.Message);
							}
						}
						else
						{
							if (!string.IsNullOrWhiteSpace(group.Suffix))
								args.Player.SendSuccessMessage("组 {0} 的后缀是 {1} .", group.Name, group.Suffix);
							else
								args.Player.SendSuccessMessage("组 {0} 无后缀.", group.Name);
						}
					}
					#endregion
					return;
				case "prefix":
					#region Prefix
					{
						if (args.Parameters.Count < 2)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}group prefix <组名> [新前缀]", Specifier);
							return;
						}

						string groupName = args.Parameters[1];
						Group group = TShock.Groups.GetGroupByName(groupName);
						if (group == null)
						{
							args.Player.SendErrorMessage("组 {0} 不存在.", groupName);
							return;
						}

						if (args.Parameters.Count > 2)
						{
							string newPrefix = string.Join(" ", args.Parameters.Skip(2));

							try
							{
								TShock.Groups.UpdateGroup(groupName, group.ParentName, group.Permissions, group.ChatColor, group.Suffix, newPrefix);

								if (!string.IsNullOrWhiteSpace(newPrefix))
									args.Player.SendSuccessMessage("设定组 {0} 的前缀为 {1} .", groupName, newPrefix);
								else
									args.Player.SendSuccessMessage("去除组 {0} 的前缀.", groupName);
							}
							catch (GroupManagerException ex)
							{
								args.Player.SendErrorMessage(ex.Message);
							}
						}
						else
						{
							if (!string.IsNullOrWhiteSpace(group.Prefix))
								args.Player.SendSuccessMessage("组 {0} 的前缀是 {1} .", group.Name, group.Prefix);
							else
								args.Player.SendSuccessMessage("组 {0} 无前缀.", group.Name);
						}
					}
					#endregion
					return;
				case "color":
					#region Color
					{
						if (args.Parameters.Count < 2 || args.Parameters.Count > 3)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}group color <组名> [新颜色(000,000,000)]", Specifier);
							return;
						}

						string groupName = args.Parameters[1];
						Group group = TShock.Groups.GetGroupByName(groupName);
						if (group == null)
						{
							args.Player.SendErrorMessage("组 {0} 不存在.", groupName);
							return;
						}

						if (args.Parameters.Count == 3)
						{
							string newColor = args.Parameters[2];

							String[] parts = newColor.Split(',');
							byte r;
							byte g;
							byte b;
							if (parts.Length == 3 && byte.TryParse(parts[0], out r) && byte.TryParse(parts[1], out g) && byte.TryParse(parts[2], out b))
							{
								try
								{
									TShock.Groups.UpdateGroup(groupName, group.ParentName, group.Permissions, newColor, group.Suffix, group.Prefix);

									args.Player.SendSuccessMessage("设定组 {0} 的对话颜色为 {1} .", groupName, newColor);
								}
								catch (GroupManagerException ex)
								{
									args.Player.SendErrorMessage(ex.Message);
								}
							}
							else
							{
								args.Player.SendErrorMessage("颜色格式无效. 正确格式: \"rrr,ggg,bbb\"");
							}
						}
						else
						{
							args.Player.SendSuccessMessage("组 {0} 的对话颜色为 {1} .", group.Name, group.ChatColor);
						}
					}
					#endregion
					return;
				case "del":
					#region Delete group
					{
						if (args.Parameters.Count != 2)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}group del <组名>", Specifier);
							return;
						}

						try
						{
							string response = TShock.Groups.DeleteGroup(args.Parameters[1]);
							if (response.Length > 0)
							{
								args.Player.SendSuccessMessage(response);
							}
						}
						catch (GroupManagerException ex)
						{
							args.Player.SendErrorMessage(ex.ToString());
						}
					}
					#endregion
					return;
				case "delperm":
					#region Delete permissions
					{
						if (args.Parameters.Count < 3)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}group delperm <组名> <权限...>", Specifier);
							return;
						}

						string groupName = args.Parameters[1];
						args.Parameters.RemoveRange(0, 2);
						if (groupName == "*")
						{
							foreach (Group g in TShock.Groups)
							{
								TShock.Groups.DeletePermissions(g.Name, args.Parameters);
							}
							args.Player.SendSuccessMessage("修改全部组完毕.");
							return;
						}
						try
						{
							string response = TShock.Groups.DeletePermissions(groupName, args.Parameters);
							if (response.Length > 0)
							{
								args.Player.SendSuccessMessage(response);
							}
							return;
						}
						catch (GroupManagerException ex)
						{
							args.Player.SendErrorMessage(ex.ToString());
						}
					}
					#endregion
					return;
				case "list":
					#region List groups
					{
						int pageNumber;
						if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
							return;
						var groupNames = from grp in TShock.Groups.groups
										 select grp.Name;
						PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(groupNames),
							new PaginationTools.Settings
							{
								HeaderFormat = "分组列表 ({0}/{1}):",
								FooterFormat = "键入 {0}group list {{0}} 以获取下一页信息.".SFormat(Specifier)
							});
					}
					#endregion
					return;
				case "listperm":
					#region List permissions
					{
						if (args.Parameters.Count == 1)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}group listperm <组名> [页码]", Specifier);
							return;
						}
						int pageNumber;
						if (!PaginationTools.TryParsePageNumber(args.Parameters, 2, args.Player, out pageNumber))
							return;

						if (!TShock.Groups.GroupExists(args.Parameters[1]))
						{
							args.Player.SendErrorMessage("组名无效!");
							return;
						}
						Group grp = TShock.Utils.GetGroup(args.Parameters[1]);
						List<string> permissions = grp.TotalPermissions;

						PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(permissions),
							new PaginationTools.Settings
							{
								HeaderFormat = "组 " + grp.Name + " 的所有权限 ({0}/{1}):",
								FooterFormat = "键入 {0}group listperm {1} {{0}} 以获取更多信息.".SFormat(Specifier, grp.Name),
								NothingToDisplayString = "组 " + grp.Name + " 当前没有权限."
							});
					}
					#endregion
					return;
			}
		}
		#endregion Group Management

		#region Item Management

		private static void ItemBan(CommandArgs args)
		{
			string subCmd = args.Parameters.Count == 0 ? "help" : args.Parameters[0].ToLower();
			switch (subCmd)
			{
				case "add":
					#region Add item
					{
						if (args.Parameters.Count != 2)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}itemban add <物品>", Specifier);
							return;
						}

						List<Item> items = TShock.Utils.GetItemByIdOrName(args.Parameters[1]);
						if (items.Count == 0)
						{
							args.Player.SendErrorMessage("物品无效!");
						}
						else if (items.Count > 1)
						{
							TShock.Utils.SendMultipleMatchError(args.Player, items.Select(i => i.name));
						}
						else
						{
							TShock.Itembans.AddNewBan(items[0].name);
							args.Player.SendSuccessMessage("成功禁止物品 " + items[0].name + " 的使用.");
						}
					}
					#endregion
					return;
				case "allow":
					#region Allow group to item
					{
						if (args.Parameters.Count != 3)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}itemban allow <物品> <组名>", Specifier);
							return;
						}

						List<Item> items = TShock.Utils.GetItemByIdOrName(args.Parameters[1]);
						if (items.Count == 0)
						{
							args.Player.SendErrorMessage("物品无效!");
						}
						else if (items.Count > 1)
						{
							TShock.Utils.SendMultipleMatchError(args.Player, items.Select(i => i.name));
						}
						else
						{
							if (!TShock.Groups.GroupExists(args.Parameters[2]))
							{
								args.Player.SendErrorMessage("组名无效!");
								return;
							}

							ItemBan ban = TShock.Itembans.GetItemBanByName(items[0].name);
							if (ban == null)
							{
								args.Player.SendErrorMessage("{0} 没有被禁止.", items[0].name);
								return;
							}
							if (!ban.AllowedGroups.Contains(args.Parameters[2]))
							{
								TShock.Itembans.AllowGroup(items[0].name, args.Parameters[2]);
								args.Player.SendSuccessMessage("禁止 {0} 使用物品 {1} 完毕.", args.Parameters[2], items[0].name);
							}
							else
							{
								args.Player.SendWarningMessage("{0} 已经拥有使用物品 {1} 的权限.", args.Parameters[2], items[0].name);
							}
						}
					}
					#endregion
					return;
				case "del":
					#region Delete item
					{
						if (args.Parameters.Count != 2)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}itemban del <物品>", Specifier);
							return;
						}

						List<Item> items = TShock.Utils.GetItemByIdOrName(args.Parameters[1]);
						if (items.Count == 0)
						{
							args.Player.SendErrorMessage("物品无效!");
						}
						else if (items.Count > 1)
						{
							TShock.Utils.SendMultipleMatchError(args.Player, items.Select(i => i.name));
						}
						else
						{
							TShock.Itembans.RemoveBan(items[0].name);
							args.Player.SendSuccessMessage("解除禁用物品 " + items[0].name + " 完毕.");
						}
					}
					#endregion
					return;
				case "disallow":
					#region Disllow group from item
					{
						if (args.Parameters.Count != 3)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}itemban disallow <物品> <组名>", Specifier);
							return;
						}

						List<Item> items = TShock.Utils.GetItemByIdOrName(args.Parameters[1]);
						if (items.Count == 0)
						{
							args.Player.SendErrorMessage("物品无效!");
						}
						else if (items.Count > 1)
						{
							TShock.Utils.SendMultipleMatchError(args.Player, items.Select(i => i.name));
						}
						else
						{
							if (!TShock.Groups.GroupExists(args.Parameters[2]))
							{
								args.Player.SendErrorMessage("组名无效!");
								return;
							}

							ItemBan ban = TShock.Itembans.GetItemBanByName(items[0].name);
							if (ban == null)
							{
								args.Player.SendErrorMessage("{0} 没有被禁止.", items[0].name);
								return;
							}
							if (ban.AllowedGroups.Contains(args.Parameters[2]))
							{
								TShock.Itembans.RemoveGroup(items[0].name, args.Parameters[2]);
								args.Player.SendSuccessMessage("禁止组 {0} 使用物品 {1} 完毕.", args.Parameters[2], items[0].name);
							}
							else
							{
								args.Player.SendWarningMessage("{0} 没有使用物品 {1} 的权限.", args.Parameters[2], items[0].name);
							}
						}
					}
					#endregion
					return;
				case "help":
					#region Help
					{
						int pageNumber;
						if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
							return;

						var lines = new List<string>
						{
							"add <物品名称/ID> - 禁止玩家使用特定物品",
                            "allow <物品名称/ID> <组名> - 允许特定组使用禁用物品.",
                            "del <物品名称/ID> - 删除禁用物品设定.",
                            "disallow <物品名称/ID> <组名> - 禁止特定组使用禁用物品",
							"list [页码] - 显示所有禁用物设定."
                        };

						PaginationTools.SendPage(args.Player, pageNumber, lines,
							new PaginationTools.Settings
							{
								HeaderFormat = "禁用物管理子指令 ({0}/{1}):",
								FooterFormat = "键入 {0}itemban help {{0}} 以获取下一页帮助.".SFormat(Specifier)
							}
						);
					}
					#endregion
					return;
				case "list":
					#region List items
					{
						int pageNumber;
						if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
							return;
						IEnumerable<string> itemNames = from itemBan in TShock.Itembans.ItemBans
														select itemBan.Name;
						PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(itemNames),
							new PaginationTools.Settings
							{
								HeaderFormat = "禁用物品列表 ({0}/{1}):",
								FooterFormat = "键入 {0}itemban list {{0}} 以查看下一页.".SFormat(Specifier),
								NothingToDisplayString = "当前没有被禁用的物品."
							});
					}
					#endregion
					return;
			}
		}
		#endregion Item Management

		#region Projectile Management

		private static void ProjectileBan(CommandArgs args)
		{
			string subCmd = args.Parameters.Count == 0 ? "help" : args.Parameters[0].ToLower();
			switch (subCmd)
			{
				case "add":
					#region Add projectile
					{
						if (args.Parameters.Count != 2)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}projban add <proj id>", Specifier);
							return;
						}
						short id;
						if (Int16.TryParse(args.Parameters[1], out id) && id > 0 && id < Main.maxProjectileTypes)
						{
							TShock.ProjectileBans.AddNewBan(id);
							args.Player.SendSuccessMessage("成功封禁抛射体 {0}.", id);
						}
						else
							args.Player.SendErrorMessage("抛射体ID无效!");
					}
					#endregion
					return;
				case "allow":
					#region Allow group to projectile
					{
						if (args.Parameters.Count != 3)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}projban allow <id> <组名>", Specifier);
							return;
						}

						short id;
						if (Int16.TryParse(args.Parameters[1], out id) && id > 0 && id < Main.maxProjectileTypes)
						{
							if (!TShock.Groups.GroupExists(args.Parameters[2]))
							{
								args.Player.SendErrorMessage("组名无效!");
								return;
							}

							ProjectileBan ban = TShock.ProjectileBans.GetBanById(id);
							if (ban == null)
							{
								args.Player.SendErrorMessage("抛射体 {0} 没有被禁止.", id);
								return;
							}
							if (!ban.AllowedGroups.Contains(args.Parameters[2]))
							{
								TShock.ProjectileBans.AllowGroup(id, args.Parameters[2]);
								args.Player.SendSuccessMessage("组 {0} 可以使用抛射体 {1} 了.", args.Parameters[2], id);
							}
							else
								args.Player.SendWarningMessage("组 {0} 之前就可以使用抛射体 {1}.", args.Parameters[2], id);
						}
						else
							args.Player.SendErrorMessage("抛射体ID无效!");
					}
					#endregion
					return;
				case "del":
					#region Delete projectile
					{
						if (args.Parameters.Count != 2)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}projban del <id>", Specifier);
							return;
						}

						short id;
						if (Int16.TryParse(args.Parameters[1], out id) && id > 0 && id < Main.maxProjectileTypes)
						{
							TShock.ProjectileBans.RemoveBan(id);
							args.Player.SendSuccessMessage("解除封禁抛射体 {0}.", id);
							return;
						}
						else
							args.Player.SendErrorMessage("抛射体ID无效!");
					}
					#endregion
					return;
				case "disallow":
					#region Disallow group from projectile
					{
						if (args.Parameters.Count != 3)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}projban disallow <id> <组名>", Specifier);
							return;
						}

						short id;
						if (Int16.TryParse(args.Parameters[1], out id) && id > 0 && id < Main.maxProjectileTypes)
						{
							if (!TShock.Groups.GroupExists(args.Parameters[2]))
							{
								args.Player.SendErrorMessage("组名无效!");
								return;
							}

							ProjectileBan ban = TShock.ProjectileBans.GetBanById(id);
							if (ban == null)
							{
								args.Player.SendErrorMessage("抛射体 {0} 没有被禁止.", id);
								return;
							}
							if (ban.AllowedGroups.Contains(args.Parameters[2]))
							{
								TShock.ProjectileBans.RemoveGroup(id, args.Parameters[2]);
								args.Player.SendSuccessMessage("组 {0} 不能使用抛射体 {1} 了.", args.Parameters[2], id);
								return;
							}
							else
								args.Player.SendWarningMessage("组 {0} 之前就不能使用抛射体 {1}.", args.Parameters[2], id);
						}
						else
							args.Player.SendErrorMessage("抛射体ID无效!");
					}
					#endregion
					return;
				case "help":
					#region Help
					{
						int pageNumber;
						if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
							return;

						var lines = new List<string>
						{
							"add <抛射体ID> - 封禁ID.",
                            "allow <抛射体ID> <组名> - 允许某组使用特定的抛射体.",
                            "del <抛射体ID> - 解除对特定抛射体的禁止.",
                            "disallow <抛射体ID> <组名> - 停止允许某组使用特定抛射体.",
							"list [页码] - 列出所有被封禁的抛射体."
                        };

						PaginationTools.SendPage(args.Player, pageNumber, lines,
							new PaginationTools.Settings
							{
								HeaderFormat = "抛射体管理子指令 ({0}/{1}):",
								FooterFormat = "键入 {0}projban help {{0}} 以查看更多信息.".SFormat(Specifier)
							}
						);
					}
					#endregion
					return;
				case "list":
					#region List projectiles
					{
						int pageNumber;
						if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
							return;
						IEnumerable<Int16> projectileIds = from projectileBan in TShock.ProjectileBans.ProjectileBans
														   select projectileBan.ID;
						PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(projectileIds),
							new PaginationTools.Settings
							{
								HeaderFormat = "被禁止的抛射体 ({0}/{1}):",
								FooterFormat = "键入 {0}projban list {{0}} 以获取更多信息.".SFormat(Specifier),
								NothingToDisplayString = "当前没有被封禁的抛射体."
							});
					}
					#endregion
					return;
			}
		}
		#endregion Projectile Management

		#region Tile Management
		private static void TileBan(CommandArgs args)
		{
			string subCmd = args.Parameters.Count == 0 ? "help" : args.Parameters[0].ToLower();
			switch (subCmd)
			{
				case "add":
					#region Add tile
					{
						if (args.Parameters.Count != 2)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}tileban add <物块ID>", Specifier);
							return;
						}
						short id;
						if (Int16.TryParse(args.Parameters[1], out id) && id >= 0 && id < Main.maxTileSets)
						{
							TShock.TileBans.AddNewBan(id);
							args.Player.SendSuccessMessage("禁止了物块 {0}.", id);
						}
						else
							args.Player.SendErrorMessage("物块ID无效!");
					}
					#endregion
					return;
				case "allow":
					#region Allow group to place tile
					{
						if (args.Parameters.Count != 3)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}tileban allow <物块ID> <组>", Specifier);
							return;
						}

						short id;
						if (Int16.TryParse(args.Parameters[1], out id) && id >= 0 && id < Main.maxTileSets)
						{
							if (!TShock.Groups.GroupExists(args.Parameters[2]))
							{
								args.Player.SendErrorMessage("组名无效!");
								return;
							}

							TileBan ban = TShock.TileBans.GetBanById(id);
							if (ban == null)
							{
								args.Player.SendErrorMessage("物块 {0} 未被禁用.", id);
								return;
							}
							if (!ban.AllowedGroups.Contains(args.Parameters[2]))
							{
								TShock.TileBans.AllowGroup(id, args.Parameters[2]);
								args.Player.SendSuccessMessage("允许 {0} 组放置物块 {1}.", args.Parameters[2], id);
							}
							else
								args.Player.SendWarningMessage("{0} 已有放置物块 {1} 的权限.", args.Parameters[2], id);
						}
						else
							args.Player.SendErrorMessage("物块ID无效!");
					}
					#endregion
					return;
				case "del":
					#region Delete tile ban
					{
						if (args.Parameters.Count != 2)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}tileban del <id>", Specifier);
							return;
						}

						short id;
						if (Int16.TryParse(args.Parameters[1], out id) && id >= 0 && id < Main.maxTileSets)
						{
							TShock.TileBans.RemoveBan(id);
							args.Player.SendSuccessMessage("解除禁用物块 {0}.", id);
							return;
						}
						else
							args.Player.SendErrorMessage("物块ID无效!");
					}
					#endregion
					return;
				case "disallow":
					#region Disallow group from placing tile
					{
						if (args.Parameters.Count != 3)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}tileban disallow <id> <组名>", Specifier);
							return;
						}

						short id;
						if (Int16.TryParse(args.Parameters[1], out id) && id >= 0 && id < Main.maxTileSets)
						{
							if (!TShock.Groups.GroupExists(args.Parameters[2]))
							{
								args.Player.SendErrorMessage("组名无效!");
								return;
							}

							TileBan ban = TShock.TileBans.GetBanById(id);
							if (ban == null)
							{
								args.Player.SendErrorMessage("物块 {0} 未被禁用..", id);
								return;
							}
							if (ban.AllowedGroups.Contains(args.Parameters[2]))
							{
								TShock.TileBans.RemoveGroup(id, args.Parameters[2]);
								args.Player.SendSuccessMessage("禁止 {0} 组放置物块 {1}.", args.Parameters[2], id);
								return;
							}
							else
								args.Player.SendWarningMessage("{0} 组没有放置物块 {1} 的权限.", args.Parameters[2], id);
						}
						else
							args.Player.SendErrorMessage("物块ID无效!");
					}
					#endregion
					return;
				case "help":
					#region Help
					{
						int pageNumber;
						if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
							return;

						var lines = new List<string>
						{
                            "add <物块ID> - 禁止放置某物块.",
							"allow <物块ID> <组> - 允许特定分组放置特定物块.",
							"del <物块ID> - 停止禁用某物块.",
							"disallow <物块ID> <组> - 不再允许某分组使用特定物块.",
							"list [页] - 显示被禁用的物块."
                        };

						PaginationTools.SendPage(args.Player, pageNumber, lines,
							new PaginationTools.Settings
							{
								HeaderFormat = "管理禁用物块子指令 ({0}/{1}):",
								FooterFormat = "键入 {0}tileban help {{0}} 以获取下一页子指令.".SFormat(Specifier)
							}
						);
					}
					#endregion
					return;
				case "list":
					#region List tile bans
					{
						int pageNumber;
						if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
							return;
						IEnumerable<Int16> tileIds = from tileBan in TShock.TileBans.TileBans
														   select tileBan.ID;
						PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(tileIds),
							new PaginationTools.Settings
							{
								HeaderFormat = "Tile bans ({0}/{1}):",
								FooterFormat = "键入 {0}tileban list {{0}} 以获取更多信息.".SFormat(Specifier),
								NothingToDisplayString = "There are currently no banned tiles."
							});
					}
					#endregion
					return;
			}
		}
		#endregion Tile Management

		#region Server Config Commands

		private static void SetSpawn(CommandArgs args)
		{
			Main.spawnTileX = args.Player.TileX + 1;
			Main.spawnTileY = args.Player.TileY + 3;
			SaveManager.Instance.SaveWorld(false);
			args.Player.SendSuccessMessage("出生点已被设置到你当前的位置.");
		}

		private static void SetDungeon(CommandArgs args)
		{
			Main.dungeonX = args.Player.TileX + 1;
			Main.dungeonY = args.Player.TileY + 3;
			SaveManager.Instance.SaveWorld(false);
			args.Player.SendSuccessMessage("地牢所在地已被设置到你当前的位置.");
		}

		private static void Reload(CommandArgs args)
		{
			TShock.Utils.Reload(args.Player);

			args.Player.SendSuccessMessage(
				"配置, 权限, 区域设定加载完毕. 有些变化可能需要重启服务器.");
		}

		private static void ServerPassword(CommandArgs args)
		{
			if (args.Parameters.Count != 1)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}serverpassword \"<新密码>\"", Specifier);
				return;
			}
			string passwd = args.Parameters[0];
			TShock.Config.ServerPassword = passwd;
			args.Player.SendSuccessMessage(string.Format("服务器密码被设定为: {0}.", passwd));
		}

		private static void Save(CommandArgs args)
		{
			SaveManager.Instance.SaveWorld(false);
			foreach (TSPlayer tsply in TShock.Players.Where(tsply => tsply != null))
			{
				tsply.SaveServerCharacter();
			}
			args.Player.SendSuccessMessage("地图, 云存档保存完毕.");
		}

		private static void Settle(CommandArgs args)
		{
			if (Liquid.panicMode)
			{
				args.Player.SendWarningMessage("液体已经平衡完毕!");
				return;
			}
			Liquid.StartPanic();
			args.Player.SendInfoMessage("正在平衡液体.");
		}

		private static void MaxSpawns(CommandArgs args)
		{
			if (args.Parameters.Count == 0)
			{
				args.Player.SendInfoMessage("当前最大刷怪量: {0}", TShock.Config.DefaultMaximumSpawns);
				return;
			}

			if (String.Equals(args.Parameters[0], "default", StringComparison.CurrentCultureIgnoreCase))
			{
				TShock.Config.DefaultMaximumSpawns = NPC.defaultMaxSpawns = 5;
				if (args.Silent) 
				{
					args.Player.SendInfoMessage("调整最大生成怪物数值至 5.");
				}
				else {
					TSPlayer.All.SendInfoMessage("{0} 调整最大生成怪物数值至 5.", args.Player.Name);
				}
				return;
			}

			int maxSpawns = -1;
			if (!int.TryParse(args.Parameters[0], out maxSpawns) || maxSpawns < 0 || maxSpawns > Main.maxNPCs)
			{
				args.Player.SendWarningMessage("最大刷怪量数值无效! 正确范围: {0} 至 {1}", 0, Main.maxNPCs);
				return;
			}

			TShock.Config.DefaultMaximumSpawns = NPC.defaultMaxSpawns = maxSpawns;
			if (args.Silent)
			{
				args.Player.SendInfoMessage("调整最大生成怪物数值至 {0}.", maxSpawns);
			}
			else {
				TSPlayer.All.SendInfoMessage("{0} 调整最大生成怪物数值至 {1}.", args.Player.Name, maxSpawns);
			}
		}

		private static void SpawnRate(CommandArgs args)
		{
			if (args.Parameters.Count == 0)
			{
				args.Player.SendInfoMessage("当前刷怪率: {0}", TShock.Config.DefaultSpawnRate);
				return;
			}

			if (String.Equals(args.Parameters[0], "default", StringComparison.CurrentCultureIgnoreCase))
			{
				TShock.Config.DefaultSpawnRate = NPC.defaultSpawnRate = 600;
				if (args.Silent) 
				{
					args.Player.SendInfoMessage("调整刷怪率至 600.");
				}
				else {
					TSPlayer.All.SendInfoMessage("{0} 调整刷怪率至 600.", args.Player.Name);
				}
				return;
			}

			int spawnRate = -1;
			if (!int.TryParse(args.Parameters[0], out spawnRate) || spawnRate < 0)
			{
				args.Player.SendWarningMessage("刷怪率无效!");
				return;
			}
			TShock.Config.DefaultSpawnRate = NPC.defaultSpawnRate = spawnRate;
			if (args.Silent) 
			{
				args.Player.SendInfoMessage("调整刷怪率至 {0}.", spawnRate);
			}
			else {
				TSPlayer.All.SendInfoMessage("{0} 调整刷怪率至 {1}.", args.Player.Name, spawnRate);
			}
		}

		#endregion Server Config Commands

		#region Time/PvpFun Commands

		private static void Time(CommandArgs args)
		{
			if (args.Parameters.Count == 0)
			{
				double time = Main.time / 3600.0;
				time += 4.5;
				if (!Main.dayTime)
					time += 15.0;
				time = time % 24.0;
				args.Player.SendInfoMessage("当前时间 {0}:{1:D2}.", (int)Math.Floor(time), (int)Math.Round((time % 1.0) * 60.0));
				return;
			}
			
			switch (args.Parameters[0].ToLower())
			{
				case "day":
					TSPlayer.Server.SetTime(true, 0.0);
					TSPlayer.All.SendInfoMessage("{0} 更改时间至 4:30.", args.Player.Name);
					break;
				case "night":
					TSPlayer.Server.SetTime(false, 0.0);
					TSPlayer.All.SendInfoMessage("{0} 更改时间至 19:30.", args.Player.Name);
					break;
				case "noon":
					TSPlayer.Server.SetTime(true, 27000.0);
					TSPlayer.All.SendInfoMessage("{0} 更改时间至 12:00.", args.Player.Name);
					break;
				case "midnight":
					TSPlayer.Server.SetTime(false, 16200.0);
					TSPlayer.All.SendInfoMessage("{0} 更改时间至 0:00.", args.Player.Name);
					break;
				default:
					string[] array = args.Parameters[0].Split(':');
					if (array.Length != 2)
					{
						args.Player.SendErrorMessage("时间值无效! 正确格式: hh:mm, 24小时制.");
						return;
					}

					int hours;
					int minutes;
					if (!int.TryParse(array[0], out hours) || hours < 0 || hours > 23
						|| !int.TryParse(array[1], out minutes) || minutes < 0 || minutes > 59)
					{
						args.Player.SendErrorMessage("时间值无效! 正确格式: hh:mm, 24小时制.");
						return;
					}

					decimal time = hours + (minutes / 60.0m);
					time -= 4.50m;
					if (time < 0.00m)
						time += 24.00m;

					if (time >= 15.00m)
					{
						TSPlayer.Server.SetTime(false, (double)((time - 15.00m) * 3600.0m));
					}
					else
					{
						TSPlayer.Server.SetTime(true, (double)(time * 3600.0m));
					}
					TSPlayer.All.SendInfoMessage("{0} 更改时间至 {1}:{2:D2}.", args.Player.Name, hours, minutes);
					break;
			}
		}

		private static void Rain(CommandArgs args)
		{
			if (args.Parameters.Count < 1 || args.Parameters.Count > 2)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}rain [slime] <stop/start>", Specifier);
				return;
			}

			int switchIndex = 0;
			if (args.Parameters.Count == 2 && args.Parameters[0].ToLowerInvariant() == "slime")
			{
				switchIndex = 1;
			}

			switch (args.Parameters[switchIndex].ToLower())
			{
				case "start":
					if (switchIndex == 1)
					{
						Main.StartSlimeRain(false);
						TSPlayer.All.SendData(PacketTypes.WorldInfo);
						TSPlayer.All.SendInfoMessage("{0} 开启了史莱姆雨.", args.Player.Name);
					}
					else
					{
						Main.StartRain();
						TSPlayer.All.SendData(PacketTypes.WorldInfo);
						TSPlayer.All.SendInfoMessage("{0} 开始了降雨.", args.Player.Name);
					}
					break;
				case "stop":
					if (switchIndex == 1)
					{
						Main.StopSlimeRain(false);
						TSPlayer.All.SendData(PacketTypes.WorldInfo);
						TSPlayer.All.SendInfoMessage("{0} 结束了史莱姆雨.", args.Player.Name);
					}
					else
					{
						Main.StopRain();
						TSPlayer.All.SendData(PacketTypes.WorldInfo);
						TSPlayer.All.SendInfoMessage("{0} 停止了降雨.", args.Player.Name);
					}
					break;
				default:
					args.Player.SendErrorMessage("语法无效! 正确语法: {0}rain [slime] <stop/start>", Specifier);
					break;

			}
		}

		private static void Slap(CommandArgs args)
		{
			if (args.Parameters.Count < 1 || args.Parameters.Count > 2)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}slap <玩家名> [伤害]", Specifier);
				return;
			}
			if (args.Parameters[0].Length == 0)
			{
				args.Player.SendErrorMessage("指定玩家无效!");
				return;
			}

			string plStr = args.Parameters[0];
			var players = TShock.Utils.FindPlayer(plStr);
			if (players.Count == 0)
			{
				args.Player.SendErrorMessage("指定玩家无效!");
			}
			else if (players.Count > 1)
			{
				TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
			}
			else
			{
				var plr = players[0];
				int damage = 5;
				if (args.Parameters.Count == 2)
				{
					int.TryParse(args.Parameters[1], out damage);
				}
				if (!args.Player.HasPermission(Permissions.kill))
				{
					damage = TShock.Utils.Clamp(damage, 15, 0);
				}
				plr.DamagePlayer(damage);
				TSPlayer.All.SendInfoMessage("{0} 抽打了 {1} {2} 点伤害.", args.Player.Name, plr.Name, damage);
				TShock.Log.Info("{0} 抽打了 {1} {2} 点伤害.", args.Player.Name, plr.Name, damage);
			}
		}

		private static void Wind(CommandArgs args)
		{
			if (args.Parameters.Count != 1)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}wind <风速>", Specifier);
				return;
			}

			float speed;
			if (!float.TryParse(args.Parameters[0], out speed))
			{
				args.Player.SendErrorMessage("风速无效!");
				return;
			}

			Main.windSpeed = speed;
			Main.windSpeedSet = speed;
			Main.windSpeedSpeed = 0f;
			TSPlayer.All.SendData(PacketTypes.WorldInfo);
			TSPlayer.All.SendInfoMessage("{0} 改变风速至 {1}.", args.Player.Name, speed);
		}

		#endregion Time/PvpFun Commands

        #region Region Commands

		private static void Region(CommandArgs args)
		{
			string cmd = "help";
			if (args.Parameters.Count > 0)
			{
				cmd = args.Parameters[0].ToLower();
			}
			switch (cmd)
			{
				case "name":
					{
						{
							args.Player.SendInfoMessage("敲击特定物块来获取所属的区域.");
							args.Player.AwaitingName = true;
							args.Player.AwaitingNameParameters = args.Parameters.Skip(1).ToArray();
						}
						break;
					}
				case "set":
					{
						int choice = 0;
						if (args.Parameters.Count == 2 &&
							int.TryParse(args.Parameters[1], out choice) &&
							choice >= 1 && choice <= 2)
						{
							args.Player.SendInfoMessage("敲击物块以设置点 " + choice);
							args.Player.AwaitingTempPoint = choice;
						}
						else
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: /region set <1/2>");
						}
						break;
					}
				case "define":
					{
						if (args.Parameters.Count > 1)
						{
							if (!args.Player.TempPoints.Any(p => p == Point.Zero))
							{
								string regionName = String.Join(" ", args.Parameters.GetRange(1, args.Parameters.Count - 1));
								var x = Math.Min(args.Player.TempPoints[0].X, args.Player.TempPoints[1].X);
								var y = Math.Min(args.Player.TempPoints[0].Y, args.Player.TempPoints[1].Y);
								var width = Math.Abs(args.Player.TempPoints[0].X - args.Player.TempPoints[1].X);
								var height = Math.Abs(args.Player.TempPoints[0].Y - args.Player.TempPoints[1].Y);

								if (TShock.Regions.AddRegion(x, y, width, height, regionName, args.Player.User.Name,
															 Main.worldID.ToString()))
								{
									args.Player.TempPoints[0] = Point.Zero;
									args.Player.TempPoints[1] = Point.Zero;
									args.Player.SendInfoMessage("设置区域 " + regionName + " 成功.");
								}
								else
								{
									args.Player.SendErrorMessage("区域 " + regionName + " 已经存在.");
								}
							}
							else
							{
								args.Player.SendErrorMessage("坐标还未设定完毕.");
							}
						}
						else
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}region define <区域名>", Specifier);
						break;
					}
				case "protect":
					{
						if (args.Parameters.Count == 3)
						{
							string regionName = args.Parameters[1];
							if (args.Parameters[2].ToLower() == "true")
							{
								if (TShock.Regions.SetRegionState(regionName, true))
									args.Player.SendInfoMessage("区域 " + regionName + " 现在开始被保护.");
								else
									args.Player.SendErrorMessage("无法找到给定区域.");
							}
							else if (args.Parameters[2].ToLower() == "false")
							{
								if (TShock.Regions.SetRegionState(regionName, false))
									args.Player.SendInfoMessage("取消保护区域 " + regionName + " 完毕.");
								else
									args.Player.SendErrorMessage("无法找到给定区域.");
							}
							else
								args.Player.SendErrorMessage("语法无效! 正确语法: {0}region protect <区域名> <true/false>", Specifier);
						}
						else
							args.Player.SendErrorMessage("语法无效! 正确语法: /region protect <名称> <true/false>", Specifier);
						break;
					}
				case "delete":
					{
						if (args.Parameters.Count > 1)
						{
							string regionName = String.Join(" ", args.Parameters.GetRange(1, args.Parameters.Count - 1));
							if (TShock.Regions.DeleteRegion(regionName))
							{
								args.Player.SendInfoMessage("删除区域 {0} 成功.", regionName);
							}
							else
								args.Player.SendErrorMessage("无法找到给定区域!");
						}
						else
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}region delete <名称>", Specifier);
						break;
					}
				case "clear":
					{
						args.Player.TempPoints[0] = Point.Zero;
						args.Player.TempPoints[1] = Point.Zero;
						args.Player.SendInfoMessage("清空临时点坐标完毕.");
						args.Player.AwaitingTempPoint = 0;
						break;
					}
				case "allow":
					{
						if (args.Parameters.Count > 2)
						{
							string playerName = args.Parameters[1];
							string regionName = "";

							for (int i = 2; i < args.Parameters.Count; i++)
							{
								if (regionName == "")
								{
									regionName = args.Parameters[2];
								}
								else
								{
									regionName = regionName + " " + args.Parameters[i];
								}
							}
							if (TShock.Users.GetUserByName(playerName) != null)
							{
								if (TShock.Regions.AddNewUser(regionName, playerName))
								{
									args.Player.SendInfoMessage("添加用户 " + playerName + " 至区域 " + regionName);
								}
								else
									args.Player.SendErrorMessage("区域 " + regionName + " 未找到");
							}
							else
							{
								args.Player.SendErrorMessage("玩家 " + playerName + " 未找到");
							}
						}
						else
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}region allow <玩家名> <区域>", Specifier);
						break;
					}
				case "remove":
					if (args.Parameters.Count > 2)
					{
						string playerName = args.Parameters[1];
						string regionName = "";

						for (int i = 2; i < args.Parameters.Count; i++)
						{
							if (regionName == "")
							{
								regionName = args.Parameters[2];
							}
							else
							{
								regionName = regionName + " " + args.Parameters[i];
							}
						}
						if (TShock.Users.GetUserByName(playerName) != null)
						{
							if (TShock.Regions.RemoveUser(regionName, playerName))
							{
								args.Player.SendInfoMessage("移除玩家 " + playerName + " 在 " + regionName + " 的领域使用权");
							}
							else
								args.Player.SendErrorMessage("区域 " + regionName + " 未找到");
						}
						else
						{
							args.Player.SendErrorMessage("玩家 " + playerName + " 未找到");
						}
					}
					else
						args.Player.SendErrorMessage("语法无效! 正确语法: {0}region remove <名称> <区域>", Specifier);
					break;
				case "allowg":
					{
						if (args.Parameters.Count > 2)
						{
							string group = args.Parameters[1];
							string regionName = "";

							for (int i = 2; i < args.Parameters.Count; i++)
							{
								if (regionName == "")
								{
									regionName = args.Parameters[2];
								}
								else
								{
									regionName = regionName + " " + args.Parameters[i];
								}
							}
							if (TShock.Groups.GroupExists(group))
							{
								if (TShock.Regions.AllowGroup(regionName, group))
								{
									args.Player.SendInfoMessage("添加组 " + group + " 到 " + regionName);
								}
								else
									args.Player.SendErrorMessage("区域 " + regionName + " 未找到");
							}
							else
							{
								args.Player.SendErrorMessage("组 " + group + " 未找到");
							}
						}
						else
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}region allowg <组名> <区域名>", Specifier);
						break;
					}
				case "removeg":
					if (args.Parameters.Count > 2)
					{
						string group = args.Parameters[1];
						string regionName = "";

						for (int i = 2; i < args.Parameters.Count; i++)
						{
							if (regionName == "")
							{
								regionName = args.Parameters[2];
							}
							else
							{
								regionName = regionName + " " + args.Parameters[i];
							}
						}
						if (TShock.Groups.GroupExists(group))
						{
							if (TShock.Regions.RemoveGroup(regionName, group))
							{
								args.Player.SendInfoMessage("移除组 " + group + " 在 " + regionName + " 的领域使用权");
							}
							else
								args.Player.SendErrorMessage("区域 " + regionName + " 未找到");
						}
						else
						{
							args.Player.SendErrorMessage("组 " + group + " 未找到");
						}
					}
					else
						args.Player.SendErrorMessage("语法无效! 正确语法: {0}region removeg <组名> <区域名>", Specifier);
					break;
				case "list":
					{
						int pageNumber;
						if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
							return;

						IEnumerable<string> regionNames = from region in TShock.Regions.Regions
														  where region.WorldID == Main.worldID.ToString()
														  select region.Name;
						PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(regionNames),
							new PaginationTools.Settings
							{
								HeaderFormat = "区域列表 ({0}/{1}):",
								FooterFormat = "键入 {0}region list {{0}} 以获取下一页区域列表.".SFormat(Specifier),
								NothingToDisplayString = "当前没有定义区域."
							});
						break;
					}
				case "info":
					{
						if (args.Parameters.Count == 1 || args.Parameters.Count > 4)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}region info <区域> [-d] [页面]", Specifier);
							break;
						}

						string regionName = args.Parameters[1];
						bool displayBoundaries = args.Parameters.Skip(2).Any(
							p => p.Equals("-d", StringComparison.InvariantCultureIgnoreCase)
						);

						Region region = TShock.Regions.GetRegionByName(regionName);
						if (region == null)
						{
							args.Player.SendErrorMessage("区域 {0} 不存在.", regionName);
							break;
						}

						int pageNumberIndex = displayBoundaries ? 3 : 2;
						int pageNumber;
						if (!PaginationTools.TryParsePageNumber(args.Parameters, pageNumberIndex, args.Player, out pageNumber))
							break;

						List<string> lines = new List<string>
                        {
                            string.Format("X: {0}; Y: {1}; W: {2}; H: {3}, Z: {4}", region.Area.X, region.Area.Y, region.Area.Width, region.Area.Height, region.Z),
                            string.Concat("所有者: ", region.Owner),
                            string.Concat("保护状态: ", region.DisableBuild.ToString()),
                        };

						if (region.AllowedIDs.Count > 0)
						{
							IEnumerable<string> sharedUsersSelector = region.AllowedIDs.Select(userId =>
							{
								User user = TShock.Users.GetUserByID(userId);
								if (user != null)
									return user.Name;

								return string.Concat("{ID: ", userId, "}");
							});
							List<string> extraLines = PaginationTools.BuildLinesFromTerms(sharedUsersSelector.Distinct());
							extraLines[0] = "玩家间分享状态: " + extraLines[0];
							lines.AddRange(extraLines);
						}
						else
						{
							lines.Add("区域没有和其他玩家共享.");
						}

						if (region.AllowedGroups.Count > 0)
						{
							List<string> extraLines = PaginationTools.BuildLinesFromTerms(region.AllowedGroups.Distinct());
							extraLines[0] = "组间分享状态: " + extraLines[0];
							lines.AddRange(extraLines);
						}
						else
						{
							lines.Add("区域没有和其他组共享.");
						}

						PaginationTools.SendPage(
							args.Player, pageNumber, lines, new PaginationTools.Settings
							{
								HeaderFormat = string.Format("区域 {0} 的信息 ({{0}}/{{1}}):", region.Name),
								FooterFormat = string.Format("键入 {0}region info {1} {{0}} 以查看下一页信息.", Specifier, regionName)
							}
						);

						if (displayBoundaries)
						{
							Rectangle regionArea = region.Area;
							foreach (Point boundaryPoint in Utils.Instance.EnumerateRegionBoundaries(regionArea))
							{
								// Preferring dotted lines as those should easily be distinguishable from actual wires.
								if ((boundaryPoint.X + boundaryPoint.Y & 1) == 0)
								{
									// Could be improved by sending raw tile data to the client instead but not really 
									// worth the effort as chances are very low that overwriting the wire for a few 
									// nanoseconds will cause much trouble.
									Tile tile = Main.tile[boundaryPoint.X, boundaryPoint.Y];
									bool oldWireState = tile.wire();
									tile.wire(true);

									try
									{
										args.Player.SendTileSquare(boundaryPoint.X, boundaryPoint.Y, 1);
									}
									finally
									{
										tile.wire(oldWireState);
									}
								}
							}

							Timer boundaryHideTimer = null;
							boundaryHideTimer = new Timer((state) =>
							{
								foreach (Point boundaryPoint in Utils.Instance.EnumerateRegionBoundaries(regionArea))
									if ((boundaryPoint.X + boundaryPoint.Y & 1) == 0)
										args.Player.SendTileSquare(boundaryPoint.X, boundaryPoint.Y, 1);

								Debug.Assert(boundaryHideTimer != null);
								boundaryHideTimer.Dispose();
							},
								null, 5000, Timeout.Infinite
							);
						}

						break;
					}
				case "z":
					{
						if (args.Parameters.Count == 3)
						{
							string regionName = args.Parameters[1];
							int z = 0;
							if (int.TryParse(args.Parameters[2], out z))
							{
								if (TShock.Regions.SetZ(regionName, z))
									args.Player.SendInfoMessage("区域的优先值(Z组)被设定为 " + z);
								else
									args.Player.SendErrorMessage("无法找到给定区域");
							}
							else
								args.Player.SendErrorMessage("语法无效! 正确语法: {0}region z <名称> <#>", Specifier);
						}
						else
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}region z <名称> <#>", Specifier);
						break;
					}
				case "resize":
				case "expand":
					{
						if (args.Parameters.Count == 4)
						{
							int direction;
							switch (args.Parameters[2])
							{
								case "u":
								case "up":
									{
										direction = 0;
										break;
									}
								case "r":
								case "right":
									{
										direction = 1;
										break;
									}
								case "d":
								case "down":
									{
										direction = 2;
										break;
									}
								case "l":
								case "left":
									{
										direction = 3;
										break;
									}
								default:
									{
										direction = -1;
										break;
									}
							}
							int addAmount;
							int.TryParse(args.Parameters[3], out addAmount);
							if (TShock.Regions.ResizeRegion(args.Parameters[1], addAmount, direction))
							{
								args.Player.SendInfoMessage("区域调整大小成功!");
								TShock.Regions.Reload();
							}
							else
								args.Player.SendErrorMessage("语法无效! 正确语法: {0}region resize <区域> <u/d/l/r> <量>", Specifier);
						}
						else
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}region resize <区域> <u/d/l/r> <量>", Specifier);
						break;
					}
				case "tp":
					{
						if (!args.Player.HasPermission(Permissions.tp))
						{
							args.Player.SendErrorMessage("你没有权限执行操作.");
							break;
						}
						if (args.Parameters.Count <= 1)
						{
							args.Player.SendErrorMessage("语法无效! 正确语法: {0}region tp <区域>.", Specifier);
							break;
						}

						string regionName = string.Join(" ", args.Parameters.Skip(1));
						Region region = TShock.Regions.GetRegionByName(regionName);
						if (region == null)
						{
							args.Player.SendErrorMessage("区域 {0} 不存在.", regionName);
							break;
						}

						args.Player.Teleport(region.Area.Center.X * 16, region.Area.Center.Y * 16);
						break;
					}
				case "help":
				default:
					{
						int pageNumber;
						int pageParamIndex = 0;
						if (args.Parameters.Count > 1)
							pageParamIndex = 1;
						if (!PaginationTools.TryParsePageNumber(args.Parameters, pageParamIndex, args.Player, out pageNumber))
							return;

						List<string> lines = new List<string> {
                          "set <1/2> - 设置区域临时坐标点.",
                          "clear - 清空设置过的临时坐标点.",
                          "define <名称> - 使用指定的名称来定义新区域.",
                          "delete <名称> - 删除给定的区域.",
                          "name [-u][-z][-p] - 显示给定坐标点所在的区域.",
                          "list - 显示所有区域.",
                          "resize <区域> <u/d/l/r> <改变值> - 重新调整区域的大小.",
                          "allow <user> <区域> - 允许玩家使用某区域.",
                          "remove <user> <区域> - 移除某区域内有权限的玩家.",
                          "allowg <组名> <区域> - 允许玩家使用某区域.",
                          "removeg <组名> <区域> - 移除某区域内有权限的用户组.",
                          "info <区域> [-d] - 显示给定区域的详细信息.",
                          "protect <名称> <true/false> - 设定区域的物块保护状态.",
                          "z <名称> <#> - 设置区域的优先值(Z值).",
                        };
						if (args.Player.HasPermission(Permissions.tp))
							lines.Add("tp <区域> - Teleports you to the given region's center.");

						PaginationTools.SendPage(
						  args.Player, pageNumber, lines,
						  new PaginationTools.Settings
						  {
							  HeaderFormat = "区域管理指令列表 ({0}/{1}):",
							  FooterFormat = "输入 {0}region {{0}} 以获取下一页子指令.".SFormat(Specifier)
						  }
						);
						break;
					}
			}
		}

        #endregion Region Commands

		#region World Protection Commands

		private static void ToggleAntiBuild(CommandArgs args)
		{
			TShock.Config.DisableBuild = !TShock.Config.DisableBuild;
			TSPlayer.All.SendSuccessMessage(string.Format("禁止建筑模式已经被{0}.", (TShock.Config.DisableBuild ? "开始" : "关闭")));
		}

		private static void ProtectSpawn(CommandArgs args)
		{
			TShock.Config.SpawnProtection = !TShock.Config.SpawnProtection;
			TSPlayer.All.SendSuccessMessage(string.Format("出生点现在{0}保护.", (TShock.Config.SpawnProtection ? "被" : "停止被")));
		}

		#endregion World Protection Commands

		#region General Commands

		private static void Help(CommandArgs args)
		{
			if (args.Parameters.Count > 1)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}help <command/page>", Specifier);
				return;
			}

			int pageNumber;
			if (args.Parameters.Count == 0 || int.TryParse(args.Parameters[0], out pageNumber))
			{
				if (!PaginationTools.TryParsePageNumber(args.Parameters, 0, args.Player, out pageNumber))
				{
					return;
				}

				IEnumerable<string> cmdNames = from cmd in ChatCommands
											   where cmd.CanRun(args.Player) && (cmd.Name != "auth" || TShock.AuthToken != 0)
											   select cmd.Names.Count>1?$"{Specifier}{cmd.Names[1]}({cmd.Name})" :$"{Specifier}{cmd.Name}";

                PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(cmdNames),
					new PaginationTools.Settings
					{
						HeaderFormat = "指令列表 ({0}/{1}):",
						FooterFormat = "键入 {0}help {{0}} 获取下一页指令.".SFormat(Specifier)
					});
			}
			else
			{
				string commandName = args.Parameters[0].ToLower();
				if (commandName.StartsWith(Specifier))
				{
					commandName = commandName.Substring(1);
				}

				Command command = ChatCommands.Find(c => c.Names.Contains(commandName));
				if (command == null)
				{
					args.Player.SendErrorMessage("指令无效.");
					return;
				}
				if (!command.CanRun(args.Player))
				{
					args.Player.SendErrorMessage("缺少执行该指令的权限.");
					return;
				}

				args.Player.SendSuccessMessage("指令 {0}{1} 说明信息: ", Specifier, command.Name);
                if (command.HelpDesc == null)
                {
                    args.Player.SendInfoMessage(command.HelpText);
                    return;
                }
                foreach (string line in command.HelpDesc)
                {
                    args.Player.SendInfoMessage(line);
                }
			}
		}

		private static void GetVersion(CommandArgs args)
		{
			args.Player.SendInfoMessage("TShock运行版本: {0} ({1}).", TShock.VersionNum, TShock.VersionCodename);
            args.Player.SendInfoMessage("TShock汉化版本: {0} ({1}).", TShock.CNVersion, TShock.CNMode);
        }

		private static void ListConnectedPlayers(CommandArgs args)
		{
			bool invalidUsage = (args.Parameters.Count > 2);

			bool displayIdsRequested = false;
			int pageNumber = 1;
			if (!invalidUsage) 
			{
				foreach (string parameter in args.Parameters)
				{
					if (parameter.Equals("-i", StringComparison.InvariantCultureIgnoreCase))
					{
						displayIdsRequested = true;
						continue;
					}

					if (!int.TryParse(parameter, out pageNumber))
					{
						invalidUsage = true;
						break;
					}
				}
			}
			if (invalidUsage)
			{
				args.Player.SendErrorMessage("用法无效! 正确用法: {0}who [-i] [pagenumber]", Specifier);
				return;
			}
			if (displayIdsRequested && !args.Player.HasPermission(Permissions.seeids))
			{
				args.Player.SendErrorMessage("缺少显示玩家ID的权限.");
				return;
			}

			args.Player.SendSuccessMessage("在线玩家 ({0}/{1})", TShock.Utils.ActivePlayers(), TShock.Config.MaxSlots);
			PaginationTools.SendPage(
				args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(TShock.Utils.GetPlayers(displayIdsRequested)), 
				new PaginationTools.Settings 
				{
					IncludeHeader = false,
					FooterFormat = string.Format("键入 {0}who {1}{{0}} 查看下页玩家.", Specifier, displayIdsRequested ? "-i " : string.Empty)
				}
			);
		}

		private static void AuthToken(CommandArgs args)
		{
			if (TShock.AuthToken == 0)
			{
				args.Player.SendWarningMessage("密钥获取权限已停止, 该次非法尝试讲被记录.");
				TShock.Utils.ForceKick(args.Player, "验证系统已经停止.", true, true);
				TShock.Log.Warn("玩家 {0} 尝试执行被禁用的 {1}auth 指令.", args.Player.IP, Specifier);
				return;
			}
			int givenCode = Convert.ToInt32(args.Parameters[0]);
			if (givenCode == TShock.AuthToken && args.Player.Group.Name != "superadmin")
			{
				try
				{
					args.Player.Group = TShock.Utils.GetGroup("superadmin");
					args.Player.SendInfoMessage("你现在已经拥有临时超级管理权, 退出游戏后就会被系统收回.");
					args.Player.SendInfoMessage("若想长期使用, 请按照下面步骤创建永久超级管理账户..");
					args.Player.SendInfoMessage("执行 -- {0}user add <用户名> <密码> superadmin", Specifier);
					args.Player.SendInfoMessage("结果 -- <用户名>(<密码>) 会被添加到超管组.");
					args.Player.SendInfoMessage("完成上述操作后, 执行 -- {0}login <用户名(若和玩家名一致, 可省略)> <密码> --.", Specifier);
					args.Player.SendInfoMessage("若明白, 请按照上述说的执行, 完成后, 执行 {0}auth-verify 来关闭验证功能.", Specifier);
				}
				catch (UserManagerException ex)
				{
					TShock.Log.ConsoleError(ex.ToString());
					args.Player.SendErrorMessage(ex.Message);
				}
				return;
			}

			if (args.Player.Group.Name == "superadmin")
			{
				args.Player.SendInfoMessage("请禁用验证系统! 若需要帮助, 联系TShock论坛. https://tshock.co/");
				args.Player.SendInfoMessage("该账户为超级管理, 请完成下面提示的步骤完成TShock配置:");
				args.Player.SendInfoMessage("请使用该账户登录, 然后关闭超管验证功能.");
                args.Player.SendInfoMessage("若明白, 请按照上述说的执行, 完成后, 执行 {0}auth-verify 来关闭验证功能.",Specifier);
                return;
			}

			args.Player.SendErrorMessage("无效超管验证密钥. 该非法尝试将被记录.");
			TShock.Log.Warn(args.Player.IP + " 尝试验证超管失败.");
		}

		private static void AuthVerify(CommandArgs args)
		{
			if (TShock.AuthToken == 0)
			{
				args.Player.SendWarningMessage("看上去你已经关闭密钥验证功能了.");
				args.Player.SendWarningMessage("若是不小心执行该指令, 删除 auth.lck 文件即可恢复功能.");
				return;
			}

			args.Player.SendSuccessMessage("你的新账户已经成功验证, 且密钥验证功能已经被关闭.");
			args.Player.SendSuccessMessage("你以后可以使用 {0}user 管理用户. 记住不要删除 auth.lck 文件.", Specifier);
			args.Player.SendSuccessMessage("你可以在官方论坛分享你的服务器, 跟其他管理交流经验等.-- https://tshock.co/");
            args.Player.SendSuccessMessage("若需汉化方面的帮助, 请访问在Github的项目.-- https://github.com/mistzzt/TShock");
            args.Player.SendSuccessMessage("感谢使用 TShock ! 感谢对该汉化版本的支持.");
			FileTools.CreateFile(Path.Combine(TShock.SavePath, "auth.lck"));
			File.Delete(Path.Combine(TShock.SavePath, "authcode.txt"));
			TShock.AuthToken = 0;
		}

		private static void ThirdPerson(CommandArgs args)
		{
			if (args.Parameters.Count == 0)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}me <文本>", Specifier);
				return;
			}
			if (args.Player.mute)
				args.Player.SendErrorMessage("你被禁言, 无法说话.");
			else
				TSPlayer.All.SendMessage(string.Format("*{0} {1}", args.Player.Name, String.Join(" ", args.Parameters)), 205, 133, 63);
		}

		private static void PartyChat(CommandArgs args)
		{
			if (args.Parameters.Count == 0)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}p <队聊文本>", Specifier);
				return;
			}
			int playerTeam = args.Player.Team;

			if (args.Player.mute)
				args.Player.SendErrorMessage("你被禁言, 无法说话.");
			else if (playerTeam != 0)
			{
				string msg = string.Format("<{0}> {1}", args.Player.Name, String.Join(" ", args.Parameters));
				foreach (TSPlayer player in TShock.Players)
				{
					if (player != null && player.Active && player.Team == playerTeam)
						player.SendMessage(msg, Main.teamColor[playerTeam].R, Main.teamColor[playerTeam].G, Main.teamColor[playerTeam].B);
				}
			}
			else
				args.Player.SendErrorMessage("你未加入特定队伍!");
		}

		private static void Mute(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}mute <玩家名> [原因]", Specifier);
				return;
			}

			var players = TShock.Utils.FindPlayer(args.Parameters[0]);
			if (players.Count == 0)
			{
				args.Player.SendErrorMessage("指定玩家无效!");
			}
			else if (players.Count > 1)
			{
				TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
			}
			else if (players[0].HasPermission(Permissions.mute))
			{
				args.Player.SendErrorMessage("你无法禁言该玩家.");
			}
			else if (players[0].mute)
			{
				var plr = players[0];
				plr.mute = false;
				TSPlayer.All.SendInfoMessage("{0} 被 {1} 解除禁言.", plr.Name, args.Player.Name);
			}
			else
			{
				string reason = "无指明原因.";
				if (args.Parameters.Count > 1)
					reason = String.Join(" ", args.Parameters.ToArray(), 1, args.Parameters.Count - 1);
				var plr = players[0];
				plr.mute = true;
				TSPlayer.All.SendInfoMessage("{0} 被 {1} 禁言. 原因: {2}.", plr.Name, args.Player.Name, reason);
			}
		}

		private static void Motd(CommandArgs args)
		{
			TShock.Utils.ShowFileToUser(args.Player, "motd.txt");
		}

		private static void Rules(CommandArgs args)
		{
			TShock.Utils.ShowFileToUser(args.Player, "rules.txt");
		}

		private static void Whisper(CommandArgs args)
		{
			if (args.Parameters.Count < 2)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}whisper <玩家名> <内容>", Specifier);
				return;
			}

			var players = TShock.Utils.FindPlayer(args.Parameters[0]);
			if (players.Count == 0)
			{
				args.Player.SendErrorMessage("指定玩家无效!");
			}
			else if (players.Count > 1)
			{
				TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
			}
			else if (args.Player.mute)
			{
				args.Player.SendErrorMessage("你被禁言, 无法说话.");
			}
			else
			{
				var plr = players[0];
				var msg = string.Join(" ", args.Parameters.ToArray(), 1, args.Parameters.Count - 1);
				plr.SendMessage(String.Format("<来自 {0}> {1}", args.Player.Name, msg), Color.MediumPurple);
				args.Player.SendMessage(String.Format("<发至 {0}> {1}", plr.Name, msg), Color.MediumPurple);
				plr.LastWhisper = args.Player;
				args.Player.LastWhisper = plr;
			}
		}

		private static void Reply(CommandArgs args)
		{
			if (args.Player.mute)
			{
				args.Player.SendErrorMessage("你被禁言, 无法说话.");
			}
			else if (args.Player.LastWhisper != null)
			{
				var msg = string.Join(" ", args.Parameters);
				args.Player.LastWhisper.SendMessage(String.Format("<来自 {0}> {1}", args.Player.Name, msg), Color.MediumPurple);
				args.Player.SendMessage(String.Format("<发至 {0}> {1}", args.Player.LastWhisper.Name, msg), Color.MediumPurple);
			}
			else
			{
				args.Player.SendErrorMessage("你并没有接收到别人的私聊信息. 请使用 {0}whisper 以私聊至特定玩家.", Specifier);
			}
		}

		private static void Annoy(CommandArgs args)
		{
			if (args.Parameters.Count != 2)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}annoy <玩家名> <持续时间>", Specifier);
				return;
			}
			int annoy = 5;
			int.TryParse(args.Parameters[1], out annoy);

			var players = TShock.Utils.FindPlayer(args.Parameters[0]);
			if (players.Count == 0)
				args.Player.SendErrorMessage("指定玩家无效!");
			else if (players.Count > 1)
				TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
			else
			{
				var ply = players[0];
				args.Player.SendSuccessMessage("正在骚扰 " + ply.Name + ". 持续时间: " + annoy + "s.");
				(new Thread(ply.Whoopie)).Start(annoy);
			}
		}

		private static void Confuse(CommandArgs args)
		{
			if (args.Parameters.Count != 1)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}confuse <玩家名>", Specifier);
				return;
			}
			var players = TShock.Utils.FindPlayer(args.Parameters[0]);
			if (players.Count == 0)
				args.Player.SendErrorMessage("指定玩家无效!");
			else if (players.Count > 1)
				TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
			else
			{
				var ply = players[0];
				ply.Confused = !ply.Confused;
				args.Player.SendSuccessMessage("{1}扰乱 {0}.", ply.Name, ply.Confused ? "开始" : "停止");
			}
		}

		private static void Rocket(CommandArgs args)
		{
			if (args.Parameters.Count != 1)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}rocket <玩家名>", Specifier);
				return;
			}
			var players = TShock.Utils.FindPlayer(args.Parameters[0]);
			if (players.Count == 0)
				args.Player.SendErrorMessage("指定玩家无效!");
			else if (players.Count > 1)
				TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
			else
			{
				var ply = players[0];

				if (ply.IsLoggedIn && Main.ServerSideCharacter)
				{
					ply.TPlayer.velocity.Y = -50;
					TSPlayer.All.SendData(PacketTypes.PlayerUpdate, "", ply.Index);
					args.Player.SendSuccessMessage("{0} 飞了.", ply.Name);
				}
				else
				{
					args.Player.SendErrorMessage("无法飞天: 未加入游戏或非云存档模式.");
				}
			}
		}

		private static void FireWork(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}firework <玩家名> [red|green|blue|yellow]", Specifier);
				return;
			}
			var players = TShock.Utils.FindPlayer(args.Parameters[0]);
			if (players.Count == 0)
				args.Player.SendErrorMessage("指定玩家无效!");
			else if (players.Count > 1)
				TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
			else
			{
				int type = 167;
				if (args.Parameters.Count > 1)
				{
					if (args.Parameters[1].ToLower() == "green")
						type = 168;
					else if (args.Parameters[1].ToLower() == "blue")
						type = 169;
					else if (args.Parameters[1].ToLower() == "yellow")
						type = 170;
				}
				var ply = players[0];
				int p = Projectile.NewProjectile(ply.TPlayer.position.X, ply.TPlayer.position.Y - 64f, 0f, -8f, type, 0, (float)0);
				Main.projectile[p].Kill();
				args.Player.SendSuccessMessage("{0} 炸开了烟花.", ply.Name);
			}
		}

		private static void Aliases(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}aliases <指令或别称>", Specifier);
				return;
			}
			
			string givenCommandName = string.Join(" ", args.Parameters);
			if (string.IsNullOrWhiteSpace(givenCommandName)) {
				args.Player.SendErrorMessage("请输入正确的指令名或别称.");
				return;
			}

			string commandName;
			if (givenCommandName[0] == Specifier[0])
				commandName = givenCommandName.Substring(1);
			else
				commandName = givenCommandName;

			bool didMatch = false;
			foreach (Command matchingCommand in ChatCommands.Where(cmd => cmd.Names.IndexOf(commandName) != -1)) {
				if (matchingCommand.Names.Count > 1)
					args.Player.SendInfoMessage(
					    "{0}{1} 的别称: {0}{2}", Specifier, matchingCommand.Name, string.Join(", {0}".SFormat(Specifier), matchingCommand.Names.Skip(1)));
				else
					args.Player.SendInfoMessage("{0}{1} 无别称定义.", Specifier, matchingCommand.Name);

				didMatch = true;
			}

			if (!didMatch)
				args.Player.SendErrorMessage("未找到名或别称为 \"{0}\" 的指令.", givenCommandName);
		}

		#endregion General Commands

		#region Cheat Commands

		private static void Clear(CommandArgs args)
		{
			if (args.Parameters.Count != 1 && args.Parameters.Count != 2)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}clear <item/npc/projectile> [半径数值]", Specifier);
				return;
			}

			int radius = 50;
			if (args.Parameters.Count == 2)
			{
				if (!int.TryParse(args.Parameters[1], out radius) || radius <= 0)
				{
					args.Player.SendErrorMessage("半径数值无效.");
					return;
				}
			}

			switch (args.Parameters[0].ToLower())
			{
				case "item":
				case "items":
					{
						int cleared = 0;
						for (int i = 0; i < Main.maxItems; i++)
						{
							float dX = Main.item[i].position.X - args.Player.X;
							float dY = Main.item[i].position.Y - args.Player.Y;

							if (Main.item[i].active && dX * dX + dY * dY <= radius * radius * 256f)
							{
								Main.item[i].active = false;
								TSPlayer.All.SendData(PacketTypes.ItemDrop, "", i);
								cleared++;
							}
						}
						args.Player.SendSuccessMessage("成功在半径 {1} 内清空 {0} 件物品.", cleared, radius);
					}
					break;
				case "npc":
				case "npcs":
					{
						int cleared = 0;
						for (int i = 0; i < Main.maxNPCs; i++)
						{
							float dX = Main.npc[i].position.X - args.Player.X;
							float dY = Main.npc[i].position.Y - args.Player.Y;

							if (Main.npc[i].active && dX * dX + dY * dY <= radius * radius * 256f)
							{
								Main.npc[i].active = false;
								Main.npc[i].type = 0;
								TSPlayer.All.SendData(PacketTypes.NpcUpdate, "", i);
								cleared++;
							}
						}
						args.Player.SendSuccessMessage("成功在半径 {1} 内清空 {0} 只怪物.", cleared, radius);
					}
					break;
				case "proj":
				case "projectile":
				case "projectiles":
					{
						int cleared = 0;
						for (int i = 0; i < Main.maxProjectiles; i++)
						{
							float dX = Main.projectile[i].position.X - args.Player.X;
							float dY = Main.projectile[i].position.Y - args.Player.Y;

							if (Main.projectile[i].active && dX * dX + dY * dY <= radius * radius * 256f)
							{
								Main.projectile[i].active = false;
								Main.projectile[i].type = 0;
								TSPlayer.All.SendData(PacketTypes.ProjectileNew, "", i);
								cleared++;
							}
						}
						args.Player.SendSuccessMessage("成功在半径 {1} 内清空 {0} 个抛射体.", cleared, radius);
					}
					break;
				default:
					args.Player.SendErrorMessage("无效清除对象! 可用: item / npc / projectile");
					break;
			}
		}

		private static void Kill(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}kill <玩家名>", Specifier);
				return;
			}

			string plStr = String.Join(" ", args.Parameters);
			var players = TShock.Utils.FindPlayer(plStr);
			if (players.Count == 0)
			{
				args.Player.SendErrorMessage("指定玩家无效!");
			}
			else if (players.Count > 1)
			{
				TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
			}
			else
			{
				var plr = players[0];
				plr.DamagePlayer(999999);
				args.Player.SendSuccessMessage(string.Format("你杀死了 {0}!", plr.Name));
				plr.SendErrorMessage("你已死亡, 凶手是 {0} .", args.Player.Name);
			}
		}

		private static void Butcher(CommandArgs args)
		{
			if (args.Parameters.Count > 1)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}butcher [怪物种类]", Specifier);
				return;
			}

			int npcId = 0;

			if (args.Parameters.Count == 1)
			{
				var npcs = TShock.Utils.GetNPCByIdOrName(args.Parameters[0]);
				if (npcs.Count == 0)
				{
					args.Player.SendErrorMessage("指定NPC无效!");
					return;
				}
				else if (npcs.Count > 1)
				{
					TShock.Utils.SendMultipleMatchError(args.Player, npcs.Select(n => n.name));
					return;
				}
				else
				{
					npcId = npcs[0].netID;
				}
			}

			int kills = 0;
			for (int i = 0; i < Main.npc.Length; i++)
			{
				if (Main.npc[i].active && ((npcId == 0 && !Main.npc[i].townNPC && Main.npc[i].netID != NPCID.TargetDummy) || Main.npc[i].netID == npcId))
				{
					TSPlayer.Server.StrikeNPC(i, (int)(Main.npc[i].life + (Main.npc[i].defense*0.5)), 0, 0);
					kills++;
				}
			}
			TSPlayer.All.SendInfoMessage("{0} 清空了 {1} 个怪物单位.", args.Player.Name, kills);
		}
		
		private static void Item(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}item <物品名/ID> [数量] [前缀ID/名称]", Specifier);
				return;
			}

			int amountParamIndex = -1;
			int itemAmount = 0;
			for (int i = 1; i < args.Parameters.Count; i++)
			{
				if (int.TryParse(args.Parameters[i], out itemAmount))
				{
					amountParamIndex = i;
					break;
				}
			}

			string itemNameOrId;
			if (amountParamIndex == -1)
				itemNameOrId = string.Join(" ", args.Parameters);
			else
				itemNameOrId = string.Join(" ", args.Parameters.Take(amountParamIndex));

			Item item;
			List<Item> matchedItems = TShock.Utils.GetItemByIdOrName(itemNameOrId);
			if (matchedItems.Count == 0)
			{
				args.Player.SendErrorMessage("物品种类无效!");
				return;
			}
			else if (matchedItems.Count > 1)
			{
				TShock.Utils.SendMultipleMatchError(args.Player, matchedItems.Select(i => i.name));
				return;
			}
			else
			{
				item = matchedItems[0];
			}
			if (item.type < 1 && item.type >= Main.maxItemTypes)
			{
				args.Player.SendErrorMessage("物品 {0} 无效.", itemNameOrId);
				return;
			}

			int prefixId = 0;
			if (amountParamIndex != -1 && args.Parameters.Count > amountParamIndex + 1)
			{
				string prefixidOrName = args.Parameters[amountParamIndex + 1];
				var prefixIds = TShock.Utils.GetPrefixByIdOrName(prefixidOrName);

				if (item.accessory && prefixIds.Contains(42))
				{
					prefixIds.Remove(42);
					prefixIds.Remove(76);
					prefixIds.Add(76);
				}
				else if (!item.accessory && prefixIds.Contains(42))
					prefixIds.Remove(76);

				if (prefixIds.Count > 1) 
				{
					TShock.Utils.SendMultipleMatchError(args.Player, prefixIds.Select(p => p.ToString()));
					return;
				}
				else if (prefixIds.Count == 0) 
				{
					args.Player.SendErrorMessage("没有符合条件 \"{0}\" 的前缀.", prefixidOrName);
					return;
				}
				else
				{
					prefixId = prefixIds[0];
				}
			}

			if (args.Player.InventorySlotAvailable || (item.type > 70 && item.type < 75) || item.ammo > 0 || item.type == 58 || item.type == 184)
			{
				if (itemAmount == 0 || itemAmount > item.maxStack)
					itemAmount = item.maxStack;

				if (args.Player.GiveItemCheck(item.type, item.name, item.width, item.height, itemAmount, prefixId))
				{
					item.prefix = (byte)prefixId;
					args.Player.SendSuccessMessage("成功生成 {0} 个 {1}.", itemAmount, item.AffixName());
				}
				else
				{
					args.Player.SendErrorMessage("你无法生成被封禁的物品.");
				}
			}
			else
			{
				args.Player.SendErrorMessage("背包已满, 无法生成物品.");
			}
		}
		
		private static void RenameNPC(CommandArgs args)
		{
			if (args.Parameters.Count != 2)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}renameNPC <guide/nurse/...> <新名字>", Specifier);
				return;
			}
			int npcId = 0;
			if (args.Parameters.Count == 2)
			{
				List<NPC> npcs = TShock.Utils.GetNPCByIdOrName(args.Parameters[0]);
				if (npcs.Count == 0)
				{
					args.Player.SendErrorMessage("指定NPC无效.");
					return;
				}
				else if (npcs.Count > 1)
				{
					TShock.Utils.SendMultipleMatchError(args.Player, npcs.Select(n => n.name));
					return;
				}
				else if (args.Parameters[1].Length >200)
				{
					args.Player.SendErrorMessage("指定的名字长度超过限制!");
					return;
				}
				else
				{
					npcId = npcs[0].netID;
				}
			}
			int done=0;
			for (int i = 0; i < Main.npc.Length; i++)
			{
				if (Main.npc[i].active && ((npcId == 0 && !Main.npc[i].townNPC) || (Main.npc[i].netID == npcId && Main.npc[i].townNPC)))
				{
				Main.npc[i].displayName= args.Parameters[1];
				NetMessage.SendData(56, -1, -1, args.Parameters[1], i, 0f, 0f, 0f, 0);
				done++;
				}
			}
			if (done >0 )
			{
			TSPlayer.All.SendInfoMessage("{0} 赐予了 {1} 新名字.", args.Player.Name, args.Parameters[0]);
			}
			else
			{
			args.Player.SendErrorMessage("无法重命名 {0} !", args.Parameters[0]);
			}
		}
		
		private static void Give(CommandArgs args)
		{
			if (args.Parameters.Count < 2)
			{
				args.Player.SendErrorMessage(
                    "语法无效! 正确语法: {0}give <物品名/ID> <玩家名> [数量] [前缀ID/名称]", Specifier);
				return;
			}
			if (args.Parameters[0].Length == 0)
			{
				args.Player.SendErrorMessage("语法无效: 缺少物品名/ID.");
				return;
			}
			if (args.Parameters[1].Length == 0)
			{
				args.Player.SendErrorMessage("语法无效: 缺少玩家名.");
				return;
			}
			int itemAmount = 0;
			int prefix = 0;
			var items = TShock.Utils.GetItemByIdOrName(args.Parameters[0]);
			args.Parameters.RemoveAt(0);
			string plStr = args.Parameters[0];
			args.Parameters.RemoveAt(0);
			if (args.Parameters.Count == 1)
				int.TryParse(args.Parameters[0], out itemAmount);
			if (items.Count == 0)
			{
				args.Player.SendErrorMessage("物品种类无效!");
			}
			else if (items.Count > 1)
			{
				TShock.Utils.SendMultipleMatchError(args.Player, items.Select(i => i.name));
			}
			else
			{
				var item = items[0];

				if (args.Parameters.Count == 2)
				{
					int.TryParse(args.Parameters[0], out itemAmount);
					var prefixIds = TShock.Utils.GetPrefixByIdOrName(args.Parameters[1]);
					if (item.accessory && prefixIds.Contains(42))
					{
						prefixIds.Remove(42);
						prefixIds.Remove(76);
						prefixIds.Add(76);
					}
					else if (!item.accessory && prefixIds.Contains(42))
						prefixIds.Remove(76);
					if (prefixIds.Count == 1)
						prefix = prefixIds[0];
				}

				if (item.type >= 1 && item.type < Main.maxItemTypes)
				{
					var players = TShock.Utils.FindPlayer(plStr);
					if (players.Count == 0)
					{
						args.Player.SendErrorMessage("指定玩家无效!");
					}
					else if (players.Count > 1)
					{
						TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
					}
					else
					{
						var plr = players[0];
						if (plr.InventorySlotAvailable || (item.type > 70 && item.type < 75) || item.ammo > 0 || item.type == 58 || item.type == 184)
						{
							if (itemAmount == 0 || itemAmount > item.maxStack)
								itemAmount = item.maxStack;
							if (plr.GiveItemCheck(item.type, item.name, item.width, item.height, itemAmount, prefix))
							{
								args.Player.SendSuccessMessage(string.Format("成功给 {0} 生成了 {1} 个 {2}.", plr.Name, itemAmount, item.name));
								plr.SendSuccessMessage(string.Format("{0} 给你了 {1} 个 {2}.", args.Player.Name, itemAmount, item.name));
							}
							else
							{
								args.Player.SendErrorMessage("无法生成被禁用的物品.");
							}
							
						}
						else
						{
							args.Player.SendErrorMessage("目标玩家背包已满!");
						}
					}
				}
				else
				{
					args.Player.SendErrorMessage("物品种类无效!");
				}
			}
		}

		private static void Heal(CommandArgs args)
		{
			TSPlayer playerToHeal;
			if (args.Parameters.Count > 0)
			{
				string plStr = String.Join(" ", args.Parameters);
				var players = TShock.Utils.FindPlayer(plStr);
				if (players.Count == 0)
				{
					args.Player.SendErrorMessage("指定玩家无效!");
					return;
				}
				else if (players.Count > 1)
				{
					TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
					return;
				}
				else
				{
					playerToHeal = players[0];
				}
			}
			else if (!args.Player.RealPlayer)
			{
				args.Player.SendErrorMessage("你无法恢复自己的生命值!");
				return;
			}
			else
			{
				playerToHeal = args.Player;
			}

			playerToHeal.Heal();
			if (playerToHeal == args.Player)
			{
				args.Player.SendSuccessMessage("你的生命值已被恢复!");
			}
			else
			{
				args.Player.SendSuccessMessage(string.Format("你恢复了 {0} 的生命值!", playerToHeal.Name));
				playerToHeal.SendSuccessMessage(string.Format("{0} 恢复了你的生命值!", args.Player.Name));
			}
		}

		private static void Buff(CommandArgs args)
		{
			if (args.Parameters.Count < 1 || args.Parameters.Count > 2)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}buff <buff id/名称> [持续时间(秒)]", Specifier);
				return;
			}
			int id = 0;
			int time = 60;
			if (!int.TryParse(args.Parameters[0], out id))
			{
				var found = TShock.Utils.GetBuffByName(args.Parameters[0]);
				if (found.Count == 0)
				{
					args.Player.SendErrorMessage("Buff名称无效!");
					return;
				}
				else if (found.Count > 1)
				{
					TShock.Utils.SendMultipleMatchError(args.Player, found.Select(f => Main.buffName[f]));
					return;
				}
				id = found[0];
			}
			if (args.Parameters.Count == 2)
				int.TryParse(args.Parameters[1], out time);
			if (id > 0 && id < Main.maxBuffTypes)
			{
				if (time < 0 || time > short.MaxValue)
					time = 60;
				args.Player.SetBuff(id, time*60);
				args.Player.SendSuccessMessage(string.Format("你给自己加上了buff {0}({1}), 持续时间: {2}s!",
													  TShock.Utils.GetBuffName(id), TShock.Utils.GetBuffDescription(id), (time)));
			}
			else
				args.Player.SendErrorMessage("Buff编号无效!");
		}

		private static void GBuff(CommandArgs args)
		{
			if (args.Parameters.Count < 2 || args.Parameters.Count > 3)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}gbuff <玩家名> <buff id/名称> [持续时间(秒)]", Specifier);
				return;
			}
			int id = 0;
			int time = 60;
			var foundplr = TShock.Utils.FindPlayer(args.Parameters[0]);
			if (foundplr.Count == 0)
			{
				args.Player.SendErrorMessage("指定玩家无效!");
				return;
			}
			else if (foundplr.Count > 1)
			{
				TShock.Utils.SendMultipleMatchError(args.Player, foundplr.Select(p => p.Name));
				return;
			}
			else
			{
				if (!int.TryParse(args.Parameters[1], out id))
				{
					var found = TShock.Utils.GetBuffByName(args.Parameters[1]);
					if (found.Count == 0)
					{
						args.Player.SendErrorMessage("Buff名称无效!");
						return;
					}
					else if (found.Count > 1)
					{
						TShock.Utils.SendMultipleMatchError(args.Player, found.Select(b => Main.buffName[b]));
						return;
					}
					id = found[0];
				}
				if (args.Parameters.Count == 3)
					int.TryParse(args.Parameters[2], out time);
				if (id > 0 && id < Main.maxBuffTypes)
				{
					if (time < 0 || time > short.MaxValue)
						time = 60;
					foundplr[0].SetBuff(id, time*60);
					args.Player.SendSuccessMessage(string.Format("{0} 被附加了 {3} 秒的buff {1}({2})!",
														  foundplr[0].Name, TShock.Utils.GetBuffName(id),
														  TShock.Utils.GetBuffDescription(id), (time)));
					foundplr[0].SendSuccessMessage(string.Format("{0} 给你附加了持续时间为 {3} 秒的buff {1}({2})!",
														  args.Player.Name, TShock.Utils.GetBuffName(id),
														  TShock.Utils.GetBuffDescription(id), (time)));
				}
				else
					args.Player.SendErrorMessage("Buff编号无效!");
			}
		}

		private static void Grow(CommandArgs args)
		{
			if (args.Parameters.Count != 1)
			{
				args.Player.SendErrorMessage("语法无效! 正确语法: {0}grow <tree/epictree/mushroom/cactus/herb>", Specifier);
				return;
			}
			var name = "Fail";
			var x = args.Player.TileX;
			var y = args.Player.TileY + 3;

			if (!TShock.Regions.CanBuild(x, y, args.Player))
			{
				args.Player.SendErrorMessage("你无法改动这里的物块!");
				return;
			}

			switch (args.Parameters[0].ToLower())
			{
				case "tree":
					for (int i = x - 1; i < x + 2; i++)
					{
						Main.tile[i, y].active(true);
						Main.tile[i, y].type = 2;
						Main.tile[i, y].wall = 0;
					}
					Main.tile[x, y - 1].wall = 0;
					WorldGen.GrowTree(x, y);
					name = "树";
					break;
				case "epictree":
					for (int i = x - 1; i < x + 2; i++)
					{
						Main.tile[i, y].active(true);
						Main.tile[i, y].type = 2;
						Main.tile[i, y].wall = 0;
					}
					Main.tile[x, y - 1].wall = 0;
					Main.tile[x, y - 1].liquid = 0;
					Main.tile[x, y - 1].active(true);
					WorldGen.GrowEpicTree(x, y);
					name = "巨树";
					break;
				case "mushroom":
					for (int i = x - 1; i < x + 2; i++)
					{
						Main.tile[i, y].active(true);
						Main.tile[i, y].type = 70;
						Main.tile[i, y].wall = 0;
					}
					Main.tile[x, y - 1].wall = 0;
					WorldGen.GrowShroom(x, y);
					name = "蘑菇";
					break;
				case "cactus":
					Main.tile[x, y].type = 53;
					WorldGen.GrowCactus(x, y);
					name = "仙人掌";
					break;
				case "herb":
					Main.tile[x, y].active(true);
					Main.tile[x, y].frameX = 36;
					Main.tile[x, y].type = 83;
					WorldGen.GrowAlch(x, y);
					name = "草药";
					break;
				default:
					args.Player.SendErrorMessage("指定的植物名无效!");
					return;
			}
			args.Player.SendTileSquare(x, y);
			args.Player.SendSuccessMessage("尝试生成" + name + "...");
		}

		private static void ToggleGodMode(CommandArgs args)
		{
			TSPlayer playerToGod;
			if (args.Parameters.Count > 0)
			{
				if (!args.Player.HasPermission(Permissions.godmodeother))
				{
					args.Player.SendErrorMessage("你没有权限设置其他玩家无敌!");
					return;
				}
				string plStr = String.Join(" ", args.Parameters);
				var players = TShock.Utils.FindPlayer(plStr);
				if (players.Count == 0)
				{
					args.Player.SendErrorMessage("指定玩家无效!");
					return;
				}
				else if (players.Count > 1)
				{
					TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
					return;
				}
				else
				{
					playerToGod = players[0];
				}
			}
			else if (!args.Player.RealPlayer)
			{
				args.Player.SendErrorMessage("你无法打开非真实玩家的无敌模式!");
				return;
			}
			else
			{
				playerToGod = args.Player;
			}

			playerToGod.GodMode = !playerToGod.GodMode;

			if (playerToGod == args.Player)
			{
				args.Player.SendSuccessMessage(string.Format("你现在{0}无敌模式.", args.Player.GodMode ? "处于" : "已经停止"));
			}
			else
			{
				args.Player.SendSuccessMessage(string.Format("{0} 现在{1}无敌模式.", playerToGod.Name, playerToGod.GodMode ? "处于" : "已经停止"));
				playerToGod.SendSuccessMessage(string.Format("你现在{0}无敌模式.", playerToGod.GodMode ? "处于" : "已经停止"));
			}
		}

		#endregion Cheat Comamnds
	}
}
