﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BudgetAnalyser.Engine.BankAccount;
using BudgetAnalyser.Engine.Annotations;
using BudgetAnalyser.Engine.Budget;
using BudgetAnalyser.Engine.Statement;

namespace BudgetAnalyser.Engine.Ledger
{
    [AutoRegisterWithIoC(SingleInstance = true)]
    internal class ReconciliationBuilder : IReconciliationBuilder
    {
        internal const string MatchedPrefix = "Matched ";
        private static readonly string[] DisallowedChars = { "\\", "{", "}", "[", "]", "^", "=", "/", ";", ".", ",", "-", "+" };
        private readonly ILogger logger;
        private LedgerEntryLine newReconciliationLine;

        public ReconciliationBuilder([NotNull] ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            this.logger = logger;
        }

        public LedgerBook LedgerBook { get; set; }

        public LedgerEntryLine CreateNewMonthlyReconciliation(
            DateTime reconciliationDateExcl,
            IEnumerable<BankBalance> bankBalances,
            BudgetModel budget,
            StatementModel statement,
            ToDoCollection toDoList)
        {
            if (bankBalances == null)
            {
                throw new ArgumentNullException(nameof(bankBalances));
            }

            if (budget == null)
            {
                throw new ArgumentNullException(nameof(budget));
            }

            if (statement == null)
            {
                throw new ArgumentNullException(nameof(statement));
            }

            if (toDoList == null)
            {
                throw new ArgumentNullException(nameof(toDoList));
            }

            if (LedgerBook == null)
            {
                throw new ArgumentException("The Ledger Book property cannot be null. You must set this prior to calling this method.");
            }

            try
            {
                this.newReconciliationLine = new LedgerEntryLine(reconciliationDateExcl, bankBalances);
                AddNew(budget, statement, CalculateDateForReconcile(LedgerBook, reconciliationDateExcl), toDoList);

                CreateToDoForAnyOverdrawnSurplusBalance(toDoList);

                return this.newReconciliationLine;
            }
            finally
            {
                this.newReconciliationLine = null;
            }
        }

        /// <summary>
        ///     When creating a new reconciliation a start date is required to be able to search a statement for transactions
        ///     between the start date and the reconciliation date specified (today or pay day). The start date should start from
        ///     the previous ledger entry line or
        ///     one month prior if no records exist.
        /// </summary>
        /// <param name="ledgerBook">The Ledger Book to find the date for</param>
        /// <param name="reconciliationDate">The chosen reconciliation date from the user</param>
        internal static DateTime CalculateDateForReconcile(LedgerBook ledgerBook, DateTime reconciliationDate)
        {
            if (ledgerBook.Reconciliations.Any())
            {
                return ledgerBook.Reconciliations.First().Date;
            }

            DateTime startDateIncl = reconciliationDate.AddMonths(-1);
            return startDateIncl;
        }

        internal static IEnumerable<Transaction> TransactionsToAutoMatch(IEnumerable<Transaction> transactions, string autoMatchingReference)
        {
            IOrderedEnumerable<Transaction> txns = transactions.Where(
                t =>
                    t.Reference1.TrimEndSafely() == autoMatchingReference
                    || t.Reference2.TrimEndSafely() == autoMatchingReference
                    || t.Reference3.TrimEndSafely() == autoMatchingReference)
                .OrderBy(t => t.Amount);
            return txns;
        }

        private static IEnumerable<LedgerEntry> CompileLedgersAndBalances(LedgerBook parentLedgerBook)
        {
            var ledgersAndBalances = new List<LedgerEntry>();
            LedgerEntryLine previousLine = parentLedgerBook.Reconciliations.FirstOrDefault();
            if (previousLine == null)
            {
                return parentLedgerBook.Ledgers.Select(ledger => new LedgerEntry { Balance = 0, LedgerBucket = ledger });
            }

            foreach (LedgerBucket ledger in parentLedgerBook.Ledgers)
            {
                // Ledger Columns from a previous are not necessarily equal if the StoredInAccount has changed.
                LedgerEntry previousEntry = previousLine.Entries.FirstOrDefault(e => e.LedgerBucket.BudgetBucket == ledger.BudgetBucket);

                // Its important to use the ledger column value from the book level map, not from the previous entry. The user
                // could have moved the ledger to a different account and so, the ledger column value in the book level map will be different.
                if (previousEntry == null)
                {
                    // Indicates a new ledger column has been added to the book starting this month.
                    ledgersAndBalances.Add(new LedgerEntry { Balance = 0, LedgerBucket = ledger });
                }
                else
                {
                    ledgersAndBalances.Add(previousEntry);
                }
            }

            return ledgersAndBalances;
        }

