using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Threading;
using System.Security.Principal;
using System.Data;

using NI.Data;
using NI.Data.Permissions;

using NUnit.Framework;

namespace NI.Tests.Data.Dalc
{
	/// <summary>
	/// </summary>
	[TestFixture]
	[NUnit.Framework.Category("NI.Data")]
	public class PermissionTest
	{
		[Test]
		public void test_PermissionRules() {
			var sqliteTest = new SQLiteDalcTest();
			sqliteTest.SetUp();

			try {
				var dalc = sqliteTest.Dalc;

				var oldCmdGen = (DbCommandGenerator)dalc.CommandGenerator;
				dalc.CommandGenerator = new DbPermissionCommandGenerator(dalc.DbFactory, oldCmdGen.Views, new Func<PermissionContext, QueryNode>[] {
					new QueryRule("users", DalcOperation.Select, "role!=\"3\":int32 or \"IdentityName\":var=\"Mike\"" ).ComposeCondition,
					new QueryRule("users", DalcOperation.Change, " \"IdentityName\":var=name " ).ComposeCondition
				});

				Assert.AreEqual(3, dalc.RecordsCount(new Query("users")), "Select rule failed");

				Assert.AreEqual(0, dalc.Delete(new Query("users")), "Change rule failed");
				Assert.AreEqual(0, dalc.Update(new Query("users"), new Dictionary<string, IQueryValue> {
					{"role", (QConst)1 }
				}), "Change rule failed");

				var ds = new DataSet();
				dalc.Load( new Query("users", (QField)"name"==(QConst)"Mike" ), ds);
				ds.Tables["users"].PrimaryKey = new[] { ds.Tables["users"].Columns["id"] };

				ds.Tables["users"].Rows[0]["name"] = "NewMike";

				Assert.Catch<DBConcurrencyException>(() => {
					dalc.Update(ds.Tables["users"]);
				}, "Change rule failed");

				var oldPrincipal = Thread.CurrentPrincipal;
				try {

					Thread.CurrentPrincipal = new GenericPrincipal(new GenericIdentity("Mike"), new string[0] { });

					Assert.AreEqual(4, dalc.RecordsCount(new Query("users")), "Select rule failed");

					Assert.DoesNotThrow(() => {
						dalc.Update(ds.Tables["users"]);
					}, "Change rule failed");

					Assert.AreEqual(1, dalc.Update(new Query("users"), new Dictionary<string, IQueryValue> {
						{"role", (QConst)1 }
					}), "Change rule failed");
				} finally {
					Thread.CurrentPrincipal = oldPrincipal;
				}

			} finally {
				sqliteTest.CleanUp();
			}
		}
	}
}
