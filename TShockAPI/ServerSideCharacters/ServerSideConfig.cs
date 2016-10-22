/*
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

﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
/*
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

using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace TShockAPI.ServerSideCharacters
{
	public class ServerSideConfig
	{
		[Description("打开云端存档(SSC)模式, 开启后客户端无法保存! 实验性!!!!!")]
		[JsonProperty("启用")]
		public bool Enabled = false;

		[Description("SSC保存的间隔, 单位为分钟.")]
		[JsonProperty("云存档保存间隔")]
		public int ServerSideCharacterSave = 5;

		[Description("单位为毫秒的时间间隔, 防止玩家在登录后丢弃物品.")]
		[JsonProperty("登入后丢物间隔")]
		public int LogonDiscardThreshold = 250;

		[Description("新玩家SSC存档的生命值.")]
		[JsonProperty("初始生命值")]
		public int StartingHealth = 100;

		[Description("新玩家SSC存档的魔法值.")]
		[JsonProperty("初始魔法值")]
		public int StartingMana = 20;

		[Description("新玩家SSC存档的初始物品.")]
		[JsonProperty("初始物品")]
		public List<NetItem> StartingInventory = new List<NetItem>();

		public static ServerSideConfig Read(string path)
		{
			using (var reader = new StreamReader(path))
			{
				string txt = reader.ReadToEnd();
				var config = JsonConvert.DeserializeObject<ServerSideConfig>(txt);
				return config;
			}
		}

		public void Write(string path)
		{
			using (var writer = new StreamWriter(path))
			{
				writer.Write(JsonConvert.SerializeObject(this, Formatting.Indented));
			}
		}

		/// <summary>
		/// Dumps all configuration options to a text file in Markdown format
		/// </summary>
		public static void DumpDescriptions()
		{
			var sb = new StringBuilder();
			var defaults = new ServerSideConfig();

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
				sb.AppendLine("字段种类: {0}  ".SFormat(type));
				sb.AppendLine("说明: {0}  ".SFormat(desc));
				sb.AppendLine("默认值: \"{0}\"  ".SFormat(def));
				sb.AppendLine();
			}

			File.WriteAllText("云存档配置说明.txt", sb.ToString());
		}
	}
}
