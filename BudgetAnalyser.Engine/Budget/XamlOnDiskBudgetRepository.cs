using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Xaml;
using BudgetAnalyser.Engine.Annotations;
using BudgetAnalyser.Engine.Budget.Data;

namespace BudgetAnalyser.Engine.Budget
{
    [AutoRegisterWithIoC(SingleInstance = true)]
    public class XamlOnDiskBudgetRepository : IBudgetRepository, IApplicationHookEventPublisher
    {
        //private const string EmptyBudgetFileName = ":::EmptyBudget";
        //private const string EmptyBudgetXaml =
        //    @"<?xml version=""1.0"" encoding=""utf-8"" ?><BudgetCollectionDto FileName="":::EmptyBudget""xmlns=""clr-namespace:BudgetAnalyser.Engine.Budget.Data;assembly=BudgetAnalyser.Engine""xmlns:scg=""clr-namespace:System.Collections.Generic;assembly=mscorlib""xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""><BudgetCollectionDto.Buckets><scg:List x:TypeArguments=""BudgetBucketDto"" Capacity=""1""/>    </BudgetCollectionDto.Buckets><BudgetCollectionDto.Budgets><scg:List x:TypeArguments=""BudgetModelDto"" Capacity=""4""><BudgetModelDto LastModifiedComment=""{x:Null}"" EffectiveFrom=""2014-01-19T00:00+13:00"" LastModified=""2014-01-19T11:49:07.7350234+13:00"" Name=""Default Budget""><BudgetModelDto.Expenses><scg:List x:TypeArguments=""ExpenseDto"" Capacity=""1""/></BudgetModelDto.Expenses><BudgetModelDto.Incomes><scg:List x:TypeArguments=""IncomeDto"" Capacity=""1""/></BudgetModelDto.Incomes></BudgetModelDto></scg:List></BudgetCollectionDto.Budgets></BudgetCollectionDto>";

        private readonly BasicMapper<BudgetCollectionDto, BudgetCollection> toDomainMapper;
        private readonly BasicMapper<BudgetCollection, BudgetCollectionDto> toDtoMapper;
        private BudgetCollection currentBudgetCollection;

        public XamlOnDiskBudgetRepository(
            [NotNull] IBudgetBucketRepository bucketRepository,
            [NotNull] BasicMapper<BudgetCollection, BudgetCollectionDto> toDtoMapper,
            [NotNull] BasicMapper<BudgetCollectionDto, BudgetCollection> toDomainMapper)
        {
            if (bucketRepository == null)
            {
                throw new ArgumentNullException("bucketRepository");
            }

            if (toDtoMapper == null)
            {
                throw new ArgumentNullException("toDtoMapper");
            }

            if (toDomainMapper == null)
            {
                throw new ArgumentNullException("toDomainMapper");
            }

            BudgetBucketRepository = bucketRepository;
            this.toDtoMapper = toDtoMapper;
            this.toDomainMapper = toDomainMapper;
        }

        public event EventHandler<ApplicationHookEventArgs> ApplicationEvent;
        public IBudgetBucketRepository BudgetBucketRepository { get; private set; }

        public BudgetCollection CreateNew()
        {
            var budget = new BudgetModel();
            this.currentBudgetCollection = new BudgetCollection(new[] { budget });
            BudgetBucketRepository.Initialise(new List<BudgetBucketDto>());
            return this.currentBudgetCollection;
        }

        public BudgetCollection CreateNew([NotNull] string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            var newBudget = new BudgetModel
            {
                EffectiveFrom = DateTime.Today,
                Name = "Default Budget"
            };

            var newCollection = new BudgetCollection(new[] { newBudget })
            {
                FileName = fileName
            };

            BudgetBucketRepository.Initialise(new List<BudgetBucketDto>());

            Save(newCollection);

            return newCollection;
        }

        public async Task<BudgetCollection> LoadAsync(string fileName)
        {
            if (!FileExists(fileName))
            {
                throw new FileNotFoundException("File not found.", fileName);
            }

            object serialised;
            try
            {
                serialised = await LoadFromDisk(fileName); // May return null for some errors.
            }
            catch (XamlObjectWriterException ex)
            {
                throw new DataFormatException(
                    string.Format(CultureInfo.CurrentCulture, "The budget file '{0}' is an invalid format. This is probably due to changes in the code, most likely namespace changes.", fileName),
                    ex);
            }
            catch (Exception ex)
            {
                throw new DataFormatException("Deserialisation the Budget file failed, an exception was thrown by the Xaml deserialiser, the file format is invalid.", ex);
            }

            var correctDataFormat = serialised as BudgetCollectionDto;
            if (correctDataFormat == null)
            {
                throw new DataFormatException(
                    string.Format(CultureInfo.InvariantCulture, "The file used to store application state ({0}) is not in the correct format. It may have been tampered with.", fileName));
            }

            // Bucket Repository must be initialised first, the budget model incomes/expenses are dependent on the bucket repository.
            BudgetBucketRepository.Initialise(correctDataFormat.Buckets);

            BudgetCollection budgetCollection = this.toDomainMapper.Map(correctDataFormat);
            budgetCollection.FileName = fileName;
            this.currentBudgetCollection = budgetCollection;
            return budgetCollection;
        }

        public void Save()
        {
            if (this.currentBudgetCollection == null)
            {
                throw new InvalidOperationException("There is no current budget collection loaded.");
            }

            Save(this.currentBudgetCollection);
        }

        public void Save(BudgetCollection budget)
        {
            if (this.currentBudgetCollection == null)
            {
                throw new InvalidOperationException("There is no current budget collection loaded.");
            }

            BudgetCollectionDto dataFormat = this.toDtoMapper.Map(budget);

            string serialised = Serialise(dataFormat);
            WriteToDisk(dataFormat.FileName, serialised);

            EventHandler<ApplicationHookEventArgs> handler = ApplicationEvent;
            if (handler != null)
            {
                handler(this, new ApplicationHookEventArgs(ApplicationHookEventType.Repository, "BudgetRepository", ApplicationHookEventArgs.Save));
            }
        }

        protected virtual bool FileExists(string fileName)
        {
            return File.Exists(fileName);
        }

        protected async virtual Task<object> LoadFromDisk(string fileName)
        {
            object result = null;
            await Task.Run(() => result = XamlServices.Load(fileName));
            return result;
        }

        protected virtual string Serialise(BudgetCollectionDto budgetData)
        {
            return XamlServices.Save(budgetData);
        }

        protected virtual void WriteToDisk(string fileName, string data)
        {
            File.WriteAllText(fileName, data);
        }
    }
}