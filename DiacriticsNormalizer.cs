using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gramma.LanguageModel.Greek.TrainingSources
{
	public static class DiacriticsNormalizer
	{
		/// <summary>
		/// Change the vareia vowel characters of range 1F70 to range 03AC.
		/// </summary>
		/// <param name="input"></param>
		/// <returns></returns>
		public static string NormalizeDashes(this string input)
		{
			if (input == null) throw new ArgumentNullException("input");

			var outputBuilder = new StringBuilder(input.Length);

			for (int i = 0; i < input.Length; i++)
			{
				char inputCharacter = input[i];

				switch (inputCharacter)
				{
					case '\x1F71':
						inputCharacter = 'ά'; // 03AC
						break;

					case '\x1F73':
						inputCharacter = 'έ'; // 03AD
						break;

					case '\x1F75':
						inputCharacter = 'ή'; // 03AE
						break;

					case '\x1F77':
						inputCharacter = 'ί'; // 03AF
						break;

					case '\x1F79':
						inputCharacter = 'ό'; // 03CC
						break;

					case '\x1F7B':
						inputCharacter = 'ύ'; // 03CD
						break;

					case '\x1F7D':
						inputCharacter = 'ώ'; // 03CE
						break;
				}

				outputBuilder.Append(inputCharacter);
			}

			return outputBuilder.ToString();
		}
	}
}
