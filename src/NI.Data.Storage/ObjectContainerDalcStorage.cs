﻿#region License
/*
 * Open NIC.NET library (http://nicnet.googlecode.com/)
 * Copyright 2013-2014 Vitalii Fedorchenko
 * Copyright 2014 NewtonIdeas
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Threading.Tasks;
using System.Globalization;

using NI.Data;
using NI.Data.Storage.Model;

namespace NI.Data.Storage {
	
	/// <summary>
	/// Generic IDalc-based implementation of <see cref="IObjectContainerStorage"/>
	/// </summary>
	public class ObjectContainerDalcStorage : IObjectContainerStorage {

		static Logger log = new Logger(typeof(ObjectContainerDalcStorage));

		#region Source names

		public string ObjectLogTableName { get; set; }
		public string ObjectTableName { get; set; }
		public string ObjectRelationTableName { get; set; }
		public string ObjectRelationLogTableName { get; set; }
		public IDictionary<string, string> DataTypeTableNames { get; set; }
		
		/// <summary>
		/// Value source name to log source name mapping
		/// </summary>
		public IDictionary<string, string> ValueTableNameForLogs { get; set; }

		#endregion

		protected DataRowDalcMapper DbMgr;

		protected IDalc LogDalc;

		protected Func<DataSchema> GetSchema;

		public Func<object,object> GetContextAccountId { get; set; }

		/// <summary>
		/// Enables data logging
		/// </summary>
		/// <remarks>If data logging is enabled all data changes are logged into special log tables</remarks>
		public bool LoggingEnabled { get; set; }

		/// <summary>
		/// Get or set custom implementation for comparing property values.
		/// </summary>
		/// <remarks>If not set default <see cref="DbValueComparer"/> implementation is used</remarks>
		public IComparer ValueComparer { get; set; }

		/// <summary>
		/// Get or set query batch size (number of objects explicitly specified in one query). Default value is 1000.
		/// </summary>
		public int QueryBatchSize { get; set; }

		/// <summary>
		/// Get or set mapping for resolving derive type expression 
		/// </summary>
		public IDictionary<string, string> DeriveTypeMapping { get; set; }

		/// <summary>
		/// Initializes a new instance of the ObjectContainerDalcStorage.
		/// </summary>
		/// <param name="objectDbMgr">instance of <see cref="NI.Data.DataRowDalcMapper"/> for accessing EAV data tables</param>
		/// <param name="logDalc">instance of <see cref="NI.Data.IDalc"/> for writing data changes log</param>
		/// <param name="getSchema">data schema provider delegate</param>
		public ObjectContainerDalcStorage(DataRowDalcMapper objectDbMgr, IDalc logDalc, Func<DataSchema> getSchema) {
			DbMgr = objectDbMgr;
			LogDalc = logDalc;
			GetSchema = getSchema;
			ObjectLogTableName = "objects_log";
			ObjectTableName = "objects";
			ObjectRelationTableName = "object_relations";
			ObjectRelationLogTableName = "object_relations_log";
			DataTypeTableNames = new Dictionary<string, string>() {
				{PropertyDataType.Boolean.ID, "object_integer_values"},
				{PropertyDataType.Integer.ID, "object_integer_values"},
				{PropertyDataType.Decimal.ID, "object_decimal_values"},
				{PropertyDataType.String.ID, "object_string_values"},
				{PropertyDataType.Date.ID, "object_datetime_values"},
				{PropertyDataType.DateTime.ID, "object_datetime_values"}
			};
			ValueTableNameForLogs = new Dictionary<string, string>() {
				{"object_integer_values", "object_integer_values_log"},
				{"object_decimal_values", "object_decimal_values_log"},
				{"object_string_values", "object_string_values_log"},
				{"object_datetime_values", "object_datetime_values_log"}
			};

			LoggingEnabled = logDalc!=null;
			QueryBatchSize = 1000;
		}

		protected void WriteObjectLog(DataRow objRow, string action) {
			if (!LoggingEnabled)
				return;

			var logData = new Hashtable();
			logData["timestamp"] = DateTime.Now;
			if (GetContextAccountId!=null)
				logData["account_id"] = GetContextAccountId(null);
			logData["compact_class_id"] = objRow["compact_class_id"];
			logData["object_id"] = objRow["id"];
			logData["action"] = action;
			LogDalc.Insert(ObjectLogTableName, logData);
		}

		protected void WriteValueLog(DataRow valRow, bool deleted = false) {
			if (!LoggingEnabled)
				return;

			var logSrcName = ValueTableNameForLogs[valRow.Table.TableName];

			var logData = new Hashtable();
			logData["timestamp"] = DateTime.Now;
			if (GetContextAccountId!=null)
				logData["account_id"] = GetContextAccountId(null);
			logData["object_id"] = valRow["object_id"];
			logData["property_compact_id"] = valRow["property_compact_id"];
			logData["value"] = valRow["value"];
			logData["deleted"] = deleted;
			LogDalc.Insert(logSrcName, logData);
		}

		protected void WriteRelationLog(DataRow refRow, bool deleted = false) {
			if (!LoggingEnabled)
				return;

			var logData = new Hashtable();
			logData["timestamp"] = DateTime.Now;
			if (GetContextAccountId!=null)
				logData["account_id"] = GetContextAccountId(null);
			logData["deleted"] = deleted;

			logData["subject_id"] = refRow["subject_id"];
			logData["predicate_class_compact_id"] = refRow["predicate_class_compact_id"];
			logData["object_id"] = refRow["object_id"];

			LogDalc.Insert(ObjectRelationLogTableName, logData);		
		}

		protected void EnsureKnownDataType(string dataType) {
			if (!DataTypeTableNames.ContainsKey(dataType))
				throw new Exception("Unknown data type: "+dataType);
		}

		protected object SerializeValueData(Property p, object val) {
			if (val == null)
				return DBNull.Value;
			
			var convertedVal = p.DataType.ConvertToValueType(val);
			if (convertedVal is bool)
				return ((bool)convertedVal) ? 1d : 0d;

			return convertedVal;
		}

		protected object DeserializeValueData(Property prop, object val) {
			if (DBNull.Value.Equals(val))
				return null;
			if (prop.DataType.ValueType == typeof(bool)) {
				return Convert.ToDecimal(val) != 0;
			}
			return val;
		}

		IEnumerable<DataRow> findPropertyRows(Property p, DataTable tbl) {
			for (int i = 0; i < tbl.Rows.Count; i++) {
				var r = tbl.Rows[i];
				if (r.RowState != DataRowState.Added
					&& r.RowState != DataRowState.Deleted
					&& Convert.ToInt32(r["property_compact_id"]) == p.CompactID)
					yield return tbl.Rows[i];
			}
		}

		protected void SaveValues(ObjectContainer obj, DataRow objRow, bool newValues = false) {
			// determine values to load
			var propSrcNameProps = new Dictionary<string, IList<long>>();
			foreach (var v in obj) {
				EnsureKnownDataType(v.Key.DataType.ID);
				var propLocation = v.Key.GetLocation(obj.GetClass());

				if (propLocation.Location == PropertyValueLocationType.ValueTable) {
					var valueSrcName = DataTypeTableNames[v.Key.DataType.ID];
					if (!propSrcNameProps.ContainsKey(valueSrcName))
						propSrcNameProps[valueSrcName] = new List<long>();
					if (!propSrcNameProps[valueSrcName].Contains(v.Key.CompactID))
						propSrcNameProps[valueSrcName].Add(v.Key.CompactID);
				}
			}
			// load value tables
			var propSrcNameToTbl = new Dictionary<string, DataTable>();
			foreach (var srcNameEntry in propSrcNameProps) {
				if (newValues) {
					var ds = DbMgr.CreateDataSet(srcNameEntry.Key);
					propSrcNameToTbl[srcNameEntry.Key] = ds.Tables[srcNameEntry.Key];
				} else { 
					propSrcNameToTbl[srcNameEntry.Key] = DbMgr.LoadAll(
						new Query(srcNameEntry.Key, 
							(QField)"object_id" == new QConst(obj.ID.Value)
							&
							new QueryConditionNode((QField)"property_compact_id", Conditions.In, new QConst(srcNameEntry.Value))));
				}
			}
			Action<DataRow> deleteValueRow = r => {
				r["value"] = DBNull.Value;
				WriteValueLog(r, true);
				r.Delete();
			};
			Action<DataTable,Property, object> addValueRow = (tbl, p, v) => {
				var valRow = tbl.NewRow();
				valRow["object_id"] = obj.ID.Value;
				valRow["property_compact_id"] = p.CompactID;
				valRow["value"] = SerializeValueData(p,v);
				WriteValueLog(valRow);
				tbl.Rows.Add(valRow);
			};
			int objTablePropsCount = 0;
			// process values
			foreach (var v in obj) {
				var propLocation = v.Key.GetLocation(obj.GetClass());

				if (propLocation.Location == PropertyValueLocationType.TableColumn && !v.Key.PrimaryKey) {
					objRow[propLocation.TableColumnName] = SerializeValueData(v.Key, v.Value);
					objTablePropsCount++;
				} else if (propLocation.Location == PropertyValueLocationType.ValueTable) {

					var valueSrcName = DataTypeTableNames[v.Key.DataType.ID];
					var tbl = propSrcNameToTbl[valueSrcName];

					var isEmpty = IsEmpty(v.Key, v.Value);
					if (isEmpty) {
						// just remove all rows of the property
						foreach (var r in findPropertyRows(v.Key, tbl)) {
							deleteValueRow(r);
						}
					} else {
						var propRows = newValues ? new DataRow[0] : findPropertyRows(v.Key, tbl).ToArray();

						if (v.Key.Multivalue) {
							var vs = v.Value is IList ? (IList)v.Value : new[] { v.Value };
							var unchangedRows = new List<DataRow>();
							foreach (var vEntry in vs) {
								var isNewValue = true;
								var dbValue = SerializeValueData(v.Key, vEntry);
								for (int i = 0; i < propRows.Length; i++) {
									var pRow = propRows[i];
									if (unchangedRows.Contains(pRow))
										continue;
									if (DbValueEquals(pRow["value"], dbValue)) {
										isNewValue = false;
										unchangedRows.Add(pRow);
									}
								}
								if (isNewValue) {
									addValueRow(tbl, v.Key, vEntry);
								}
							}
							// remove not matched rows
							foreach (var r in propRows)
								if (!unchangedRows.Contains(r))
									deleteValueRow(r);

						} else {
							// hm... cleanup
							if (propRows.Length > 1)
								for (int i = 1; i < propRows.Length; i++)
									deleteValueRow(propRows[i]);
							if (propRows.Length == 0) {
								addValueRow(tbl, v.Key, v.Value);
							} else {
								// just update
								var newValue = SerializeValueData(v.Key, v.Value);
								if (!DbValueEquals(propRows[0]["value"], newValue)) {
									propRows[0]["value"] = newValue;
									WriteValueLog(propRows[0]);
								}
							}
						}

					}

				}

			}

			if (objTablePropsCount>0) {
				DbMgr.Update(objRow);
			}

			// push changes to DB
			foreach (var entry in propSrcNameToTbl)
				DbMgr.Update(entry.Value);
				/*foreach (DataRow r in entry.Value.Rows)
					if (r.RowState == DataRowState.Modified) {
						DbMgr.Dalc.Update(
							new Query(r.Table.TableName, (QField)"id"==new QConst(r["id"]) ),
							new Dictionary<string, IQueryValue>() {
								{"value", new QConst(r["value"])}
							}
						);
					} else if (r.RowState==DataRowState.Added) {
						DbMgr.Dalc.Insert(
							r.Table.TableName,
							new Dictionary<string, IQueryValue>() {
								{"object_id", new QConst(r["object_id"])},
								{"property_compact_id", new QConst(r["property_compact_id"])},
								{"value", new QConst(r["value"])}
							}
						);
					} else if (r.RowState == DataRowState.Deleted) {
						DbMgr.Dalc.Delete(new Query(r.Table.TableName, (QField)"id"==new QConst(r["id",DataRowVersion.Original]) ));
					}*/
		}

		protected bool DbValueEquals(object oldValue, object newValue) {
			if (oldValue == null)
				oldValue = DBNull.Value;
			if (newValue == null)
				newValue = DBNull.Value;
			return newValue.Equals(oldValue);
		}

		public IDictionary<long, ObjectContainer> Load(long[] ids) {
			return Load(ids, null);
		}

		protected IEnumerable<long[]> SliceBatchIds(long[] ids, int batchSize) {
			var batchesCount = Math.Ceiling(((double)ids.Length) / batchSize);
			for (int i=0; i<batchesCount; i++) {
				var start = batchSize * i;
				var length = Math.Min(ids.Length - start, batchSize);
				var batchArr = new long[length];
				Array.Copy(ids, start, batchArr, 0, length);  
				yield return batchArr;
			}
		}

		/// <summary>
		/// Load objects by explicit list of ID.
		/// </summary>
		/// <param name="props">Properties to load. If null all properties are loaded</param>
		/// <param name="ids">object IDs</param>
		/// <returns>All matched by ID objects</returns>
		public IDictionary<long,ObjectContainer> Load(long[] ids, Property[] props) {
			var objById = new Dictionary<long,ObjectContainer>();
			if (ids.Length==0)
				return objById;

			var loadWithoutProps = props!=null && props.Length==0;

			var dataSchema = GetSchema();
			var valueSourceNames = new Dictionary<string,List<long>>();
			var propertyIdDerivedLocations = new Dictionary<long,List<ClassPropertyLocation>>();

			var derivedFromValueTable = new List<ClassPropertyLocation>();

			// construct object containers + populate source names for values to load
			foreach (var batchIds in SliceBatchIds(ids, QueryBatchSize)) {
				var objectQuery = new Query(ObjectTableName, new QueryConditionNode((QField)"id", Conditions.In, new QConst(batchIds)));

				DbMgr.Dalc.ExecuteReader(objectQuery, (rdr) => {
					while (rdr.Read()) {
						var compactClassId = Convert.ToInt32(rdr["compact_class_id"]);
						var objId = Convert.ToInt64(rdr["id"]);
						var objClass = dataSchema.FindClassByCompactID(compactClassId);
						if (objClass == null) {
							log.Info("Class compact_id={0} of object id={1} not found; load object skipped", compactClassId, objId);
							continue;
						}
						var obj = new ObjectContainer(objClass, objId);

						// populate value source names by class properties
						if (!loadWithoutProps) {
							foreach (var p in objClass.Properties) {
								if (p.PrimaryKey || (props != null && !props.Contains(p)))
									continue;

								var pLoc = p.GetLocation(objClass);
								switch (pLoc.Location) {
									case PropertyValueLocationType.TableColumn:
										obj[p] = DeserializeValueData(p, rdr[pLoc.TableColumnName]);
										break;
									case PropertyValueLocationType.Derived:
										if (pLoc.DerivedFrom.Location == PropertyValueLocationType.ValueTable) { 
											var derivedFromCompactID = pLoc.DerivedFrom.Property.CompactID;
											if (!propertyIdDerivedLocations.ContainsKey(derivedFromCompactID))
												propertyIdDerivedLocations[derivedFromCompactID] = new List<ClassPropertyLocation>();
											if (!propertyIdDerivedLocations[derivedFromCompactID].Contains(pLoc))
												propertyIdDerivedLocations[derivedFromCompactID].Add(pLoc);

											// mark derived prop to load
											pLoc = pLoc.DerivedFrom;
										} else if (pLoc.DerivedFrom.Location==PropertyValueLocationType.TableColumn) {
											derivedFromValueTable.Add(pLoc);
										}
										break;
								}
 		
								if (pLoc.Location == PropertyValueLocationType.ValueTable) { 
									EnsureKnownDataType(pLoc.Property.DataType.ID);
									var pSrcName = DataTypeTableNames[pLoc.Property.DataType.ID];
									if (!valueSourceNames.ContainsKey(pSrcName))
										valueSourceNames[pSrcName] = new List<long>();

									if (!valueSourceNames[pSrcName].Contains(pLoc.Property.CompactID))
										valueSourceNames[pSrcName].Add(pLoc.Property.CompactID);
								}
							}
						}
						objById[obj.ID.Value] = obj;					
					}
				});
			}

			// special cases: no objects at all or no properties to load
			if (objById.Count==0 || loadWithoutProps)
				return objById;

			// load values by sourcenames
			var objIds = objById.Keys.ToArray();
			foreach (var valSrcName in valueSourceNames) {
				var objIdsBatchSize = Math.Max( QueryBatchSize-valSrcName.Value.Count, QueryBatchSize/2);
				foreach (var batchIds in SliceBatchIds(objIds, objIdsBatchSize)) {
					var valQuery = new Query(valSrcName.Key,
							new QueryConditionNode((QField)"object_id", Conditions.In, new QConst(batchIds))
							&
							new QueryConditionNode((QField)"property_compact_id",
								Conditions.In, new QConst(valSrcName.Value))
						);
					var fldList = new List<QField>();
					fldList.Add( (QField)"object_id" );
					fldList.Add( (QField)"property_compact_id" );
					fldList.Add( (QField)"value" );

					// derived props handling
					Func<ClassPropertyLocation,string> getDerivedFldName = (d) => {
						return String.Format("derived_{0}_{1}", d.Class.CompactID, d.Property.CompactID);
					};
					foreach (var propCompactId in valSrcName.Value) {
						if (propertyIdDerivedLocations.ContainsKey(propCompactId)) {
							foreach (var derivedPropLoc in propertyIdDerivedLocations[propCompactId]) {
								var derivedFld = ResolveDerivedProperty(derivedPropLoc, String.Format("{0}.value", valSrcName.Key) );
								fldList.Add( new QField( getDerivedFldName(derivedPropLoc), derivedFld.Expression) );
							}
						}
					}
					valQuery.Fields = fldList.ToArray();

					DbMgr.Dalc.ExecuteReader( valQuery, (rdr) => {
						while (rdr.Read()) {
							var propertyCompactId = Convert.ToInt32(rdr["property_compact_id"]);
							var objId = Convert.ToInt64(rdr["object_id"]);
							var prop = dataSchema.FindPropertyByCompactID(propertyCompactId);
							if (prop != null) {
								if (objById.ContainsKey(objId)) { 
									var obj = objById[objId];
									var objClass = obj.GetClass();
									var propLoc = prop.GetLocation(objClass);
									if (propLoc!=null) {
										if (propLoc.Location == PropertyValueLocationType.ValueTable) { 
											// TBD: handle multi-values props
											obj[prop] = DeserializeValueData(prop, rdr["value"]);
										}
										// check derived
										if (propertyIdDerivedLocations.ContainsKey(propertyCompactId)) {
											foreach (var derivedPropLoc in propertyIdDerivedLocations[propertyCompactId])
												if (objClass == derivedPropLoc.Class) {
													obj[derivedPropLoc.Property] = DeserializeValueData(prop, rdr[getDerivedFldName(derivedPropLoc)]);
												}
										}
									} else {
										// property for this class no longer exist... TBD: handle that somehow
									}
								}
							}							
						}
					});
				}
			}

			// load derived from objects table properties
			if (derivedFromValueTable.Count > 0) { 
				foreach (var batchIds in SliceBatchIds(ids, QueryBatchSize)) {
					var objectQuery = new Query(ObjectTableName, new QueryConditionNode((QField)"id", Conditions.In, new QConst(batchIds)));
					var flds = new List<QField>();
					flds.Add(new QField("id"));
					foreach (var derived in derivedFromValueTable) {
						var derivedFld = ResolveDerivedProperty(derived, String.Format("{0}.{1}", ObjectTableName, derived.DerivedFrom.Property.ID ) );
						flds.Add(derivedFld);
					}
					objectQuery.Fields = flds.ToArray();
					DbMgr.Dalc.ExecuteReader(objectQuery, (rdr) => {
						while (rdr.Read()) {
							var objId = Convert.ToInt64(rdr["id"]);
							ObjectContainer obj;
							if (objById.TryGetValue(objId, out obj)) {
								var objClass = obj.GetClass();
								foreach (var derived in derivedFromValueTable) {
									if (derived.Class == objClass) {
										obj[derived.Property] = rdr[derived.Property.ID];
									}
								}
							}
						}
					});
				}
			}

			return objById;
		}

		public void Insert(ObjectContainer obj) {
			var c = obj.GetClass();
			if (c.ObjectLocation!=ObjectLocationType.ObjectTable)
				throw new NotSupportedException();

			var objRow = DbMgr.Insert(ObjectTableName, new Dictionary<string, object>() {
				{"compact_class_id", c.CompactID}
			});
			obj.ID = Convert.ToInt64( objRow["id"] );

			SaveValues(obj, objRow, true);

			if (LoggingEnabled)
				WriteObjectLog(objRow, "insert");
		}

		protected Query ComposeLoadRelationsQuery(ObjectRelation[] relations) {
			var loadRelQ = new Query(ObjectRelationTableName);
			var orCondition = new QueryGroupNode(QueryGroupNodeType.Or);
			loadRelQ.Condition = orCondition;
			foreach (var r in relations) {
				if (r.Relation.Inferred) {
					throw new ArgumentException("Add/Remove operations are not supported for inferred relationship");
				}

				var subjIdFld = r.Relation.Reversed ? "object_id" : "subject_id";
				var objIdFld = r.Relation.Reversed ? "subject_id" : "object_id";
				var relCondition = (QField)subjIdFld == new QConst(r.SubjectID)
					& (QField)objIdFld == new QConst(r.ObjectID)
					& (QField)"predicate_class_compact_id" == new QConst(r.Relation.Predicate.CompactID);
				orCondition.Nodes.Add(relCondition);
			}
			if (orCondition.Nodes.Count == 0) {
				orCondition.Nodes.Add( new QueryConditionNode( new QConst(1), Conditions.Equal, new QConst(2) ) );
			}
			return loadRelQ;
		}

		public void AddRelation(params ObjectRelation[] relations) {
			var loadRelQ = ComposeLoadRelationsQuery(relations);
			var relTbl = DbMgr.LoadAll(loadRelQ);
			foreach (var r in relations) {
				
				var subjIdFld = r.Relation.Reversed ? "object_id" : "subject_id";
				var objIdFld = r.Relation.Reversed ? "subject_id" : "object_id";
				
				DataRow relRow = null;
				foreach (DataRow row in relTbl.Rows) {
					if (Convert.ToInt64(row[subjIdFld])==r.SubjectID &&
						Convert.ToInt64(row[objIdFld])==r.ObjectID &&
						Convert.ToInt32(row["predicate_class_compact_id"])==r.Relation.Predicate.CompactID) {
						relRow = row;
						break;
					}
				}
				if (relRow == null) {
					// check multiplicity constraint
					if (!r.Relation.Multiplicity) {
						if (DbMgr.Dalc.RecordsCount( ComposeSubjectRelationQuery(r.Relation, r.SubjectID) )>0)
							throw new ConstraintException(String.Format("{0} doesn't allow multiplicity", r.Relation ) );
					}

					// create new relation entry
					relRow = relTbl.NewRow();
					relRow[subjIdFld] = r.SubjectID;
					relRow[objIdFld] = r.ObjectID;
					relRow["predicate_class_compact_id"] = r.Relation.Predicate.CompactID;
					relTbl.Rows.Add(relRow);
					WriteRelationLog(relRow);
				}
			}
			DbMgr.Update(relTbl);
		}

		protected Query ComposeSubjectRelationQuery(Relationship relationship, long subjectId) {
			var q = new Query(ObjectRelationTableName);
			var cond = QueryGroupNode.And((QField)"predicate_class_compact_id" == new QConst(relationship.Predicate.CompactID));
			if (relationship.Reversed) {
				cond.Nodes.Add( (QField)"object_id"==new QConst(subjectId) );
			} else {
				cond.Nodes.Add((QField)"subject_id" == new QConst(subjectId));
			}
			q.Condition = cond;
			return q;
		}

		public void RemoveRelation(params ObjectRelation[] relations) {
			if (relations.Length==0)
				return; // nothing to do
			var loadRelQ = ComposeLoadRelationsQuery(relations);
			var relTbl = DbMgr.LoadAll(loadRelQ);

			foreach (DataRow relRow in relTbl.Rows) {
				if (relRow!=null) {
					WriteRelationLog(relRow,true);
					relRow.Delete();
				}
			}
			DbMgr.Update(relTbl);
		}

		public void Delete(ObjectContainer obj) {
			if (!obj.ID.HasValue)
				throw new ArgumentException("Object ID is required for delete");
			var delCount = Delete(obj.ID.Value);
			if (delCount == 0)
				throw new DBConcurrencyException(String.Format("Object id={0} doesn't exist", obj.ID.Value));
		}

		public int Delete(params long[] objIds) {
			if (objIds.Length==0)
				return 0;

			var objTbl = DbMgr.LoadAll( new Query(ObjectTableName,
				new QueryConditionNode( (QField)"id", Conditions.In, new QConst(objIds) ) ) );
			var loadedObjIds = objTbl.Rows.Cast<DataRow>().Select( r => Convert.ToInt64(r["id"]) ).ToArray();
			if (loadedObjIds.Length==0)
				return 0;

			// load all values & remove'em
			foreach (var valSrcName in DataTypeTableNames.Values.Distinct()) {
				var valTbl = DbMgr.LoadAll(new Query(valSrcName, 
						new QueryConditionNode( (QField)"object_id", Conditions.In, new QConst(loadedObjIds) )
					) );
				foreach (DataRow valRow in valTbl.Rows) {
					valRow["value"] = DBNull.Value;
					WriteValueLog(valRow);
					valRow.Delete();
				}
				DbMgr.Update(valTbl);
			}
			// load all relations & remove'em
			var refTbl = DbMgr.LoadAll(new Query(ObjectRelationTableName,
					new QueryConditionNode( (QField)"subject_id", Conditions.In, new QConst(loadedObjIds) )
					|
					new QueryConditionNode( (QField)"object_id", Conditions.In, new QConst(loadedObjIds) )
				) );
			foreach (DataRow r in refTbl.Rows) {
				WriteRelationLog(r,true);
				r.Delete();
			}
			DbMgr.Update(refTbl);

			var delCount = objTbl.Rows.Count;
			foreach (DataRow objRow in objTbl.Rows) {
				if (LoggingEnabled)
					WriteObjectLog(objRow, "delete");
				objRow.Delete();
			}
			DbMgr.Update(objTbl);
			return delCount;
		}

		public void Update(ObjectContainer obj) {
			if (!obj.ID.HasValue)
				throw new ArgumentException("Object ID is required for update");
			
			var c = obj.GetClass();
			if (c.ObjectLocation!=ObjectLocationType.ObjectTable)
				throw new NotSupportedException();

			var objRow = DbMgr.Load(ObjectTableName, obj.ID);
			if (objRow == null)
				throw new DBConcurrencyException(String.Format("Object with ID={0} doesn't exist", obj.ID));

			SaveValues(obj, objRow);

			if (LoggingEnabled)
				WriteObjectLog(objRow, "update");
		}

		bool IsEmpty(Property p, object v) {
			if (p.DataType.IsEmpty(v))
				return true;
			return DBNull.Value.Equals(v);
		}

		bool AreValuesEqual(object o1, object o2) {
			// normalize null
			var o1norm = o1==DBNull.Value ? null : o1;
			var o2norm = o2==DBNull.Value ? null : o2;

			if (ValueComparer!=null)
				return ValueComparer.Compare(o1norm, o2norm)==0;

			return DbValueComparer.Instance.Compare( o1norm, o2norm )==0;
		}

		public IEnumerable<ObjectRelation> LoadRelations(ObjectContainer obj, IEnumerable<Relationship> rels) {
			return LoadRelations(new[] { obj }, rels);
		}
		public IEnumerable<ObjectRelation> LoadRelations(ObjectContainer[] objs, IEnumerable<Relationship> rels) {
			if (objs.Length == 0)
				return new ObjectRelation[0];

			var objIdsList = new List<long>();
			for (int i=0; i<objs.Length;i++)
				if (objs[i].ID.HasValue)
					objIdsList.Add( objs[i].ID.Value );
			var objIds = objIdsList.ToArray();
			var rs = new List<ObjectRelation>();

			if (objIds.Length==0)
				return new ObjectRelation[0];

			var dataSchema = GetSchema();

			foreach (var objBatchIds in SliceBatchIds( objIds, QueryBatchSize / 2 )) {
				var loadQ = new Query(ObjectRelationTableName);
				var orCond = QueryGroupNode.Or();
				loadQ.Condition = orCond;

				var subjCond = new QueryConditionNode((QField)"subject_id", Conditions.In, new QConst(objBatchIds));
				var objCond = new QueryConditionNode((QField)"object_id", Conditions.In, new QConst(objBatchIds));

				if (rels!=null && rels.Count() > 0) {
					var relCompactIds = new List<long>();
					var revRelCompactIds = new List<long>();
					foreach (var r in rels) {
						// lets include first relation of inferred relationships into first query
						var rr = r.Inferred ? r.InferredByRelationships.First() : r;
						if (rr.Reversed) {
							if (!revRelCompactIds.Contains(rr.Predicate.CompactID))
								revRelCompactIds.Add(rr.Predicate.CompactID);
						} else {
							if (!relCompactIds.Contains(rr.Predicate.CompactID))
								relCompactIds.Add(rr.Predicate.CompactID);
						}
					}

					if (relCompactIds.Count>0) {
						orCond.Nodes.Add(
							subjCond
							&
							new QueryConditionNode( (QField)"predicate_class_compact_id", Conditions.In, new QConst(relCompactIds) )
						);
					}
					if (revRelCompactIds.Count>0) {
						orCond.Nodes.Add(
							objCond
							&
							new QueryConditionNode( (QField)"predicate_class_compact_id", Conditions.In, new QConst(revRelCompactIds) )
						);
					}
				} else {
					orCond.Nodes.Add( subjCond );
					orCond.Nodes.Add( objCond );
				}
				var relData = LoadRelationData(loadQ);

				var objIdToClass = new Dictionary<long, Class>();
				foreach (var rel in relData) {
					if (rel.SubjectClassCompactId.HasValue) {
						var subjClass = dataSchema.FindClassByCompactID(rel.SubjectClassCompactId.Value);
						if (subjClass!=null)
							objIdToClass[ rel.SubjectId ] = subjClass;
					}
					if (rel.ObjectClassCompactId.HasValue) {
						var objClass = dataSchema.FindClassByCompactID(rel.ObjectClassCompactId.Value);
						if (objClass != null)
							objIdToClass[rel.ObjectId] = objClass;
					}
				}

				foreach (var rel in relData) {
					long relSubjId, relObjId;
					var isReversed = !objBatchIds.Contains(rel.SubjectId);
					if (isReversed) {
						relSubjId = rel.ObjectId;
						relObjId = rel.SubjectId;
					} else {
						relSubjId = rel.SubjectId;
						relObjId = rel.ObjectId;
					}

					var subjClass = objIdToClass[relSubjId];
					var predClass = subjClass.Schema.FindClassByCompactID(rel.PredicateClassCompactId);
					if (predClass==null) {
						log.Info("Predicate with compact ID={0} doesn't exist: relation skipped", rel.PredicateClassCompactId);
						continue;
					}

					var relationship = subjClass.FindRelationship(predClass, objIdToClass[relObjId], isReversed);
					if (relationship != null) {
						if (rels==null || rels.Contains(relationship) )
							rs.Add(new ObjectRelation(relSubjId, relationship, relObjId));
					} else {
						log.Info( "Relation between ObjectID={0} and ObjectID={1} with predicate ClassID={2} doesn't exist: relation skipped",
							relSubjId, relObjId, predClass.ID);
					}
				}

			
			}


			if (rels != null) {
				// process inferred relations, if specified
				var inferredRels = rels.Where(r => r.Inferred).ToArray();
				if (inferredRels.Length > 0) {
					var maxLevel = inferredRels.Select(r => r.InferredByRelationships.Count()).Max();

					var loadedRels = new Dictionary<Relationship, RelationMappingInfo>();
					foreach (var r in rs) {
						if (!loadedRels.ContainsKey(r.Relation))
							loadedRels[r.Relation] = new RelationMappingInfo();
						loadedRels[r.Relation].Data.Add(r);
						loadedRels[r.Relation].ObjectIdToSubjectId[r.ObjectID] = r.SubjectID;
					}

					// load relation data
					foreach (var infRel in inferredRels) {
						var relSeqList = new List<Relationship>();
						IList<long> relSeqSubjIds = objIds;
						Relationship prevSeqRel = null;
						foreach (var rship in infRel.InferredByRelationships) {
							relSeqList.Add(rship);
							var seqInfRel = relSeqList.Count == 1 ?
									relSeqList[0] :
									new Relationship(infRel.Subject, relSeqList.ToArray(), rship.Object);

							if (loadedRels.ContainsKey(seqInfRel)) {
								relSeqSubjIds = loadedRels[seqInfRel].ObjectIdToSubjectId.Keys.ToArray();
							} else {
								var q = new Query(ObjectRelationTableName,
										(QField)"predicate_class_compact_id" == new QConst(rship.Predicate.CompactID)
										&
										new QueryConditionNode(
											(QField)(rship.Reversed ? "object_id" : "subject_id"),
											Conditions.In, new QConst(relSeqSubjIds))
									) {
										Fields = new[] { (QField)"subject_id", (QField)"object_id" }
									};
								var seqInfRelationInfo = new RelationMappingInfo();
								loadedRels[seqInfRel] = seqInfRelationInfo;
								var seqObjIds = new List<long>();
								DbMgr.Dalc.ExecuteReader(q, (rdr) => {
									while (rdr.Read()) {
										var loadedSubjId = Convert.ToInt64(rdr[rship.Reversed ? "object_id" : "subject_id"]);
										var loadedObjId = Convert.ToInt64(rdr[rship.Reversed ? "subject_id" : "object_id"]);
										seqObjIds.Add(loadedObjId);

										var mappedSubjId = prevSeqRel != null ?
												loadedRels[prevSeqRel].ObjectIdToSubjectId[loadedSubjId] : loadedSubjId;

										seqInfRelationInfo.Data.Add(new ObjectRelation(
											mappedSubjId, seqInfRel, loadedObjId
										));
										seqInfRelationInfo.ObjectIdToSubjectId[loadedObjId] = mappedSubjId;
									}
								});

								relSeqSubjIds = seqObjIds;
							}

							prevSeqRel = seqInfRel;
						}

						// lets copy resolved inferred relations to resultset
						foreach (var r in loadedRels[infRel].Data) {
							rs.Add(r);
						}
					}
				}
			}


			return rs;
		}

		public sealed class RelationData {
			public long SubjectId;
			public long ObjectId;
			public int PredicateClassCompactId;
			public int? SubjectClassCompactId;
			public int? ObjectClassCompactId;
		}

		protected virtual IList<RelationData> LoadRelationData(Query q) {
			var relData = new List<RelationData>();
			DbMgr.Dalc.ExecuteReader( q, (rdr) => {
				while (rdr.Read()) {
					var rd = new RelationData();
					rd.SubjectId = Convert.ToInt64(rdr["subject_id"]);
					rd.ObjectId = Convert.ToInt64(rdr["object_id"]);
					rd.PredicateClassCompactId = Convert.ToInt32(rdr["predicate_class_compact_id"]);
					relData.Add(rd);
				}
			});

			var relObjToLoad = new List<long>();
			foreach (var rel in relData) {
				if (!relObjToLoad.Contains(rel.SubjectId))
					relObjToLoad.Add(rel.SubjectId);
				if (!relObjToLoad.Contains(rel.ObjectId))
					relObjToLoad.Add(rel.ObjectId);
			}
			if (relObjToLoad.Count>0) {
				var objIdToClassCompactId = new Dictionary<long, int>();
				foreach (var relObjToLoadBatch in SliceBatchIds(relObjToLoad.ToArray(), QueryBatchSize)) {
					var objQuery = new Query(ObjectTableName,
							new QueryConditionNode((QField)"id", Conditions.In, new QConst(relObjToLoadBatch)));
					objQuery.Fields = new[] { (QField)"id", (QField)"compact_class_id" };
					DbMgr.Dalc.ExecuteReader( objQuery, (rdr) => {
						while (rdr.Read()) {
							objIdToClassCompactId[ Convert.ToInt64(rdr["id"]) ] = Convert.ToInt32( rdr["compact_class_id"] );
						}
					});
				}
				for (int i=0; i<relData.Count; i++) {
					var rel = relData[i];
					if (objIdToClassCompactId.ContainsKey(rel.SubjectId))
						rel.SubjectClassCompactId = objIdToClassCompactId[rel.SubjectId];
					if (objIdToClassCompactId.ContainsKey(rel.ObjectId))
						rel.ObjectClassCompactId = objIdToClassCompactId[rel.ObjectId];
				}
			}

			return relData;			
		}

		protected class RelationMappingInfo {
			internal IList<ObjectRelation> Data;
			internal IDictionary<long,long> ObjectIdToSubjectId;

			internal RelationMappingInfo() {
				Data = new List<ObjectRelation>();
				ObjectIdToSubjectId = new Dictionary<long,long>();
			}
		}

		protected virtual QField ResolveDerivedProperty(ClassPropertyLocation classProp, string derivedFromFldName) {
			var deriveType = classProp.DeriveType;
			if (DeriveTypeMapping != null) {
				if (DeriveTypeMapping.ContainsKey(deriveType))
					deriveType = DeriveTypeMapping[deriveType];
			}
			return new QField(classProp.Property.ID, String.Format(deriveType, derivedFromFldName) );
		}

		protected virtual DalcStorageQueryTranslator GetQueryTranslator(DataSchema schema) {
			return new DalcStorageQueryTranslator(schema, this, ResolveDerivedProperty );
		}

		public IEnumerable<ObjectRelation> LoadRelations(string relationshipId, QueryNode conditions) {
			var schema = GetSchema();
			// check for relation table
			var relationship = schema.FindRelationshipByID(relationshipId);
			if (relationship == null)
				throw new Exception(String.Format("Relationship with ID={0} does not exist", relationshipId));

			var qTranslator = GetQueryTranslator(schema);
			var query = new Query(relationshipId, conditions);
			var relQuery = qTranslator.TranslateSubQuery( query );
			relQuery.Sort = query.Sort; // leave as is
			
			var rs = new List<ObjectRelation>();
			DbMgr.Dalc.ExecuteReader( relQuery, (rdr) => {
				while (rdr.Read()) {
					var subjectId = Convert.ToInt64( rdr["subject_id"] );
					var objectId = Convert.ToInt64( rdr["object_id"] );
					rs.Add( new ObjectRelation(subjectId, relationship, objectId) );
				}
			});

			return rs;
		}


		public long[] GetObjectIds(Query q) {
			var schema = GetSchema();
			var dataClass = schema.FindClassByID(q.Table.Name);
			var qTranslator = GetQueryTranslator(schema);

			var translatedQuery = new Query( new QTable( ObjectTableName, q.Table.Alias ) );
			translatedQuery.StartRecord = q.StartRecord;
			translatedQuery.RecordCount = q.RecordCount;
			translatedQuery.Condition = TranslateQueryCondition(dataClass, schema, q.Condition);

			translatedQuery.Fields = new[] { new QField(q.Table.Alias, "id", null) };
			return LoadTranslatedQueryInternal(dataClass, translatedQuery, q);
		}

		protected virtual long[] LoadTranslatedQueryInternal(Class dataClass, Query translatedQuery, Query originalQuery) {
			var sort = originalQuery.Sort;
			var applySort = sort!=null && sort.Length>0;
			var ids = new List<long>();
			if (applySort) {
				translatedQuery.StartRecord = 0;
				translatedQuery.RecordCount = Int32.MaxValue;
			}
			var loadedIds = DbMgr.Dalc.LoadAllValues( translatedQuery );
			var idsArr = new long[loadedIds.Length];
			for (int i=0; i<loadedIds.Length; i++)
				idsArr[i] = Convert.ToInt64(loadedIds[i]);

			if (applySort) {
				// the following "in-code" implementation is used for abstract IDalc implementations
				var sortProperties = new List<Property>();
				foreach (var sortFld in sort) {
					// id is not handled. TBD: predefined column
					var p = dataClass.FindPropertyByID( sortFld.Field );
					if (p==null)
						throw new Exception("Unknown property "+sortFld.Field );
					sortProperties.Add(p);
				}

				var idToObj = Load(idsArr, sortProperties.ToArray() );
				Array.Sort( idsArr, (a, b) => {
					for (int i=0; i<sortProperties.Count; i++) {
						var aVal = idToObj.ContainsKey(a) ? idToObj[a][sortProperties[i]] : null;
						var bVal = idToObj.ContainsKey(b) ? idToObj[b][sortProperties[i]] : null;
						var compareRes = DbValueComparer.Instance.Compare(aVal,bVal);
						if (sort[i].SortDirection==System.ComponentModel.ListSortDirection.Descending)
							compareRes = -compareRes;
						if (compareRes!=0)
							return compareRes;
					}
					return 0;
				});
				idsArr = idsArr.Skip(originalQuery.StartRecord).Take(originalQuery.RecordCount).ToArray();
			}
			return idsArr;
		}

		public int GetObjectsCount(Query q) {
			var schema = GetSchema();
			var dataClass = schema.FindClassByID(q.Table.Name);
			var translatedQuery = new Query(new QTable(ObjectTableName, q.Table.Alias));
			translatedQuery.Condition = TranslateQueryCondition(dataClass, schema, q.Condition);

			return DbMgr.Dalc.RecordsCount( translatedQuery );
		}

		protected QueryNode TranslateQueryCondition(Class dataClass, DataSchema schema, QueryNode condition) {
			var conditionGrp = QueryGroupNode.And();
			conditionGrp.Nodes.Add(
				(QField)"compact_class_id" == new QConst(dataClass.CompactID)
			);
			var qTranslator = GetQueryTranslator(schema);
			if (condition != null)
				conditionGrp.Nodes.Add(qTranslator.TranslateQueryNode(dataClass, condition));
			
			return conditionGrp;
		}


	}
}
