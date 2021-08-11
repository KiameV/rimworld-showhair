using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ShowHair
{
	public interface IHairUtility
	{
		bool TryGetCustomHairMat(Pawn pawn, Rot4 facing, out Material mat);
	}

	public static class HairUtilityFactory
	{
		private static HairUtility hu = null;
		public static IHairUtility GetHairUtility()
		{
			if (hu == null)
				hu = new HairUtility();
			return hu;
		}

		private class HairUtility : IHairUtility
		{
			//IsHeadCoverage: bool, CoverageType: ICoverageType
			protected Dictionary<bool, IHeadCoverage> headCoverages;

			public HairUtility()
			{
				this.headCoverages = new Dictionary<bool, IHeadCoverage>()
				{
					{ false, new HeadCoverage.NotCovered() },
					{ true, new HeadCoverage.Covered() }
					//{ true, Settings.UseFallbackTextures ? (IHeadCoverage)new HeadCoverage.Covered_Fallback() : (IHeadCoverage)new HeadCoverage.Covered() }
				};
			}

			//returns a custom hair texture based on the current hair texture
			public bool TryGetCustomHairMat(Pawn pawn, Rot4 facing, out Material mat)
			{
				try
				{
					var maxCoverageDef = this.getMaxCoverageDef(pawn); //find the def with max coverage

					//using IsHeadDef as the key to return a HeadCoverage type i.e. covered or not covered
					var headCoverage = this.headCoverages[maxCoverageDef.GetModExtension<BodyPartGroupDefExtension>().IsHeadDef];

					//passing in the pawn & coverage level to get the custom texture path
					string texPath = headCoverage.GetTexPath(pawn, maxCoverageDef.GetModExtension<BodyPartGroupDefExtension>().CoverageLevel);
					if (texPath == null)
						mat = null;
					else
						mat = GraphicDatabase.Get<Graphic_Multi>(texPath, ShaderDatabase.Cutout, Vector2.one, pawn.story.hairColor).MatAt(facing); // Set new graphic
				}
				catch
                {
					mat = null;
                }
				return mat != null;
			}

			//gets the def with the highest coverage level
			private BodyPartGroupDef getMaxCoverageDef(Pawn pawn)
			{
				//dubs bad hygeine clears apparelGraphics when washing, so only check for coverage if the pawn's headgear is actually rendered
				if (pawn.Drawer.renderer.graphics.apparelGraphics.Any())
				{
					//from the worn apparels, get the body part groups they're attached to
					var bodypartGroups = from apparel in pawn.apparel.WornApparel.Where(a => !a.def.apparel.hatRenderedFrontOfFace)
										 from bodyPartGroup in apparel.def.apparel.bodyPartGroups
										 select bodyPartGroup;

					//get the def with the highest coverage level
					return bodypartGroups.OrderByDescending(b => b.GetModExtension<BodyPartGroupDefExtension>().CoverageLevel).FirstOrDefault();
				}
				else
				{
					return BodyPartGroupDefOf.Torso; //using Torso as a default 'None' type bodypartgroupdef
				}
			}
		}
	}
}
