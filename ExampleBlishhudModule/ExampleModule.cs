using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Gw2Sharp.WebApi.V2.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

// You can rename the namespace to whatever you want. Including the module name in the namespace is good idea.
// If you change this in the future again after you already released a module version, you should let freesnow know about it.
// Because they have to update that in the Sentry Blish Bug tracker, too.
// This namespace does not have to match the namespace in the manifest.json. They are not related. 
// (Side note: the manifest.json namespace has to be set once and must NOT be changed after a module was released)
namespace ExampleBlishhudModule
{
    [Export(typeof(Module))]
    public class ExampleModule : Module
    {
        private static readonly Logger Logger = Logger.GetLogger<ExampleModule>();

        internal SettingsManager SettingsManager => this.ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => this.ModuleParameters.Gw2ApiManager;

        // Ideally you should keep the constructor as is (empty). Instead use LoadAsync() to handle initializing the module.
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
            // The following settings will automatically create controls in the settings panel of your module tab.
            // If you want to design the settings tab yourself instead of it getting created automatically, you can use this override:
            // public override IView GetSettingsView() { return new MyCustomSettingsView(); }
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

            _enumExampleSetting = settings.DefineSetting(
                "enum example",
                ColorType.Blue,
                () => "This is an enum setting (drop down menu)",
                () => "...");

            // you can get or set the setting value somewhere else in your module with the .Value property like this:
            _boolExampleSetting.Value = false;

            // you can react to a user changing a setting value by subscribing to this event: 
            _enumExampleSetting.SettingChanged += UpdateCharacterWindowColor;

            // If you really need to, you can recall your settings values with the SettingsManager
            // It is better if you just hold onto the returned "SettingsEntry" instance when doing your initial DefineSetting, though
            SettingEntry<string> setting1 = SettingsManager.ModuleSettings["ExampleSetting"] as SettingEntry<string>;

            // internal settings that should not be displayed to the user in the settings window have to be stored in subcollections
            // e.g. saving x,y position of a window
            _internalExampleSettingSubCollection = settings.AddSubCollection("internal settings (not visible in UI)");
            _hiddenIntExampleSetting = _internalExampleSettingSubCollection.DefineSetting("example window x position", 50);
            _hiddenIntExampleSetting2 = _internalExampleSettingSubCollection.DefineSetting("example window y position", 50);
        }

        private void UpdateCharacterWindowColor(object sender, ValueChangedEventArgs<ColorType> e)
        {
            if (_enumExampleSetting.Value == ColorType.Black) // you could use e.NewValue instead of _enumExampleSetting.Value in this case too
                _charactersFlowPanel.BackgroundColor = Color.Black;
            else
                _charactersFlowPanel.BackgroundColor = Color.Blue;
        }

        // Load content and more here. This call is asynchronous, so it is a good time to run
        // any long running steps for your module including loading resources from file or ref.
        // Use LoadAsync() instead of Initialize(), OnModuleLoaded() and ModuleLoaded event. The latter run synchronously and block the
        // blish update loop
        protected override async Task LoadAsync()
        {
            // Get your manifest registered directories with the DirectoriesManager. Those can be used to store data.
            foreach (string directoryName in DirectoriesManager.RegisteredDirectories)
            {
                string fullDirectoryPath = DirectoriesManager.GetFullDirectoryPath(directoryName);
                var allFiles = Directory.EnumerateFiles(fullDirectoryPath, "*", SearchOption.AllDirectories).ToList();

                // example of how to log something in the blishhud.XXX-XXX.log file in %userprofile%\Documents\Guild Wars 2\addons\blishhud\logs
                Logger.Info($"'{directoryName}' can be found at '{fullDirectoryPath}' and has {allFiles.Count} total files within it.");
            }

            // Load content from the ref directory in the module.bhm with the ContentsManager
            _mugTexture = ContentsManager.GetTexture("test/603447.png");

            // if you want to use gw2 textures without having to add them to the ref folder, you can use DatAssetCache instead:
            // search the texture here: https://search.gw2dat.com/ and get the texture asset id. e.g. 155997 from https://assets.gw2dat.com/155997.png
            // Now get the texture with DatAssetCache in one of the following ways:
            // option 1: 
            // GameService.Content.DatAssetCache.TryGetTextureFromAssetId(155997, out AsyncTexture2D windowBackground);
            // option 2: 
            var windowBackgroundTexture = AsyncTexture2D.FromAssetId(155997);

            // Create some UI
            await CreateGw2StyleWindowThatDisplaysAllCurrencies(windowBackgroundTexture);
            CreateWindowWithCharacterNames();
            CreateCornerIconWithDungeonsContextMenu();
        }