        private static string ExtractNarrative(Transaction t)
        {
            if (!string.IsNullOrWhiteSpace(t.Description))
            {
                return t.Description;
            }

            if (t.TransactionType != null)
            {
                return t.TransactionType.ToString();
            }

            return string.Empty;
        }

        private static IEnumerable<LedgerTransaction> IncludeStatementTransactions(LedgerEntry newEntry, ICollection<Transaction> filteredStatementTransactions)
        {
            if (filteredStatementTransactions.None())
            {
                return new List<LedgerTransaction>();
            }

            List<Transaction> transactions = filteredStatementTransactions.Where(t => t.BudgetBucket == newEntry.LedgerBucket.BudgetBucket).ToList();
            if (transactions.Any())
            {
                IEnumerable<LedgerTransaction> newLedgerTransactions = transactions.Select<Transaction, LedgerTransaction>(
                    t =>
                    {
                        if (t.Amount < 0)
                        {
                            return new CreditLedgerTransaction(t.Id)
                            {
                                Amount = t.Amount,
                                Narrative = ExtractNarrative(t),
                                Date = t.Date
                            };
                        }

                        return new CreditLedgerTransaction(t.Id)
                        {
                            Amount = t.Amount,
                            Narrative = ExtractNarrative(t),
                            Date = t.Date
                        };
                    });

                return newLedgerTransactions.ToList();
            }

            return new List<LedgerTransaction>();
        }

        private static string IssueTransactionReferenceNumber()
        {
            var reference = new StringBuilder();
            do
            {
                reference.Append(Convert.ToBase64String(Guid.NewGuid().ToByteArray()));
                foreach (string disallowedChar in DisallowedChars)
                {
                    reference.Replace(disallowedChar, string.Empty);
                }
            } while (reference.Length < 8);
            return reference.ToString().Substring(0, 7);
        }

        private void AddBalanceAdjustmentsForFutureTransactions(StatementModel statement, DateTime reconciliationDate, ToDoCollection toDoList)
        {
            var adjustmentsMade = false;
            foreach (Transaction futureTransaction in statement.AllTransactions
                .Where(t => t.Account.AccountType != AccountType.CreditCard && t.Date >= reconciliationDate && !(t.BudgetBucket is JournalBucket)))
            {
                adjustmentsMade = true;
                this.newReconciliationLine.BalanceAdjustment(-futureTransaction.Amount, "Remove future transaction for " + futureTransaction.Date.ToShortDateString())
                    .WithAccount(futureTransaction.Account);
            }

            if (adjustmentsMade)
            {
                toDoList.Add(new ToDoTask("Check auto-generated balance adjustments for future transactions.", true));
            }
        }

