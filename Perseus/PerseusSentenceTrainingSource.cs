using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Gramma.LanguageModel.TrainingSources;
using Gramma.LanguageModel.Grammar;

namespace Gramma.LanguageModel.Greek.TrainingSources.Perseus
{
	/// <summary>
	/// Sentence training source from the Perseus treebanks.
	/// </summary>
	public class PerseusSentenceTrainingSource : SentenceTrainingSource
	{
		#region Private fields

		private string treeBankFilename;

		private BetaImport.BetaConverter betaConverter;

		private XmlReader reader;

		private Stream stream;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		public PerseusSentenceTrainingSource()
		{
			treeBankFilename = "Complete treebank - agdt-1.7.xml";

			betaConverter = new BetaImport.PrecombinedDiacriticsBetaConverter();

			this.Encoding = GreekEncoding.BetaCode;
		}

		#endregion

		#region Properties

		/// <summary>
		/// The name of the tree bank file.
		/// </summary>
		public string TreeBankFilename
		{
			get
			{
				return treeBankFilename;
			}
			set
			{
				if (value == null) throw new ArgumentNullException("value");

				treeBankFilename = value;
			}
		}

		/// <summary>
		/// The encoding used for greek literals.
		/// Default is <see cref="GreekEncoding.BetaCode"/>.
		/// </summary>
		public GreekEncoding Encoding { get; set; }

		#endregion

		#region Protected methods

		protected override void OpenImplementation()
		{
			stream = new FileStream(this.TreeBankFilename, FileMode.Open, FileAccess.Read);

			reader = new XmlTextReader(stream);
		}

		protected override void CloseImplementation()
		{
			if (reader != null)
			{
				reader.Close();

				((IDisposable)reader).Dispose();

				reader = null;
			}

			if (stream != null)
			{
				stream.Close();

				stream.Dispose();

				stream = null;
			}
		}

