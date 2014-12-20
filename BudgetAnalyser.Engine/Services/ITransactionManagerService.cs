﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BudgetAnalyser.Engine.Account;
using BudgetAnalyser.Engine.Annotations;
using BudgetAnalyser.Engine.Budget;
using BudgetAnalyser.Engine.Statement;

namespace BudgetAnalyser.Engine.Services
{
    /// <summary>
    ///     An interface for managing, viewing, and storing transactions
    /// </summary>
    public interface ITransactionManagerService : IServiceFoundation
    {
        /// <summary>
        ///     Detects duplicate transactions in the current <see cref="StatementModel" /> and returns a summary string for
        ///     displaying in the UI.
        ///     Each individual duplicate transactions will be flagged by the <see cref="Transaction.IsSuspectedDuplicate" />
        ///     property.
        /// </summary>
        /// <returns>A textual summary of duplicates found. Null if none are detected or no statement is loaded.</returns>
        string DetectDuplicateTransactions();

        /// <summary>
        ///     Provides a list of buckets for display purposes for filtering the transactions shown.
        ///     This list will include a blank item to represent no filtering, and a [Uncategorised] to represent a filter to show
        ///     only transactions
        ///     with no bucket allocation.
        /// </summary>
        /// <returns>A string list of bucket codes.</returns>
        IEnumerable<string> FilterableBuckets();

        /// <summary>
        ///     Filters the transactions using the filter object provided.
        /// </summary>
        void FilterTransactions([NotNull] GlobalFilterCriteria criteria);

        /// <summary>
        ///     Filters the transactions using the search text provided against any text field in <see cref="Transaction" />.
        /// </summary>
        /// <param name="searchText">The search text.</param>
        void FilterTransactions([NotNull] string searchText);

        /// <summary>
        ///     Imports a bank's transaction extract and merges it with the currently loaded Budget Analyser Statement.
        ///     It is recommended to follow this up with <see cref="ValidateWithCurrentBudgetsAsync" />.
        /// </summary>
        /// <exception cref="NotSupportedException">Will be thrown if the format of the bank extract is not supported.</exception>
        /// <exception cref="KeyNotFoundException">
        ///     Will be thrown if the bank extract cannot be located using the given
        ///     <paramref name="storageKey" />
        /// </exception>
        void ImportAndMergeBankStatement(
            [NotNull] string storageKey,
            [NotNull] AccountType account);

        /// <summary>
        ///     Parses and loads the persisted state data from the provided object.
        /// </summary>
        /// <param name="stateData">The state data loaded from persistent storage.</param>
        StatementApplicationState LoadPersistedStateData(object stateData);

        /// <summary>
        ///     Loads an existing Budget Analyser <see cref="StatementModel" />.
        /// </summary>
        /// <param name="storageKey">Pass a known storage key (database identifier or filename) to load.</param>
        /// <exception cref="NotSupportedException">Will be thrown if the format of the bank extract is not supported.</exception>
        /// <exception cref="KeyNotFoundException">
        ///     Will be thrown if the bank extract cannot be located using the given <paramref name="storageKey" />
        /// </exception>
        /// <exception cref="StatementModelChecksumException">
        ///     Will be thrown if the statement model's internal checksum detects corrupt data indicating tampering.
        /// </exception>
        /// <exception cref="DataFormatException">
        ///     Will be thrown if the format of the bank extract contains unexpected data
        ///     indicating it is corrupt or an old unsupported file.
        /// </exception>
        Task<StatementModel> LoadStatementModelAsync([NotNull] string storageKey);

        /// <summary>
        ///     Populates a collection grouped by bucket with date sorted transactions contained in each group.
        /// </summary>
        /// <param name="groupByBucket">
        ///     True if the UI is currently showing the transactions grouped by bucket, false if not.
        /// </param>
        IEnumerable<TransactionGroupedByBucket> PopulateGroupByBucketCollection(bool groupByBucket);

        /// <summary>
        ///     Prepares the persistent state data to save to storage.
        /// </summary>
        object PreparePersistentStateData();

        /// <summary>
        ///     Removes the provided transaction from the currently loaded Budget Analyser Statement.
        /// </summary>
        /// <param name="transactionToRemove">The transaction to remove.</param>
        void RemoveTransaction([NotNull] Transaction transactionToRemove);

        /// <summary>
        ///     Save the currently loaded <see cref="StatementModel" /> into persistent storage.
        ///     (Saving and preserving bank statement files is not supported.)
        /// </summary>
        /// <param name="close">If true, will close the currently loaded Budget Analyser statement, false to keep it open.</param>
        Task SaveAsync(bool close);

        /// <summary>
        ///     Splits the provided transaction into two. The provided transactions is removed, and two new transactions are
        ///     created.
        ///     Both transactions must add up to the existing transaction amount.
        /// </summary>
        /// <param name="originalTransaction">The original transaction.</param>
        /// <param name="splinterAmount1">The splinter amount1.</param>
        /// <param name="splinterAmount2">The splinter amount2.</param>
        /// <param name="splinterBucket1">The splinter bucket1.</param>
        /// <param name="splinterBucket2">The splinter bucket2.</param>
        void SplitTransaction(
            [NotNull] Transaction originalTransaction,
            decimal splinterAmount1,
            decimal splinterAmount2,
            [NotNull] BudgetBucket splinterBucket1,
            [NotNull] BudgetBucket splinterBucket2);

        /// <summary>
        ///     Validates the currently loaded <see cref="StatementModel" /> against the provided budgets and ensures all buckets
        ///     used by the transactions
        ///     exist in the budgets.  This is performed asynchronously.
        ///     This method can be called when a budget is loaded or changed or when a new Budget Analyser Statement is loaded.
        /// </summary>
        /// <param name="budgets">
        ///     The current budgets. This must be provided at least once. It can be omitted when
        ///     calling this method after the statement model has changed if the budget was previously provided.
        /// </param>
        /// <returns>
        ///     A task that will result in true if all buckets used, are present in the budgets, otherwise false.
        ///     If false, this indicates that some transactions may have their bucket allocation removed possibly resulting in
        ///     unintended data loss.
        /// </returns>
        Task<bool> ValidateWithCurrentBudgetsAsync(BudgetCollection budgets = null);
    }
}