        /// <summary>
        ///     This is effectively stage 2 of the Reconciliation process.
        ///     Called by <see cref="ReconciliationBuilder.CreateNewMonthlyReconciliation" />. It builds the contents of the new
        ///     ledger line based on budget and
        ///     statement input.
        /// </summary>
        /// <param name="budget">The current applicable budget</param>
        /// <param name="statement">The current period statement.</param>
        /// <param name="startDateIncl">
        ///     The date of the previous ledger line. This is used to include transactions from the
        ///     Statement starting from this date and including this date.
        /// </param>
        /// <param name="toDoList">
        ///     The task list that will have tasks added to it to remind the user to perform transfers and
        ///     payments etc.
        /// </param>
        private void AddNew(
            BudgetModel budget,
            StatementModel statement,
            DateTime startDateIncl,
            ToDoCollection toDoList)
        {
            if (!this.newReconciliationLine.IsNew)
            {
                throw new InvalidOperationException("Cannot add a new entry to an existing Ledger Line, only new Ledger Lines can have new entries added.");
            }

            DateTime reconciliationDate = this.newReconciliationLine.Date;
            // Date filter must include the start date, which goes back to and includes the previous ledger date up to the date of this ledger line, but excludes this ledger date.
            // For example if this is a reconciliation for the 20/Feb then the start date is 20/Jan and the finish date is 20/Feb. So transactions pulled from statement are between
            // 20/Jan (inclusive) and 19/Feb (inclusive).
            List<Transaction> filteredStatementTransactions = statement?.AllTransactions.Where(t => t.Date >= startDateIncl && t.Date < reconciliationDate).ToList()
                                                              ?? new List<Transaction>();

            IEnumerable<LedgerEntry> previousLedgerBalances = CompileLedgersAndBalances(LedgerBook);

            var entries = new List<LedgerEntry>();
            foreach (LedgerEntry previousLedgerEntry in previousLedgerBalances)
            {
                LedgerBucket ledgerBucket;
                decimal openingBalance = previousLedgerEntry.Balance;
                LedgerBucket bookLedgerDefaults = LedgerBook.Ledgers.Single(l => l.BudgetBucket == previousLedgerEntry.LedgerBucket.BudgetBucket);
                if (previousLedgerEntry.LedgerBucket.StoredInAccount != bookLedgerDefaults.StoredInAccount)
                {
                    // Check to see if a ledger has been moved into a new default account since last reconciliation.
                    ledgerBucket = bookLedgerDefaults;
                }
                else
                {
                    ledgerBucket = previousLedgerEntry.LedgerBucket;
                }

                var newEntry = new LedgerEntry(true) { Balance = openingBalance, LedgerBucket = ledgerBucket };
                List<LedgerTransaction> transactions = IncludeBudgetedAmount(budget, ledgerBucket, reconciliationDate, toDoList);
                transactions.AddRange(IncludeStatementTransactions(newEntry, filteredStatementTransactions));
                AutoMatchTransactionsAlreadyInPreviousPeriod(filteredStatementTransactions, previousLedgerEntry, transactions, toDoList);
                newEntry.SetTransactionsForReconciliation(transactions, reconciliationDate);

                entries.Add(newEntry);
            }

            this.newReconciliationLine.SetNewLedgerEntries(entries);

            CreateBalanceAdjustmentTasksIfRequired(toDoList);
            if (statement != null)
            {
                AddBalanceAdjustmentsForFutureTransactions(statement, reconciliationDate, toDoList);
            }

            CreateTasksToTransferFundsIfPaidFromDifferentAccount(filteredStatementTransactions, toDoList);
        }

        /// <summary>
        ///     Match statement transaction with special automatching references to Ledger transactions.
        /// </summary>
        private void AutoMatchTransactionsAlreadyInPreviousPeriod(
            List<Transaction> transactions,
            LedgerEntry previousLedgerEntry,
            List<LedgerTransaction> newLedgerTransactions,
            ToDoCollection toDoList)
        {
            List<LedgerTransaction> ledgerAutoMatchTransactions = previousLedgerEntry.Transactions.Where(t => !string.IsNullOrWhiteSpace(t.AutoMatchingReference)).ToList();
            var checkMatchedTxns = new List<LedgerTransaction>();
            var checkMatchCount = 0;
            foreach (LedgerTransaction lastMonthLedgerTransaction in ledgerAutoMatchTransactions)
            {
                this.logger.LogInfo(
                    l =>
                        l.Format(
                            "Ledger Reconciliation - AutoMatching - Found {0} {1} ledger transaction that require matching.",
                            ledgerAutoMatchTransactions.Count(),
                            previousLedgerEntry.LedgerBucket.BudgetBucket.Code));

                LedgerTransaction ledgerTxn = lastMonthLedgerTransaction;
                foreach (Transaction matchingStatementTransaction in TransactionsToAutoMatch(transactions, lastMonthLedgerTransaction.AutoMatchingReference))
                {
                    this.logger.LogInfo(l => l.Format("Ledger Reconciliation - AutoMatching - Matched {0} ==> {1}", ledgerTxn, matchingStatementTransaction));
                    ledgerTxn.Id = matchingStatementTransaction.Id;
                    if (!ledgerTxn.AutoMatchingReference.StartsWith(MatchedPrefix, StringComparison.Ordinal))
                    {
                        // There will be two statement transactions but only one ledger transaction to match to.
                        checkMatchCount++;
                        ledgerTxn.AutoMatchingReference = string.Format(CultureInfo.InvariantCulture, "{0}{1}", MatchedPrefix, ledgerTxn.AutoMatchingReference);
                        checkMatchedTxns.Add(ledgerTxn);
                    }

                    LedgerTransaction duplicateTransaction = newLedgerTransactions.FirstOrDefault(t => t.Id == matchingStatementTransaction.Id);
                    if (duplicateTransaction != null)
                    {
                        this.logger.LogInfo(l => l.Format("Ledger Reconciliation - Removing Duplicate Ledger transaction after auto-matching: {0}", duplicateTransaction));
                        newLedgerTransactions.Remove(duplicateTransaction);
                    }
                }
            }

            if (ledgerAutoMatchTransactions.Any() && ledgerAutoMatchTransactions.Count() != checkMatchCount)
            {
                this.logger.LogWarning(
                    l =>
                        l.Format(
                            "Ledger Reconciliation - WARNING {0} ledger transactions appear to be waiting to be automatched, but not statement transactions were found. {1}",
                            ledgerAutoMatchTransactions.Count(),
                            ledgerAutoMatchTransactions.First().AutoMatchingReference));
                IEnumerable<LedgerTransaction> unmatchedTxns = ledgerAutoMatchTransactions.Except(checkMatchedTxns);
                foreach (LedgerTransaction txn in unmatchedTxns)
                {
                    toDoList.Add(
                        new ToDoTask(
                            string.Format(
                                CultureInfo.CurrentCulture,
                                "WARNING: Missing auto-match transaction. Transfer {0:C} with reference {1} Dated {2:d} to {3}. See log for more details.",
                                txn.Amount,
                                txn.AutoMatchingReference,
                                this.newReconciliationLine.Date.AddDays(-1),
                                previousLedgerEntry.LedgerBucket.StoredInAccount),
                            true));
                }
            }
        }

