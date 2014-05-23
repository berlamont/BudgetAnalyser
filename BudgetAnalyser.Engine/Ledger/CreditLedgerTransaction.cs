﻿using System;

namespace BudgetAnalyser.Engine.Ledger
{
    public class CreditLedgerTransaction : LedgerTransaction
    {
        public CreditLedgerTransaction()
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="CreditLedgerTransaction"/>.
        /// Called using reflection during deserialisation.
        /// </summary>
        public CreditLedgerTransaction(Guid id)
            : base(id)
        {
        }

        public override LedgerTransaction WithAmount(decimal amount)
        {
            base.WithAmount(amount);
            Credit = amount;
            Debit = 0;
            return this;
        }

        public override LedgerTransaction WithReversal(decimal amount)
        {
            base.WithReversal(amount);
            Credit = -amount;
            Debit = 0;
            return this;
        }
    }
}