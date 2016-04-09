using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Grammophone.LanguageModel.Grammar;

namespace Grammophone.LanguageModel.Greek.TrainingSources
{
	/// <summary>
	/// Interprets CCAT morphological annotations, which are based on Packard's annotations.
	/// </summary>
	public class CcatMorphologyParser
	{
		#region Private fields

		private GrammarModel grammarModel;

		private char[] posComponentsSplitCharacters = new char[] { '-' };

		#endregion

		#region Construction

		public CcatMorphologyParser(GrammarModel grammarModel)
		{
			if (grammarModel == null) throw new ArgumentNullException("grammarModel");

			this.grammarModel = grammarModel;
		}

		#endregion

		#region Public methods

		/// <summary>
		/// Make punctionation tag for the given punctuation character.
		/// </summary>
		/// <param name="punctuation">The punctuation character.</param>
		/// <returns>The tag of type 'punctuation'.</returns>
		public Tag GetPunctuationTag(char punctuation)
		{
			var punctuationInflectionType = grammarModel.TagTypes["[PUNCTUATION]"];

			return grammarModel.GetTag(punctuationInflectionType, null, punctuation.ToString());
		}

		/// <summary>
		/// Get the tag corresponding to a part-of-speech code formed according to CCAT.
		/// </summary>
		/// <param name="posCode">The part-of-speech code.</param>
		/// <param name="lemma">The lemma of the word, to be included in the tag in case where the tag is 'singular'.</param>
		/// <returns>Returns the tag, if possible, else null.</returns>
		/// <remarks>
		/// The method returns null when the word is Hebrew, especially when it has not been hellenized.
		/// </remarks>
		/// <exception cref="ArgumentException">
		/// When the <paramref name="posCode"/> is not properly formed.
		/// </exception>
		public Tag GetFormTag(string posCode, string lemma)
		{
			string[] components = posCode.Split(posComponentsSplitCharacters, StringSplitOptions.RemoveEmptyEntries);

			if (components.Length == 0) throw new ArgumentException("posCode is empty", "posCode");

			string posType = components[0];

			if (posType.Length < 1) throw new ArgumentException("No part of speech prefix is present", "posCode");

			char posTypePrefix = posType[0];

			switch (posTypePrefix)
			{
				case 'V':
					{
						if (components.Length < 2) throw new ArgumentException("posCode should have at least 2 components.", "posCode");

						var verbCode = components[1];

						if (verbCode.Length < 3) throw new ArgumentException("posCode[1] as verb should have at least 3 subcomponents", "poscode");

						var tenseCode = verbCode[0];
						var voiceCode = verbCode[1];
						var moodCode = verbCode[2];

						var tenseInflection = GetTenseInflection(tenseCode);
						var voiceInflection = GetVoiceInflection(voiceCode);

						switch (moodCode)
						{
							case 'I':
							case 'S':
							case 'O':
							case 'D':
								{
									var moodInflection = GetMoodInflection(moodCode);

									if (verbCode.Length < 5) throw new ArgumentException("verb section should have at least 5 components.", "posCode");

									var personCode = verbCode.Substring(3);

									var personInflection = GetPersonInflection(personCode);

									var verbTagType = grammarModel.TagTypes["verb"];

									return grammarModel.GetTag(verbTagType, new Inflection[] { tenseInflection, voiceInflection, moodInflection, personInflection });
								}

							case 'N':
								{
									var moodInflection = GetMoodInflection(moodCode);

									var verbTagType = grammarModel.TagTypes["verb"];

									return grammarModel.GetTag(verbTagType, new Inflection[] { tenseInflection, voiceInflection, moodInflection });
								}

							case 'P':
								{
									if (verbCode.Length < 6)
										throw new ArgumentException("verb section should have at least 6 components.", "posCode");

									var nameCode = verbCode.Substring(3);

									var nameInflections = GetNameInflections(nameCode, lemma);

									if (nameInflections == null) return null;

									var verbTagType = grammarModel.TagTypes["part"];

									var inflections = new List<Inflection>(6) { tenseInflection, voiceInflection };

									inflections.AddRange(nameInflections);

									return grammarModel.GetTag(verbTagType, inflections);
								}

							default:
								throw GetUnsupportedMoodException(moodCode);
						}

					}


				case 'N':
				case 'A':
				case 'R':
					{
						if (components.Length < 2 || posType.Length < 2) return null; // Possibly Hebrew.

						var nameInflections = GetNameInflections(components[1], lemma);

						if (nameInflections == null) return null;

						TagType tagType = null;

						switch (posTypePrefix)
						{
							case 'N':
								tagType = grammarModel.TagTypes["noun"];
								break;

							case 'A':
								tagType = grammarModel.TagTypes["adj"];

								string degreeCode = components.Length > 2 ? components[2] : null;

								nameInflections.Add(GetDegreeInflection(degreeCode));
								break;

							case 'R':
								if (posType.Length > 1 && posType[1] == 'A')
									tagType = grammarModel.TagTypes["article"];
								else
									tagType = grammarModel.TagTypes["pron"];
								break;
						}

						return grammarModel.GetTag(tagType, nameInflections);
					}

				case 'D':
					{
						string degreeCode = components.Length > 1 ? components[1] : null;

						var inflections = new Inflection[] { GetDegreeInflection(degreeCode) };

						return grammarModel.GetTag(grammarModel.TagTypes["adv"], inflections);
					}

				case 'C':
					{
						var conjunctionTagType = grammarModel.TagTypes["conj"];

						return grammarModel.GetTag(conjunctionTagType, null, lemma);
					}

				case 'X':
					{
						var particleTagType = grammarModel.TagTypes["partic"];

						return grammarModel.GetTag(particleTagType, null, lemma);
					}

				case 'P':
					{
						var prepositionTagType = grammarModel.TagTypes["prep"];

						return grammarModel.GetTag(prepositionTagType, null, lemma);
					}

				case 'I':
					{
						var exclamationTagType = grammarModel.TagTypes["exclam"];

						return grammarModel.GetTag(exclamationTagType, null, lemma);
					}

				case 'M':
					{
						var numeralTagType = grammarModel.TagTypes["numeral"];

						return grammarModel.GetTag(numeralTagType);
					}

				default:
					throw new ArgumentException(String.Format("Unknown posCode '{0}'", posCode), "posCode");

			}

		}

