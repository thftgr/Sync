﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sync.Source;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using Sync.Tools;
using System.Timers;
using Sync.MessageFilter;

namespace DefaultPlugin.Sources.Twitch
{
    public class Twitch : SendableSource, IConfigurable
    {
        public const string SOURCE_NAME = "Twitch";
        public const string SOURCE_AUTHOR = "DarkProjector";

        static Regex parseRawMessageRegex = new Regex(@":(?<UserName>.+)!.+(PRIVMSG\s*#.+:)(?<Message>.+)");

        TwitchIRCIO currentIRCIO;

        int prev_ViewersCount = int.MinValue;

        int onlineViewersCountInv = 6;

        int viewersUpdateInterval = 60000;

        public Timer viewerUpdateTimer;

        string oauth="", clientId="", channelName="";

        bool isUsingDefaultChannelID = true;

        public Twitch() : base(SOURCE_NAME, SOURCE_AUTHOR)
        {
        }

        public string OAuth { get { return oauth; } set { oauth = value; } }
        public string ClientID { get { return clientId; } set { clientId = value; } }
        public string ChannelName { get { return channelName; } set { channelName = value; } }
        public bool IsUsingDefaultChannelID { get { return isUsingDefaultChannelID; } set { isUsingDefaultChannelID = value; } }

        public ConfigurationElement HostChannelName { get; set; } = "";
        public ConfigurationElement DefaultClientID { get; set; } = "";
        public ConfigurationElement CurrentClientID { get; set; } = "";
        public ConfigurationElement IsUsingCurrentClientID { get; set; } = "1";
        public ConfigurationElement SOAuth { get; set; } = "";
        #region 接口实现

        public void LoadConfig()
        {
            ClientID = IsUsingCurrentClientID == "1" ? CurrentClientID : DefaultClientID;
            IsUsingDefaultChannelID = !(IsUsingCurrentClientID=="1");
            OAuth = SOAuth;
            ChannelName = HostChannelName;
        }

        public void SaveConfig()
        {
            CurrentClientID = (ClientID == DefaultClientID ? "" : ClientID);
            HostChannelName = ChannelName;
            SOAuth = OAuth;
            IsUsingCurrentClientID = IsUsingDefaultChannelID ? "1" : "0";
            DefaultClientID = "esmhw2lcvrgtqw545ourqjwlg7twee";
        }

        public void Connect(string roomName)
        {
            if (Status==SourceStatus.CONNECTING)
            {
                Disconnect();
            }

            channelName = roomName;

            if (channelName.Length == 0)
            {
                IO.CurrentIO.WriteColor("频道名不能为空!",ConsoleColor.Red);
                return;
            }

            while (oauth.Length==0)
            {
                var result = RequestSetup();

                if (result == false)
                    return;
            }

            SaveConfig();

            if (currentIRCIO != null)
            {
                currentIRCIO.DisConnect();

                currentIRCIO.OnRecieveRawMessage -= onRecieveRawMessage;

                currentIRCIO = null;
            }
            try
            {
                currentIRCIO = new TwitchIRCIO(roomName)
                {
                    OAuth = oauth,
                    ChannelName = channelName,
                    ClientID = clientId
                };
                currentIRCIO.Connect();

                currentIRCIO.OnRecieveRawMessage += onRecieveRawMessage;

                RaiseEvent(new BaseStatusEvent(SourceStatus.CONNECTED_WORKING));
                UpdateChannelViewersCount();

                viewerUpdateTimer = new Timer(viewersUpdateInterval);
                viewerUpdateTimer.Elapsed += (z,zz) => UpdateChannelViewersCount();
                viewerUpdateTimer.Start();

                Status = SourceStatus.CONNECTED_WORKING;
                SendStatus = true;
            }
            catch (Exception e)
            {
                IO.CurrentIO.WriteColor("twitch connect error!" + e.Message, ConsoleColor.Red);

                Status = SourceStatus.USER_DISCONNECTED;
            }
        }

        public override void Disconnect()
        {
            currentIRCIO?.DisConnect();
            currentIRCIO = null;
            RaiseEvent(new BaseStatusEvent(SourceStatus.USER_DISCONNECTED));

            viewerUpdateTimer?.Stop();
            viewerUpdateTimer?.Dispose();

            Status = SourceStatus.USER_DISCONNECTED;
        }

        public bool Stauts()
        {
            return currentIRCIO != null && currentIRCIO.IsConnected;
        } 

        public override void Send(IMessageBase message)
        {
            currentIRCIO?.SendMessage(message.Message);
        }

        #endregion  

        public void onRecieveRawMessage(string rawMessage)
        {
            var result=parseRawMessageRegex.Match(rawMessage);

            if (!result.Success)
                return;

            string userName = result.Groups["UserName"].Value;
            string message = result.Groups["Message"].Value;

            base.RaiseEvent<IBaseDanmakuEvent>(new BaseDanmakuEvent(message, userName,DateTime.Now.ToString()));
        }

        /// <summary>
        /// 更新观众人数并汇报
        /// </summary>
        public async void UpdateChannelViewersCount()
        {
            //currentIRCIO?.SendRawMessage(@"NAMES");
            int nowViewersCount = await Task.Run(() =>
            {
                string uri = $"https://api.twitch.tv/kraken/streams/{currentIRCIO.ChannelName}&client_id={currentIRCIO.ClientID}";

                HttpWebRequest request = HttpWebRequest.Create(uri) as HttpWebRequest;
                request.Method = "GET";

                try
                {
                    var response = (HttpWebResponse)request.GetResponse();
                    StreamReader stream;
                    using (stream = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    {
                        string data = stream.ReadToEnd();
                        string viewers = GetJSONValue(ref data, "viewers");
                        return int.Parse(viewers);
                    }
                }
                catch (Exception)
                {
                    return prev_ViewersCount;//就当做啥事都没发生(
                }
            });

            if (Math.Abs(nowViewersCount - prev_ViewersCount) > onlineViewersCountInv)
            {
                RaiseEvent(new BaseOnlineCountEvent() { Count = nowViewersCount });
                prev_ViewersCount = nowViewersCount;
            }
        }

        private bool RequestSetup()
        {
            TwitchAuthenticationDialog AuthDialog = new TwitchAuthenticationDialog(this);
            var result = AuthDialog.ShowDialog();
            return result != System.Windows.Forms.DialogResult.Cancel;
        }

        private string GetJSONValue(ref string text, string key)
        {
            var result = Regex.Match(text, $"{key}\":\"(.+?)\"");

            if (!result.Success)
                return null;

            return result.Groups[1].Value;
        }

        public override void Connect() => Connect(channelName);

        public override string ToString()
        {
            return SOURCE_NAME;
        }

        public void onConfigurationLoad()
        {
            LoadConfig();
            LiveID = HostChannelName;
        }

        public void onConfigurationSave()
        {
            SaveConfig();
            HostChannelName = LiveID;
        }

        public override void Login(string user, string password)
        {
            RequestSetup();
        }

        public void onConfigurationReload()
        {
            onConfigurationLoad();
        }
    }
}
