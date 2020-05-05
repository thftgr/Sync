﻿using OsuRTDataProvider.BeatmapInfo;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace OsuRTDataProvider.Memory
{
    internal class OsuBeatmapFinder : OsuFinderBase
    {
        private static readonly string s_beatmap_pattern = "\xDB\x5D\xE8\x8B\x45\xE8\xA3\x0\x0\x0\x0\x8B\x35\x0\x0\x0\x0\x85\xF6";

        private static readonly string s_beatmap_mask = "xxxxxxx????xx????xx";

        private static readonly int s_beatmap_offset = 0xc4;
        private static readonly int s_beatmap_set_offset = 0xc8;

        private static readonly int s_beatmap_folder_offset = 0x74;
        private static readonly int s_beatmap_filename_offset = 0x8c;

        private int BeatmapAddressOffset { get; }
        private int BeatmapSetAddressOffset { get; }
        private int BeatmapFolderAddressOffset { get; }
        private int BeatmapFileNameAddressOffset { get; }

        private const int MAX_RETRY_COUNT = 10;

        private IntPtr m_beatmap_address;

        public OsuBeatmapFinder(Process osu) : base(osu)
        {
            BeatmapAddressOffset = s_beatmap_offset;
            BeatmapSetAddressOffset = s_beatmap_set_offset;
            BeatmapFolderAddressOffset = s_beatmap_folder_offset;
            BeatmapFileNameAddressOffset = s_beatmap_filename_offset;

            //兼容20190816以前的屙屎
            var cmp_ver20190816 = Utils.ConvertVersionStringToValue("20190816");

            Logger.Info($"osu!version compatible condition: {Setting.CurrentOsuVersionValue.ToString(CultureInfo.InvariantCulture)} < {cmp_ver20190816} ?");

            if (Setting.CurrentOsuVersionValue < cmp_ver20190816)
            {
                Logger.Warn("BeatmapAddressOffset and others =-4 for osu!ver < 20190816");
                BeatmapAddressOffset -= 4;
                BeatmapSetAddressOffset -= 4;
                BeatmapFolderAddressOffset -= 4;
                BeatmapFileNameAddressOffset -= 4;
            }
        }

        public override bool TryInit()
        {
            bool success = false;
            SigScan.Reload();
            {
                //Find Beatmap ID Address
                m_beatmap_address = SigScan.FindPattern(StringToByte(s_beatmap_pattern), s_beatmap_mask, 13);
                LogHelper.LogToFile($"Beatmap Base Address (0):0x{(int)m_beatmap_address:X8}");

                success = TryReadIntPtrFromMemory(m_beatmap_address, out m_beatmap_address);
                LogHelper.LogToFile($"Beatmap Base Address (1):0x{(int)m_beatmap_address:X8}");
            }
            SigScan.ResetRegion();

            if (m_beatmap_address == IntPtr.Zero)
                success = false;

            return success;
        }

        public Beatmap GetCurrentBeatmap(int osu_id)
        {
            TryReadIntPtrFromMemory(m_beatmap_address, out IntPtr cur_beatmap_address);
            TryReadIntFromMemory(cur_beatmap_address + BeatmapAddressOffset, out int id);
            TryReadIntFromMemory(cur_beatmap_address + BeatmapSetAddressOffset, out int set_id);

            string filename = GetCurrentBeatmapFilename();
            string folder = GetCurrentBeatmapFolder();

            Beatmap beatmap = Beatmap.Empty;

            try
            {
                if (!(string.IsNullOrWhiteSpace(filename) || string.IsNullOrWhiteSpace(folder)))
                {
                    string folder_full = Path.Combine(Setting.SongsPath, folder);
                    string filename_full = Path.Combine(folder_full, filename);
                    using (var fs = File.OpenRead(filename_full))
                    {
                        beatmap = new Beatmap(osu_id, set_id, id, fs);
                    }
                }
            }
            catch (Exception e)
            {
                StringBuilder sb = new StringBuilder();

                sb.AppendLine("------------- ORTDP(Exception)--------------- ");
                sb.AppendLine(e.ToString());

                if (Setting.DebugMode)
                {
                    sb.AppendLine("--------------ORTDP(Detail)-----------------");
                    sb.AppendLine($"Songs Path:{Setting.SongsPath}");
                    sb.AppendLine($"Filename:{filename}");
                    sb.AppendLine($"Folder:{folder}");
                    sb.AppendLine($"BeatmapID:{id}");
                    sb.AppendLine($"BeatmapSetID:{set_id}");
                    sb.AppendLine("--------------------------------------------");
                }

                Logger.Warn(sb.ToString());
            }

            return beatmap;
        }

        #region Beatmap Info

        private string GetCurrentBeatmapFolder()
        {
            TryReadIntPtrFromMemory(m_beatmap_address, out var cur_beatmap_address);
            bool success = TryReadStringFromMemory(cur_beatmap_address + BeatmapFolderAddressOffset, out string str);
            if (!success) return "";
            return str;
        }

        private string GetCurrentBeatmapFilename()
        {
            TryReadIntPtrFromMemory(m_beatmap_address, out var cur_beatmap_address);
            bool success = TryReadStringFromMemory(cur_beatmap_address + BeatmapFileNameAddressOffset, out string str);
            if (!success) return "";
            return str;
        }

        #endregion Beatmap Info
    }
}