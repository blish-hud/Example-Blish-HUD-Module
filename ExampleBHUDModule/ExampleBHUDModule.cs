using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Modules;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ExampleBHUDModule {

    [Export(typeof(ExternalModule))]
    public class ExampleBHUDModule : Blish_HUD.Modules.ExternalModule {

        private Texture2D               _mugTexture;
        private CornerIcon              _exampleIcon;
        private double                  _runningTime     = 0;
        private long                    _fakeLoadCounter = 0;
        private CancellationTokenSource _fakeLoadCounterCT;

        /// <summary>
        /// Ideally you should keep the constructor as is.
        /// Use <see cref="Initialize"/> to handle initializing the module.
        /// </summary>
        [ImportingConstructor]
        public ExampleBHUDModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) { /* NOOP */ }

        /// <summary>
        /// Define the settings you would like to use in your module.  Settings are persistent
        /// between updates to both Blish HUD and your module.
        /// </summary>
        protected override void DefineSettings(SettingsManager settingsManager) {
            settingsManager.DefineSetting("This is an example setting.",          "This is the value of the setting", "This is the default value", true, "If exposed, this setting will be shown in settings with this description, automatically.");
            settingsManager.DefineSetting("Settings can be many different types", true,                               true,                        true, "This setting is a bool setting.");
        }

        /// <summary>
        /// Allows your module to perform any initialization it needs before starting to run.
        /// Please note that Initialize is NOT asynchronous and will block Blish HUD's update
        /// and render loop, so be sure to not do anything here that takes too long.
        /// </summary>
        protected override void Initialize() {
            Console.WriteLine("Example Blish HUD module initialized.");
        }

        /// <summary>
        /// Load content and more here. This call is asynchronous, so it is a good time to
        /// run any long running steps for your module. Be careful when instancing
        /// <see cref="Blish_HUD.Entities.Entity"/> and <see cref="Blish_HUD.Controls.Control"/>.
        /// Setting their parent is not thread-safe and can cause the application to crash.
        /// You will want to queue them to add later while on the main thread or in a delegate queued
        /// with <see cref="Blish_HUD.DirectorService.QueueMainThreadUpdate(Action{GameTime})"/>.
        /// </summary>
        protected override void LoadAsync() {
            // Load content from the ref directory automatically with the ContentsManager
            _mugTexture = ContentsManager.GetTexture("603447.png");

            // Recall your settings values with the SettingsManager
            SettingEntry<string> setting1 = SettingsManager.GetSetting<string>("This is an example setting.");

            // Get your registered directories with the DirectoriesManager
            foreach (string directoryName in this.DirectoriesManager.RegisteredDirectories) {
                string fullDirectoryPath = DirectoriesManager.GetFullDirectoryPath(directoryName);

                var allFiles = Directory.EnumerateFiles(fullDirectoryPath, "*", SearchOption.AllDirectories).ToList();

                Console.WriteLine($"'{directoryName}' can be found at '{fullDirectoryPath}' and has {allFiles.Count} total files within it.");
            }
        }

        /// <summary>
        /// Allows you to perform an action once your module has finished loading (once
        /// <see cref="LoadAsync"/> has completed).  You must call "base.OnModuleLoaded(e)" at the
        /// end for the <see cref="ExternalModule.ModuleLoaded"/> event to fire and for
        /// <see cref="ExternalModule.Loaded" /> to update correctly.
        /// </summary>
        protected override void OnModuleLoaded(EventArgs e) {
            _exampleIcon = new CornerIcon() {
                Icon             = _mugTexture,
                BasicTooltipText = $"{this.Name} [{this.Namespace}]",
                Parent           = GameService.Graphics.SpriteScreen
            };

            _exampleIcon.Click += delegate(object sender, MouseEventArgs args) {
                Notification.ShowNotification(null, "Hello from Blish HUD!", 4);
            };

            base.OnModuleLoaded(e);
        }

        /// <summary>
        /// Allows your module to run logic such as updating UI elements,
        /// checking for conditions, playing audio, calculating changes, etc.
        /// This method will block the primary Blish HUD loop, so any long
        /// running tasks should be executed on a separate thread to prevent
        /// slowing down the overlay.
        /// </summary>
        protected override void Update(GameTime gameTime) {
            _runningTime += gameTime.ElapsedGameTime.TotalMilliseconds;

            if (_runningTime > 10000) {
                _runningTime -= 10000;
                Console.WriteLine("The example module you have enabled writes output every 10 seconds.");
            }
        }

        /// <summary>
        /// For a good module experience, your module should clean up ANY and ALL entities
        /// and controls that were created and added to either the World or SpriteScreen.
        /// Be sure to remove any tabs added to the Director window, CornerIcons, etc.
        /// </summary>
        protected override void Unload() {
            Console.WriteLine($"Unloading module '{Name}'.");

            _exampleIcon.Dispose();

            Console.WriteLine($"Module '{Name}' unloaded.");
        }

    }

}
