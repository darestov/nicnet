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

namespace NI.Common
{
	/// <summary>
	/// Defines a mechanism for retrieving a named service object
	/// </summary>
	public interface INamedServiceProvider
	{
		/// <summary>
		/// Get the service object by name
		/// </summary>
		object GetService(string name);
	}
}