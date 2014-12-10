using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;
using BudgetAnalyser.Annotations;
using BudgetAnalyser.Engine;
using BudgetAnalyser.Engine.Budget;
using BudgetAnalyser.Engine.Services;
using BudgetAnalyser.ShellDialog;
using GalaSoft.MvvmLight.CommandWpf;
using Rees.UserInteraction.Contracts;
using Rees.Wpf;
using Rees.Wpf.ApplicationState;

namespace BudgetAnalyser.Budget
{
    [AutoRegisterWithIoC(SingleInstance = true)]
    public class BudgetController : ControllerBase, IShowableController
    {
        private const string CloseBudgetMenuName = "Close _Budget";
        private const string EditBudgetMenuName = "Edit Current _Budget";

        private readonly DemoFileHelper demoFileHelper;
        private readonly IBudgetMaintenanceService maintenanceService;
        private readonly Func<IUserPromptOpenFile> fileOpenDialogFactory;
        private readonly Func<IUserPromptSaveFile> fileSaveDialogFactory;
        private readonly IUserInputBox inputBox;
        private readonly IUserMessageBox messageBox;
        private readonly List<BudgetBucket> newBuckets = new List<BudgetBucket>();
        private readonly IUserQuestionBoxYesNo questionBox;
        private string budgetMenuItemName;
        private Guid dialogCorrelationId;
        private bool dirty;
        private BudgetCurrencyContext doNotUseModel;
        private bool doNotUseShownBudget;
        private decimal expenseTotal;
        private decimal incomeTotal;
        private bool loading;
        private decimal surplus;

