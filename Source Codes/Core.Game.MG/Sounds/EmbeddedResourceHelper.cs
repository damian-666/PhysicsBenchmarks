using Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Core.Game.MG
{
	public static class EmbeddedResourceHelper
	{
		public static string ProjectNamespace { set; get; }

		public static string CheckAndSanitizePath(string fileName)
		{

            return AudioManager.Instance.GetMediaSoundFolder() +"\\"+ fileName;

		}


		public static string CheckAndSanitizeEmbeddedPath(string filename)
		{
			 

			return Serialization.GetGameAssembyNamepace()  + "." + AudioManager.Instance.GetEmbeddedMediaSoundFolder() + "."+ filename;

		}


	}
}