		protected override IEnumerable<TaggedSentence> GetDataImplementation()
		{
			var grammarModel = this.LanguageProvider.GrammarModel;

			while (reader.Read())
			{
				if (!reader.ReadToFollowing("sentence")) yield break;

				var wordForms = new List<TaggedWordForm>();

				string previousRawForm = String.Empty;

				string beforeHyphenRawForm = String.Empty;

				for (bool nextWordExists = reader.ReadToDescendant("word"); nextWordExists; nextWordExists = reader.ReadToNextSibling("word"))
				{
					string rawForm = reader.GetAttribute("form").NormalizeBeta();

					if (rawForm == null || previousRawForm == rawForm) continue;

					if (this.Encoding == GreekEncoding.BetaCode) rawForm = rawForm.NormalizeBeta();

					if (beforeHyphenRawForm.Length > 0)
					{
						rawForm = beforeHyphenRawForm + rawForm;

						beforeHyphenRawForm = String.Empty;
					}

					string form;

					switch (this.Encoding)
					{
						case GreekEncoding.BetaCode:
							form = this.LanguageProvider.NormalizeWord(betaConverter.Convert(rawForm))
								.Trim(' ', '[', ']', '"', '“', '”', '«', '»', '\r', '\n');
							break;

						case GreekEncoding.Unicode:
							form = this.LanguageProvider.NormalizeWord(rawForm)
								.Trim(' ', '[', ']', '"', '“', '”', '«', '»', '\r', '\n');
							break;

						default:
							throw new ApplicationException(String.Format("Unsupported Encoding value for PerseusSentenceTrainingSource: {0}", this.Encoding));
					}

					string tagCode = reader.GetAttribute("postag");

					if (tagCode == null) continue;

					if (tagCode.Length != 9) continue;

					char posCode = tagCode[0];

					string rawLemma = reader.GetAttribute("lemma");

					if (rawLemma == null) continue;

					if (this.Encoding == GreekEncoding.BetaCode) rawLemma = rawLemma.NormalizeBeta();

					string lemma;

					switch (posCode)
					{
						case 'u':
						case '-':
							lemma = form;
							break;

						default:
							if (this.Encoding == GreekEncoding.BetaCode)
								lemma = betaConverter.Convert(rawLemma).StripNumerics();
							else
								lemma = rawLemma.StripNumerics();
							break;
					}

					TagType tagType;

					Tag tag;

					Inflection[] inflections;

					switch (tagCode[0]) // Tag type.
					{
						case 'n':
							tagType = grammarModel.TagTypes["noun"];

							inflections = new Inflection[]
							{
								GetCaseInflection(tagCode),
								GetGenderInflection(tagCode)
							};

							tag = grammarModel.GetTag(tagType, inflections);

							break;

						case 'v':
							tagType = grammarModel.TagTypes["verb"];

							inflections = new Inflection[]
							{
								GetPersonInflection(tagCode),
								GetTenseInflection(tagCode),
								GetMoodInflection(tagCode),
								GetVoiceInflection(tagCode)
							};

							tag = grammarModel.GetTag(tagType, inflections);

							break;

						case 't':
							tagType = grammarModel.TagTypes["part"];

							inflections = new Inflection[]
							{
								GetCaseInflection(tagCode),
								GetGenderInflection(tagCode),
								GetTenseInflection(tagCode),
								GetVoiceInflection(tagCode)
							};

							tag = grammarModel.GetTag(tagType, inflections);

							break;

						case 'a':
							tagType = grammarModel.TagTypes["adj"];

							inflections = new Inflection[]
							{
								GetCaseInflection(tagCode),
								GetGenderInflection(tagCode),
								GetDegreeInflection(tagCode)
							};

							tag = grammarModel.GetTag(tagType, inflections);

							break;

						case 'd':
							tagType = grammarModel.TagTypes["adv"];

							inflections = new Inflection[]
							{
								GetDegreeInflection(tagCode)
							};

							tag = grammarModel.GetTag(tagType, inflections);

							break;

						case 'l':
							tagType = grammarModel.TagTypes["article"];

							inflections = new Inflection[]
							{
								GetCaseInflection(tagCode),
								GetGenderInflection(tagCode)
							};

							tag = grammarModel.GetTag(tagType, inflections);

							break;

						case 'g':
							switch (lemma)
							{
								case "μή":
									tagType = grammarModel.TagTypes["conj"];
									break;

								default:
									tagType = grammarModel.TagTypes["partic"];
									break;
							}

							tag = grammarModel.GetTag(tagType, null, lemma);

							break;

						case 'c':
							tagType = grammarModel.TagTypes["conj"];

							tag = grammarModel.GetTag(tagType, null, lemma);

							break;

						case 'r':
							tagType = grammarModel.TagTypes["prep"];

							tag = grammarModel.GetTag(tagType, null, lemma);

							break;

						case 'p':
							tagType = grammarModel.TagTypes["pron"];

							inflections = new Inflection[]
							{
								GetCaseInflection(tagCode),
								GetGenderInflection(tagCode)
							};

							tag = grammarModel.GetTag(tagType, inflections);

							break;

						case 'e':
						case 'i':
							tagType = grammarModel.TagTypes["exclam"];

							tag = grammarModel.GetTag(tagType, null, lemma);

							break;

						case 'u':
							tagType = grammarModel.TagTypes["[PUNCTUATION]"];

							// Check for alternative upper stop.
							if (form == "\x00B7") form = "\x0387";

							tag = grammarModel.GetTag(tagType, null, form);

							break;

						case 'm':
							tagType = grammarModel.TagTypes["numeral"];

							tag = grammarModel.GetTag(tagType);

							break;

						default:
							switch (form)
							{
								case ";":
								case ".":
								case ":":
								case "·":
									tagType = grammarModel.TagTypes["[PUNCTUATION]"];

									tag = grammarModel.GetTag(tagType, null, form);

									break;

								case "\x00B7": // This is alternative upper stop.
									tagType = grammarModel.TagTypes["[PUNCTUATION]"];

									tag = grammarModel.GetTag(tagType, null, "\x0387");

									break;

								case "-":
									beforeHyphenRawForm = previousRawForm;
									continue;

								default:
									previousRawForm = rawForm;
									continue;
							}

							break;

					}

					previousRawForm = rawForm;

					wordForms.Add(new TaggedWordForm(form, lemma, tag));

				}

				yield return new TaggedSentence(wordForms.ToArray());

			}
		}

		#endregion

		#region Private methods

		private static void CheckTagCode(string tagCode)
		{
			if (tagCode == null) throw new ArgumentNullException("tagCode");
			if (tagCode.Length != 9) throw new ArgumentException("the tagCode is invalid", "tagCode");
		}

		private Inflection GetPersonInflection(string tagCode)
		{
			CheckTagCode(tagCode);

			var grammarModel = this.LanguageProvider.GrammarModel;

			char personCode = tagCode[1];

			char numberCode = tagCode[2];

			var personInflectionType = grammarModel.InflectionTypes["person"];

			switch (numberCode)
			{
				case 'p':
					switch (personCode)
					{
						case '1':
							return personInflectionType.Inflections["1st pl"];

						case '2':
							return personInflectionType.Inflections["2nd pl"];

						default:
							return personInflectionType.Inflections["3rd pl"];
					}

				case 'd':
					switch (personCode)
					{
						case '2':
							return personInflectionType.Inflections["2nd dual"];

						default:
							return personInflectionType.Inflections["3rd dual"];
					}

				default:
					switch (personCode)
					{
						case '1':
							return personInflectionType.Inflections["1st sg"];

						case '2':
							return personInflectionType.Inflections["2nd sg"];

						default:
							return personInflectionType.Inflections["3rd sg"];
					}
			}
		}

