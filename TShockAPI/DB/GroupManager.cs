﻿/*
TShock, a server mod for Terraria
Copyright (C) 2011-2016 Nyx Studios (fka. The TShock Team)

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
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using MySql.Data.MySqlClient;

namespace TShockAPI.DB
{
	public class GroupManager : IEnumerable<Group>
	{
		private IDbConnection database;
		public readonly List<Group> groups = new List<Group>();

		public GroupManager(IDbConnection db)
		{
			database = db;

			var table = new SqlTable("GroupList",
			                         new SqlColumn("GroupName", MySqlDbType.VarChar, 32) {Primary = true},
			                         new SqlColumn("Parent", MySqlDbType.VarChar, 32),
			                         new SqlColumn("Commands", MySqlDbType.Text),
			                         new SqlColumn("ChatColor", MySqlDbType.Text),
			                         new SqlColumn("Prefix", MySqlDbType.Text),
			                         new SqlColumn("Suffix", MySqlDbType.Text)
				);
			var creator = new SqlTableCreator(db,
			                                  db.GetSqlType() == SqlType.Sqlite
			                                  	? (IQueryBuilder) new SqliteQueryCreator()
			                                  	: new MysqlQueryCreator());
			if (creator.EnsureTableStructure(table))
			{
				// Add default groups if they don't exist
				AddDefaultGroup("guest", "",
					string.Join(",", Permissions.canbuild, Permissions.canregister, Permissions.canlogin, Permissions.canpartychat,
						Permissions.cantalkinthird, Permissions.canchat));

				AddDefaultGroup("default", "guest",
					string.Join(",", Permissions.warp, Permissions.canchangepassword));

				AddDefaultGroup("newadmin", "default",
					string.Join(",", Permissions.kick, Permissions.editspawn, Permissions.reservedslot));

				AddDefaultGroup("admin", "newadmin",
					string.Join(",", Permissions.ban, Permissions.whitelist, "tshock.world.time.*", Permissions.spawnboss,
						Permissions.spawnmob, Permissions.managewarp, Permissions.time, Permissions.tp, Permissions.slap,
						Permissions.kill, Permissions.logs,
						Permissions.immunetokick, Permissions.tpothers));

				AddDefaultGroup("trustedadmin", "admin",
					string.Join(",", Permissions.maintenance, "tshock.cfg.*", "tshock.world.*", Permissions.butcher, Permissions.item, Permissions.give,
						Permissions.heal, Permissions.immunetoban, Permissions.usebanneditem));

				AddDefaultGroup("vip", "default", string.Join(",", Permissions.reservedslot));
			}

			// Load Permissions from the DB
			LoadPermisions();

			Group.DefaultGroup = GetGroupByName(TShock.Config.DefaultGuestGroupName);
		}

		private void AddDefaultGroup(string name, string parent, string permissions)
		{
			if (!GroupExists(name))
				AddGroup(name, parent, permissions, Group.defaultChatColor);
		}


		public bool GroupExists(string group)
		{
			if (group == "superadmin")
				return true;

			return groups.Any(g => g.Name.Equals(group));
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public IEnumerator<Group> GetEnumerator()
		{
			return groups.GetEnumerator();
		}

		public Group GetGroupByName(string name)
		{
			var ret = groups.Where(g => g.Name == name);
			return 1 == ret.Count() ? ret.ElementAt(0) : null;
		}

		/// <summary>
		/// Adds group with name and permissions if it does not exist.
		/// </summary>
		/// <param name="name">name of group</param>
		/// <param name="parentname">parent of group</param>
		/// <param name="permissions">permissions</param>
		/// <param name="chatcolor">chatcolor</param>
		public void AddGroup(String name, string parentname, String permissions, String chatcolor)
		{
			if (GroupExists(name))
			{
				throw new GroupExistsException(name);
			}

			var group = new Group(name, null, chatcolor);
			group.Permissions = permissions;
			if (!string.IsNullOrWhiteSpace(parentname))
			{
				var parent = groups.FirstOrDefault(gp => gp.Name == parentname);
				if (parent == null || name == parentname)
				{
					var error = "组 {1} 的父组 {0} 无效.".SFormat(parentname, group.Name);
					TShock.Log.ConsoleError(error);
					throw new GroupManagerException(error);
				}
				group.Parent = parent;
			}

			string query = (TShock.Config.StorageType.ToLower() == "sqlite")
							? "INSERT OR IGNORE INTO GroupList (GroupName, Parent, Commands, ChatColor) VALUES (@0, @1, @2, @3);"
							: "INSERT IGNORE INTO GroupList SET GroupName=@0, Parent=@1, Commands=@2, ChatColor=@3";
			if (database.Query(query, name, parentname, permissions, chatcolor) == 1)
			{
				groups.Add(group);
			}
			else
				throw new GroupManagerException("无法添加组 " + name + ".");
		}

		/// <summary>
		/// Adds group with name and permissions if it does not exist.
		/// </summary>
		/// <param name="name">name of group</param>
		/// <param name="parentname">parent of group</param>
		/// <param name="permissions">permissions</param>
		/// <param name="chatcolor">chatcolor</param>
		/// <param name="exceptions">exceptions true indicates use exceptions for errors false otherwise</param>
		[Obsolete("Use AddGroup(name, parentname, permissions, chatcolor) instead.")]
		public String AddGroup(String name, string parentname, String permissions, String chatcolor = Group.defaultChatColor, bool exceptions = false)
		{
			if (GroupExists(name))
			{
				if (exceptions)
					throw new GroupExistsException(name);
				return "错误: 已存在; 故无法添加.";
			}

			var group = new Group(name, null, chatcolor);
			group.Permissions = permissions;
			if (!string.IsNullOrWhiteSpace(parentname))
			{
				var parent = groups.FirstOrDefault(gp => gp.Name == parentname);
				if (parent == null || name == parentname)
				{
					var error = "组 {1} 的父组 {0} 无效.".SFormat(parentname, group.Name);
					if (exceptions)
						throw new GroupManagerException(error);
					TShock.Log.ConsoleError(error);
					return error;
				}
				group.Parent = parent;
			}

			string query = (TShock.Config.StorageType.ToLower() == "sqlite")
							? "INSERT OR IGNORE INTO GroupList (GroupName, Parent, Commands, ChatColor) VALUES (@0, @1, @2, @3);"
							: "INSERT IGNORE INTO GroupList SET GroupName=@0, Parent=@1, Commands=@2, ChatColor=@3";
			if (database.Query(query, name, parentname, permissions, chatcolor) == 1)
			{
				groups.Add(group);
				return "创建组 " + name + " 成功!";
			}
			else if (exceptions)
				throw new GroupManagerException("无法添加组 " + name + ".");

			return "";
		}

		[Obsolete("Use AddGroup(name, parentname, permissions, chatcolor) instead.")]
		public String AddGroup(String name, String permissions)
		{
			return AddGroup(name, null, permissions, Group.defaultChatColor, false);
		}

		/// <summary>
		/// Updates a group including permissions
		/// </summary>
		/// <param name="name">name of the group to update</param>
		/// <param name="parentname">parent of group</param>
		/// <param name="permissions">permissions</param>
		/// <param name="chatcolor">chatcolor</param>
		/// <param name="suffix">suffix</param>
		/// <param name="prefix">prefix</param> //why is suffix before prefix?!
		public void UpdateGroup(string name, string parentname, string permissions, string chatcolor, string suffix, string prefix)
		{
			Group group = GetGroupByName(name);
			if (group == null)
				throw new GroupNotExistException(name);

			Group parent = null;
			if (!string.IsNullOrWhiteSpace(parentname))
			{
				parent = GetGroupByName(parentname);
				if (parent == null || parent == group)
					throw new GroupManagerException("组 {1} 的父组 {0} 无效.".SFormat(parentname, name));

				// Check if the new parent would cause loops.
				List<Group> groupChain = new List<Group> { group, parent };
				Group checkingGroup = parent.Parent;
				while (checkingGroup != null)
				{
					if (groupChain.Contains(checkingGroup))
						throw new GroupManagerException(
							string.Format("Invalid parent \"{0}\" for group \"{1}\" would cause loops in the parent chain.", parentname, name));

					groupChain.Add(checkingGroup);
					checkingGroup = checkingGroup.Parent;
				}
			}

			// Ensure any group validation is also persisted to the DB.
			var newGroup = new Group(name, parent, chatcolor, permissions);
			newGroup.Prefix = prefix;
			newGroup.Suffix = suffix;
			string query = "UPDATE GroupList SET Parent=@0, Commands=@1, ChatColor=@2, Suffix=@3, Prefix=@4 WHERE GroupName=@5";
			if (database.Query(query, parentname, newGroup.Permissions, newGroup.ChatColor, suffix, prefix, name) != 1)
				throw new GroupManagerException(string.Format("Failed to update group \"{0}\".", name));

			group.ChatColor = chatcolor;
			group.Permissions = permissions;
			group.Parent = parent;
			group.Prefix = prefix;
			group.Suffix = suffix;
		}

		public String DeleteGroup(String name, bool exceptions = false)
		{
			if (!GroupExists(name))
			{
				if (exceptions)
					throw new GroupNotExistException(name);
				return "错误: 组不存在.";
			}

			if (database.Query("DELETE FROM GroupList WHERE GroupName=@0", name) == 1)
			{
				groups.Remove(TShock.Utils.GetGroup(name));
				return "组 " + name + " 成功被删除.";
			}
			else if (exceptions)
				throw new GroupManagerException("删除组 " + name + " 失败.");

			return "";
		}

		public String AddPermissions(String name, List<String> permissions)
		{
			if (!GroupExists(name))
				return "错误: 组不存在.";

			var group = TShock.Utils.GetGroup(name);
			var oldperms = group.Permissions; // Store old permissions in case of error
			permissions.ForEach(p => group.AddPermission(p));
 
			if (database.Query("UPDATE GroupList SET Commands=@0 WHERE GroupName=@1", group.Permissions, name) == 1)
				return "成功修改组 " + name + " .";

			// Restore old permissions so DB and internal object are in a consistent state
			group.Permissions = oldperms;
			return "";
		}

		public String DeletePermissions(String name, List<String> permissions)
		{
			if (!GroupExists(name))
				return "错误: 组不存在.";

			var group = TShock.Utils.GetGroup(name);
			var oldperms = group.Permissions; // Store old permissions in case of error
			permissions.ForEach(p => group.RemovePermission(p));

			if (database.Query("UPDATE GroupList SET Commands=@0 WHERE GroupName=@1", group.Permissions, name) == 1)
                return "成功修改组 " + name + " .";

            // Restore old permissions so DB and internal object are in a consistent state
            group.Permissions = oldperms;
			return "";
		}

		public void LoadPermisions()
		{
			try
			{
				List<Group> newGroups = new List<Group>(groups.Count);
				Dictionary<string,string> newGroupParents = new Dictionary<string, string>(groups.Count);
				using (var reader = database.QueryReader("SELECT * FROM GroupList"))
				{
					while (reader.Read())
					{
						string groupName = reader.Get<string>("GroupName");
						if (groupName == "superadmin")
						{
							TShock.Log.ConsoleInfo("警告: 组 \"superadmin\" 出现在数据库列表中, 但该组为系统预留组, 不应被添加.");
							continue;
						}

						newGroups.Add(new Group(groupName, null, reader.Get<string>("ChatColor"), reader.Get<string>("Commands")) {
							Prefix = reader.Get<string>("Prefix"),
							Suffix = reader.Get<string>("Suffix"),
						});

						try
						{
							newGroupParents.Add(groupName, reader.Get<string>("Parent"));
						}
						catch (ArgumentException)
						{
							// Just in case somebody messed with the unique primary key.
							TShock.Log.ConsoleError("错误: 组 {0} 出现多次. 将保留当前组设定.");
							return;
						}
					}
				}

				try
				{
					// Get rid of deleted groups.
					for (int i = 0; i < groups.Count; i++)
						if (newGroups.All(g => g.Name != groups[i].Name))
							groups.RemoveAt(i--);

					// Apply changed group settings while keeping the current instances and add new groups.
					foreach (Group newGroup in newGroups)
					{
						Group currentGroup = groups.FirstOrDefault(g => g.Name == newGroup.Name);
						if (currentGroup != null)
							newGroup.AssignTo(currentGroup);
						else
							groups.Add(newGroup);
					}

					// Resolve parent groups.
					Debug.Assert(newGroups.Count == newGroupParents.Count);
					for (int i = 0; i < groups.Count; i++)
					{
						Group group = groups[i];
						string parentGroupName;
						if (!newGroupParents.TryGetValue(group.Name, out parentGroupName) || string.IsNullOrEmpty(parentGroupName))
							continue;

						group.Parent = groups.FirstOrDefault(g => g.Name == parentGroupName);
						if (group.Parent == null)
						{
							TShock.Log.ConsoleError(
								"错误: 组 {0} 引用了不存在的父组 {1} , 系统将忽略该错误设定.", 
								group.Name, parentGroupName);
						}
						else
						{
							if (group.Parent == group)
								TShock.Log.ConsoleInfo(
                                    "警告: 组 {0} 引用了本身为父组, 系统将忽略该错误设定.", group.Name);

							List<Group> groupChain = new List<Group> { group };
							Group checkingGroup = group;
							while (checkingGroup.Parent != null)
							{
								if (groupChain.Contains(checkingGroup.Parent))
								{
									TShock.Log.ConsoleError(
                                        "错误: 组 {0} 引用了导致循环的父组 {1} , 系统将忽略该错误设定.",
										checkingGroup.Name, checkingGroup.Parent.Name);

									checkingGroup.Parent = null;
									break;
								}
								groupChain.Add(checkingGroup);
								checkingGroup = checkingGroup.Parent;
							}
						}
					}
				}
				finally
				{
					if (!groups.Any(g => g is SuperAdminGroup))
						groups.Add(new SuperAdminGroup());
				}
			}
			catch (Exception ex)
			{
				TShock.Log.ConsoleError("重载组发生错误: " + ex);
			}
		}
	}

	[Serializable]
	public class GroupManagerException : Exception
	{
		public GroupManagerException(string message)
			: base(message)
		{
		}

		public GroupManagerException(string message, Exception inner)
			: base(message, inner)
		{
		}
	}

	[Serializable]
	public class GroupExistsException : GroupManagerException
	{
		public GroupExistsException(string name)
			: base("已经存在组 " + name + " .")
		{
		}
	}

	[Serializable]
	public class GroupNotExistException : GroupManagerException
	{
		public GroupNotExistException(string name)
			: base("不存在组 " + name + " .")
		{
		}
	}
} 
