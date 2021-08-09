using RimWorld;
using System.Text;
using Verse;

namespace ShowHair
{
    class CompCeilingDetect : ThingComp
    {
        public bool IsIndoors = false;

        public override void CompTickRare()
        {
            Pawn pawn = base.parent as Pawn;
            Map map = pawn?.Map;
            if (map != null && Settings.HideHatsIndoors && pawn.RaceProps?.Humanlike == true && pawn.Faction?.IsPlayer == true && !pawn.Dead)
            {
                //StringBuilder sb = new StringBuilder();
                //sb.AppendLine($"{pawn.Name.ToStringShort}   Setting: {Settings.HideHatsIndoors}");
                var roof = map.roofGrid.RoofAt(pawn.Position);
                bool orig = this.IsIndoors;
                this.IsIndoors = false;
                //sb.AppendLine($" - IsNatural {roof?.isNatural}");
                if (roof != null && !roof.isNatural)
                {
                    this.IsIndoors = true;
                }
                if (orig != this.IsIndoors)
                {
                    //sb.AppendLine(" -Dirty Textures");
                    PortraitsCache.SetDirty(pawn);
                    GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(pawn);
                }
                //sb.AppendLine($" -{this.IsIndoors}");
                //Log.Warning(sb.ToString());
            }
        }
    }
}
