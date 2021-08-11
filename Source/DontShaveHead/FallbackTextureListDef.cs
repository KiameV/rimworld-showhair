using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ShowHair
{
	//Def structure to hold defs from Defs/FallbackTextureList.xml

	//If enabled, FallbackTextures are used when no custom texture exists for a particular hair style(instead of using the default 'shaved' hair type).

	//The existing hair will have its length calculated, and will return the list of texturePaths of comparable length, depending on whhich bottomPixelRange its bottom pixel falls in e.g. short hair will return short hair styles etc.

	//The mod will then semi-random-but-deterministically select a hair style from the list. This is done by hashing and comparing the original texture path string vs the texturePathList strings, and selecting the closest match. This way it should always return the same 'random' hair style.


	public class FallbackTextureListDef : Def
	{
		public Range bottomPixelRange;
		//	bottomPixelRange is the range for the bottom pixel of a texture to fall in to determines the type of hair.It's measured in percentage, to account for different texture sizes (128x128 vs 256x256 etc)

		//e.g.
		//	if the bottom pixel of the texture is 41% above the bottom, then it's FallbackTextures_Short
		//	if the bottom pixel of the texture is 27% above the bottom, then it's FallbackTextures_Medium
		//	if the bottom pixel of the texture is 12% above the bottom, then it's FallbackTextures_Long

		public List<string> texturePathList;

		public string GetClostestFallbackTexturePath(string texPath)
		{
			//create hash from current hair path to compare against
			var currentHairMD5 = this.createMD5String(texPath.Split('/').Last());

			//compares the hashes of the texture paths to retreive a semi-random-but-deterministic texture, so it should pick the same texture each time.
			var closestFallback = this.getFallbackTextures().OrderBy(h => h.Hash).OrderByDescending(h => h.Hash.CompareTo(currentHairMD5)).First();

			return closestFallback.Path;
		}

		private List<FallbackTexture> fallbackTextures;
		private List<FallbackTexture> getFallbackTextures()
		{
			//if fallbackTextures hasn't been initialised yet
			if (this.fallbackTextures == null)
			{
				//for each of the texture paths in the def xml file, add the path & its hash
				this.fallbackTextures = this.texturePathList.Select(path => new FallbackTexture()
				{
					Hash = this.createMD5String(path), //creating a hash to use for comparison
					Path = path
				}).ToList();

			}
			return this.fallbackTextures;
		}

		private string createMD5String(string input)
		{
			// Use input string to calculate MD5 hash
			using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
			{
				byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
				byte[] hashBytes = md5.ComputeHash(inputBytes);

				// Convert the byte array to hexadecimal string
				StringBuilder sb = new StringBuilder();
				for (int i = 0; i < hashBytes.Length; i++)
				{
					sb.Append(hashBytes[i].ToString("X2"));
				}
				return sb.ToString();
			}
		}
	}

	public class Range
	{
		public int start;
		public int end;
	}

	public class FallbackTexture
	{
		public string Hash { get; set; }
		public string Path { get; set; }
	}
}