        private void CreateBalanceAdjustmentTasksIfRequired(ToDoCollection toDoList)
        {
            List<TransferTask> transferTasks = toDoList.OfType<TransferTask>().ToList();
            foreach (IGrouping<BankAccount.Account, TransferTask> grouping in transferTasks.GroupBy(t => t.SourceAccount, tasks => tasks))
            {
                // Rather than create a task, just do it
                this.newReconciliationLine.BalanceAdjustment(
                    -grouping.Sum(t => t.Amount),
                    "Adjustment for moving budgeted amounts from income account. ")
                    .WithAccount(grouping.Key);
            }

            foreach (IGrouping<BankAccount.Account, TransferTask> grouping in transferTasks.GroupBy(t => t.DestinationAccount, tasks => tasks))
            {
                // Rather than create a task, just do it
                this.newReconciliationLine.BalanceAdjustment(
                    grouping.Sum(t => t.Amount),
                    "Adjustment for moving budgeted amounts to destination account. ")
                    .WithAccount(grouping.Key);
            }
        }

        private void CreateTasksToTransferFundsIfPaidFromDifferentAccount(IEnumerable<Transaction> transactions, ToDoCollection toDoList)
        {
            var syncRoot = new object();
            Dictionary<BudgetBucket, BankAccount.Account> ledgerBuckets = this.newReconciliationLine.Entries.Select(e => e.LedgerBucket)
                .Distinct()
                .ToDictionary(l => l.BudgetBucket, l => l.StoredInAccount);

            var debitAccountTransactionsOnly = transactions.Where(t => t.Account.AccountType != AccountType.CreditCard).ToList();

            // Amount < 0: This is because we are only interested in looking for debit transactions against a different account. These transactions will need to be journaled from the stored-in account.
            var proposedTasks = new List<Tuple<Transaction, TransferTask>>();
            Parallel.ForEach(
                debitAccountTransactionsOnly.Where(t => t.Amount < 0).ToList(),
                t =>
                {
                    if (!ledgerBuckets.ContainsKey(t.BudgetBucket))
                    {
                        return;
                    }
                    BankAccount.Account ledgerAccount = ledgerBuckets[t.BudgetBucket];
                    if (t.Account != ledgerAccount)
                    {
                        var reference = IssueTransactionReferenceNumber();
                        lock (syncRoot)
                        {
                            proposedTasks.Add(
                                Tuple.Create(
                                    t,
                                    new TransferTask(
                                        $"A {t.BudgetBucket.Code} payment for {t.Amount:C} on the {t.Date:d} has been made from {t.Account}, but funds are stored in {ledgerAccount}. Use reference {reference}",
                                        true)
                                    {
                                        Amount = -t.Amount,
                                        SourceAccount = ledgerAccount,
                                        DestinationAccount = t.Account,
                                        BucketCode = t.BudgetBucket.Code,
                                        Reference = reference
                                    }));
                        }
                    }
                });
            // Now check to ensure the detected transactions themselves are not one side of a journal style transfer.
            foreach (Tuple<Transaction, TransferTask> tuple in proposedTasks)
            {
                var suspectedPaymentTransaction = tuple.Item1;
                var transferTask = tuple.Item2;
                Transaction matchingTransferTransaction = debitAccountTransactionsOnly.FirstOrDefault(
                    t => t.Amount == -suspectedPaymentTransaction.Amount 
                        && t.Date == suspectedPaymentTransaction.Date 
                        && t.BudgetBucket == suspectedPaymentTransaction.BudgetBucket
                        && t.Account != suspectedPaymentTransaction.Account 
                        && t.Reference1 == suspectedPaymentTransaction.Reference1);
                if (matchingTransferTransaction == null)
                {
                    // No matching transaction exists - therefore the transaction is a payment.
                    toDoList.Add(transferTask);
                }
            }
        }

