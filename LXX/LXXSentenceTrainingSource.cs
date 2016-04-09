using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Grammophone.LanguageModel.Grammar;
using Grammophone.LanguageModel.TrainingSources;

namespace Grammophone.LanguageModel.Greek.TrainingSources.LXX
{
	/// <summary>
	/// Sentences training source from the morphological Old Testament, as produced by the
	/// LXXCombiner F# project.
	/// </summary>
	public class LXXSentenceTrainingSource : SentenceTrainingSource
	{
		#region Private fields

		private string filename;

		private TextReader reader;

		private GrammarModel grammarModel;

		#endregion

		#region Construction

		public LXXSentenceTrainingSource()
		{
			filename = String.Empty;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The morphological Old Testament filename.
		/// </summary>
		/// <remarks>
		/// This file is produced by the LXXCombiner F# project.
		/// </remarks>
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

			grammarModel = this.LanguageProvider.GrammarModel;
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
			var sentenceWordForms = new List<TaggedWordForm>();

			var morphologyParser = new CcatMorphologyParser(this.LanguageProvider.GrammarModel);

			for (var line = reader.ReadLine(); line != null && line.Length > 0; line = reader.ReadLine())
			{
				line = line.Trim();

				if (line.Length == 0) continue;

				var tabComponents = line.Split('\t');

				if (tabComponents.Length < 4) continue;

				var sentenceLine = tabComponents[3];

				var sentenceComponents = sentenceLine.Split();

				string formWithPunctuation = null;
				string lemma = null;
				string form = null;
				char punctuation = '\x0000';

				bool isSentenceAcceptable = true;

				for (int i = 0; i < sentenceComponents.Length; i++)
				{
					var sentenceComponent = sentenceComponents[i];

					if (sentenceComponent.Length == 0) continue;

					int dashIndex = sentenceComponent.IndexOf('-');

					if (dashIndex != -1 && form != null) // We found '-'. This is the end of a word specification.
					{
						string posCode = sentenceComponent.Substring(dashIndex + 1);

						if (lemma == null)
							lemma = sentenceComponent.Substring(0, dashIndex);
						else
							lemma += sentenceComponent.Substring(0, dashIndex);

						lemma = lemma.Replace("*", String.Empty).NormalizeDashes();

						try
						{

							var tag = morphologyParser.GetFormTag(posCode, lemma);

							// Reject sentences having non-declinable Hebrew forms according to the Greek language.
							if (tag != null && isSentenceAcceptable)
							{
								if (form.StartsWith("["))
								{
									var openParanthesisTag = morphologyParser.GetPunctuationTag('(');

									sentenceWordForms.Add(new TaggedWordForm("(", "(", openParanthesisTag));

									form = form.Substring(1);

									sentenceWordForms.Add(new TaggedWordForm(form, lemma, tag));
								}
								else if (form.EndsWith("]"))
								{
									form = form.Substring(0, form.Length - 1);

									sentenceWordForms.Add(new TaggedWordForm(form, lemma, tag));

									var closingParenthesisTag = morphologyParser.GetPunctuationTag(')');

									sentenceWordForms.Add(new TaggedWordForm(")", ")", closingParenthesisTag));
								}
								else
								{
									sentenceWordForms.Add(new TaggedWordForm(form, lemma, tag));
								}
							}
							else
							{
								isSentenceAcceptable = false;
							}
						}
						catch (ArgumentException)
						{
							// If the posCode is malformed due to data entry error, reject the sentence.
							isSentenceAcceptable = false;
						}

						if (punctuation != '\x0000')
						{
							if (isSentenceAcceptable)
							{
								var punctuationTag = morphologyParser.GetPunctuationTag(punctuation);

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
									if (isSentenceAcceptable)
										yield return new TaggedSentence(sentenceWordForms.ToArray());
									else
										isSentenceAcceptable = true; // Else, a new sentence starts, hoping that it will be acceptable.

									sentenceWordForms.Clear();

									break;
							}
						}

						form = null;
						lemma = null;
						formWithPunctuation = null;
						punctuation = '\x0000';
					}
					else if (formWithPunctuation == null)
					{
						formWithPunctuation = sentenceComponent.NormalizeDashes();

						char lastFormCharacter = formWithPunctuation[formWithPunctuation.Length - 1];

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

							case '\x037E': // This is question mark in the greek unicode range.
								punctuation = ';';
								form = formWithPunctuation.Substring(0, formWithPunctuation.Length - 1);
								break;

							default:
								punctuation = '\x0000';
								form = formWithPunctuation;
								break;
						}

						form = NormalizeSpelling(form);
					}
					else if (lemma == null)
					{
						lemma = sentenceComponent;
					}
					else
					{
						lemma += sentenceComponent;
					}

				}

			}
		}

		#endregion

		#region Private methods

		private static string NormalizeSpelling(string word)
		{
			switch (word)
			{
				case "ἀντ":
					return "ἀντ᾿";

				default:
					return word;
			}
		}

		#endregion
	}
}
