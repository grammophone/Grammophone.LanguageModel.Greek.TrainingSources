using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gramma.LanguageModel.TrainingSources;
using System.IO;
using System.Xml;

namespace Gramma.LanguageModel.Greek.TrainingSources.Perseus
{
	/// <summary>
	/// Provides all the word forms inside the Perseus 'greek.morph.xml' file.
	/// </summary>
	public class PerseusUntaggedWordTrainingSource : UntaggedWordTrainingSource
	{
		#region Private fields

		private string morphologyFilename;

		private XmlReader reader;
		
		private Stream stream;

		private BetaImport.BetaConverter betaConverter;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		public PerseusUntaggedWordTrainingSource()
		{
			this.MorphologyFilename = "greek.morph.xml";
			
			this.betaConverter = new BetaImport.PrecombinedDiacriticsBetaConverter();
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

		protected override IEnumerable<string> GetDataImplementation()
		{
			var grammarModel = this.LanguageProvider.GrammarModel;

			while (reader.Read())
			{
				if (!reader.ReadToFollowing("analysis")) yield break;

				if (!reader.ReadToFollowing("form")) yield break;

				string betaForm = reader.ReadString().NormalizeBeta();

				string form = this.LanguageProvider.NormalizeWord(betaConverter.Convert(betaForm)).StripNumerics();

				yield return form;
			}
		}

		#endregion
	}
}
