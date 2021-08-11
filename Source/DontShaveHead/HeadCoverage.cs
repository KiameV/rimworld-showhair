using UnityEngine;
using Verse;

namespace ShowHair
{
	public interface IHeadCoverage
	{
		string GetTexPath(Pawn pawn, Enums.Coverage coverage);
	}

	public static class HeadCoverage
	{
		//nothing covering head
		public class NotCovered : IHeadCoverage
		{
			public string GetTexPath(Pawn pawn, Enums.Coverage coverage)
			{
				//return normal hair
				return pawn.story.hairDef.texPath;
			}
		}

		//head covered, but not using fallback textures
		public class Covered : IHeadCoverage
		{
			public string GetTexPath(Pawn pawn, Enums.Coverage coverage)
			{
				//Log.Warning($"GetTexPath {pawn.story.hairDef.texPath} Coverage: {coverage.ToString()}");
				// Check if custom texture path exists
				if (!ContentFinder<Texture2D>.Get($"{pawn.story.hairDef.texPath}/{coverage}_south", false))
				{
					//if no custom texture
					//Log.Error("-not found");
					return null;
				}

				//Log.Error($"-found {pawn.story.hairDef.texPath}/{coverage}");
				return $"{pawn.story.hairDef.texPath}/{coverage}";
			}
		}
	}
}
