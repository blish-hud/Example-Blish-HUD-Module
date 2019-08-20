using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Modules;
using Blish_HUD.Settings;
using Blish_HUD.Modules.Managers;
using Gw2Sharp.WebApi.V2.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Blish_HUD.Input;

namespace ExampleBHUDModule {

    [Export(typeof(Module))]
    public class ExampleBHUDModule : Module {

        /// <summary>
        /// This is your logger for writing to the log.  Ensure the type of of your module class.
        /// Other classes can have their own logger.  Instance those loggers the same as you have
        /// here, but with their type as the argument.
        /// </summary>
        private static Logger Logger = Logger.GetLogger(typeof(ExampleBHUDModule));

        internal static ExampleBHUDModule ModuleInstance;

        // Service Managers
        internal SettingsManager    SettingsManager    => this.ModuleParameters.SettingsManager;
        internal ContentsManager    ContentsManager    => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager      Gw2ApiManager      => this.ModuleParameters.Gw2ApiManager;

        private Texture2D     _mugTexture;
        private double        _runningTime = 0;
        private List<Dungeon> _dungeons;

        private SettingEntry<bool> _anotherExampleSetting;

        // Controls (be sure to dispose of these in Unload()
        private CornerIcon       _exampleIcon;
        private ContextMenuStrip _dungeonContextMenuStrip;

        /// <summary>
        /// Ideally you should keep the constructor as is.
        /// Use <see cref="Initialize"/> to handle initializing the module.
        /// </summary>
        [ImportingConstructor]
        public ExampleBHUDModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) {
            ModuleInstance = this;
        }

        /// <summary>
        /// Define the settings you would like to use in your module.  Settings are persistent
        /// between updates to both Blish HUD and your module.
        /// </summary>
        protected override void DefineSettings(SettingCollection settings) {
            settings.DefineSetting("ExampleSetting.", "This is the value of the setting", "Display name of setting", "If exposed, this setting will be shown in settings with this description, automatically.");

            // Assigning the return value is the preferred way of keeping track of your settings
            _anotherExampleSetting = settings.DefineSetting("AnotherExample", true, "This setting is a bool setting.", "Settings can be many different types");
        }

        /// <summary>
        /// Allows your module to perform any initialization it needs before starting to run.
        /// Please note that Initialize is NOT asynchronous and will block Blish HUD's update
        /// and render loop, so be sure to not do anything here that takes too long.
        /// </summary>
        protected override void Initialize() {
            
        }

        /// <summary>
        /// Load content and more here. This call is asynchronous, so it is a good time to
        /// run any long running steps for your module. Be careful when instancing
        /// <see cref="Blish_HUD.Entities.Entity"/> and <see cref="Blish_HUD.Controls.Control"/>.
        /// Setting their parent is not thread-safe and can cause the application to crash.
        /// You will want to queue them to add later while on the main thread or in a delegate queued
        /// with <see cref="Blish_HUD.OverlayService.QueueMainThreadUpdate(Action{GameTime})"/>.
        /// </summary>
        protected override async Task LoadAsync() {
            // Load content from the ref directory automatically with the ContentsManager
            _mugTexture = ContentsManager.GetTexture("603447.png");

            // Use the Gw2ApiManager to make requests to the API using the permissions provided in your manifest
            var dungeonRequest = await Gw2ApiManager.Gw2ApiClient.Dungeons.AllAsync();
            _dungeons = dungeonRequest.ToList();

            // If you really need to, you can recall your settings values with the SettingsManager
            // It is better if you just hold onto the returned "TypeEntry" instance when doing your initial DefineSetting, though
            SettingEntry<string> setting1 = SettingsManager.ModuleSettings["ExampleSetting"] as SettingEntry<string>;

            // Get your manifest registered directories with the DirectoriesManager
            foreach (string directoryName in this.DirectoriesManager.RegisteredDirectories) {
                string fullDirectoryPath = DirectoriesManager.GetFullDirectoryPath(directoryName);

                var allFiles = Directory.EnumerateFiles(fullDirectoryPath, "*", SearchOption.AllDirectories).ToList();

                Logger.Info($"'{directoryName}' can be found at '{fullDirectoryPath}' and has {allFiles.Count} total files within it.");
            }
        }

        /// <summary>
        /// Allows you to perform an action once your module has finished loading (once
        /// <see cref="LoadAsync"/> has completed).  You must call "base.OnModuleLoaded(e)" at the
        /// end for the <see cref="Module.ModuleLoaded"/> event to fire and for
        /// <see cref="Module.Loaded" /> to update correctly.
        /// </summary>
        protected override void OnModuleLoaded(EventArgs e) {
            // Add a mug icon in the top left next to the other icons
            _exampleIcon = new CornerIcon() {
                Icon             = _mugTexture,
                BasicTooltipText = $"{this.Name} [{this.Namespace}]",
                Parent           = GameService.Graphics.SpriteScreen
            };

            // Show a notification in the middle of the screen when the icon is clicked
            _exampleIcon.Click += delegate(object sender, MouseEventArgs args) {
                ScreenNotification.ShowNotification("Hello from Blish HUD!");
            };

            // Add a right click menu to the icon that shows each Revenant legend (pulled from the API)
            _dungeonContextMenuStrip = new ContextMenuStrip();

            foreach (var dungeon in _dungeons) {
                var dungeonItem = _dungeonContextMenuStrip.AddMenuItem(dungeon.Id);

                var dungeonMenu = new ContextMenuStrip();

                foreach (var path in dungeon.Paths) {
                    dungeonMenu.AddMenuItem($"{path.Id} ({path.Type})");
                }

                dungeonItem.Submenu = dungeonMenu;
            }

            _exampleIcon.Menu = _dungeonContextMenuStrip;

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

            if (_runningTime > 60000) {
                _runningTime -= 60000;
                ScreenNotification.ShowNotification("The examples module shows this message every 60 seconds!", ScreenNotification.NotificationType.Warning);
            }
        }

        /// <summary>
        /// For a good module experience, your module should clean up ANY and ALL entities
        /// and controls that were created and added to either the World or SpriteScreen.
        /// Be sure to remove any tabs added to the Director window, CornerIcons, etc.
        /// </summary>
        protected override void Unload() {
            _exampleIcon.Dispose();
            _dungeonContextMenuStrip.Dispose();

            // Static members are not automatically cleared and will keep a reference to your,
            // module unless manually unset.
            ModuleInstance = null;
        }

    }

}
