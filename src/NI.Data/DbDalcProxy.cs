#region License
/*
 * Open NIC.NET library (http://nicnet.googlecode.com/)
 * Copyright 2004-2012 NewtonIdeas
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
using NI.Common;

namespace NI.Data
{
    /// <summary>
    /// Pipe Db dalc
    /// </summary>
    /*public class DbDalcProxy : ISqlDalc {

        private ISqlDalc _UnderlyingDalc;

        /// <summary>
        /// Underlying dalc
        /// </summary>
        public ISqlDalc UnderlyingDalc {
            get { return _UnderlyingDalc; }
            set { _UnderlyingDalc = value; }
        }

        #region IDbDalc Members

        public System.Data.IDbConnection Connection {
            get {
                return UnderlyingDalc.Connection;
            }
            set {
                UnderlyingDalc.Connection = value;
            }
        }

        public System.Data.IDbTransaction Transaction {
            get {
                return UnderlyingDalc.Transaction;
            }
            set {
                UnderlyingDalc.Transaction = value;
            }
        }

        public int Execute(string sqlText) {
            return UnderlyingDalc.Execute(sqlText);
        }

        public System.Data.IDataReader ExecuteReader(string sqlText) {
            return UnderlyingDalc.ExecuteReader(sqlText);
        }

        public System.Data.IDataReader LoadReader(Query q) {
            return UnderlyingDalc.LoadReader(q);
        }

        public void Load(System.Data.DataSet ds, string sqlText) {
            UnderlyingDalc.Load(ds, sqlText);
        }

        public bool LoadRecord(System.Collections.IDictionary data, string sqlCommandText) {
            return UnderlyingDalc.LoadRecord(data, sqlCommandText);
        }

        #endregion

        #region IDalc Members

        public void Load(System.Data.DataSet ds, Query query) {
            UnderlyingDalc.Load(ds, query);
        }

        public void Update(System.Data.DataSet ds, string sourceName) {
            UnderlyingDalc.Update(ds, sourceName);
        }

        public int Update(System.Collections.IDictionary data, Query query) {
            return UnderlyingDalc.Update(data, query);
        }

        public void Insert(System.Collections.IDictionary data, string sourceName) {
            UnderlyingDalc.Insert(data, sourceName);
        }

        public int Delete(Query query) {
            return UnderlyingDalc.Delete(query);
        }

        public bool LoadRecord(System.Collections.IDictionary data, Query query) {
            return UnderlyingDalc.LoadRecord(data, query);
        }

        public int RecordsCount(string sourceName, QueryNode conditions) {
            return UnderlyingDalc.RecordsCount(sourceName, conditions);
        }

        #endregion
    }*/
}