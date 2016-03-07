﻿using System;
using System.Linq;
using System.Threading.Tasks;
using BudgetAnalyser.Engine.Statement;
using BudgetAnalyser.Engine.Statement.Data;
using BudgetAnalyser.Engine.UnitTest.Helper;
using BudgetAnalyser.Engine.UnitTest.TestData;
using BudgetAnalyser.Engine.UnitTest.TestHarness;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Rees.TangyFruitMapper;

namespace BudgetAnalyser.Engine.UnitTest.Statement
{
    [TestClass]
    public class CsvOnDiskStatementModelRepositoryV1Test
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CtorShouldThrowGivenNullBankImportUtils()
        {
            new CsvOnDiskStatementModelRepositoryV1(
                null,
                new FakeLogger(),
                new DtoMapperStub<TransactionSetDto, StatementModel>());
            Assert.Fail();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CtorShouldThrowGivenNullMapper()
        {
            new CsvOnDiskStatementModelRepositoryV1(
                new BankImportUtilities(new FakeLogger()),
                new FakeLogger(),
                null);
            Assert.Fail();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CtorShouldThrowGivenNullLogger()
        {
            new CsvOnDiskStatementModelRepositoryV1(
                new BankImportUtilities(new FakeLogger()),
                null,
                new DtoMapperStub<TransactionSetDto, StatementModel>());
            Assert.Fail();
        }

        [TestMethod]
        public async Task IsValidFileShouldReturnFalseGivenIncorrectVersionHashFile()
        {
            CsvOnDiskStatementModelRepositoryV1TestHarness subject = Arrange();
            subject.ReadLinesOverride = file => BudgetAnalyserRawCsvTestDataV1.BadTestData_IncorrectVersionHash();
            bool result = await subject.IsStatementModelAsync("Foo.foo");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task IsValidFileShouldReturnTrueGivenGoodFile()
        {
            CsvOnDiskStatementModelRepositoryV1TestHarness subject = Arrange();
            subject.ReadLinesOverride = file => BudgetAnalyserRawCsvTestDataV1.TestData1();
            bool result = await subject.IsStatementModelAsync("Foo.foo");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task LoadShouldReturnAStatementModelGivenFileWithNoTransactions()
        {
            CsvOnDiskStatementModelRepositoryV1TestHarness subject = Arrange();
            subject.ReadLinesOverride = file => BudgetAnalyserRawCsvTestDataV1.EmptyTestData();
            StatementModel model = await subject.LoadAsync("Foo.foo");

            Assert.IsNotNull(model);
        }

        [TestMethod]
        public async Task LoadShouldReturnStatementModelWithFilenameGivenTestData1()
        {
            CsvOnDiskStatementModelRepositoryV1TestHarness subject = Arrange();
            subject.ReadLinesOverride = file => BudgetAnalyserRawCsvTestDataV1.TestData1();
            StatementModel model = await subject.LoadAsync("Foo.foo");

            Assert.AreEqual("Foo.foo", model.StorageKey);
        }

        [TestMethod]
        public async Task LoadShouldReturnStatementModelWithImportedDateGivenTestData1()
        {
            CsvOnDiskStatementModelRepositoryV1TestHarness subject = Arrange();
            subject.ReadLinesOverride = file => BudgetAnalyserRawCsvTestDataV1.TestData1();
            StatementModel model = await subject.LoadAsync("Foo.foo");
            Console.WriteLine(model.LastImport);
            Assert.AreEqual(new DateTime(2012, 08, 20), model.LastImport);
        }

        [TestMethod]
        public async Task LoadShouldReturnStatementModelWithNoTransactionsGivenFileWithNoTransactions()
        {
            CsvOnDiskStatementModelRepositoryV1TestHarness subject = Arrange();
            subject.ReadLinesOverride = file => BudgetAnalyserRawCsvTestDataV1.EmptyTestData();
            StatementModel model = await subject.LoadAsync("Foo.foo");

            Assert.AreEqual(0, model.AllTransactions.Count());
        }

        [TestMethod]
        public async Task LoadShouldReturnStatementModelWithOneDurationGivenTestData1()
        {
            CsvOnDiskStatementModelRepositoryV1TestHarness subject = Arrange();
            subject.ReadLinesOverride = file => BudgetAnalyserRawCsvTestDataV1.TestData1();
            StatementModel model = await subject.LoadAsync("Foo.foo");

            Assert.AreEqual(1, model.DurationInMonths);
        }

        [TestMethod]
        public async Task LoadShouldReturnStatementModelWithTransactionsGivenTestData1()
        {
            CsvOnDiskStatementModelRepositoryV1TestHarness subject = Arrange();
            subject.ReadLinesOverride = file => BudgetAnalyserRawCsvTestDataV1.TestData1();
            StatementModel model = await subject.LoadAsync("Foo.foo");

            Assert.AreEqual(15, model.AllTransactions.Count());
        }

        [TestMethod]
        public async Task LoadShouldReturnStatementModelWithZeroDurationGivenFileWithNoTransactions()
        {
            CsvOnDiskStatementModelRepositoryV1TestHarness subject = Arrange();
            subject.ReadLinesOverride = file => BudgetAnalyserRawCsvTestDataV1.EmptyTestData();
            StatementModel model = await subject.LoadAsync("Foo.foo");

            Assert.AreEqual(0, model.DurationInMonths);
        }

        [TestMethod]
        [ExpectedException(typeof(StatementModelChecksumException))]
        public async Task LoadShouldThrowGivenFileWithIncorrectChecksum()
        {
            CsvOnDiskStatementModelRepositoryV1TestHarness subject = Arrange();
            subject.ReadLinesOverride = file => BudgetAnalyserRawCsvTestDataV1.BadTestData_IncorrectChecksum();
            await subject.LoadAsync("foo.foo");

            Assert.Fail();
        }

        [TestMethod]
        [ExpectedException(typeof(DataFormatException))]
        public async Task LoadShouldThrowGivenFileWithIncorrectDataTypes()
        {
            CsvOnDiskStatementModelRepositoryV1TestHarness subject = Arrange();
            subject.ReadLinesOverride = file => BudgetAnalyserRawCsvTestDataV1.BadTestData_IncorrectDataTypeInRow1();
            await subject.LoadAsync("foo.foo");
            Assert.Fail();
        }

        [TestMethod]
        [ExpectedException(typeof(NotSupportedException))]
        public async Task LoadShouldThrowGivenIncorrectVersionHashFile()
        {
            CsvOnDiskStatementModelRepositoryV1TestHarness subject = Arrange();
            subject.ReadLinesOverride = file => BudgetAnalyserRawCsvTestDataV1.BadTestData_IncorrectVersionHash();
            await subject.LoadAsync("Foo.foo");

            Assert.Fail();
        }

        [TestMethod]
        public async Task MustBeAbleToLoadDemoStatementFile()
        {
            CsvOnDiskStatementModelRepositoryV1TestHarness subject = Arrange();
            subject.ReadLinesOverride = EmbeddedResourceHelper.ExtractString;

            StatementModel model = await subject.LoadAsync(TestDataConstants.DemoTransactionsFileName);

            Assert.IsNotNull(model);
            Assert.AreEqual(33, model.AllTransactions.Count());
        }

        [TestMethod]
        public async Task MustBeAbleToLoadDemoStatementFile2()
        {
            CsvOnDiskStatementModelRepositoryV1TestHarness subject = Arrange();
            subject.ReadLinesOverride = file => EmbeddedResourceHelper.ExtractLines(TestDataConstants.DemoTransactionsFileName, true);
            StatementModel model = await subject.LoadAsync("Foo.foo");
            Console.WriteLine(model.DurationInMonths);
            Assert.AreEqual(1, model.DurationInMonths);
        }

        [TestMethod]
        [ExpectedException(typeof(StatementModelChecksumException))]
        public async Task SaveShouldThrowGivenMappingDoesNotMapAllTransactions()
        {
            var mapper = new Mock<IDtoMapper<TransactionSetDto, StatementModel>>();
            CsvOnDiskStatementModelRepositoryV1TestHarness subject = ArrangeWithMockMappers(mapper.Object);
            StatementModel model = StatementModelTestData.TestData2();
            model.Filter(new GlobalFilterCriteria { BeginDate = new DateTime(2013, 07, 20), EndDate = new DateTime(2013, 08, 19) });

            mapper.Setup(m => m.ToDto(model)).Returns(
                new TransactionSetDto
                {
                    StorageKey = "Foo.bar",
                    LastImport = new DateTime(2013, 07, 20),
                    Transactions = TransactionSetDtoTestData.TestData2().Transactions.Take(2).ToList()
                });

            await subject.SaveAsync(model, "Foo.bar");

            Assert.Fail();
        }

        private CsvOnDiskStatementModelRepositoryV1TestHarness Arrange()
        {
            return new CsvOnDiskStatementModelRepositoryV1TestHarness(new BankImportUtilitiesTestHarness());
        }

        private CsvOnDiskStatementModelRepositoryV1TestHarness ArrangeWithMockMappers(IDtoMapper<TransactionSetDto, StatementModel> mapper)
        {
            return new CsvOnDiskStatementModelRepositoryV1TestHarness(new BankImportUtilitiesTestHarness(), mapper);
        }
    }
}