        [SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors", Justification = "OnPropertyChange is ok to call here")]
        public BudgetController(
            [NotNull] UiContext uiContext,
            [NotNull] DemoFileHelper demoFileHelper, 
            [NotNull] IBudgetMaintenanceService maintenanceService)
        {
            if (uiContext == null)
            {
                throw new ArgumentNullException("uiContext");
            }

            if (demoFileHelper == null)
            {
                throw new ArgumentNullException("demoFileHelper");
            }
            
            if (maintenanceService == null)
            {
                throw new ArgumentNullException("maintenanceService");
            }

            this.demoFileHelper = demoFileHelper;
            this.maintenanceService = maintenanceService;
            this.questionBox = uiContext.UserPrompts.YesNoBox;
            this.messageBox = uiContext.UserPrompts.MessageBox;
            this.fileOpenDialogFactory = uiContext.UserPrompts.OpenFileFactory;
            this.fileSaveDialogFactory = uiContext.UserPrompts.SaveFileFactory;
            this.inputBox = uiContext.UserPrompts.InputBox;
            BudgetPieController = uiContext.BudgetPieController;
            Shown = false;

            MessengerInstance = uiContext.Messenger;
            MessengerInstance.Register<ApplicationStateRequestedMessage>(this, OnApplicationStateRequested);
            MessengerInstance.Register<ApplicationStateLoadedMessage>(this, OnApplicationStateLoaded);
            MessengerInstance.Register<ShellDialogResponseMessage>(this, OnPopUpResponseReceived);

            CurrentBudget = this.maintenanceService.CreateNewBudgetCollection();
        }

        public ICommand AddNewExpenseCommand
        {
            get { return new RelayCommand<ExpenseBucket>(OnAddNewExpenseExecute); }
        }

        public ICommand AddNewIncomeCommand
        {
            get { return new RelayCommand(OnAddNewIncomeExecute); }
        }

        public string BudgetMenuItemName
        {
            get { return this.budgetMenuItemName; }

            set
            {
                this.budgetMenuItemName = value;
                RaisePropertyChanged(() => BudgetMenuItemName);
            }
        }

        public BudgetPieController BudgetPieController { get; private set; }

        public BudgetCollection Budgets { get; private set; }

        public BudgetCurrencyContext CurrentBudget
        {
            get { return this.doNotUseModel; }

            private set
            {
                this.doNotUseModel = value;
                ReleaseListBindingEvents(Incomes);
                ReleaseListBindingEvents(Expenses);
                if (this.doNotUseModel == null)
                {
                    Incomes = null;
                    Expenses = null;
                }
                else
                {
                    Incomes = new BindingList<Income>(this.doNotUseModel.Model.Incomes.ToList());
                    Incomes.ToList().ForEach(i =>
                    {
                        i.PropertyChanged += OnIncomeAmountPropertyChanged;
                        i.Bucket.PropertyChanged += OnIncomeAmountPropertyChanged;
                    });
                    Expenses = new BindingList<Expense>(this.doNotUseModel.Model.Expenses.ToList());
                    Expenses.ToList().ForEach(e =>
                    {
                        e.PropertyChanged += OnExpenseAmountPropertyChanged;
                        e.Bucket.PropertyChanged += OnExpenseAmountPropertyChanged;
                    });
                }

                RaisePropertyChanged(() => Incomes);
                RaisePropertyChanged(() => Expenses);
                OnExpenseAmountPropertyChanged(null, EventArgs.Empty);
                OnIncomeAmountPropertyChanged(null, EventArgs.Empty);
                RaisePropertyChanged(() => CurrentBudget);
            }
        }

        public ICommand DeleteBudgetItemCommand
        {
            get { return new RelayCommand<object>(OnDeleteBudgetItemCommandExecute); }
        }

        public ICommand DemoBudgetCommand
        {
            get { return new RelayCommand(OnDemoBudgetCommandExecuted); }
        }

        public ICommand DetailsCommand
        {
            get { return new RelayCommand(OnDetailsCommandExecute); }
        }

        public decimal ExpenseTotal
        {
            get { return this.expenseTotal; }

            private set
            {
                this.expenseTotal = value;
                RaisePropertyChanged(() => ExpenseTotal);
            }
        }

        public BindingList<Expense> Expenses { get; private set; }

        public decimal IncomeTotal
        {
            get { return this.incomeTotal; }

            private set
            {
                this.incomeTotal = value;
                RaisePropertyChanged(() => IncomeTotal);
            }
        }

        public BindingList<Income> Incomes { get; private set; }

        public ICommand LoadBudgetCommand
        {
            get { return new RelayCommand(OnLoadBudgetCommandExecute); }
        }

        public ICommand SaveAsCommand
        {
            get { return new RelayCommand(OnSaveAsCommandExecute); }
        }

        public ICommand ShowAllCommand
        {
            get { return new RelayCommand(OnShowAllCommandExecuted); }
        }

        public ICommand ShowPieCommand
        {
            get { return new RelayCommand(OnShowPieCommandExecuted, CanExecuteShowPieCommand); }
        }

        public bool Shown
        {
            get { return this.doNotUseShownBudget; }

            set
            {
                if (value == this.doNotUseShownBudget)
                {
                    return;
                }

                this.doNotUseShownBudget = value;
                RaisePropertyChanged(() => Shown);
                BudgetMenuItemName = this.doNotUseShownBudget ? CloseBudgetMenuName : EditBudgetMenuName;
                if (!value)
                {
                    ValidateAndClose();
                }
            }
        }

        public decimal Surplus
        {
            get { return this.surplus; }
            private set
            {
                this.surplus = value;
                RaisePropertyChanged(() => Surplus);
            }
        }

        public string TruncatedFileName
        {
            get { return Budgets.FileName.TruncateLeft(100, true); }
        }

        protected virtual string BuildDefaultFileName()
        {
            string path = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            return Path.Combine(path, "BudgetModel.xml");
        }

        protected virtual bool SaveBudgetCollection()
        {
            string comment = this.inputBox.Show("Budget Maintenance", "Enter an optional comment to describe what you changed.");
            if (comment == null)
            {
                return false;
            }

            this.newBuckets.Clear();
            var valid = this.maintenanceService.SaveBudget(CurrentBudget.Model, comment);
            if (!valid)
            {
                return SaveBudgetModel();
            }

            return true;
        }

        private bool CanExecuteShowPieCommand()
        {
            if (Expenses == null || Incomes == null || CurrentBudget == null)
            {
                return false;
            }

            return Expenses.Any() || Incomes.Any();
        }

        private string GetFileNameFromUserForOpen()
        {
            IUserPromptOpenFile fileOpenDialog = this.fileOpenDialogFactory();
            fileOpenDialog.CheckFileExists = true;
            fileOpenDialog.CheckPathExists = true;
            bool? result = fileOpenDialog.ShowDialog();
            if (result == null || result == false)
            {
                return null;
            }

            return fileOpenDialog.FileName;
        }

        private string GetFileNameFromUserForSave()
        {
            IUserPromptSaveFile fileSaveDialog = this.fileSaveDialogFactory();
            fileSaveDialog.CheckPathExists = true;
            fileSaveDialog.AddExtension = true;
            fileSaveDialog.DefaultExt = ".xml";
            bool? result = fileSaveDialog.ShowDialog();
            if (result == null || result == false)
            {
                return null;
            }

            return fileSaveDialog.FileName;
        }

        private void HandleBudgetFileExceptions(string message)
        {
            string defaultFileName = BuildDefaultFileName();
            this.messageBox.Show("Budget File", "{0}\n{1}", message, defaultFileName);
            LoadBudget(defaultFileName);
        }

        private void LoadBudget(string fileName)
        {
            try
            {
                this.loading = true;
                CurrentBudget = this.maintenanceService.LoadBudgetsCollection(fileName);
                Budgets = CurrentBudget.BudgetCollection;
                BudgetBucketBindingSource.BucketRepository = this.maintenanceService.BudgetBucketRepository;
                RaisePropertyChanged(() => TruncatedFileName);
                if (CurrentBudget != null)
                {
                    MessengerInstance.Send(new BudgetReadyMessage(CurrentBudget, Budgets));
                }
            }
            catch (DataFormatException)
            {
                this.messageBox.Show("That is not a valid Budget-Analyser Budget file.");
            }
            finally
            {
                this.loading = false;
            }
        }

        private void LoadDemoBudget()
        {
            LoadBudget(this.demoFileHelper.FindDemoFile("DemoBudget.xml"));
        }

        private void OnAddNewExpenseExecute(ExpenseBucket expense)
        {
            this.dirty = true;
            Expense newExpense = Expenses.AddNew(); 
            newExpense.Amount = 0;

            // New buckets must be created because the one passed in, is a single command parameter instance to be used as a type indicator only.
            // If it was used, the same instance would overwritten each time an expense is created.
            if (expense is SpentMonthlyExpenseBucket)
            {
                newExpense.Bucket = new SpentMonthlyExpenseBucket(string.Empty, string.Empty);
            }
            else if (expense is SavedUpForExpenseBucket)
            {
                newExpense.Bucket = new SavedUpForExpenseBucket(string.Empty, string.Empty);
            }
            else if (expense is SavingsCommitmentBucket)
            {
                newExpense.Bucket = new SavingsCommitmentBucket(string.Empty, string.Empty);
            }
            else
            {
                throw new InvalidCastException("Invalid type passed to Add New Expense: " + expense);
            }

            // With every new expense created we need to create a new bucket.
            this.newBuckets.Add(newExpense.Bucket);

            Expenses.RaiseListChangedEvents = true;
            newExpense.PropertyChanged += OnExpenseAmountPropertyChanged;
        }

        private void OnAddNewIncomeExecute()
        {
            this.dirty = true;
            var newIncome = new Income { Bucket = new IncomeBudgetBucket(string.Empty, string.Empty), Amount = 0 };
            this.newBuckets.Add(newIncome.Bucket);
            Incomes.Add(newIncome);
            newIncome.PropertyChanged += OnIncomeAmountPropertyChanged;
        }

        private void OnApplicationStateLoaded(ApplicationStateLoadedMessage message)
        {
            try
            {
                if (!message.RehydratedModels.ContainsKey(typeof(LastBudgetLoadedV1)))
                {
                    return;
                }

                var budgetFileName = message.RehydratedModels[typeof(LastBudgetLoadedV1)].AdaptModel<string>();
                if (string.IsNullOrWhiteSpace(budgetFileName))
                {
                    LoadDemoBudget();
                    return;
                }

                LoadBudget(budgetFileName);
            }
            catch (DataFormatException)
            {
                HandleBudgetFileExceptions("The last Budget file is an invalid file format. A empty default file will use the default file instead.");
            }
            catch (FileNotFoundException)
            {
                HandleBudgetFileExceptions("The last Budget file used cannot be found. A empty default file will use the default file instead.");
            }
        }

        private void OnApplicationStateRequested(ApplicationStateRequestedMessage message)
        {
            // Only the filename of the current budget is saved using the ApplicationState mechanism.  The budget itself is saved on demand when it has changed.
            // Save the filename of the last budget used by the application.
            var persistentModel = new LastBudgetLoadedV1 { Model = Budgets.FileName };
            message.PersistThisModel(persistentModel);
        }

        private void OnDeleteBudgetItemCommandExecute(object budgetItem)
        {
            bool? response = this.questionBox.Show("Are you sure you want to delete this budget bucket?\nAnalysis may not work correctly if transactions are allocated to this bucket.",
                "Delete Budget Bucket");
            if (response == null || response.Value == false)
            {
                return;
            }

            this.dirty = true;
            var expenseItem = budgetItem as Expense;
            if (expenseItem != null)
            {
                expenseItem.PropertyChanged -= OnExpenseAmountPropertyChanged;
                expenseItem.Bucket.PropertyChanged -= OnExpenseAmountPropertyChanged;
                Expenses.Remove(expenseItem);
                return;
            }

            var incomeItem = budgetItem as Income;
            if (incomeItem != null)
            {
                incomeItem.PropertyChanged -= OnIncomeAmountPropertyChanged;
                incomeItem.Bucket.PropertyChanged -= OnIncomeAmountPropertyChanged;
                Incomes.Remove(incomeItem);
            }
        }

        private void OnDemoBudgetCommandExecuted()
        {
            LoadDemoBudget();
        }

        private void OnDetailsCommandExecute()
        {
            var popUpRequest = new ShellDialogRequestMessage(BudgetAnalyserFeature.Budget, CurrentBudget, ShellDialogType.Ok);
            MessengerInstance.Send(popUpRequest);
        }

        private void OnExpenseAmountPropertyChanged(object sender, EventArgs propertyChangedEventArgs)
        {
            if (!this.loading)
            {
                // Let the first property change event through, because it is the initial set of the value.
                if (ExpenseTotal != 0)
                {
                    this.dirty = true;
                }
            }

            ExpenseTotal = Expenses.Sum(x => x.Amount);
            Surplus = IncomeTotal - ExpenseTotal;
        }

        private void OnIncomeAmountPropertyChanged(object sender, EventArgs propertyChangedEventArgs)
        {
            if (!this.loading)
            {
                // Let the first property change event through, because it is the initial set of the value.
                if (IncomeTotal != 0)
                {
                    this.dirty = true;
                }
            }

            IncomeTotal = Incomes.Sum(x => x.Amount);
            Surplus = IncomeTotal - ExpenseTotal;
        }

        private void OnLoadBudgetCommandExecute()
        {
            bool valid = ValidateAndSaveIfRequired();
            if (!valid)
            {
                return;
            }

            this.dirty = false;
            string fileName = GetFileNameFromUserForOpen();
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            LoadBudget(fileName);
        }

        private void OnPopUpResponseReceived(ShellDialogResponseMessage message)
        {
            if (!message.IsItForMe(this.dialogCorrelationId))
            {
                return;
            }

            var viewModel = (BudgetSelectionViewModel)message.Content;
            if (viewModel.Selected == null || viewModel.Selected == CurrentBudget.Model)
            {
                return;
            }

            ShowOtherBudget(viewModel.Selected);
        }

        private void OnSaveAsCommandExecute()
        {
            string fileName = GetFileNameFromUserForSave();
            if (fileName == null)
            {
                return;
            }

            this.dirty = true;
            Budgets.FileName = fileName;
            SaveBudgetModel();
            RaisePropertyChanged(() => TruncatedFileName);
        }

        private void OnShowAllCommandExecuted()
        {
            SelectOtherBudget();
        }

        private void OnShowPieCommandExecuted()
        {
            BudgetPieController.Load(CurrentBudget.Model);
        }

        private void ReleaseListBindingEvents(IEnumerable<BudgetItem> items)
        {
            if (items == null)
            {
                return;
            }

            foreach (BudgetItem item in items)
            {
                item.PropertyChanged -= OnIncomeAmountPropertyChanged;
                item.Bucket.PropertyChanged -= OnIncomeAmountPropertyChanged;
                item.PropertyChanged -= OnExpenseAmountPropertyChanged;
                item.Bucket.PropertyChanged -= OnExpenseAmountPropertyChanged;
            }
        }

        private bool SaveBudgetModel()
        {
            var validationMessages = new StringBuilder();
            bool valid = this.maintenanceService.UpdateAndValidateBudget(CurrentBudget.Model, Incomes, Expenses, validationMessages);
            if (!valid)
            {
                this.messageBox.Show(validationMessages.ToString(), "Unable to save, some data is invalid");
                return false;
            }

            if (SaveBudgetCollection())
            {
                this.dirty = false;
                return true;
            }

            this.newBuckets.Clear();
            return false;
        }

        private void SelectOtherBudget()
        {
            this.dialogCorrelationId = Guid.NewGuid();
            var popUpRequest = new ShellDialogRequestMessage(BudgetAnalyserFeature.Budget, new BudgetSelectionViewModel(Budgets), ShellDialogType.Ok)
            {
                CorrelationId = this.dialogCorrelationId,
            };
            MessengerInstance.Send(popUpRequest);
        }

        private void ShowOtherBudget(BudgetModel budgetToShow)
        {
            CurrentBudget = new BudgetCurrencyContext(Budgets, budgetToShow);
            Shown = true;
            this.dirty = false; // Need to reset this because events fire needlessly (in this case) as a result of setting the CurrentBudget.
        }

        private void ValidateAndClose()
        {
            if (CurrentBudget == null)
            {
                // No budget loaded yet
                return;
            }

            if (ValidateAndSaveIfRequired())
            {
                if (CurrentBudget.Model != Budgets.CurrentActiveBudget)
                {
                    // Were viewing a different budget other than the current active budget for today's date.  Reset back to active budget.
                    CurrentBudget = new BudgetCurrencyContext(Budgets, Budgets.CurrentActiveBudget);
                    this.dirty = false;
                }

                MessengerInstance.Send(new BudgetReadyMessage(CurrentBudget, Budgets));
            }
        }

        private bool ValidateAndSaveIfRequired()
        {
            bool valid = true;

            // If no changes made to the budget model data return straight away.
            if (this.dirty)
            {
                bool? decision = this.questionBox.Show("Save changes to the budget?", "Edit Budget");
                if (decision != null && decision == true)
                {
                    // Yes, please save the changes.
                    valid = SaveBudgetModel();
                }
                else
                {
                    // No thanks, discard the changes. To do this, we'll need to revert from file.
                    LoadBudget(Budgets.FileName);
                }
            }

            return valid;
        }
    }
}