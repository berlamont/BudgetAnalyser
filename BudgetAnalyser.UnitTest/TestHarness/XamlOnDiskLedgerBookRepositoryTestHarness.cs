﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Xaml;
using BudgetAnalyser.Engine;
using BudgetAnalyser.Engine.Annotations;
using BudgetAnalyser.Engine.Ledger;
using BudgetAnalyser.Engine.Ledger.Data;

namespace BudgetAnalyser.UnitTest.TestHarness
{
    public class XamlOnDiskLedgerBookRepositoryTestHarness : XamlOnDiskLedgerBookRepository
    {
        public XamlOnDiskLedgerBookRepositoryTestHarness(
            [NotNull] BasicMapper<LedgerBookDto, LedgerBook> dataToDomainMapper,
            [NotNull] BasicMapper<LedgerBook, LedgerBookDto> domainToDataMapper
            ) : base(dataToDomainMapper, domainToDataMapper, new FakeLogger())
        {
            LoadXamlFromDiskFromEmbeddedResources = true;
        }

        public Func<string, bool> FileExistsOverride { get; set; }
        public LedgerBookDto LedgerBookDto { get; private set; }

        public Func<string, string> LoadXamlAsStringOverride { get; set; }

        public bool LoadXamlFromDiskFromEmbeddedResources { get; set; }
        public Action<string, string> WriteToDiskOverride { get; set; }

        protected override bool FileExistsOnDisk(string fileName)
        {
            return FileExistsOverride(fileName);
        }

        protected override string LoadXamlAsString(string fileName)
        {
            if (LoadXamlAsStringOverride == null)
            {
                return base.LoadXamlAsString(fileName);
            }

            return LoadXamlAsStringOverride(fileName);
        }

        protected override LedgerBookDto LoadXamlFromDisk(string fileName)
        {
            if (LoadXamlFromDiskFromEmbeddedResources)
            {
                // this line of code is useful to figure out the name Vs has given the resource! The name is case sensitive.
                Assembly.GetExecutingAssembly().GetManifestResourceNames().ToList().ForEach(n => Debug.WriteLine(n));
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(fileName))
                {
                    if (stream == null)
                    {
                        throw new MissingManifestResourceException("Cannot find resource named: " + fileName);
                    }

                    return (LedgerBookDto)XamlServices.Load(new XamlXmlReader(stream));
                }
            }

            LedgerBookDto = base.LoadXamlFromDisk(fileName);
            return LedgerBookDto;
        }

        protected override void WriteToDisk(string filename, string data)
        {
            WriteToDiskOverride(filename, data);
        }
    }
}