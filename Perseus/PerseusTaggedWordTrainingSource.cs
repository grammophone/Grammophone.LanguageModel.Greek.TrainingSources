using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Gramma.GenericContentModel;
using Gramma.LanguageModel.Grammar;
using Gramma.LanguageModel.TrainingSources;
using System.Globalization;

namespace Gramma.LanguageModel.Greek.TrainingSources.Perseus
{
	/// <summary>
	/// Suitable for opening the perseus tagged words file 'greek.morph.xml'.
	/// </summary>
	public class PerseusTaggedWordTrainingSource : TaggedWordTrainingSource
	{
		#region Private fields

		private string morphologyFilename;

		private XmlReader reader;
		
		private Stream stream;

		private BetaImport.BetaConverter betaConverter;

		private Map<string, Dialect> allowedDialects;

		private CultureInfo greekCultureInfo;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		public PerseusTaggedWordTrainingSource()
		{
			this.MorphologyFilename = "greek.morph.xml";
			
			this.betaConverter = new BetaImport.PrecombinedDiacriticsBetaConverter();
			this.allowedDialects = new Map<string, Dialect>();
			this.greekCultureInfo = new CultureInfo("el");
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The full filename of the 'greek.morph.xml' file.
		/// </summary>
		public string MorphologyFilename
		{
			get
			{
				return morphologyFilename;
			}
			set
			{
				if (value == null) throw new ArgumentNullException("value");
				morphologyFilename = value;
			}
		}

		/// <summary>
		/// The allowed dialects. If the collection is empty, all dialects are allowed.
		/// </summary>
		/// <remarks>
		/// If a word has no designated dialect in the data source, it always passes.
		/// </remarks>
		public Map<string, Dialect> AllowedDialects
		{
			get
			{
				return allowedDialects;
			}
		}

		#endregion

		#region Protected methods

		protected override void OpenImplementation()
		{
			stream = new FileStream(this.MorphologyFilename, FileMode.Open, FileAccess.Read);

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

		protected override IEnumerable<TaggedWordForm> GetDataImplementation()
		{
			var grammarModel = this.LanguageProvider.GrammarModel;

			while (reader.Read())
			{
				if (!reader.ReadToFollowing("analysis")) yield break;

				if (!reader.ReadToFollowing("form")) yield break;

				string betaForm = reader.ReadString().NormalizeBeta();

				// Words with hyphens are problematic, because they are split to their components losing their junction spelling, 
				// for example κατά-ἕζομαι instead of καθέζομαι.
				// If we included them as they are, wrong command sequences would be learned.
				// Joining them properly would need proper grammar rules, which needs a lot of coding effort. So, skip them for now.
				if (betaForm.Length > 1 && betaForm.Contains('-')) continue;

				string form = betaConverter.Convert(betaForm)
					.StripNumerics()
					.ToLower(greekCultureInfo);

				if (!reader.ReadToFollowing("lemma")) yield break;

				string betaLemma = reader.ReadString().NormalizeBeta();

				// Words with hyphens are problematic, because they are split to their components losing their junction spelling, 
				// for example κατά-ἕζομαι instead of καθέζομαι.
				// If we included them as they are, wrong command sequences would be learned.
				// Joining them properly would need proper grammar rules, which needs a lot of coding effort. So, skip them for now.
				if (betaLemma.Length > 1 && betaLemma.Contains('-')) continue;

				string lemma = betaConverter.Convert(betaLemma)
					.StripNumerics()
					.ToLower(greekCultureInfo);

				if (!reader.ReadToFollowing("pos")) yield break;

				string tagTypeKey = reader.ReadString();

				if (!grammarModel.TagTypes.ContainsKey(tagTypeKey)) continue;

				var tagType = grammarModel.TagTypes[tagTypeKey];

				var inflections = new List<Inflection>();

				string number = String.Empty;
				string person = String.Empty;
				string cas = String.Empty;

				String inflectionTypeKey, inflectionKey;

				InflectionType inflectionType;
				Inflection inflection;

				string[] dialects = null;

				while (reader.Read())
				{
					if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "analysis") break;

					if (reader.NodeType == XmlNodeType.Element)
					{
						inflectionTypeKey = reader.LocalName;

						switch (inflectionTypeKey)
						{
							case "number":
								number = reader.ReadString();
								continue;

							case "case":
								cas = reader.ReadString();
								continue;

							case "person":
								person = reader.ReadString();
								continue;

							case "dialect":
								if (this.AllowedDialects.Count == 0) continue;

								dialects = reader.ReadString().Split();

								continue;

							default:
								inflectionKey = reader.ReadString();
								break;
						}

						if (!grammarModel.InflectionTypes.ContainsKey(inflectionTypeKey)) continue;

						inflectionType = grammarModel.InflectionTypes[inflectionTypeKey];

						if (!inflectionType.Inflections.ContainsKey(inflectionKey)) continue;

						inflection = inflectionType.Inflections[inflectionKey];

						inflections.Add(inflection);
					}
				}

				if (dialects != null && this.AllowedDialects.Count > 0)
				{
					if (!dialects.Any(d => this.AllowedDialects.ContainsKey(d))) continue;
				}

				if (number.Length > 0)
				{
					if (cas.Length > 0)
					{
						if (!grammarModel.InflectionTypes.ContainsKey("case")) continue;

						inflectionType = grammarModel.InflectionTypes["case"];

						inflectionKey = String.Format("{0} {1}", cas, number);
					}
					else if (person.Length > 0)
					{
						if (!grammarModel.InflectionTypes.ContainsKey("person")) continue;

						inflectionType = grammarModel.InflectionTypes["person"];

						inflectionKey = String.Format("{0} {1}", person, number);
					}
					else
					{
						continue;
					}

					if (!inflectionType.Inflections.ContainsKey(inflectionKey)) continue;

					inflection = inflectionType.Inflections[inflectionKey];

					inflections.Add(inflection);
				}

				Tag tag;

				switch (tagTypeKey)
				{
					case "exclam":
					case "conj":
					case "prep":
					case "partic":
					case "[PUNCTUATION]":
						tag = grammarModel.GetTag(tagType, null, lemma);
						break;

					case "adj":
					case "adv":
						// Check if the innflections contain type "degree". If not, add degree "positive".

						var degreeInflectionType = grammarModel.InflectionTypes["degree"];

						bool foundDegreeInflectionType = false;

						foreach (var addedInflection in inflections)
						{
							if (addedInflection.Type == degreeInflectionType)
							{
								foundDegreeInflectionType = true;
								break;
							}
						}

						if (!foundDegreeInflectionType)
						{
							var positiveDegreeInflection = degreeInflectionType.Inflections["pos"];

							inflections.Add(positiveDegreeInflection);
						}

						tag = grammarModel.GetTag(tagType, inflections);
						
						break;

					default:
						tag = grammarModel.GetTag(tagType, inflections);
						break;
				}

				yield return new TaggedWordForm(form, lemma, tag);
			}
		}

		#endregion
	}
}
