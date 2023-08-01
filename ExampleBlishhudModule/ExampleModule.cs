using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Overlay.UI.Views;
using Blish_HUD.Settings;
using Gw2Sharp.WebApi.V2.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace ExampleBlishhudModule
{
    [Export(typeof(Module))]
    public class ExampleModule : Module
    {
        private static readonly Logger Logger = Logger.GetLogger<ExampleModule>();

        #region Service Managers

        internal SettingsManager SettingsManager => this.ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => this.ModuleParameters.Gw2ApiManager;

        #endregion

        // Ideally you should keep the constructor as is.
        // Use LoadAsync() to handle initializing the module.
        [ImportingConstructor]
        public ExampleModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters)
        {
            // Static members (fields/properties/events):
            // static members like ExampleModuleInstance can be used as global variables in your module.
            // E.g. some module devs use them to be able to access their module settings everywhere in their module.
            // Just be aware, that you MUST clear static members in Module.Unload() by setting them to null.
            // Otherwise those static members can keep your module or parts of your module alive though your module was uninstalled or unloaded.
            // Or your module may load with an unexpected old state from the last time it was loaded.
            // This can lead to bugs that are hard to fix. In general, static member should be avoided whenever possible.
            // If you dont want to use it, you can remove this static member.
            ExampleModuleInstance = this;
        }

        // Define the settings you would like to use in your module.  Settings are persistent
        // between updates to both Blish HUD and your module.
        protected override void DefineSettings(SettingCollection settings)
        {
            settings.DefineSetting(
                "ExampleSetting",
                "This is the default value of the setting",
                () => "Display name of setting",
                () => "Tooltip text of setting");

            // Assigning the return value is the preferred way of keeping track of your settings.
            _boolExampleSetting = settings.DefineSetting(
                "bool example",
                true,
                () => "This is a bool setting (checkbox)",
                () => "Settings can be many different types");

            _stringExampleSetting = settings.DefineSetting(
                "string example",
                "myText",
                () => "This is an string setting (textbox)",
                () => "Settings can be many different types");

            _valueRangeExampleSetting = settings.DefineSetting(
                "int example",
                20,
                () => "This is an int setting (slider)",
                () => "Settings can be many different types");

            _valueRangeExampleSetting.SetRange(0, 255); // for min and max range of the setting

            _enumExampleSetting = settings.DefineSetting("enum example",
                ColorType.Blue,
                () => "This is an enum setting (drop down menu)",
                () => "...");

            _enumExampleSetting.SettingChanged += UpdateCharacterWindowColor;

            // you can get or set the setting value somewhere else in your module with the .Value property like this:
            _boolExampleSetting.Value = false;

            // internal settings that should not be displayed to the user in the settings window have to be stored in subcollections
            // e.g. saving x,y position of a window
            _internalExampleSettingSubCollection = settings.AddSubCollection("internal settings (not visible in UI)");
            _hiddenIntExampleSetting = _internalExampleSettingSubCollection.DefineSetting("example window x position", 50);
            _hiddenIntExampleSetting2 = _internalExampleSettingSubCollection.DefineSetting("example window y position", 50);
        }

        private void UpdateCharacterWindowColor(object sender, ValueChangedEventArgs<ColorType> e)
        {
            if (_enumExampleSetting.Value == ColorType.Black)
                _charactersFlowPanel.BackgroundColor = Color.Black;
            else
                _charactersFlowPanel.BackgroundColor = Color.Blue;
        }

        // Some API requests need an api key. e.g. accessing account data like inventory or bank content
        // Blish hud gives you an api subToken you can use instead of the real api key the user entered in blish.
        // But this api subToken may not be available when your module is loaded.
        // Because of that api requests, which require an api key, may fail when they are called in Initialize() or LoadAsync().
        // Or the user can delete the api key or add a new api key with the wrong permissions while your module is already running.
        // You can react to that by subscribing to Gw2ApiManager.SubtokenUpdated. This event will be raised when your module gets the api subToken or
        // when the user adds a new API key.
        private async void OnApiSubTokenUpdated(object sender, ValueEventArgs<IEnumerable<TokenPermission>> e)
        {
            await GetCharacterNamesFromApiAndShowThemInLabel();
        }

        // Load content and more here. This call is asynchronous, so it is a good time to run
        // any long running steps for your module including loading resources from file or ref.
        // Use LoadAsync() instead of Initialize(), OnModuleLoaded() and ModuleLoaded event. The latter run synchronously and block the
        // blish update loop
        protected override async Task LoadAsync()
        {
            Gw2ApiManager.SubtokenUpdated += OnApiSubTokenUpdated;
            createCharacterNamesWindow();

            // usually the api subtoken is not available when the module is loaded. But in case it already is,
            // we try to receive the character names from the api here.
            await GetCharacterNamesFromApiAndShowThemInLabel();

            try
            {
                // Use the Gw2ApiManager to make requests to the API. Some Api requests, like this one, do not need an api key.
                // Because of that it is not necessary to check for api key permissions in this case or for the api subtoken to be available.
                var dungeonRequest = await Gw2ApiManager.Gw2ApiClient.V2.Dungeons.AllAsync();
                _dungeons.Clear();
                _dungeons.AddRange(dungeonRequest.ToList());
            }
            catch (Exception e)
            {
                Logger.Info($"Failed to get dungeons from api.");
            }

            // If you really need to, you can recall your settings values with the SettingsManager
            // It is better if you just hold onto the returned "TypeEntry" instance when doing your initial DefineSetting, though
            SettingEntry<string> setting1 = SettingsManager.ModuleSettings["ExampleSetting"] as SettingEntry<string>;

            // Get your manifest registered directories with the DirectoriesManager
            foreach (string directoryName in this.DirectoriesManager.RegisteredDirectories)
            {
                string fullDirectoryPath = DirectoriesManager.GetFullDirectoryPath(directoryName);
                var allFiles = Directory.EnumerateFiles(fullDirectoryPath, "*", SearchOption.AllDirectories).ToList();

                // example of how to log something in the blishhud.XXX-XXX.log file in %userprofile%\Documents\Guild Wars 2\addons\blishhud\logs
                Logger.Info($"'{directoryName}' can be found at '{fullDirectoryPath}' and has {allFiles.Count} total files within it.");
            }

            // Load content from the ref directory in the module.bhm automatically with the ContentsManager
            _mugTexture       = ContentsManager.GetTexture("603447.png");
            _windowBackgroundTexture = ContentsManager.GetTexture("155985.png");

            // show a window with gw2 window style.
            _exampleWindow = new StandardWindow(
                _windowBackgroundTexture,
                new Rectangle(40, 26, 913, 691),
                new Rectangle(70, 71, 839, 605))
            {
                Parent        = GameService.Graphics.SpriteScreen,
                Title         = "Example Window Title",
                Emblem        = _mugTexture,
                Subtitle      = "Example Subtitle",
                Location      = new Point(300, 300),
                SavesPosition = true,
                // Id has to be unique not only in your module but also within blish core and any other module
                Id = $"{nameof(ExampleModule)}_My_Unique_ID_123" 
            };

            // show blish hud overlay settings content inside the window
            _exampleWindow.Show(new OverlaySettingsView());

            // Add a mug corner icon in the top left next to the other icons in guild wars 2 (e.g. inventory icon, Mail icon)
            _exampleCornerIcon = new CornerIcon()
            {
                Icon             = _mugTexture,
                BasicTooltipText = $"My Corner Icon Tooltip for {Name}",
                // Priority determines the position relative to cornerIcons of other modules
                // because of that it MUST be set to a constant random value.
                // Do not recalculate this value on every module start up. Just use a constant value.
                // It has to be random to prevent that two modules use the same priority (e.g. "4") which would cause the cornerIcons to be in 
                // a different position on every startup.
                Priority         = 1645843523, 
                Parent           = GameService.Graphics.SpriteScreen
            };

            // Show a notification in the middle of the screen when the icon is clicked
            _exampleCornerIcon.Click += delegate
            {
                ScreenNotification.ShowNotification("Hello from Blish HUD!");
            };

            // Add a right click menu to the corner icon which lists all dungeons with their dungeons paths as subcategories (pulled from the API)
            _dungeonContextMenuStrip = new ContextMenuStrip();

            foreach (var dungeon in _dungeons)
            {
                var dungeonPathMenu = new ContextMenuStrip();

                foreach (var path in dungeon.Paths)
                    dungeonPathMenu.AddMenuItem($"{path.Id} ({path.Type})");

                var dungeonMenuItem = _dungeonContextMenuStrip.AddMenuItem(dungeon.Id);
                dungeonMenuItem.Submenu = dungeonPathMenu;
            }

            _exampleCornerIcon.Menu = _dungeonContextMenuStrip;
        }

        // Allows your module to run logic such as updating UI elements,
        // checking for conditions, playing audio, calculating changes, etc.
        // This method will block the primary Blish HUD loop, so any long
        // running tasks should be executed on a separate thread to prevent
        // slowing down the overlay.
        protected override void Update(GameTime gameTime)
        {
            _runningTime += gameTime.ElapsedGameTime.TotalMilliseconds;

            if (_runningTime > 60000)
            {
                _runningTime -= 60000;
                ScreenNotification.ShowNotification("The examples module shows this message every 60 seconds!", ScreenNotification.NotificationType.Warning);
            }
        }

        // For a good module experience, your module should clean up ANY and ALL entities
        // and controls that were created and added to either the World or SpriteScreen.
        // Be sure to remove any tabs added to the Director window, CornerIcons, etc.
        protected override void Unload()
        {
            // it is best practise to unsubscribe from events. That is typically done inside of .Dispose() or Module.Unload().
            // Unsubscribing only works if you subscribed with a named method (e.g. += MyMethod;).
            // It doesnt work with lambda expressions (e.g. += () => 2+2;)
            // Not unsubscribing from events can result in the event subscriber (right side) being kept alive by the event publisher (left side).
            // This can lead to memory leaks and bugs where an object, that shouldnt exist aynmore,
            // still responds to events and is messing with your module.
            Gw2ApiManager.SubtokenUpdated -= OnApiSubTokenUpdated;
            _enumExampleSetting.SettingChanged -= UpdateCharacterWindowColor;

            // Unload() can be called on your module anytime. Even while it is currently loading and creating the objects.
            // Because of that you always have to check if the objects you want to access in Unload() are not null.
            // This can be done by using if null checks or by using the null-condition operator ?. (question mark with dot).
            _exampleCornerIcon?.Dispose();
            _dungeonContextMenuStrip?.Dispose();
            _charactersFlowPanel?.Dispose(); // this will dispose the child labels we added as well
            _exampleWindow?.Dispose();
            _windowBackgroundTexture?.Dispose();
            _mugTexture?.Dispose();

            // All static members must be manually unset
            // Static members are not automatically cleared and will keep a reference to your,
            // module unless manually unset.
            ExampleModuleInstance = null;
        }

        private void createCharacterNamesWindow()
        {
            _charactersFlowPanel = new FlowPanel()
            {
                BackgroundColor = _enumExampleSetting.Value == ColorType.Black ? Color.Black : Color.Blue,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                WidthSizingMode = SizingMode.AutoSize,
                HeightSizingMode = SizingMode.AutoSize,
                Location = new Point(200, 200),
                Parent = GameService.Graphics.SpriteScreen,
            };

            _charactersHeaderLabel = new Label() // this label is used as heading
            {
                Text = "My Characters:",
                TextColor = Color.Red,
                Font = GameService.Content.DefaultFont32,
                ShowShadow = true,
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                //Location     = new Point(2, 0), // without a FlowPanel as parent, you can set the exact position inside the parent this way
                Parent = _charactersFlowPanel
            };

            _characterNamesLabel = new Label() // this label will be used to display the character names requested from the API
            {
                Text = "getting data from api...",
                TextColor = Color.DarkGray,
                Font = GameService.Content.DefaultFont32,
                ShowShadow = true,
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                //Location     = new Point(2, 50), // without a FlowPanel as parent, you can set the exact position inside the parent this way
                Parent = _charactersFlowPanel
            };
        }

        private async Task GetCharacterNamesFromApiAndShowThemInLabel()
        {
            // check if api subToken has the permissions you need for your request: Gw2ApiManager.HasPermissions() 
            // Make sure that you added the api key permissions you need in the manifest.json.
            // Don't set them to '"optional": false' if you dont plan to handle that case.
            // e.g. the api request further down in this code needs the "characters" permission.
            // if the Gw2ApiManager.HasPermissions returns false it can also mean, that your module did not get the api subtoken yet or the user removed
            // the api key from blish hud. Because of that it is best practice to call .HasPermissions before every api request which requires an api key
            var apiKeyPermissions = new List<TokenPermission>
            {
                TokenPermission.Account, // this permission can be used to check if your module got a token at all because every api key has it.
                TokenPermission.Characters
            };

            if (!Gw2ApiManager.HasPermissions(apiKeyPermissions))
            {
                _characterNamesLabel.Text = "api permissions are missing or api sub token not available yet";
                return;
            }

            // even when the api request and api subToken are okay, the api requests can still fail for various reasons.
            // Examples are timeouts or the api is down or the api randomly responds with an error code instead of the correct response.
            // Because of that always use try catch when doing api requests to catch api request exceptions.
            // otherwise api request exceptions can crash your module and blish hud.
            try
            {
                // request characters endpoint from api. 
                var charactersResponse = await Gw2ApiManager.Gw2ApiClient.V2.Characters.AllAsync();
                // extract character names from the api response and show them inside a label
                var characterNames = charactersResponse.Select(c => c.Name);
                var characterNamesText = string.Join("\n", characterNames);
                _characterNamesLabel.Text = characterNamesText;
            }
            catch (Exception e)
            {
                // this is just an example for logging.
                // You do not have to log api response exceptions. Just make sure that your module has no issue with failing api requests.
                // Warning:
                // Blish Hud uses the tool Sentry in combination with the ErrorSubmissionModule to register ERROR and FATAL log entries.
                // Because of that you must not use Logger.Error() or .Fatal() to log api response exceptions. Because sometimes the GW2 api
                // can be down for up to a few days. That triggers a lot of api exceptions which would end up spamming the Sentry tool.
                // Instead use Logger.Info() or .Warn() if you want to log api response errors. Those do not get stored by the Sentry tool.
                Logger.Info($"Failed to get character names from api.");
            }
        }

        internal static ExampleModule ExampleModuleInstance;
        private SettingEntry<bool> _boolExampleSetting;
        private SettingEntry<int> _valueRangeExampleSetting;
        private SettingEntry<string> _stringExampleSetting;
        private SettingEntry<ColorType> _enumExampleSetting;
        private SettingCollection _internalExampleSettingSubCollection;
        private SettingEntry<int> _hiddenIntExampleSetting;
        private SettingEntry<int> _hiddenIntExampleSetting2;
        private Texture2D _windowBackgroundTexture;
        private Texture2D _mugTexture;
        private List<Dungeon> _dungeons = new List<Dungeon>();
        private CornerIcon _exampleCornerIcon;
        private ContextMenuStrip _dungeonContextMenuStrip;
        private double _runningTime;
        private Label _charactersHeaderLabel;
        private Label _characterNamesLabel;
        private FlowPanel _charactersFlowPanel;
        private StandardWindow _exampleWindow;
    }
}