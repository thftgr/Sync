﻿using System;
using Sync.Source;
using System.Threading.Tasks;
using Sync.Tools;
using Sync.MessageFilter;
using DefaultPlugin.Sources.BiliBili.BiliBili_dm;

namespace DefaultPlugin.Sources.BiliBili
{
    /// <summary>
    /// BiliBili Live的同步源类
    /// </summary>
    class BiliBili : SendableSource , IConfigurable
    {
        public const string SOURCE_NAME = "Bilibili";
        public const string SOURCE_AUTHOR = "Sender: Deliay, Receive: copyliu";
        DanmakuLoader client = new DanmakuLoader();
        BiliBiliSender sender;
        private bool isConnected = false;

        public static ConfigurationElement Cookies { get; set; } = "";
        public static ConfigurationElement RoomID { get; set; } = "";

        public BiliBili() : base(SOURCE_NAME, SOURCE_AUTHOR)
        {
            sender = new BiliBiliSender("", "");
        }

        public override void Send(IMessageBase Message)
        {
            if(sender.loginStauts)
            {
                sender.send(Message.Message);
            }
        }

        public override void Connect()
        {
            client.ReceivedDanmaku += Dl_ReceivedDanmaku;
            client.ReceivedRoomCount += Dl_ReceivedRoomCount;
            client.Disconnected += Dl_Disconnected;
            Task<bool> task = client.ConnectAsync(int.Parse(LiveID));
            if(task.Status == TaskStatus.Running)
            {
                isConnected = true;
            }
            Status = SourceStatus.CONNECTED_WORKING;
            SendStatus = true;
            RaiseEvent(new BaseStatusEvent(SourceStatus.CONNECTED_WORKING));
        }

        public override void Disconnect()
        {
            client.Disconnect();
            //
            Status = SourceStatus.USER_DISCONNECTED;
            client.ReceivedDanmaku -= Dl_ReceivedDanmaku;
            client.ReceivedRoomCount -= Dl_ReceivedRoomCount;
            client.Disconnected -= Dl_Disconnected;
        }
        private void Dl_Disconnected(object sender, DisconnectEvtArgs args)
        {
            isConnected = false;
            RaiseEvent(new BaseStatusEvent(SourceStatus.REMOTE_DISCONNECTED));
        }

        private void Dl_ReceivedRoomCount(object sender, ReceivedRoomCountArgs e)
        {
            base.RaiseEvent<BaseOnlineCountEvent>(
                new BaseOnlineCountEvent() {
                        Count = (int)e.UserCount
                });
        }

        private void Dl_ReceivedDanmaku(object sender, ReceivedDanmakuArgs e)
        {

            if (e.Danmaku.MsgType == MsgTypeEnum.Comment)
            {
                base.RaiseEvent<IBaseDanmakuEvent>(new BiliBiliDanmuku(e.Danmaku));
            }
            else if (e.Danmaku.MsgType == MsgTypeEnum.GiftSend)
            {
                base.RaiseEvent<IBaseGiftEvent>(new BiliBiliGift(e.Danmaku));
            }
        }

        public bool Stauts()
        {
            return isConnected;
        }

        public override void Login(string user, string password)
        {
            sender = new BiliBiliSender(user, password);
            sender.login();
        }

        public Type getSourceType()
        {
            return typeof(BiliBili);
        }

        public bool LoginStauts()
        {
            return sender.loginStauts;
        }

        public string getSourceName()
        {
            return SOURCE_NAME;
        }

        public string getSourceAuthor()
        {
            return SOURCE_AUTHOR;
        }

        public override string ToString()
        {
            return SOURCE_NAME;
        }

        public void onConfigurationLoad()
        {
            if (Cookies.ToString().Length > 0)
            {
                sender.loginStauts = true;
            }

            LiveID = RoomID;
        }

        public void onConfigurationSave()
        {
            RoomID = LiveID;
        }

        public void onConfigurationReload()
        {
            onConfigurationLoad();
        }
    }
}