        // Allows your module to run logic such as updating UI elements,
        // checking for conditions, playing audio, calculating changes, etc.
        // This method will block the primary Blish HUD loop, so any long
        // running tasks should be executed on a separate thread to prevent
        // slowing down the overlay.
        protected override void Update(GameTime gameTime)
        {
            _notificationRunningTime += gameTime.ElapsedGameTime.TotalMilliseconds;

            if (_notificationRunningTime > 60_000)
            {
                _notificationRunningTime = 0;
                // Show a notification in the middle of the screen
                ScreenNotification.ShowNotification("The examples module shows this message every 60 seconds!", ScreenNotification.NotificationType.Warning);
            }

            _updateCharactersRunningTime += gameTime.ElapsedGameTime.TotalMilliseconds;

            if (_updateCharactersRunningTime > 5_000)
            {
                _updateCharactersRunningTime = 0;
                // we use Task.Run here to prevent blocking the update loop with a possibly long running task
                Task.Run(GetCharacterNamesFromApiAndShowThemInLabel);
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
            _enumExampleSetting.SettingChanged -= UpdateCharacterWindowColor;

            // Unload() can be called on your module anytime. Even while it is currently loading and creating the objects.
            // Because of that you always have to check if the objects you want to access in Unload() are not null.
            // This can be done by using if null checks or by using the null-condition operator ?. (question mark with dot).
            _cornerIcon?.Dispose();
            _contextMenuStrip?.Dispose();
            _charactersFlowPanel?.Dispose(); // this will dispose the child labels we added as well
            _exampleWindow?.Dispose();
            // only .Dispose() textures you created yourself or loaded from your ref folder
            // NEVER .Dipose() textures from DatAssetCache because those textures are shared between modules and blish.
            _mugTexture?.Dispose(); 

            // All static members must be manually unset
            // Static members are not automatically cleared and will keep a reference to your,
            // module unless manually unset.
            ExampleModuleInstance = null;
        }
        
        private void CreateCornerIconWithDungeonsContextMenu()
        {
            // Add a menu icon in the top left next to the other icons in guild wars 2 (e.g. inventory icon, Mail icon)
            // Priority: Determines the position relative to cornerIcons of other modules
            // because of that it MUST be set to a constant random value.
            // Do not recalculate this value on every module start up. Just use a constant value.
            // It has to be random to prevent that two modules use the same priority (e.g. "4") which would cause the cornerIcons to be in 
            // a different position on every startup.
            _cornerIcon = new CornerIcon()
            {
                Icon = _mugTexture,
                BasicTooltipText = $"My Corner Icon Tooltip for {Name}",
                Priority = 1645843523,
                Parent = GameService.Graphics.SpriteScreen
            };

            // Clicking on the cornerIcon shows/hides the example window
            _cornerIcon.Click += (s, e) => _exampleWindow.ToggleWindow();

            // Add a right click menu to the corner icon
            _contextMenuStrip = new ContextMenuStrip();
            _contextMenuStrip.AddMenuItem("A");
            var bMenuItem = _contextMenuStrip.AddMenuItem("B");
            var bSubMenuStrip = new ContextMenuStrip();
            bSubMenuStrip.AddMenuItem("B1");
            bSubMenuStrip.AddMenuItem("B2");
            bMenuItem.Submenu = bSubMenuStrip;
            _cornerIcon.Menu = _contextMenuStrip;
        }

        private void CreateWindowWithCharacterNames()
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

            new Label() // this label is used as heading
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
            // Some API requests need an api key. e.g. for accessing account data like inventory or bank content.
            // Because of security reasons blish hud gives you an api subToken you can use instead of the real api key the user entered in blish.
            // Make sure that you added the api key permissions you need in the manifest.json.
            // Don't set them to '"optional": false' if you dont plan to handle that the user may disable certain permissions for your module.
            // e.g. the api request further down in this code needs the "characters" permission.
            // The api subToken may not be available when your module is loaded.
            // Because of that api requests, which require an api key, may fail when they are called in Initialize() or LoadAsync().
            // Or the user can delete the api key or add a new api key with the wrong permissions while your module is already running.
            // To handle those cases you could subscribe to Gw2ApiManager.SubtokenUpdated event but this is considered as bad practice.
            // Instead you should call Gw2ApiManager.HasPermissions before every api request that requires an api key.
            var apiKeyPermissions = new List<TokenPermission>
            {
                TokenPermission.Account, // this permission can be used to check if your module got a token at all because every api key has this persmission.
                TokenPermission.Characters // this is the permission we actually require here to get the character names
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
                // Warning:
                // Blish Hud uses the tool Sentry in combination with the ErrorSubmissionModule to upload ERROR and FATAL log entries to a web server.
                // Because of that you must not use Logger.Error() or .Fatal() to log api response exceptions. Sometimes the GW2 api
                // can be down for up to a few days. That triggers a lot of api exceptions which would end up spamming the Sentry tool.
                // Instead use Logger.Info() or .Warn() if you want to log api response errors. Those do not get stored by the Sentry tool.
                // But you do not have to log api response exceptions. Just make sure that your module has no issues with failing api requests.
                Logger.Info("Failed to get character names from api.");
            }
        }