        /// <summary>
        ///     An overdrawn surplus balance is not valid, and indicates that one or more ledger buckets have been overdrawn.  A
        ///     transfer probably needs to be manually done by the user.
        /// </summary>
        private void CreateToDoForAnyOverdrawnSurplusBalance(ToDoCollection toDoList)
        {
            foreach (BankBalance surplusBalance in this.newReconciliationLine.SurplusBalances.Where(s => s.Balance < 0))
            {
                toDoList.Add(
                    new ToDoTask(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            "{0} has a negative surplus balance {1}, there must be one or more transfers to action.",
                            surplusBalance.Account,
                            surplusBalance.Balance),
                        true));
            }
        }

        private List<LedgerTransaction> IncludeBudgetedAmount(BudgetModel currentBudget, LedgerBucket ledgerBucket, DateTime reconciliationDate, ToDoCollection toDoList)
        {
            Expense budgetedExpense = currentBudget.Expenses.FirstOrDefault(e => e.Bucket.Code == ledgerBucket.BudgetBucket.Code);
            var transactions = new List<LedgerTransaction>();
            if (budgetedExpense != null)
            {
                BudgetCreditLedgerTransaction budgetedAmount;
                if (ledgerBucket.StoredInAccount.IsSalaryAccount)
                {
                    budgetedAmount = new BudgetCreditLedgerTransaction
                    {
                        Amount = budgetedExpense.Bucket.Active ? budgetedExpense.Amount : 0,
                        Narrative = budgetedExpense.Bucket.Active ? "Budgeted Amount" : "Warning! Bucket has been disabled."
                    };
                }
                else
                {
                    budgetedAmount = new BudgetCreditLedgerTransaction
                    {
                        Amount = budgetedExpense.Bucket.Active ? budgetedExpense.Amount : 0,
                        Narrative =
                            budgetedExpense.Bucket.Active
                                ? "Budget amount must be transferred into this account with a bank transfer, use the reference number for the transfer."
                                : "Warning! Bucket has been disabled.",
                        AutoMatchingReference = IssueTransactionReferenceNumber()
                    };
                    // TODO Maybe the budget should know which account the incomes go into, perhaps mapped against each income?
                    BankAccount.Account salaryAccount = this.newReconciliationLine.BankBalances.Single(b => b.Account.IsSalaryAccount).Account;
                    toDoList.Add(
                        new TransferTask(
                            string.Format(
                                CultureInfo.CurrentCulture,
                                "Budgeted Amount for {0} transfer {1:C} from Salary Account to {2} with auto-matching reference: {3}",
                                budgetedExpense.Bucket.Code,
                                budgetedAmount.Amount,
                                ledgerBucket.StoredInAccount,
                                budgetedAmount.AutoMatchingReference),
                            true)
                        {
                            Amount = budgetedAmount.Amount,
                            SourceAccount = salaryAccount,
                            DestinationAccount = ledgerBucket.StoredInAccount,
                            BucketCode = budgetedExpense.Bucket.Code,
                            Reference = budgetedAmount.AutoMatchingReference
                        });
                }

                budgetedAmount.Date = reconciliationDate;
                transactions.Add(budgetedAmount);
            }

            return transactions;
        }
    }
}