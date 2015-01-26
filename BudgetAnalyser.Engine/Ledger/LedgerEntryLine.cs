﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BudgetAnalyser.Engine.Account;
using BudgetAnalyser.Engine.Annotations;
using BudgetAnalyser.Engine.Budget;
using BudgetAnalyser.Engine.Statement;

namespace BudgetAnalyser.Engine.Ledger
{
    /// <summary>
    ///     This represents the horizontal row on the <see cref="LedgerBook" /> that crosses all <see cref="LedgerColumn" />s
    ///     for a date.
    ///     Each <see cref="LedgerEntry" /> must have a reference to an instance of this.
    /// </summary>
    public class LedgerEntryLine : IModelValidate
    {
        public const string MatchedPrefix = "Matched ";
        private static readonly string[] DisallowedChars = { "\\", "{", "}", "[", "]", "^", "=" };
        private readonly ILogger logger;
        private List<BankBalanceAdjustmentTransaction> bankBalanceAdjustments = new List<BankBalanceAdjustmentTransaction>();
        private List<BankBalance> bankBalancesList;
        private List<LedgerEntry> entries = new List<LedgerEntry>();

        /// <summary>
        ///     Constructs a new instance of <see cref="LedgerEntryLine" />.
        ///     Only AutoMapper uses this constructor.  It it easier for AutoMapper configuration. Date and BankBalances are set
        ///     implicitly using the
        ///     private and internal setters.
        /// </summary>
        /// <param name="logger">The diagnostics logger</param>
        internal LedgerEntryLine([NotNull] ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            this.logger = logger;
            IsNew = true;
        }

        /// <summary>
        ///     Constructs a new instance of <see cref="LedgerEntryLine" />.
        ///     Use this constructor for adding a new line when reconciling once a month.
        /// </summary>
        /// <param name="date">The date of the line</param>
        /// <param name="bankBalances">The bank balances for this date.</param>
        /// <param name="logger">The diagnostics logger</param>
        internal LedgerEntryLine(DateTime date, [NotNull] IEnumerable<BankBalance> bankBalances, [NotNull] ILogger logger)
            : this(logger)
        {
            if (bankBalances == null)
            {
                throw new ArgumentNullException("bankBalances");
            }

            Date = date;
            this.bankBalancesList = bankBalances.ToList();
        }

        /// <summary>
        ///     A collection of optional adjustments to the bank balance that can be added during a reconciliation.
        ///     This is to compensate for transactions that may not have been reflected in the bank account at the time of the
        ///     reconciliation.
        ///     Most commonly this is a credit card payment once the user has ascertained how much surplus they have.
        /// </summary>
        public IEnumerable<BankBalanceAdjustmentTransaction> BankBalanceAdjustments
        {
            get { return this.bankBalanceAdjustments; }
            [UsedImplicitly] private set { this.bankBalanceAdjustments = value.ToList(); }
        }

        /// <summary>
        ///     The bank balances of all the bank accounts being tracked by the ledger book.
        /// </summary>
        public IEnumerable<BankBalance> BankBalances
        {
            get { return this.bankBalancesList; }
            [UsedImplicitly] private set { this.bankBalancesList = value.ToList(); }
        }

        /// <summary>
        ///     The total surplus as at the given date.  This is the total surplus across all the bank accounts being tracked by
        ///     the ledger book.
        ///     This is the amount of money left over after funds have been allocated to all budget buckets being tracked by the
        ///     ledger entries.
        /// </summary>
        public decimal CalculatedSurplus
        {
            get { return LedgerBalance - Entries.Sum(e => e.Balance); }
        }

        /// <summary>
        ///     This is the "as-at" date. It is the date of the fixed snapshot in time when this reconciliation line was created.
        ///     It is not editable as it is used to match transactions from the statement.  Changing this date would mean all
        ///     transactions
        ///     now falling outside the date range would need to be removed, thus affected balances.
        /// </summary>
        public DateTime Date { get; internal set; }

        public IEnumerable<LedgerEntry> Entries
        {
            get { return this.entries; }
            [UsedImplicitly] private set { this.entries = value.ToList(); }
        }

