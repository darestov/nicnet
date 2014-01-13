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
using System.Reflection;

using NI.Common;

namespace NI.Winter {

	/// <summary>
	/// StaticPropertyInvokingFactory used for defining instance as static property of some class.
	/// </summary>
	/// <example><code>
	/// &lt;component name="datetimenow" type="NI.Winter.StaticPropertyInvokingFactory,NI.Winter" singleton="false" lazy-init="true"&gt;
	///		&lt;property name="TargetType"&gt;&lt;type&gt;System.DateTime,Mscorlib&lt;/type&gt;&lt;/property&gt;
	///		&lt;property name="TargetProperty"&gt;&lt;value&gt;Now&lt;/value&gt;&lt;/property&gt;
	/// &lt;/component&gt;
	/// </code></example>
	public class StaticPropertyInvokingFactory : Component, IFactoryComponent {
		Type _TargetType;
		string _TargetProperty;
	
		/// <summary>
		/// Get or set target type
		/// </summary>
		[Dependency]
		public Type TargetType {
			get { return _TargetType; }
			set { _TargetType = value; }
		}
		
		/// <summary>
		/// Get or set static target property name
		/// </summary>
		[Dependency]
		public string TargetProperty {
			get { return _TargetProperty; }
			set { _TargetProperty = value; }
		}

		
		
		public StaticPropertyInvokingFactory() {
		}
		
		public object GetObject() {
			
			System.Reflection.PropertyInfo pInfo = TargetType.GetProperty( TargetProperty, BindingFlags.Static|BindingFlags.Public);
			if (pInfo==null)
				throw new MissingMemberException( TargetType.ToString(), TargetProperty);
			return pInfo.GetValue( null, null );
		}
		
		public Type GetObjectType() {
			System.Reflection.PropertyInfo pInfo = TargetType.GetProperty( TargetProperty, BindingFlags.Static|BindingFlags.Public);
			if (pInfo==null)
				throw new MissingMemberException( TargetType.ToString(), TargetProperty);
			return pInfo.PropertyType;
		}		
		
		
	}
}