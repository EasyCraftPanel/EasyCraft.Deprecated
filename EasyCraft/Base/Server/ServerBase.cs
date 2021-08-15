﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EasyCraft.Base.Core;
using EasyCraft.HttpServer.Api;
using EasyCraft.Utils;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace EasyCraft.Base.Server
{
    [JsonObject(MemberSerialization.OptOut)]
    public class ServerBase
    {
        [JsonProperty("baseInfo")] public ServerBaseInfo BaseInfo;
        [JsonProperty("id")] public int Id;
        [JsonProperty("startInfo")] public ServerStartInfo StartInfo;
        [JsonProperty("statusInfo")] public ServerStatusInfo StatusInfo;

        [JsonIgnore] public string ServerDir => Directory.GetCurrentDirectory() + "/data/servers/" + Id + "/";
        [JsonIgnore] public CoreBase Core => CoreManager.Cores[StartInfo.Core];

        public ServerBase(SqliteDataReader reader)
        {
            Id = reader.GetInt32(0);
            BaseInfo = ServerBaseInfo.CreateFromSqlReader(reader);
            StartInfo = ServerStartInfo.CreateFromSqliteById(reader.GetInt32(0));
            StatusInfo = new();
        }

        public void LoadConfigFile()
        {
            foreach (var kvConfigInfo in CoreManager.Cores[StartInfo.Core].ConfigInfo)
            {
                if (!File.Exists(ServerDir + "/" + kvConfigInfo.Key) && kvConfigInfo.Value.Required)
                    File.Create(ServerDir + "/" + kvConfigInfo.Key);
                else
                    continue;
                WriteConfigFile(kvConfigInfo.Key);
            }
        }

        public string PhraseServerVar(string origin)
        {
            return origin
                .Replace("{{SERVERID}}", Id.ToString())
                .Replace("{{SERVERDIR}}", ServerDir)
                .Replace("{{CORE}}", StartInfo.Core)
                .Replace("{{PORT}}", BaseInfo.Port.ToString())
                .Replace("{{PLAYER}}", BaseInfo.Player.ToString())
                .Replace("{{WORLD}}", StartInfo.World);
        }

        public void WriteConfigFile(string filename, Dictionary<string, string> vals = null)
        {
            StringBuilder sb = new();
            var configInfo = Core.ConfigInfo[filename];
            if (configInfo == null)
                return;
            if (vals == null)
            {
                vals = new Dictionary<string, string>();
            }

            switch (configInfo.Type)
            {
                case "properties":
                    var content = File.ReadLines(ServerDir + "/" + filename);
                    foreach (string s in content)
                    {
                        if (s.StartsWith("#"))
                        {
                            sb.AppendLine(s);
                            continue;
                        }

                        var kvp = s.Split('=').Select(t => t.Trim()).ToArray();
                        var knownItem = configInfo.Known.FirstOrDefault(t => t.Key == kvp[0]);
                        if (knownItem != null)
                        {
                            string value = "";
                            if (vals.ContainsKey(kvp[0]))
                            {
                                value = vals[kvp[0]];
                            }

                            if (knownItem.Force)
                            {
                                value = knownItem.Value;
                            }

                            sb.AppendLine(kvp[0] + "=" + value);
                        }
                        else
                        {
                            sb.AppendLine(s);
                            // ReSharper disable once RedundantJumpStatement
                            continue;
                        }
                    }

                    break;
            }

            File.WriteAllText(ServerDir + "/" + filename, sb.ToString());
        }


        public async Task<ServerStartException> Start()
        {
            // 开启服务器
            // 首先检查是否到期
            if (BaseInfo.Expired)
            {
                StatusInfo.OnConsoleOutput("服务器已于 {0} 到期.".Translate(BaseInfo.ExpireTime.ToString("s")));
                return new ServerStartException
                {
                    Code = (int)ApiReturnCode.ServerExpired,
                    Message = "服务器已于 {0} 到期.".Translate(BaseInfo.ExpireTime.ToString("s"))
                };
            }

            // 再检查开服核心是否存在
            if (!CoreManager.Cores.ContainsKey(StartInfo.Core))
            {
                StatusInfo.OnConsoleOutput("服务器核心 {0} 不存在.".Translate(StartInfo.Core));
                return new ServerStartException
                {
                    Code = (int)ApiReturnCode.CoreNotFound,
                    Message = "服务器核心 {0} 不存在.".Translate(StartInfo.Core)
                };
            }

            // TODO: 再进行开服器校验
            // Write Your Here

            // 在广播到插件 - 此处事件广播位点可以提出更改
            var ret = (await PluginBase.PluginController.BroadcastEventAsync("OnServerStart", new object[] { Id }))
                .Where(t => !(bool)t.Value).ToArray();
            if (ret.Length != 0)
            {
                StatusInfo.OnConsoleOutput("服务器被插件 {0} 拒绝开启.".Translate(ret[0].Key));
                return new ServerStartException
                {
                    Code = (int)ApiReturnCode.PluginReject,
                    Message = "服务器被插件 {0} 拒绝开启.".Translate(ret[0].Key)
                };
            }

            // 检查结束, 先进行开服前准备

            // 先检查是否更换核心
            if (StartInfo.LastCore != StartInfo.Core)
            {
                StatusInfo.OnConsoleOutput("你的核心已更换, 正在加载核心文件".Translate(),
                    false);
                Utils.Utils.DirectoryCopy(Directory.GetCurrentDirectory() + "/data/cores/" + StartInfo.Core + "/files",
                    ServerDir);
            }

            StatusInfo.OnConsoleOutput("正在加载配置项".Translate(),
                false);
            LoadConfigFile();
            
            StatusInfo.OnConsoleOutput("正在尝试调用开服器".Translate(),
                false);
            // TODO: 调用开服器
            
            return new ServerStartException()
            {
                Code = 200,
                Message = "成功开服".Translate()
            };
        }
    }
}