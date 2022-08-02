using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace kotae.AssistantOverlays
{
    public class ConfigCategoryOptions
    {
        public bool ShouldShowInList { get; set; } = true;
        public bool ShouldDrawOverlay { get; set; } = true;
        public bool ShouldDrawSnaplines { get; set; } = false;
        public Color DrawColor { get; set; } = Color.White;
    }
    public class ModConfig
    {
        public ConfigCategoryOptions TruffleOptions { get; set; } = new ConfigCategoryOptions() { DrawColor = Color.Blue };
        public ConfigCategoryOptions LadderStoneOptions { get; set; } = new ConfigCategoryOptions() { DrawColor = Color.Lime, ShouldDrawSnaplines = true };
        public ConfigCategoryOptions LadderOptions { get; set; } = new ConfigCategoryOptions() { DrawColor = Color.Magenta, ShouldDrawSnaplines = true };
        public ConfigCategoryOptions EnemyOptions { get; set; } = new ConfigCategoryOptions() { DrawColor = Color.Red };
        public ConfigCategoryOptions QuartzOptions { get; set; } = new ConfigCategoryOptions() { DrawColor = Color.Brown, ShouldDrawSnaplines = true };
        public ConfigCategoryOptions OverlayObjectOptions { get; set; } = new ConfigCategoryOptions() { DrawColor = Color.Coral };
        public ConfigCategoryOptions DebrisOptions { get; set; } = new ConfigCategoryOptions() { DrawColor = Color.DeepPink };
        public ConfigCategoryOptions BushOptions { get; set; } = new ConfigCategoryOptions() { DrawColor = Color.Blue };
        public ConfigCategoryOptions ResourceClumpOptions { get; set; } = new ConfigCategoryOptions() { DrawColor = Color.Yellow };
        public ConfigCategoryOptions HayGrassOptions { get; set; } = new ConfigCategoryOptions() { DrawColor = Color.Purple, ShouldShowInList = false };
        public ConfigCategoryOptions GenericForageableOptions { get; set; } = new ConfigCategoryOptions() { DrawColor = Color.Aquamarine };
        public ConfigCategoryOptions TreeOptions { get; set; } = new ConfigCategoryOptions() { DrawColor = Color.MonoGameOrange, ShouldShowInList = false };
        public ConfigCategoryOptions CropOptions { get; set; } = new ConfigCategoryOptions() { DrawColor = Color.Orange };
        public ConfigCategoryOptions ArtifactSpotOptions { get; set; } = new ConfigCategoryOptions() { DrawColor = Color.Magenta };
        public ConfigCategoryOptions GenericObjectOptions { get; set; } = new ConfigCategoryOptions() { DrawColor = Color.Lime, ShouldShowInList = false };
        public ConfigCategoryOptions FiberWeedsOptions { get; set; } = new ConfigCategoryOptions() { DrawColor = Color.BlanchedAlmond };
        public ConfigCategoryOptions NPCOptions { get; set; } = new ConfigCategoryOptions() { DrawColor = Color.Blue, ShouldShowInList = false };
        public Color DefaultTextColor { get; set; } = Color.White;
    }
}
