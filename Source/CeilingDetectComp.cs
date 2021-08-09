using RimWorld;
using Verse;

namespace ShowHair
{
    class CompCeilingDetect : ThingComp
    {
        public bool? isIndoors = null;

        public bool IsIndoors
        {
            get
            {
                if (isIndoors != null)
                    return isIndoors.Value;
                return false;
            }
        }

        public override void CompTickRare()
        {
            Pawn pawn = base.parent as Pawn;
            Map map = pawn?.Map;
            if (map != null && Settings.HideHatsIndoors && pawn.RaceProps?.Humanlike == true && pawn.Faction?.IsPlayer == true && !pawn.Dead)
            {
                if (this.isIndoors == null)
                {
                    this.isIndoors = DetermineIsIndoors(pawn, map);
                    PortraitsCache.SetDirty(pawn);
                    GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(pawn);
                    return;
                }

                bool orig = this.isIndoors.Value;
                this.isIndoors = this.DetermineIsIndoors(pawn, map);
                if (orig != this.isIndoors.Value)
                {
                    PortraitsCache.SetDirty(pawn);
                    GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(pawn);
                }
            }
        }

        private bool DetermineIsIndoors(Pawn pawn, Map map)
        {
            return pawn.GetRoom() != null && map.roofGrid.RoofAt(pawn.Position) != null;
        }
    }
}
