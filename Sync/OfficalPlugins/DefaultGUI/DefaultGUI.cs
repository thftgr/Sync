﻿using Sync.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sync;
using System.Threading;
using System.Windows.Forms;
using Sync.Command;
using static DefaultGUI.Language;
using Sync.Source;
using Sync.Tools;

namespace DefaultGUI
{
    [Serializable]
    [SyncPluginID("7b94f736-e1bd-4dc8-9706-2bf6d0dedbf9", "2.16.0")]
    [SyncPluginDependency("c620c63a-32b6-4281-87a9-b7da62be0215", Version = "^2.15.0", Require = true)]
    public class DefaultGUI : Plugin
    {
        public const string PLUGIN_NAME = "Default GUI";
        public const string PLUGIN_AUTHOR = "Deliay";

        public frmDefault frmUI;
        public Thread frmThread;

        public static bool InputFlag = true;

        public static SyncHost hoster = null;

        public DefaultGUI() : base(PLUGIN_NAME, PLUGIN_AUTHOR)
        {

        }

        public override void OnEnable()
        {
            I18n.Instance.ApplyLanguage(new Language());
            frmUI = new frmDefault();
            frmThread = new Thread(ShowForm);
            frmThread.SetApartmentState(ApartmentState.STA);
            frmThread.Name = "STAThreadForm";

            EventBus.BindEvent<PluginEvents.InitCommandEvent>(evt => {
                evt.Commands.Dispatch.bind("gui", (t) =>
                {
                    frmUI.ShowMe();
                    return true;
                }, UI_DISPLAY);
            });

            EventBus.BindEvent<PluginEvents.LoadCompleteEvent>(evt => 
            {
                hoster = evt.Host;

                SourceEvents.Instance.BindEvent<StartSourceEvent>(e => frmUI.UpdateStautsAuto());
                SourceEvents.Instance.BindEvent<StopSyncEvent>(e => frmUI.UpdateStautsAuto());

            });

            EventBus.BindEvent<PluginEvents.ConfigurationChange>((e) => {
                frmUI.ready();
            });

            EventBus.BindEvent<PluginEvents.ProgramReadyEvent>(evt => 
            {
                frmThread.Start();
                IO.SetIO(frmUI);
            });

            IO.CurrentIO.Write("Default GUI by Deliay : )");

        }

        [STAThread]
        public void ShowForm()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(frmUI);
            frmUI.ready();
            
        }
    }
}
