using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Grammophone.GenericContentModel;

namespace Grammophone.LanguageModel.Greek.TrainingSources.Perseus
{
	/// <summary>
	/// A specification of a dialect.
	/// </summary>
	public class Dialect : IKeyedElement<string>
	{
		#region Private fields

		private string name;

		#endregion

		#region Construction

		public Dialect()
		{
			this.name = String.Empty;
		}

		public Dialect(string name)
		{
			this.name = name;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// Name of the dialect. Typical values are "doric", "aeolic", "epic", "ionic", "homeric", "attic", "poetic" etc.
		/// </summary>
		public string Name
		{
			get
			{
				return name;
			}
			set
			{
				if (value == null) throw new ArgumentNullException("value");
				name = value;
			}
		}

		#endregion

		#region IKeyedElement<string> Members

		string IKeyedElement<string>.Key
		{
			get { return this.name; }
		}

		#endregion
	}
}