		#endregion

		#region Private methods

		private List<Inflection> GetNameInflections(string inflectionCode, string lemma)
		{
			var inflections = new List<Inflection>(2);

			/* If it is indeclinable, return no inflections as they are not given. */
			switch (inflectionCode)
			{
				case "PRI":
				case "NUI":
				case "LI":
				case "OI":
					return inflections;
			}

			char caseCode = '\0';
			char numberCode = '\0';
			char genderCode = '\0';

			if (inflectionCode.Length < 3)
			{
				if (inflectionCode.Length == 2)
				{
					switch (lemma)
					{
						case "φισων":
						case "γηων":
						case "τίγρις":
							genderCode = 'M';
							break;

						case "γομορρα":
						case "σιδῶν":
						case "καππαδοκία":
							genderCode = 'F';
							break;

						case "σοδομα":
							genderCode = 'N';
							break;

						default:
							return null;
					}
				}
				else
				{
					return null;
				}
			}
			else
			{
				genderCode = inflectionCode[2];
			}

			caseCode = inflectionCode[0];
			numberCode = inflectionCode[1];

			if (caseCode == '/' || numberCode == '/') return null;

			var caseInflectionType = grammarModel.InflectionTypes["case"];

			switch (caseCode)
			{
				case 'N':
					switch (numberCode)
					{
						case 'S':
							inflections.Add(caseInflectionType.Inflections["nom sg"]);
							break;

						case 'P':
							inflections.Add(caseInflectionType.Inflections["nom pl"]);
							break;

						case 'D':
							inflections.Add(caseInflectionType.Inflections["nom dual"]);
							break;

						default:
							throw GetUnsupportedNumberCodeException(numberCode);
					}
					break;

				case 'G':
					switch (numberCode)
					{
						case 'S':
							inflections.Add(caseInflectionType.Inflections["gen sg"]);
							break;

						case 'P':
							inflections.Add(caseInflectionType.Inflections["gen pl"]);
							break;

						case 'D':
							inflections.Add(caseInflectionType.Inflections["gen dual"]);
							break;

						default:
							throw GetUnsupportedNumberCodeException(numberCode);
					}
					break;

				case 'D':
					switch (numberCode)
					{
						case 'S':
							inflections.Add(caseInflectionType.Inflections["dat sg"]);
							break;

						case 'P':
							inflections.Add(caseInflectionType.Inflections["dat pl"]);
							break;

						case 'D':
							inflections.Add(caseInflectionType.Inflections["dat dual"]);
							break;

						default:
							throw GetUnsupportedNumberCodeException(numberCode);
					}
					break;

				case 'A':
					switch (numberCode)
					{
						case 'S':
							inflections.Add(caseInflectionType.Inflections["acc sg"]);
							break;

						case 'P':
							inflections.Add(caseInflectionType.Inflections["acc pl"]);
							break;

						case 'D':
							inflections.Add(caseInflectionType.Inflections["acc dual"]);
							break;

						default:
							throw GetUnsupportedNumberCodeException(numberCode);
					}
					break;

				case 'V':
					switch (numberCode)
					{
						case 'S':
							inflections.Add(caseInflectionType.Inflections["voc sg"]);
							break;

						case 'P':
							inflections.Add(caseInflectionType.Inflections["voc pl"]);
							break;

						case 'D':
							inflections.Add(caseInflectionType.Inflections["voc dual"]);
							break;

						default:
							throw GetUnsupportedNumberCodeException(numberCode);
					}
					break;

				default:
					throw GetUnsupportedCaseCodeException(caseCode);
			}

			var genderInflectionType = grammarModel.InflectionTypes["gender"];

			switch (genderCode)
			{
				case 'M':
					inflections.Add(genderInflectionType.Inflections["masc"]);
					break;

				case 'F':
					inflections.Add(genderInflectionType.Inflections["fem"]);
					break;

				case 'N':
					inflections.Add(genderInflectionType.Inflections["neut"]);
					break;

				case '/':
					return null;

				default:
					throw GetUnsupportedGenderCodeException(genderCode);
			}

			return inflections;
		}

