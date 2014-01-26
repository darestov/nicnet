#region License
/*
 * Open NIC.NET library (http://nicnet.googlecode.com/)
 * Copyright 2004-2012 NewtonIdeas,  Vitalii Fedorchenko (v.2 changes)
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
using System.ComponentModel;
using System.Reflection;

namespace NI.Ioc
{
	/// <summary>
	/// Factory component which returns a value which is the result of a static or instance method invocation.
	/// </summary>
	/// <example><code>
	/// &lt;component name="testEnabled" type="NI.Ioc.ReplacingFactory,NI.Ioc" singleton="true"&gt;
	///		&lt;property name="TargetObject"&gt;&lt;value&gt;&lt;ref name="testEnabledVariable"/&gt;&lt;/property&gt;	
	///	&lt;/component&gt;
	/// </code></example>
	public class ReplacingFactory : IFactoryComponent
	{
		object _TargetObject;
		
		public object TargetObject {
			get { return _TargetObject; }
			set { _TargetObject = value; }
		}
		
		public ReplacingFactory()
		{
		}
		
		public object GetObject() {
			return TargetObject;
		}
		
		public Type GetObjectType() {
			return TargetObject.GetType();
		}
		
	}
}