        /// <summary>
        ///     A variable to keep track if this is a newly created entry for a new reconciliation as opposed to creation from
        ///     loading from file.
        ///     This variable is intentionally not persisted.
        ///     AutoMapper always sets this to false.
        ///     When a LedgerBook is saved the whole book is reloaded which will set this to false.
        /// </summary>
        internal bool IsNew { get; private set; }

        public decimal LedgerBalance
        {
            get { return TotalBankBalance + TotalBalanceAdjustments; }
        }

        public string Remarks { get; internal set; }

        /// <summary>
        ///     The individual surplus balance in each bank account being tracked by the Legder book.  These will add up to the
        ///     <see cref="CalculatedSurplus" />.
        /// </summary>
        public IEnumerable<BankBalance> SurplusBalances
        {
            get
            {
                var adjustedBalances = BankBalances.Select(b => new BankBalance(b.Account, b.Balance + TotalBankBalanceAdjustmentForAccount(b.Account)));
                var results = Entries.GroupBy(
                    e => e.LedgerColumn.StoredInAccount,
                    (accountType, ledgerEntries) => new BankBalance(accountType, ledgerEntries.Sum(e => e.Balance)));
                return adjustedBalances.Select(a => new BankBalance(a.Account, a.Balance - results.Where(r => r.Account == a.Account).Sum(r => r.Balance)));
            }
        }

        public decimal TotalBalanceAdjustments
        {
            get { return BankBalanceAdjustments.Sum(a => a.Amount); }
        }

        public decimal TotalBankBalance
        {
            get { return this.bankBalancesList.Sum(b => b.Balance); }
        }

        public BankBalanceAdjustmentTransaction BalanceAdjustment(decimal adjustment, string narrative)
        {
            if (!IsNew)
            {
                throw new InvalidOperationException("Cannot adjust existing ledger lines, only newly added lines can be adjusted.");
            }

            if (adjustment == 0)
            {
                throw new ArgumentException("The balance adjustment amount cannot be zero.", "adjustment");
            }

            var newAdjustment = new BankBalanceAdjustmentTransaction { Narrative = narrative, Amount = adjustment };

            this.bankBalanceAdjustments.Add(newAdjustment);
            return newAdjustment;
        }

        public void CancelBalanceAdjustment(Guid transactionId)
        {
            if (!IsNew)
            {
                throw new InvalidOperationException("Cannot adjust existing ledger lines, only newly added lines can be adjusted.");
            }

            var txn = this.bankBalanceAdjustments.FirstOrDefault(t => t.Id == transactionId);
            if (txn != null)
            {
                this.bankBalanceAdjustments.Remove(txn);
            }
        }

        public void UpdateBankBalances(IEnumerable<BankBalance> updatedBankBalances)
        {
            if (!IsNew)
            {
                throw new InvalidOperationException("You cannot update the bank balances for this ledger line.");
            }

            this.bankBalancesList = updatedBankBalances.ToList();
        }

        public bool UpdateRemarks(string remarks)
        {
            if (IsNew)
            {
                Remarks = remarks;
                return true;
            }

            return false;
        }

        public bool Validate([NotNull] StringBuilder validationMessages)
        {
            if (validationMessages == null)
            {
                throw new ArgumentNullException("validationMessages");
            }

            var result = true;

            if (Entries.None())
            {
                validationMessages.AppendFormat(CultureInfo.CurrentCulture, "The Ledger Entry does not contain any entries, either delete it or add entries.");
                result = false;
            }

            foreach (var ledgerEntry in Entries)
            {
                if (!ledgerEntry.Validate())
                {
                    validationMessages.AppendFormat("Ledger Entry with Balance {0:C} is invalid.", ledgerEntry.Balance);
                    result = false;
                }
            }

            return result;
        }

