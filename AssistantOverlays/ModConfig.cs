using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace kotae.AssistantOverlays
{
    public class ModConfig
    {
        public Color TruffleColor { get; set; } = Color.Blue;
        public Color LadderStoneColor { get; set; } = Color.Lime;
        public Color LadderColor { get; set; } = Color.Magenta;
        public Color EnemyColor { get; set; } = Color.Red;

        public Color QuartzColor { get; set; } = Color.Brown;
        public Color OverlayObjectColor { get; set; } = Color.Coral;
        public Color DebrisColor { get; set; } = Color.DeepPink;
        public Color BushColor { get; set; } = Color.Blue;
        public Color ResourceClumpColor { get; set; } = Color.Yellow;
        public Color HayGrassColor { get; set; } = Color.Purple;
        public Color GenericForageableColor { get; set; } = Color.Aquamarine;
        public Color TreeColor { get; set; } = Color.MonoGameOrange;
        public Color CropColor { get; set; } = Color.Orange;
        public Color ArtifactSpotColor { get; set; } = Color.Magenta;
        public Color GenericObjectColor { get; set; } = Color.Lime;
        public Color TextColor { get; set; } = Color.White;
    }
}
