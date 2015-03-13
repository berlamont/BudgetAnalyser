﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BudgetAnalyser.Engine.Account;
using BudgetAnalyser.Engine.Annotations;

namespace BudgetAnalyser.Engine.Statement
{
    /// <summary>
    ///     A <see cref="IBankStatementImporter" /> repository based on a list of dependency injected importers.
    ///     These are the bank specific file formats exported from online banking to be imported into the Budget Analyser.
    /// </summary>
    [AutoRegisterWithIoC(SingleInstance = true)]
    public class BankStatementImporterRepository : IBankStatementImporterRepository
    {
        private readonly IEnumerable<IBankStatementImporter> importers;

        public BankStatementImporterRepository([NotNull] IEnumerable<IBankStatementImporter> importers)
        {
            if (importers == null || importers.None())
            {
                throw new ArgumentNullException("importers");
            }

            this.importers = importers.ToList();
        }

        /// <summary>
        ///     Can any importer in this repository read and import the given file.
        ///     Calling this method will open the file and read some of its contents.
        /// </summary>
        /// <returns>True if this file can be imported one of the importers in the repository.</returns>
        public async Task<bool> CanImportAsync(string fullFileName)
        {
            foreach (var importer in this.importers)
            {
                if (await importer.TasteTestAsync(fullFileName))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Import the given file.  It is recommended to call <see cref="IBankStatementImporterRepository.CanImportAsync" /> first.
        ///     If the file cannot
        ///     be imported by any of this repositories importers a <see cref="NotSupportedException" /> will be thrown.
        /// </summary>
        public async Task<StatementModel> ImportAsync(string fullFileName, AccountType accountType)
        {
            foreach (IBankStatementImporter importer in this.importers)
            {
                if (await importer.TasteTestAsync(fullFileName))
                {
                    return importer.Load(fullFileName, accountType);
                }
            }

            throw new NotSupportedException("The requested file name cannot be loaded. It is not of any known format.");
        }
    }
}