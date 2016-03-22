using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Gramma.LanguageModel.Greek.TrainingSources.Perseus
{
	/// <summary>
	/// The BETA code string of all Perseus data seem to have lower case characters and numbers.
	/// This class offers methods to strip numbers and restore case according to the BETA code specification.
	/// </summary>
	public static class Normalizer
	{
		#region Private fields

		private static Regex stripNumericsRegex = new Regex("[0-9]+", RegexOptions.Compiled | RegexOptions.Singleline);

		#endregion

		#region Public methods

		/// <summary>
		/// Convert a perseus BETA string to a proper BETA string according to the BETA specification.
		/// </summary>
		/// <param name="beta">The string to convert.</param>
		/// <returns>Returns the BETA string according to the specification.</returns>
		public static string NormalizeBeta(this string beta)
		{
			if (beta == null) return null;

			return beta
				.ToUpperInvariant()
				.Replace("^", String.Empty)
				.Replace("_", String.Empty);
		}

		/// <summary>
		/// Remove numeric characters from string.
		/// </summary>
		/// <param name="str">The string to convert.</param>
		/// <returns>Returns the converted string.</returns>
		public static string StripNumerics(this string str)
		{
			if (str == null) throw new ArgumentNullException("str");

			return stripNumericsRegex.Replace(str, String.Empty);
		}

		/// <summary>
		/// Remove in-word hyphens such as those appearing in lemmata in Perseus data sources.
		/// Following the hyphen, pneumata over vowels are removed.
		/// </summary>
		public static string StripHyphens(this string str)
		{
			if (str == null) throw new ArgumentNullException("str");

			if (str.Length <= 1) return str;

			var stringBuilder = new StringBuilder(str.Length);

			bool hyphenFound = false;

			for (int i = 0; i < str.Length; i++)
			{
				char c = str[i];

				if (c == '-')
				{
					hyphenFound = true;
					continue;
				}

				if (hyphenFound)
				{
					switch (c)
					{
						case 'ἀ':
						case 'ἁ':
							c = 'α';
							break;

						case 'ἐ':
						case 'ἑ':
							c = 'ε';
							break;

						case 'ἠ':
						case 'ἡ':
							c = 'η';
							break;

						case 'ἰ':
						case 'ἱ':
							c = 'ι';
							break;

						case 'ὀ':
						case 'ὁ':
							c = 'ο';
							break;

						case 'ὑ':
							c = 'υ';
							break;

						case 'ὠ':
						case 'ὡ':
							c = 'ω';
							break;
					}
				}

				stringBuilder.Append(c);

			}

			return stringBuilder.ToString();
		}

		#endregion
	}
}
