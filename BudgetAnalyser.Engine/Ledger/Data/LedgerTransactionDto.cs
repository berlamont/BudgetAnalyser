﻿using System;

namespace BudgetAnalyser.Engine.Ledger.Data
{
    /// <summary>
    /// A Dto for all subclasses of <see cref="LedgerTransaction"/>.  All subclasses are flattened into this type.
    /// </summary>
    public class LedgerTransactionDto
    {
        public string AccountType { get; set; }

        public decimal Credit { get; set; }

        public decimal Debit { get; set; }
        public Guid Id { get; set; }

        public string Narrative { get; set; }

        public string TransactionType { get; set; }
    }
}