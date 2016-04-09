using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Grammophone.LanguageModel.TrainingSources;
using System.Text.RegularExpressions;
using System.IO;
using Grammophone.LanguageModel.Grammar;

namespace Grammophone.LanguageModel.Greek.TrainingSources.Tischendorf
{
	/// <summary>
	/// Sentence training source from a Tischendorf's New Testament morphological file.
	/// </summary>
	public class TischendorfSentenceTrainingSource : SentenceTrainingSource
	{
		#region Private fields

		private string filename;

		private Regex charactersStripRegex;

		private TextReader reader;

		private GrammarModel grammarModel;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		public TischendorfSentenceTrainingSource()
		{
			filename = String.Empty;

			charactersStripRegex = new Regex(@"[\(\)]+", RegexOptions.Compiled | RegexOptions.Singleline);
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The book's filename in Tischendorf's New Testament morphological format.
		/// </summary>
		public string Filename
		{
			get
			{
				return filename;
			}
			set
			{
				if (value == null) throw new ArgumentNullException("value");

				filename = value;
			}
		}

		#endregion

		#region Protected methods

		protected override void OpenImplementation()
		{
			reader = new StreamReader(this.Filename, Encoding.UTF8);
		}

		protected override void CloseImplementation()
		{
			if (reader != null)
			{
				reader.Close();

				reader.Dispose();

				reader = null;
			}
		}

		protected override IEnumerable<TaggedSentence> GetDataImplementation()
		{
			grammarModel = this.LanguageProvider.GrammarModel;

			var sentenceWordForms = new List<TaggedWordForm>();

			bool validSentence = true;

			for (var line = reader.ReadLine(); line != null && line.Length > 0; line = reader.ReadLine())
			{
				line = line.Trim();

				if (line.Length == 0) continue;

				var lineComponents = line.Split(' ');

				if (lineComponents.Length < 10) continue;

				string formWithPunctuation = 
					lineComponents[3]
					.Replace("[", String.Empty)
					.Replace("]", String.Empty); // Parts of Tischendorf have with braces, not meaning parentheses.

				char lastFormCharacter = formWithPunctuation[formWithPunctuation.Length - 1];

				string form;

				char punctuation;

				switch (lastFormCharacter)
				{
					case '.':
					case ';':
					case ',':
					case '·': // \x0387
					case '!':
					case '…':
						punctuation = lastFormCharacter;
						form = formWithPunctuation.Substring(0, formWithPunctuation.Length - 1);
						break;

					case '\x00B7': // This is alternative upper stop.
						punctuation = '·'; // \x0387
						form = formWithPunctuation.Substring(0, formWithPunctuation.Length - 1);
						break;

					case '’': // This is the single quote instead of the apostrophe. Replace it with the greek apostrophe.
						punctuation = '\x0000';
						{
							StringBuilder formBuilder = new StringBuilder(formWithPunctuation.Length);
							
							formBuilder.Append(formWithPunctuation.Substring(0, formWithPunctuation.Length - 1));
							formBuilder.Append('᾿'); // Append the correct greek apostrophe \x1FBF.
							
							form = formBuilder.ToString();
						}
						break;

					default:
						punctuation = '\x0000';
						form = formWithPunctuation;
						break;
				}

				// If the form has no tag, discard the sentence.
				if (validSentence)
				{
					string lemma = charactersStripRegex.Replace(lineComponents[9], "");

					string posCode = lineComponents[5];

					var formTag = GetFormTag(posCode, lemma);

					if (formTag != null)
					{
						form = NormalizeSpelling(form);

						sentenceWordForms.Add(new TaggedWordForm(form, lemma, formTag));
					}
					else
					{
						validSentence = false;
					}
				}

				if (punctuation > '\x0000')
				{
					if (validSentence)
					{
						var punctuationTag = GetPunctuationTag(punctuation);

						var punctuationText = punctuation.ToString();

						sentenceWordForms.Add(new TaggedWordForm(punctuationText, punctuationText, punctuationTag));
					}

					switch (punctuation)
					{
						case '.':
						case ';':
						case '·': // \x0387
						case '!':
						case '…':
							if (validSentence)
							{
								yield return new TaggedSentence(sentenceWordForms.ToArray());
							}
							else
							{
								validSentence = true;
							}

							sentenceWordForms.Clear();
							break;
					}
				}
			}

			if (sentenceWordForms.Count > 0 && validSentence)
			{
				yield return new TaggedSentence(sentenceWordForms.ToArray());
			}
		}

		#endregion

		#region Private methods

		private Tag GetFormTag(string posCode, string lemma)
		{
			string[] components = posCode.Split('-');

			if (components.Length == 0) throw new ArgumentException("posCode is empty", "posCode");

			string posType = components[0];

			switch (posType)
			{
				case "V":
					{
						if (components.Length < 2) throw new ArgumentException("posCode should have at least 2 components.", "posCode");

						var verbCode = components[1].TrimStart('2');

						if (verbCode.Length < 3) throw new ArgumentException("posCode[1] as verb should have at least 3 subcomponents", "poscode");

						var tenseCode = verbCode[0];
						var voiceCode = verbCode[1];
						var moodCode = verbCode[2];

						var tenseInflection = GetTenseInflection(tenseCode);
						var voiceInflection = GetVoiceInflection(voiceCode, tenseCode);

						switch (moodCode)
						{
							case 'I':
							case 'S':
							case 'O':
							case 'M':
								{
									var moodInflection = GetMoodInflection(moodCode);

									if (components.Length < 3) throw new ArgumentException("posCode should have at least 3 components.", "posCode");

									var personCode = components[2];

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
							case 'R':
								{
									if (components.Length < 3) throw new ArgumentException("posCode should have at least 3 components.", "posCode");

									var nameCode = components[2];

									var nameInflections = GetNameInflections(nameCode);

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


				case "N":
				case "A":
				case "T":
				case "P":
				case "R":
				case "C":
				case "D":
				case "K":
				case "I":
				case "X":
				case "Q":
					{
						if (components.Length < 2) throw new ArgumentException("posCode should have at least 2 components.", "posCode");

						var nameInflections = GetNameInflections(components[1]);

						if (nameInflections == null) return null;

						TagType tagType = null;

						switch (posType)
						{
							case "N":
								tagType = grammarModel.TagTypes["noun"];
								break;

							case "A":
								if (nameInflections.Count == 0) // This is the sign of indeclinable numeral.
								{
									tagType = grammarModel.TagTypes["numeral"];
									break;
								}

								tagType = grammarModel.TagTypes["adj"];

								string degreeCode = components.Length > 2 ? components[2] : null;

								nameInflections.Add(GetDegreeInflection(degreeCode));
								break;

							case "T":
								tagType = grammarModel.TagTypes["article"];
								break;

							case "P":
							case "R":
							case "C":
							case "D":
							case "K":
							case "I":
							case "X":
							case "Q":
								tagType = grammarModel.TagTypes["pron"];
								break;
						}

						return grammarModel.GetTag(tagType, nameInflections);
					}

				case "S":
				case "F":
					{
						if (components.Length < 2) throw new ArgumentException("posCode should have at least 2 components.", "posCode");

						var inflectionCode = components[1];

						var nameInflections = GetNameInflections(inflectionCode.Substring(inflectionCode.Length - 3, 3));

						if (nameInflections == null) return null;

						var pronounTagType = grammarModel.TagTypes["pron"];

						return grammarModel.GetTag(pronounTagType, nameInflections);
					}

				case "ADV":
					{
						string degreeCode = components.Length > 1 ? components[1] : null;

						var inflections = new Inflection[] { GetDegreeInflection(degreeCode) };

						return grammarModel.GetTag(grammarModel.TagTypes["adv"], inflections);
					}

				case "CONJ":
				case "COND":
					{
						var conjunctionTagType = grammarModel.TagTypes["conj"];

						return grammarModel.GetTag(conjunctionTagType, null, lemma);
					}

				case "PRT":
					{
						TagType tagType;

						switch (lemma)
						{
							case "οὐ": // These are wrongly marked as particles in Tischendorf.
								tagType = grammarModel.TagTypes["adv"];
								break;

							case "μή":
								tagType = grammarModel.TagTypes["conj"];
								break;

							default:
								tagType = grammarModel.TagTypes["partic"];
								break;
						}

						return grammarModel.GetTag(tagType, null, lemma);
					}

				case "PREP":
					{
						var prepositionTagType = grammarModel.TagTypes["prep"];

						return grammarModel.GetTag(prepositionTagType, null, lemma);
					}

				case "INJ":
					{
						var exclamationTagType = grammarModel.TagTypes["exclam"];

						return grammarModel.GetTag(exclamationTagType, null, lemma);
					}

				case "HEB":
					switch (lemma)
					{
						case "ἀμήν":
						case "ὡσαννά":
							{
								var tagType = grammarModel.TagTypes["exclam"];

								return grammarModel.GetTag(tagType, null, lemma);
							}

						case "ἁλληλουϊά":
						case "ἁλληλούια":
						case "ἁλληλούϊα":
							{
								var tagType = grammarModel.TagTypes["exclam"];

								return grammarModel.GetTag(tagType, null, "ἁλληλούϊα");
							}

						default:
							return null;
					}

				case "ARAM":
					return null;

				default:
					throw new ArgumentException(String.Format("Unknown posCode '{0}'", posCode), "posCode");

			}

		}

		private Tag GetPunctuationTag(char punctuation)
		{
			var punctuationInflectionType = grammarModel.TagTypes["[PUNCTUATION]"];

			return grammarModel.GetTag(punctuationInflectionType, null, punctuation.ToString());
		}

		private List<Inflection> GetNameInflections(string inflectionCode)
		{
			var inflections = new List<Inflection>(2);

			/* If it is indeclinable, return no inflections as they are not given. */
			switch (inflectionCode)
			{
				case "PRI":
					return null; // This is Hebrew, or other foreign name.

				case "NUI":
				case "LI":
				case "OI":
					return inflections;
			}

			if (inflectionCode.Length != 3) throw new ArgumentException("Invalid inflectionCode, should have length 3.", "inflectionCode");

			char caseCode = '\0';
			char numberCode = '\0';
			char genderCode = '\0';

			switch (inflectionCode[0])
			{
				case '1':
				case '2':
				case '3':
					caseCode = inflectionCode[1];
					numberCode = inflectionCode[2];
					break;

				default:
					caseCode = inflectionCode[0];
					numberCode = inflectionCode[1];
					genderCode = inflectionCode[2];
					break;
			}

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

				case 'M':
					return moodInflectionType.Inflections["imperat"];

				case 'N':
					return moodInflectionType.Inflections["inf"];

				default:
					throw GetUnsupportedMoodException(moodCode);
			}
		}

		private Inflection GetVoiceInflection(char voiceCode, char tenseCode)
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
					switch (tenseCode)
					{
						case 'P':
						case 'I':
						case 'R':
							return voiceInflectionType.Inflections["mp"];

						default:
							return voiceInflectionType.Inflections["mid"];
					}

				case 'P':
				case 'O': // Passive deponent.
					switch (tenseCode)
					{
						case 'P':
						case 'I':
						case 'R':
							return voiceInflectionType.Inflections["mp"];

						default:
							return voiceInflectionType.Inflections["pass"];
					}

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

				case 'R':
					return tenseInflectionType.Inflections["perf"];

				case 'L':
					return tenseInflectionType.Inflections["plup"];

				case 'X':
					return tenseInflectionType.Inflections["pres"];

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

		/// <summary>
		/// Somehow the Tischendorf New Testament edition does not have proper
		/// junctions in composite words, resulting to unknown words,
		/// for example 'συνπάσχει' instead of 'συμπάσχει'.
		/// Try to fix the most common cases.
		/// </summary>
		private static string NormalizeSpelling(string input)
		{
			if (input == null) throw new ArgumentNullException("input");

			return input
				.Replace("νμ", "μμ")
				.Replace("νκ", "γκ")
				.Replace("συνπ", "συμπ")
				.Replace("συνσ", "συσ")
				.Replace("ἐνπ", "ἐμπ")
				.Replace("ἔνπ", "ἔμπ")
				.Replace("νλ", "λλ");
		}

		#endregion
	}
}