        private async Task CreateGw2StyleWindowThatDisplaysAllCurrencies(AsyncTexture2D windowBackgroundTexture)
        {
            // get all currencies from the api
            var currencies = new List<Currency>();
            try
            {
                // Use the Gw2ApiManager to make requests to the API. Some Api requests, like this one, do not need an api key.
                // Because of that it is not necessary to check for api key permissions or for the api subtoken to be available.
                var apiCurrencies = await Gw2ApiManager.Gw2ApiClient.V2.Currencies.AllAsync();
                currencies.AddRange(apiCurrencies);
            }
            catch (Exception e)
            {
                Logger.Info("Failed to get currencies from api.");
            }

            // create a window with gw2 window style.
            _exampleWindow = new StandardWindow(
                windowBackgroundTexture,
                new Rectangle(25, 26, 560, 640),
                new Rectangle(40, 50, 540, 590))
            {
                Parent = GameService.Graphics.SpriteScreen,
                Title = "Example Window Title",
                Emblem = _mugTexture,
                Subtitle = "Example Subtitle",
                Location = new Point(300, 300),
                SavesPosition = true,
                Id = $"{nameof(ExampleModule)}_My_Unique_ID_123" // Id has to be unique not only in your module but also within blish core and any other module
            };

            // add a panel to the window
            var currenciesFlowPanel = new FlowPanel
            {
                Title = "currencies",
                FlowDirection = ControlFlowDirection.LeftToRight,
                Width = 500,
                CanCollapse = true,
                HeightSizingMode = SizingMode.AutoSize,
                Parent = _exampleWindow,
            };

            // show all currencies in the panel
            foreach (var currency in currencies)
            {
                var iconAssetId = int.Parse(Path.GetFileNameWithoutExtension(currency.Icon.Url.AbsoluteUri));
                var tooltipText = $"{currency.Name}\n{currency.Description}";
                new Image(AsyncTexture2D.FromAssetId(iconAssetId))
                {
                    BasicTooltipText = tooltipText,
                    Size = new Point(40),
                    Parent = currenciesFlowPanel,
                };
            }

            _exampleWindow.Show();
        }

        internal static ExampleModule ExampleModuleInstance;
        private SettingEntry<bool> _boolExampleSetting;
        private SettingEntry<int> _valueRangeExampleSetting;
        private SettingEntry<int> _hiddenIntExampleSetting;
        private SettingEntry<int> _hiddenIntExampleSetting2;
        private SettingEntry<string> _stringExampleSetting;
        private SettingEntry<ColorType> _enumExampleSetting;
        private SettingCollection _internalExampleSettingSubCollection;
        private Texture2D _mugTexture;
        private CornerIcon _cornerIcon;
        private ContextMenuStrip _contextMenuStrip;
        private Label _characterNamesLabel;
        private FlowPanel _charactersFlowPanel;
        private StandardWindow _exampleWindow;
        private double _notificationRunningTime;
        private double _updateCharactersRunningTime;
    }
}
