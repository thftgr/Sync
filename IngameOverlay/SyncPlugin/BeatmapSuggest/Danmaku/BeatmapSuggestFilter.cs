﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Sync.MessageFilter;
using Sync.Source;
using Sync.Plugins;
using System.Threading;
using System.Net;
using Sync;
using System.IO;
using Sync.Tools;
using static BeatmapSuggest.DefaultLanguage;

namespace BeatmapSuggest.Danmaku
{
    class BeatmapSuggestFilter : IFilter, ISourceDanmaku
    {
        private MessageDispatcher msgManager = null;

        private const string suggestCommand = "?suggest";

        Regex simple_command_regex = new Regex(@"^((http(s)?://)?(osu\.ppy\.sh/)?)?(\w)/?(\d+)$", RegexOptions.IgnoreCase| RegexOptions.Multiline);
        
        internal BeatmapDownloadScheduler Scheduler { get; set; }

        internal bool EnableInsoMirrorLink { get; set; }
        internal string OsuApiKey { get; set; }

        const int timeout = 6000;//ms
        
        public void onMsg(ref IMessageBase msg)
        {
            if (msgManager == null)
                return; //没完全初始化，发送不了信息

            string message = msg.Message.RawText.Trim();
            string user = msg.User.RawText;

            if (message.StartsWith(suggestCommand))
            {
                msg.Cancel = true;
                TryParseNormalCommand(msg.User.RawText, message);
                return;
            }

            Match match = simple_command_regex.Match(message);
            if (match.Success)
            {
                int id = int.Parse(match.Groups[6].ToString());
                char isSetID=  match.Groups[5].ToString()[0];

                if (isSetID=='s'||isSetID=='b')
                {
                    SendSuggestMessage(id, user, isSetID == 's');
                    msg.Cancel = true;
                }
            }
        }

        private void TryParseNormalCommand(string user_name,string message)
        {
            int id;

            string[] param = message.Split(' ');

            if (param.Length > 2)
            {
                if (!Int32.TryParse(param[2], out id))
                {
                    IO.CurrentIO.WriteColor(string.Format(LANG_INVAILD_ID, message), ConsoleColor.Red);
                }

                switch (param[1])
                {
                    case "-b":
                        SendSuggestMessage(id, user_name, false);
                        break;
                    case "-s":
                        SendSuggestMessage(id, user_name);
                        break;
                    default:
                        IO.CurrentIO.WriteColor(string.Format(LANG_UNKOWN_PARAM, param[1]), ConsoleColor.Red);
                        break;
                }
            }
            else if (Int32.TryParse(message.Substring(suggestCommand.Length).Trim(), out id))
            {
                SendSuggestMessage(id, user_name);
            }
        }

        private async void SendSuggestMessage(int id, string userName, bool isSetId = true)
        {
            //check if users provide their api key.
            if (String.IsNullOrWhiteSpace(OsuApiKey))
            {
                return;
            }

            string[] beatmapInfo = null;
            try
            {
                beatmapInfo = await GetBeatmapInfo(id,isSetId);

                if (beatmapInfo == null)
                    throw new Exception(string.Format(LANG_GET_BEATMAP_FAILED,id, LANG_ERROR_INFO_IMCOMPLETE));
            }
            catch (Exception e)
            {
                IO.CurrentIO.WriteColor(string.Format(LANG_GET_BEATMAP_FAILED, id, e.Message),ConsoleColor.Red);
                return;
            }

            Scheduler.Push(id, isSetId, $"{beatmapInfo[3]} - {beatmapInfo[2]}");

            string message = string.Format(LANG_SUGGEST_MEG,userName,GetLink(id,isSetId),$"{beatmapInfo[3]} - {beatmapInfo[2]}[{beatmapInfo[4]}]",GetDownloadLink(int.Parse(beatmapInfo[1])),GetMirrorDownloadLink(int.Parse(beatmapInfo[0])));
            msgManager.RaiseMessage<ISourceClient>(new IRCMessage(string.Empty, message));
        }

        private async Task<string[]> GetBeatmapInfo(int id, bool isSetId)
        {
            var timeoutCancellation = new CancellationTokenSource();

            var task = new Task<string[]>(() =>
            {
                string uri = @"https://osu.ppy.sh/api/get_beatmaps?" +
                $@"k={OsuApiKey}&{(isSetId ? "s" : "b")}={id}&limit=1";

                HttpWebRequest request = HttpWebRequest.Create(uri) as HttpWebRequest;
                request.Method = "GET";

                try
                {
                    var response = (HttpWebResponse)request.GetResponse();
                    var stream = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
                    string data = stream.ReadToEnd();

                    string[] result = new string[5];
                    result[0] = GetJSONValue(ref data, "beatmapset_id"); //Beatmap SetId
                    result[1] = GetJSONValue(ref data, "beatmap_id"); //Beatmap Id
                    result[2] = GetJSONValue(ref data, "title");//Title
                    result[3] = GetJSONValue(ref data, "artist");//Artist
                    result[4] = GetJSONValue(ref data, "version"); //diffName

                    for (int i = 0; i < result.Length; i++)
                    {
                        if (result[i] == null)
                            return null;
                    }

                    return result;
                }
                catch (Exception e)
                {
                    throw e;
                }
            }, timeoutCancellation.Token);

            timeoutCancellation.Token.Register(() =>
            {
                if (!task.IsCompleted || task.IsFaulted)
                    Console.WriteLine(LANG_GET_BEATMAP_TIME_OUT, id, task.Status.ToString());
            });

            task.Start();

            timeoutCancellation.CancelAfter(timeout);

            return await task;
        }

        private string GetJSONValue(ref string text, string key)
        {
            var result = Regex.Match(text, $"{key}\":\"(.+?)\"");

            if (!result.Success)
                return null;

            return result.Groups[1].Value;
        }

        private string GetLink(int id, bool isSetId)
        {
            return (isSetId ? "https://osu.ppy.sh/s/" : "https://osu.ppy.sh/b/") + id;
        }

        private string GetDownloadLink(int id)
        {
            return "https://osu.ppy.sh/b/" + id;
        }

        private string GetMirrorDownloadLink(int beatmapSetId)
        {
            if (EnableInsoMirrorLink)
            {
                // Defualt
                return $"http://osu.uu.gl/s/{beatmapSetId}";
            }
            else
            {
                //Inso Link
                return "http://inso.link/yukiho/?" /*+ (isSetId ? $"m" : $"b") */+ $"m={beatmapSetId}";
            }
        }

        public void SetFilterManager(MessageDispatcher manager)
        {
            msgManager = manager;
        }

        public void Dispose()
        {
            //nothing to do
        }
    }
}
