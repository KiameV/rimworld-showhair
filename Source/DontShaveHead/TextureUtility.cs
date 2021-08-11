using System.Linq;
using UnityEngine;
using Verse;

namespace ShowHair
{
	public static class TextureUtility
	{
		//get lowest pixel as percentage of height, to account for different hair resolutions
		public static int GetBottomPixelPercentage(Pawn pawn, string texPath, Rot4 rot)
		{
			//load the current hair mat
			var material = GraphicDatabase.Get<Graphic_Multi>(texPath, ShaderDatabase.Cutout, Vector2.one, pawn.story.hairColor).MatAt(rot);

			//get current hair texture
			Texture2D hairTexture = getReadableTexture((Texture2D)material.mainTexture);

			//get the percentage above the bottom
			double percentage = ((double)getLowestPixel(hairTexture) / (double)hairTexture.height) * 100;

			return (int)percentage;
		}

		private static int getLowestPixel(Texture2D hairTexture)
		{
			//start from bottom row, iterate to top
			for (int y = 0; y < hairTexture.height; y++)
			{
				//get pixels one row at a time
				var pixelsRow = hairTexture.GetPixels(0, y, hairTexture.width, 1);

				if (pixelsRow.Any(c => c.a > 0f))
				{
					//if we find a non clear pixel, return;
					return y;
				}
			}

			return -1;
		}

		private static Texture2D getReadableTexture(Texture2D texture)
		{
			// Create a temporary RenderTexture of the same size as the texture
			RenderTexture tmp = RenderTexture.GetTemporary(
								texture.width,
								texture.height,
								0,
								RenderTextureFormat.Default,
								RenderTextureReadWrite.Linear);

			// Blit the pixels on texture to the RenderTexture
			Graphics.Blit(texture, tmp);

			// Backup the currently set RenderTexture
			RenderTexture previous = RenderTexture.active;

			// Set the current RenderTexture to the temporary one we created
			RenderTexture.active = tmp;

			// Create a new readable Texture2D to copy the pixels to it
			Texture2D readableTexture = new Texture2D(texture.width, texture.height);

			// Copy the pixels from the RenderTexture to the new Texture
			readableTexture.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
			readableTexture.Apply();

			// Reset the active RenderTexture
			RenderTexture.active = previous;

			// Release the temporary RenderTexture
			RenderTexture.ReleaseTemporary(tmp);

			return readableTexture;
		}
	}
}
