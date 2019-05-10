using System;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Compression;
using System.Windows.Forms;
using Blish_HUD;
using Blish_HUD.Modules;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ExampleBHUDModule {

    [Export(typeof(IModule))]
    public class ExampleBHUDModule : Blish_HUD.Modules.Module {

        public override ModuleInfo GetModuleInfo() {
            Texture2D icon = null;

            using (var zipMemoryStream = new MemoryStream(Properties.Resources._ref)) {
                using (var zipArchive = new ZipArchive(zipMemoryStream)) {
                    foreach (var zipArchiveEntry in zipArchive.Entries) {
                        icon = Texture2D.FromStream(GameService.Graphics.GraphicsDevice, zipArchiveEntry.Open().ToMemoryStream());
                    }
                }
            }

            return new ModuleInfo(
                                  "Example BHUD Module",
                                  icon,
                                  typeof(ExampleBHUDModule).FullName,
                                  "A module loaded from an external DLL.",
                                  "LandersXanders.1235",
                                  "1"
                                 );
        }

        public override void DefineSettings(Settings settings) {
            Settings = settings;
        }

        private ToolStripItem _testItem;

        public override void OnLoad() {
            Console.WriteLine("Example BHUD Module loaded!");
        }

        public override void OnEnabled() {
            Console.WriteLine("Example BHUD Module enabled!");

            _testItem = GameService.GameIntegration.TrayIconMenu.Items.Add("Example BHUD Module Loaded!");
        }

        public override void OnDisabled() {
            Console.WriteLine("Example BHUD Module disabled!");

            _testItem?.Dispose();
        }

        public override void Update(GameTime gameTime) {
        }

    }

}
