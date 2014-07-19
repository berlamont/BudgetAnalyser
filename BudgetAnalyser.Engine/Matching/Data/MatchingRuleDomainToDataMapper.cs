﻿using System;
using BudgetAnalyser.Engine.Annotations;

namespace BudgetAnalyser.Engine.Matching.Data
{
    [AutoRegisterWithIoC(SingleInstance = true, RegisterAs = typeof(BasicMapper<MatchingRule, MatchingRuleDto>))]
    public class MatchingRuleDomainToDataMapper : BasicMapper<MatchingRule, MatchingRuleDto>
    {
        public override MatchingRuleDto Map([NotNull] MatchingRule rule)
        {
            if (rule == null)
            {
                throw new ArgumentNullException("rule");
            }

            return new MatchingRuleDto
            {
                Amount = rule.Amount,
                BucketCode = rule.BucketCode, // Its important to use the BucketCode not the Bucket. See below.
                Created = rule.Created,
                Description = rule.Description,
                LastMatch = rule.LastMatch,
                MatchCount = rule.MatchCount,
                Reference1 = rule.Reference1,
                Reference2 = rule.Reference2,
                Reference3 = rule.Reference3,
                RuleId = rule.RuleId,
                TransactionType = rule.TransactionType,
            };

            // Bucket can be null, while BucketCode will always be the string read from the persistence file.  The currently loaded budget model may not have a bucket
            // that matches that code. So its important to preserve the code.
        }
    }
}