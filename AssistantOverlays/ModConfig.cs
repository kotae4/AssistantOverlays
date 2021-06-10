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
        public Color LadderColor { get; set; } = Color.Purple;
        public Color ShaftColor { get; set; } = Color.Magenta;
        public Color EnemyColor { get; set; } = Color.Red;
        public Color TextColor { get; set; } = Color.White;
    }
}
