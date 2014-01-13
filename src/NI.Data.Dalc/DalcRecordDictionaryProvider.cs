#region License
/*
 * Open NIC.NET library (http://nicnet.googlecode.com/)
 * Copyright 2004-2008 NewtonIdeas
 * Distributed under the LGPL licence
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion

using System;
using System.Collections;
using System.Collections.Specialized;

using NI.Common;
using NI.Common.Providers;

namespace NI.Data.Dalc
{
	/// <summary>
	/// DALC record dictionary provider
	/// </summary>
	public class DalcRecordDictionaryProvider :
		IDictionaryProvider, IObjectProvider
	{
		IQueryProvider _QueryProvider;
		IDalc _Dalc;
				
		/// <summary>
		/// Get or set relational expressions used to load data
		/// </summary>
		[Dependency]
		public IQueryProvider QueryProvider {
			get { return _QueryProvider; }
			set { _QueryProvider = value; }
		}
		
		/// <summary>
		/// Get or set DALC component to load data from
		/// </summary>
		[Dependency]
		public IDalc Dalc {
			get { return _Dalc; }
			set { _Dalc = value; }
		}		
		
		public DalcRecordDictionaryProvider()
		{
		}
		
		/// <summary>
		/// Get record dictionary using specified context
		/// <see cref="IDictionaryProvider.GetDictionary"/>
		/// </summary>
		public IDictionary GetDictionary(object context) {
			IQuery q = GetQuery(context);
			ListDictionary result = new ListDictionary();
			if (Dalc.LoadRecord(result, q))
				return result;
			return null;
		}
		
		protected virtual IQuery GetQuery(object context) {
			return QueryProvider.GetQuery(context);
		}	
		
		public object GetObject(object context) {
			return GetDictionary(context);
		}			
		
		/// <summary>
		/// <see cref="ITokenProvider.ProvideTokens"/>
		/// </summary>
		public IDictionary ProvideTokens(IDictionary context) {
			return GetDictionary(context);
		}
		
	}
}