		private Inflection GetCaseInflection(string tagCode)
		{
			CheckTagCode(tagCode);

			var grammarModel = this.LanguageProvider.GrammarModel;

			char caseCode = tagCode[7];

			char numberCode = tagCode[2];

			var caseInflectionType = grammarModel.InflectionTypes["case"];

			switch (numberCode)
			{
				case 'p':
					switch (caseCode)
					{
						case 'v':
							return caseInflectionType.Inflections["voc pl"];

						case 'g':
							return caseInflectionType.Inflections["gen pl"];

						case 'd':
							return caseInflectionType.Inflections["dat pl"];

						case 'a':
							return caseInflectionType.Inflections["acc pl"];

						default:
							return caseInflectionType.Inflections["nom pl"];
					}

				case 'd':
					switch (caseCode)
					{
						case 'v':
							return caseInflectionType.Inflections["voc dual"];

						case 'g':
							return caseInflectionType.Inflections["gen dual"];

						case 'd':
							return caseInflectionType.Inflections["dat dual"];

						case 'a':
							return caseInflectionType.Inflections["acc dual"];

						default:
							return caseInflectionType.Inflections["nom dual"];
					}

				default:
					switch (caseCode)
					{
						case 'v':
							return caseInflectionType.Inflections["voc sg"];

						case 'g':
							return caseInflectionType.Inflections["gen sg"];

						case 'd':
							return caseInflectionType.Inflections["dat sg"];

						case 'a':
							return caseInflectionType.Inflections["acc sg"];

						default:
							return caseInflectionType.Inflections["nom sg"];
					}
			}
		}

		private Inflection GetTenseInflection(string tagCode)
		{
			CheckTagCode(tagCode);

			var grammarModel = this.LanguageProvider.GrammarModel;

			var tenseInflectionType = grammarModel.InflectionTypes["tense"];

			char tenseCode = tagCode[3];

			switch (tenseCode)
			{
				case 'i':
					return tenseInflectionType.Inflections["imperf"];

				case 'r':
					return tenseInflectionType.Inflections["perf"];

				case 'l':
					return tenseInflectionType.Inflections["plup"];

				case 't':
					return tenseInflectionType.Inflections["futperf"];

				case 'f':
					return tenseInflectionType.Inflections["fut"];

				case 'a':
					return tenseInflectionType.Inflections["aor"];

				default: // present.
					return tenseInflectionType.Inflections["pres"];
			}
		}

		private Inflection GetMoodInflection(string tagCode)
		{
			CheckTagCode(tagCode);

			var grammarModel = this.LanguageProvider.GrammarModel;

			var moodInflectionType = grammarModel.InflectionTypes["mood"];

			char moodCode = tagCode[4];

			switch (moodCode)
			{
				case 'i':
					return moodInflectionType.Inflections["ind"];

				case 's':
					return moodInflectionType.Inflections["subj"];

				case 'o':
					return moodInflectionType.Inflections["opt"];

				case 'm':
					return moodInflectionType.Inflections["imperat"];

				default:
					return moodInflectionType.Inflections["inf"];
			}
		}

		private Inflection GetVoiceInflection(string tagCode)
		{
			CheckTagCode(tagCode);

			var grammarModel = this.LanguageProvider.GrammarModel;

			var voiceInflectionType = grammarModel.InflectionTypes["voice"];

			char voiceCode = tagCode[5];

			switch (voiceCode)
			{
				case 'p':
					return voiceInflectionType.Inflections["pass"];

				case 'm':
					return voiceInflectionType.Inflections["mid"];

				case 'e':
					return voiceInflectionType.Inflections["mp"];

				default:
					return voiceInflectionType.Inflections["act"];
			}
		}

		private Inflection GetGenderInflection(string tagCode)
		{
			CheckTagCode(tagCode);

			var grammarModel = this.LanguageProvider.GrammarModel;

			var genderInflectionType = grammarModel.InflectionTypes["gender"];

			char genderCode = tagCode[6];

			switch (genderCode)
			{
				case 'f':
					return genderInflectionType.Inflections["fem"];

				case 'n':
					return genderInflectionType.Inflections["neut"];

				default:
					return genderInflectionType.Inflections["masc"];
			}
		}

		private Inflection GetDegreeInflection(string tagCode)
		{
			CheckTagCode(tagCode);

			var grammarModel = this.LanguageProvider.GrammarModel;

			var degreeInflectionType = grammarModel.InflectionTypes["degree"];

			char degreeCode = tagCode[8];

			switch (degreeCode)
			{
				case 'c':
					return degreeInflectionType.Inflections["comp"];

				case 's':
					return degreeInflectionType.Inflections["superl"];

				default:
					return degreeInflectionType.Inflections["pos"];

			}

		}

		#endregion
	}
}