        /// <summary>
        ///     Called by <see cref="LedgerBook.Reconcile" />. It builds the contents of the new ledger line based on budget and
        ///     statement input.
        /// </summary>
        /// <param name="parentLedgerBook">
        ///     The parent Ledger Book.  Used to extract information from previous <see cref="LedgerEntry" />s to construct the
        ///     running
        ///     balance for the entries this line contains. Also used to get the LedgerColumn instance for the new Ledger Entries.
        ///     This is intentionally not necessarily the same as the previous Ledger Entry from last month, to allow the ledger to
        ///     be
        ///     transfered to a different bank account.
        /// </param>
        /// <param name="currentBudget">The current applicable budget</param>
        /// <param name="statement">The current period statement.</param>
        /// <param name="startDateIncl">The date for this ledger line.</param>
        internal void AddNew(
            LedgerBook parentLedgerBook,
            BudgetModel currentBudget,
            StatementModel statement,
            DateTime startDateIncl)
        {
            if (!IsNew)
            {
                throw new InvalidOperationException("Cannot add a new entry to an existing Ledger Line, only new Ledger Lines can have new entries added.");
            }

            var finishDate = Date;
            var filteredStatementTransactions = statement == null
                ? new List<Transaction>()
                : statement.AllTransactions.Where(t => t.Date >= startDateIncl && t.Date <= finishDate).ToList(); // Date filter must be inclusive to be consistent with the rest of the app.
            // For example the expected date range should be something like 20-Jan to 19-Feb.

            var previousLedgerBalances = CompileLedgersAndBalances(parentLedgerBook);

            foreach (var previousLedgerEntry in previousLedgerBalances)
            {
                var ledgerColumn = previousLedgerEntry.LedgerColumn;
                var openingBalance = previousLedgerEntry.Balance;
                var newEntry = new LedgerEntry(true) { Balance = openingBalance, LedgerColumn = ledgerColumn };
                var transactions = IncludeBudgetedAmount(currentBudget, ledgerColumn);
                transactions.AddRange(IncludeStatementTransactions(newEntry, filteredStatementTransactions));
                AutoMatchTransactionsAlreadyInPreviousPeriod(filteredStatementTransactions, previousLedgerEntry, transactions);
                newEntry.SetTransactionsForReconciliation(transactions);

                this.entries.Add(newEntry);
            }
        }

        internal void Unlock()
        {
            IsNew = true;
            foreach (var entry in Entries)
            {
                entry.Unlock();
            }
        }