		private Inflection GetPersonInflection(string personCode)
		{
			var personInflectionType = grammarModel.InflectionTypes["person"];

			switch (personCode)
			{
				case "1S":
					return personInflectionType.Inflections["1st sg"];

				case "2S":
					return personInflectionType.Inflections["2nd sg"];

				case "3S":
					return personInflectionType.Inflections["3rd sg"];

				case "1P":
					return personInflectionType.Inflections["1st pl"];

				case "2P":
					return personInflectionType.Inflections["2nd pl"];

				case "3P":
					return personInflectionType.Inflections["3rd pl"];

				case "2D":
					return personInflectionType.Inflections["2nd dual"];

				case "3D":
					return personInflectionType.Inflections["3rd dual"];

				default:
					throw GetUnsupportedPersonCodeException(personCode);
			}
		}

		private Inflection GetMoodInflection(char moodCode)
		{
			var moodInflectionType = grammarModel.InflectionTypes["mood"];

			switch (moodCode)
			{
				case 'I':
					return moodInflectionType.Inflections["ind"];

				case 'S':
					return moodInflectionType.Inflections["subj"];

				case 'O':
					return moodInflectionType.Inflections["opt"];

				case 'D':
					return moodInflectionType.Inflections["imperat"];

				case 'N':
					return moodInflectionType.Inflections["inf"];

				default:
					throw GetUnsupportedMoodException(moodCode);
			}
		}

		private Inflection GetVoiceInflection(char voiceCode)
		{
			var voiceInflectionType = grammarModel.InflectionTypes["voice"];

			switch (voiceCode)
			{
				case 'A':
				case 'Q': // Impersonal active.
				case 'X': // No voice.
					return voiceInflectionType.Inflections["act"];

				case 'M':
				case 'D': // Middle deponent.
					return voiceInflectionType.Inflections["mid"];

				case 'P':
				case 'O': // Passive deponent.
					return voiceInflectionType.Inflections["pass"];

				case 'E':
				case 'N': // Middle or passive deponent.
					return voiceInflectionType.Inflections["mp"];

				default:
					throw GetUnsupportedVoiceException(voiceCode);
			}
		}

		private Inflection GetTenseInflection(char tenseCode)
		{
			var tenseInflectionType = grammarModel.InflectionTypes["tense"];

			switch (tenseCode)
			{
				case 'P':
					return tenseInflectionType.Inflections["pres"];

				case 'I':
					return tenseInflectionType.Inflections["imperf"];

				case 'F':
					return tenseInflectionType.Inflections["fut"];

				case 'A':
					return tenseInflectionType.Inflections["aor"];

				case 'X':
					return tenseInflectionType.Inflections["perf"];

				case 'Y':
					return tenseInflectionType.Inflections["plup"];

				default:
					throw GetUnsupportedTenseException(tenseCode);
			}
		}

		private Inflection GetDegreeInflection(string degreeCode)
		{
			var degreeInflectionType = grammarModel.InflectionTypes["degree"];

			switch (degreeCode)
			{
				case "C":
					return degreeInflectionType.Inflections["comp"];

				case "S":
					return degreeInflectionType.Inflections["superl"];

				default:
					return degreeInflectionType.Inflections["pos"];
			}
		}

		private static Exception GetUnsupportedMoodException(char moodCode)
		{
			return new ArgumentException(String.Format("Unsupported moodCode '{0}'.", moodCode));
		}

		private static Exception GetUnsupportedVoiceException(char voiceCode)
		{
			return new ArgumentException(String.Format("Unsupported voiceCode '{0}'.", voiceCode));
		}

		private static Exception GetUnsupportedTenseException(char tenseCode)
		{
			return new ArgumentException(String.Format("Unsupported tenseCode '{0}'.", tenseCode));
		}

		private static Exception GetUnsupportedPersonCodeException(string personCode)
		{
			return new ArgumentException(String.Format("Unsupported personCode '{0}'.", personCode));
		}

		private static Exception GetUnsupportedNumberCodeException(char numberCode)
		{
			return new ArgumentException(String.Format("Unsupported number code '{0}'.", numberCode));
		}

		private static Exception GetUnsupportedCaseCodeException(char caseCode)
		{
			return new ArgumentException(String.Format("Unsupported case code '{0}'.", caseCode));
		}

		private static Exception GetUnsupportedGenderCodeException(char genderCode)
		{
			return new ArgumentException(String.Format("Unsupported gender code '{0}'.", genderCode));
		}

		#endregion
	}
}
