﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sync.Tools;

namespace RecentlyUserQuery
{
    public class DefaultLanguage:I18nProvider
    {
        public static LanguageElement LANG_HELP = @"\n以下指令cmd端和osu!irc端规则通用(在osu!irc端用请在开头加""?"")\nrecently --status |获取当前消息记录器的状态信息(osu!irc不可用)\nrecently --u <userName> |获取用户<userName>的历史消息(不建议在osu!irc用)\nrecently --i <userId> |获取用户<userId>的历史消息(不建议在osu!irc用)\nrecently |获取近期用户的名字和id,id可以用来执行""?ban --i""等指令(osu!irc适用)\nrecently --disable |关闭记录器所有功能并清除数据(osu!irc适用)\nrecently --start |重新开始记录(osu!irc适用)\nrecently --realloc <count> |重新分配记录器储存记录的容量(osu!irc适用)\nrecently --recently |获取近期用户和id\n";

        public static LanguageElement LANG_MSG_STATUS = "MessageRecord status: {0} | recordCount/Capacity : {1}/{2}";
        public static LanguageElement LANG_RUNNING = "running";
        public static LanguageElement LANG_STOP = "stopped";

        public static LanguageElement LANG_MSG_DISABLE = "消息记录器已禁用，数据已清除";
        public static LanguageElement LANG_MSG_START = "消息记录器开启";
        public static LanguageElement LANG_MSG_REALLOC_ERR = "MessageRecord: 错误的指令";
        public static LanguageElement LANG_MSG_REALLOC = "消息记录器现在可记录 {0} 条历史记录";

        public static LanguageElement LANG_MSG_NOTIMPLENT = "咕咕咕~";
        public static LanguageElement LANG_MSG_UNKNOWNCOMMAND = "未知命令";
    }
}
