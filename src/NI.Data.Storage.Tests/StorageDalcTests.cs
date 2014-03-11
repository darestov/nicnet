﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Threading.Tasks;

using NUnit.Framework;
using NI.Data;

using NI.Data.Storage.Model;

namespace NI.Data.Storage.Tests {
	
	[TestFixture]
	public class StorageDalcTests {
		
		DataSchema testSchema;
		StubObjectContainerStorageContext objContext;
		IDalc storageDalc;

		[SetUp]
		public void createTestStorageDalc() {
			testSchema = StubObjectContainerStorageContext.CreateTestSchema();
			Func<DataSchema> getTestSchema = () => { return testSchema; };

			objContext = new StubObjectContainerStorageContext(getTestSchema);
			storageDalc = new StorageDalc(objContext.StorageDbMgr.Dalc, objContext.ObjectContainerStorage, getTestSchema);
		}

		protected void addTestData() {
			var googCompany = new ObjectContainer(testSchema.FindClassByID("companies"));
			googCompany["title"] = "Google";
			
			var msCompany = new ObjectContainer(testSchema.FindClassByID("companies"));
			msCompany["title"] = "Microsoft";
			
			objContext.ObjectContainerStorage.Insert(googCompany);
			objContext.ObjectContainerStorage.Insert(msCompany);

			var johnContact = new ObjectContainer(testSchema.FindClassByID("contacts"));
			johnContact["name"] = "John";
			johnContact["is_primary"] = true;
			var maryContact = new ObjectContainer(testSchema.FindClassByID("contacts"));
			maryContact["name"] = "Mary";
			maryContact["is_primary"] = false;
			maryContact["birthday"] = new DateTime(1999, 5, 20);
			var bobContact = new ObjectContainer(testSchema.FindClassByID("contacts"));
			maryContact["name"] = "Bob";
			bobContact["is_primary"] = true;

			objContext.ObjectContainerStorage.Insert(johnContact);
			objContext.ObjectContainerStorage.Insert(maryContact);
			objContext.ObjectContainerStorage.Insert(bobContact);
		}

		[Test]
		public void Insert() {
			// direct insert
			storageDalc.Insert("companies", new {
				title = "Microsoft"
			});

			Assert.AreEqual(1, objContext.StorageDS.Tables["objects"].Rows.Count);
			Assert.AreEqual(1, objContext.StorageDS.Tables["object_string_values"].Rows.Count);
			Assert.AreEqual("Microsoft", objContext.StorageDS.Tables["object_string_values"].Rows[0]["value"]);

			// dataset insert
			var ds = new DataSet();
			var contactsTbl = testSchema.FindClassByID("contacts").CreateDataTable();
			ds.Tables.Add(contactsTbl);

			var newRow = contactsTbl.NewRow();
			newRow["name"] = "John Smith";
			newRow["birthday"] = new DateTime(1980,1,1);
			contactsTbl.Rows.Add(newRow);
			storageDalc.Update( contactsTbl );
		}

		[Test]
		public void Delete() {
			addTestData();

			Assert.AreEqual(3, storageDalc.RecordsCount( new Query("contacts") ) );

			var ds = new DataSet();
			var contactsTbl = testSchema.FindClassByID("contacts").CreateDataTable();
			ds.Tables.Add(contactsTbl);

			storageDalc.Load( new Query("contacts", (QField)"id" == (QConst)3 ), ds);
			Assert.AreEqual(1, ds.Tables["contacts"].Rows.Count);
			ds.Tables["contacts"].Rows[0].Delete();
			storageDalc.Update(ds.Tables["contacts"]);

			Assert.AreEqual(2, storageDalc.RecordsCount(new Query("contacts")));
		}

	}
}