        private void AutoMatchTransactionsAlreadyInPreviousPeriod(List<Transaction> transactions, LedgerEntry previousLedgerEntry, List<LedgerTransaction> newLedgerTransactions)
        {
            var ledgerAutoMatchTransactions = previousLedgerEntry.Transactions.Where(t => !string.IsNullOrWhiteSpace(t.AutoMatchingReference)).ToList();
            var checkMatchCount = 0;
            foreach (var lastMonthLedgerTransaction in ledgerAutoMatchTransactions)
            {
                this.logger.LogInfo(
                    l =>
                        l.Format(
                            "Ledger Reconciliation - AutoMatching - Found {0} {1} ledger transaction that require matching.",
                            ledgerAutoMatchTransactions.Count(),
                            previousLedgerEntry.LedgerColumn.BudgetBucket.Code));
                var ledgerTxn = lastMonthLedgerTransaction;
                foreach (var matchingStatementTransaction in TransactionsToAutoMatch(transactions, lastMonthLedgerTransaction.AutoMatchingReference))
                {
                    this.logger.LogInfo(l => l.Format("Ledger Reconciliation - AutoMatching - Matched {0} ==> {1}", ledgerTxn, matchingStatementTransaction));
                    ledgerTxn.Id = matchingStatementTransaction.Id;
                    if (!ledgerTxn.AutoMatchingReference.StartsWith(MatchedPrefix, StringComparison.Ordinal))
                    {
                        // There will be two statement transactions but only one ledger transaction to match to.
                        checkMatchCount++;
                        ledgerTxn.AutoMatchingReference = string.Format(CultureInfo.InvariantCulture, "{0}{1}", MatchedPrefix, ledgerTxn.AutoMatchingReference);
                    }
                    var duplicateTransaction = newLedgerTransactions.FirstOrDefault(t => t.Id == matchingStatementTransaction.Id);
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
            }
        }

        private decimal TotalBankBalanceAdjustmentForAccount(AccountType account)
        {
            return BankBalanceAdjustments.Where(a => a.BankAccount == account).Sum(a => a.Amount);
        }

        internal static IEnumerable<Transaction> TransactionsToAutoMatch(IEnumerable<Transaction> transactions, string autoMatchingReference)
        {
            return transactions.Where(
                t =>
                    t.Reference1.TrimEndSafely() == autoMatchingReference
                    || t.Reference2.TrimEndSafely() == autoMatchingReference
                    || t.Reference3.TrimEndSafely() == autoMatchingReference)
                .OrderBy(t => t.Amount);
        }

        private static IEnumerable<LedgerEntry> CompileLedgersAndBalances(LedgerBook parentLedgerBook)
        {
            var ledgersAndBalances = new List<LedgerEntry>();
            var previousLine = parentLedgerBook.DatedEntries.FirstOrDefault();
            if (previousLine == null)
            {
                return parentLedgerBook.Ledgers.Select(ledger => new LedgerEntry { Balance = 0, LedgerColumn = ledger });
            }

            foreach (var ledger in parentLedgerBook.Ledgers)
            {
                // Ledger Columns from a previous are not necessarily equal if the StoredInAccount has changed.
                var previousEntry = previousLine.Entries.FirstOrDefault(e => e.LedgerColumn.BudgetBucket == ledger.BudgetBucket);

                // Its important to use the ledger column value from the book level map, not from the previous entry. The user
                // could have moved the ledger to a different account and so, the ledger column value in the book level map will be different.
                if (previousEntry == null)
                {
                    // Indicates a new ledger column has been added to the book starting this month.
                    ledgersAndBalances.Add(new LedgerEntry { Balance = 0, LedgerColumn = ledger });
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

        private static List<LedgerTransaction> IncludeBudgetedAmount(BudgetModel currentBudget, LedgerColumn ledgerColumn)
        {
            var expenseBudget = currentBudget.Expenses.FirstOrDefault(e => e.Bucket.Code == ledgerColumn.BudgetBucket.Code);
            var transactions = new List<LedgerTransaction>();
            if (expenseBudget != null)
            {
                BudgetCreditLedgerTransaction budgetedAmount;
                if (ledgerColumn.StoredInAccount.IsSalaryAccount)
                {
                    budgetedAmount = new BudgetCreditLedgerTransaction { Amount = expenseBudget.Amount, Narrative = "Budgeted Amount" };
                }
                else
                {
                    budgetedAmount = new BudgetCreditLedgerTransaction
                    {
                        Amount = expenseBudget.Amount,
                        Narrative = "Budget amount must be transferred into this account with a bank transfer, use the reference number for the transfer.",
                        AutoMatchingReference = IssueTransactionReferenceNumber()
                    };
                }

                transactions.Add(budgetedAmount);
            }

            return transactions;
        }

        private static IEnumerable<LedgerTransaction> IncludeStatementTransactions(LedgerEntry newEntry, ICollection<Transaction> filteredStatementTransactions)
        {
            if (filteredStatementTransactions.None())
            {
                return new List<LedgerTransaction>();
            }

            var transactions = filteredStatementTransactions.Where(t => t.BudgetBucket == newEntry.LedgerColumn.BudgetBucket).ToList();
            if (transactions.Any())
            {
                var newLedgerTransactions = transactions.Select<Transaction, LedgerTransaction>(
                    t =>
                    {
                        if (t.Amount < 0)
                        {
                            return new CreditLedgerTransaction(t.Id)
                            {
                                Amount = t.Amount,
                                Narrative = ExtractNarrative(t)
                            };
                        }

                        return new CreditLedgerTransaction(t.Id)
                        {
                            Amount = t.Amount,
                            Narrative = ExtractNarrative(t)
                        };
                    });

                return newLedgerTransactions.ToList();
            }

            return new List<LedgerTransaction>();
        }

        private static string IssueTransactionReferenceNumber()
        {
            var reference = new StringBuilder(Convert.ToBase64String(Guid.NewGuid().ToByteArray()));
            foreach (var disallowedChar in DisallowedChars)
            {
                reference.Replace(disallowedChar, string.Empty);
            }

            return reference.ToString().Substring(0, 7);
        }
    }
}