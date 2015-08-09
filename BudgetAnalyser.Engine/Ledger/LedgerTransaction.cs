﻿using System;
using System.Globalization;
using BudgetAnalyser.Engine.Annotations;
using BudgetAnalyser.Engine.Statement;

namespace BudgetAnalyser.Engine.Ledger
{
    public abstract class LedgerTransaction
    {
        protected LedgerTransaction()
        {
            Id = Guid.NewGuid();
        }

        protected LedgerTransaction(Guid id)
        {
            Id = id;
        }

        /// <summary>
        ///     Gets the amount.
        ///     Credits are positive and debits are negative.
        /// </summary>
        public decimal Amount { get; internal set; }

        /// <summary>
        ///     Gets or sets the automatic matching reference.
        ///     This is allocated by the system so that when a real transactions is performed this reference number can be entered
        ///     against the transaction.
        ///     When the next reconciliation is done the real transaction will be matched using this reference number.
        /// </summary>
        /// <value>
        ///     The automatic matching reference.
        /// </value>
        public string AutoMatchingReference { get; internal set; }

        /// <summary>
        ///     Gets or sets the transaction date.
        /// </summary>
        public DateTime? Date { get; internal set; }

        /// <summary>
        ///     Gets or sets the Transaction ID. This is the same ID as the <see cref="StatementModel" />'s
        ///     <see cref="Transaction" />.
        ///     This can be used to link back to the statement and show more transaction specific data.
        /// </summary>
        public Guid Id { get; internal set; }

        public string Narrative { get; internal set; }

        /// <summary>
        ///     Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        ///     A string that represents the current object.
        /// </returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} ({1:N} {2} {3} {4})", GetType().Name, Amount, Narrative, AutoMatchingReference, Id);
        }

        internal virtual LedgerTransaction WithAmount(decimal amount)
        {
            Amount = amount;
            return this;
        }

        internal virtual LedgerTransaction WithNarrative([NotNull] string narrative)
        {
            if (narrative == null)
            {
                throw new ArgumentNullException("narrative");
            }

            Narrative = narrative;
            return this;
        }
    }
}