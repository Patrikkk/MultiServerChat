﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;
using HttpServer;
using Newtonsoft.Json;
using Rests;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace MultiServerChat
{
    [ApiVersion(1, 22)]
    public class MultiServerChat : TerrariaPlugin
    {
        ConfigFile Config = new ConfigFile();
        private string savePath = "";

        public override string Author
        {
            get { return "Zack Piispanen"; }
        }

        public override string Description
        {
            get { return "Facilitate chat between servers."; }
        }

        public override string Name
        {
            get { return "Multiserver Chat"; }
        }

        public override Version Version
        {
            get { return new Version(1, 0, 0, 0); }
        }

        public MultiServerChat(Main game) : base(game)
        {
            savePath = Path.Combine(TShock.SavePath, "multiserverchat.json");
            Config = ConfigFile.Read(savePath);
            Config.Write(savePath);
            TShockAPI.Hooks.GeneralHooks.ReloadEvent += OnReload;
        }

        public override void Initialize()
        {
            PlayerHooks.PlayerChat += OnChat;
            ServerApi.Hooks.ServerJoin.Register(this, OnJoin, 10);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave, 10);
            TShock.RestApi.Register(new SecureRestCommand("/msc", RestChat, "msc.canchat"));
            TShock.RestApi.Register(new SecureRestCommand("/jl", RestJoinLeave, "msc.canchat"));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                PlayerHooks.PlayerChat -= OnChat;
                ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
            }
            base.Dispose(disposing);
        }

        private void OnReload(ReloadEventArgs args)
        {
            if (args.Player.Group.HasPermission("msc.reload"))
            {
                Config = ConfigFile.Read(savePath);
                Config.Write(savePath);
            }
        }

        private object RestChat(RestRequestArgs args)
        {
            if (!Config.DisplayChat)
                return new RestObject();

            if (!string.IsNullOrWhiteSpace(args.Parameters["message"]))
            {
                try
                {
                    var decoded = HttpUtility.UrlDecode(args.Parameters["message"]);
                    var bytes = Convert.FromBase64String(decoded);
                    var str = Encoding.UTF8.GetString(bytes);
                    var message = Message.FromJson(str);
                    TShock.Utils.Broadcast(message.Text, message.Red, message.Green, message.Blue);
                }
                catch (Exception)
                {
                }
            }

            return new RestObject();
        }

        private object RestJoinLeave(RestRequestArgs args)
        {
            if (!Config.DisplayJoinLeave)
                return new RestObject();

            if (!string.IsNullOrWhiteSpace(args.Parameters["message"]))
            {
                try
                {
                    var decoded = HttpUtility.UrlDecode(args.Parameters["message"]);
                    var bytes = Convert.FromBase64String(decoded);
                    var str = Encoding.UTF8.GetString(bytes);
                    var message = Message.FromJson(str);
                    TShock.Utils.Broadcast(message.Text, message.Red, message.Green, message.Blue);
                }
                catch (Exception)
                {
                }
            }

            return new RestObject();
        }

        private bool failure = false;

        private void OnChat(PlayerChatEventArgs args)
        {
            if (!Config.SendChat)
                return;
            if (args.Handled)
                return;

            ThreadPool.QueueUserWorkItem(f =>
                {
                    var message = new Message()
                    {
                        Text =
                            String.Format(Config.ChatFormat, TShock.Config.ServerName, args.TShockFormattedText),
                        Red = args.TextColor.R,
                        Green = args.TextColor.G,
                        Blue = args.TextColor.B
                    };

                    var bytes = Encoding.UTF8.GetBytes(message.ToString());
                    var base64 = Convert.ToBase64String(bytes);
                    var encoded = HttpUtility.UrlEncode(base64);
                    foreach (var url in Config.RestURLs)
                    {
                        var uri = String.Format("{0}/msc?message={1}&token={2}", url, encoded, Config.Token);

                        try
                        {
                            var request = (HttpWebRequest)WebRequest.Create(uri);
                            using (var res = request.GetResponse())
                            {
                            }
                            failure = false;
                        }
                        catch (Exception)
                        {
                            if (!failure)
                            {
                                TShock.Log.Error("Failed to make request to other server, server is down?");
                                failure = true;
                            }
                        }
                    }
                });
        }

        [Obsolete("Use OnChat(PlayerChatEventArgs args) instead.", true)]
        private void OnChat(ServerChatEventArgs args)
        {
            if (!Config.SendChat)
                return;

            var tsplr = TShock.Players[args.Who];
            if (tsplr == null)
            {
                return;
            }

            if ((args.Text.StartsWith(Commands.Specifier) || args.Text.StartsWith(Commands.SilentSpecifier))
                && args.Text.Length > 1)
            {
            }
            else
            {
                if (!tsplr.Group.HasPermission(Permissions.canchat))
                {
                    return;
                }

                if (tsplr.mute)
                {
                    return;
                }

                ThreadPool.QueueUserWorkItem(f =>
                {
                    var message = new Message()
                    {
                        Text =
                            String.Format(Config.ChatFormat, TShock.Config.ServerName, tsplr.Group.Name, tsplr.Group.Prefix, tsplr.Name,
                                tsplr.Group.Suffix,
                                args.Text),
                        Red = tsplr.Group.R,
                        Green = tsplr.Group.G,
                        Blue = tsplr.Group.B
                    };

                    var bytes = Encoding.UTF8.GetBytes(message.ToString());
                    var base64 = Convert.ToBase64String(bytes);
                    var encoded = HttpUtility.UrlEncode(base64);
                    foreach (var url in Config.RestURLs)
                    {
                        var uri = String.Format("{0}/msc?message={1}&token={2}", url, encoded, Config.Token);

                        try
                        {
                            var request = (HttpWebRequest)WebRequest.Create(uri);
                            using (var res = request.GetResponse())
                            {
                            }
                            failure = false;
                        }
                        catch (Exception)
                        {
                            if (!failure)
                            {
                                TShock.Log.Error("Failed to make request to other server, server is down?");
                                failure = true;
                            }
                        }
                    }
                });
            }
        }

        private void OnJoin(JoinEventArgs args)
        {
            if (!Config.DisplayJoinLeave)
                return;

            var tsplr = TShock.Players[args.Who];
            if (tsplr == null)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(f =>
            {
                var message = new Message()
                {
                    Text =
                        String.Format(Config.JoinFormat, TShock.Config.ServerName, tsplr.Name),
                    Red = Color.Yellow.R,
                    Green = Color.Yellow.G,
                    Blue = Color.Yellow.B
                };

                var bytes = Encoding.UTF8.GetBytes(message.ToString());
                var base64 = Convert.ToBase64String(bytes);
                var encoded = HttpUtility.UrlEncode(base64);
                foreach (var url in Config.RestURLs)
                {
                    var uri = String.Format("{0}/jl?message={1}&token={2}", url, encoded, Config.Token);

                    try
                    {
                        var request = (HttpWebRequest)WebRequest.Create(uri);
                        using (var res = request.GetResponse())
                        {
                        }
                        failure = false;
                    }
                    catch (Exception)
                    {
                        if (!failure)
                        {
                            TShock.Log.Error("Failed to make request to other server, server is down?");
                            failure = true;
                        }
                    }
                }
            });
        }

        private void OnLeave(LeaveEventArgs args)
        {
            if (!Config.DisplayJoinLeave)
                return;

            var tsplr = TShock.Players[args.Who];
            if (tsplr == null)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(f =>
            {
                var message = new Message()
                {
                    Text =
                        String.Format(Config.LeaveFormat, TShock.Config.ServerName, tsplr.Name),
                    Red = Color.Yellow.R,
                    Green = Color.Yellow.G,
                    Blue = Color.Yellow.B
                };

                var bytes = Encoding.UTF8.GetBytes(message.ToString());
                var base64 = Convert.ToBase64String(bytes);
                var encoded = HttpUtility.UrlEncode(base64);
                foreach (var url in Config.RestURLs)
                {
                    var uri = String.Format("{0}/jl?message={1}&token={2}", url, encoded, Config.Token);

                    try
                    {
                        var request = (HttpWebRequest)WebRequest.Create(uri);
                        using (var res = request.GetResponse())
                        {
                        }
                        failure = false;
                    }
                    catch (Exception)
                    {
                        if (!failure)
                        {
                            TShock.Log.Error("Failed to make request to other server, server is down?");
                            failure = true;
                        }
                    }
                }
            });
        }
    }

	public class Message
	{
		public string Text { get; set; }
		public byte Red { get; set; }
		public byte Green { get; set; }
		public byte Blue { get; set; }

		public override string ToString()
		{
			return JsonConvert.SerializeObject(this, Formatting.Indented);
		}

		public static Message FromJson(string js)
		{
			return JsonConvert.DeserializeObject<Message>(js);
		}
	}
}
