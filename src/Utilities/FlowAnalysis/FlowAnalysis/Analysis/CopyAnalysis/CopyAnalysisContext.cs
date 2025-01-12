﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.CopyAnalysis
{
    using CopyAnalysisResult = DataFlowAnalysisResult<CopyBlockAnalysisResult, CopyAbstractValue>;
    using InterproceduralCopyAnalysisData = InterproceduralAnalysisData<CopyAnalysisData, CopyAnalysisContext, CopyAbstractValue>;
    using ValueContentAnalysisResult = DataFlowAnalysisResult<ValueContentBlockAnalysisResult, ValueContentAbstractValue>;

    /// <summary>
    /// Analysis context for execution of <see cref="CopyAnalysis"/> on a control flow graph.
    /// </summary>
    public sealed class CopyAnalysisContext : AbstractDataFlowAnalysisContext<CopyAnalysisData, CopyAnalysisContext, CopyAnalysisResult, CopyAbstractValue>
    {
        private CopyAnalysisContext(
            AbstractValueDomain<CopyAbstractValue> valueDomain,
            WellKnownTypeProvider wellKnownTypeProvider,
            ControlFlowGraph controlFlowGraph,
            ISymbol owningSymbol,
            AnalyzerOptions analyzerOptions,
            InterproceduralAnalysisConfiguration interproceduralAnalysisConfig,
            bool pessimisticAnalysis,
            bool exceptionPathsAnalysis,
            PointsToAnalysisResult pointsToAnalysisResultOpt,
            Func<CopyAnalysisContext, CopyAnalysisResult> tryGetOrComputeAnalysisResult,
            ControlFlowGraph parentControlFlowGraphOpt,
            InterproceduralCopyAnalysisData interproceduralAnalysisDataOpt,
            InterproceduralAnalysisPredicate interproceduralAnalysisPredicateOpt)
            : base(valueDomain, wellKnownTypeProvider, controlFlowGraph, owningSymbol, analyzerOptions, interproceduralAnalysisConfig, pessimisticAnalysis,
                  predicateAnalysis: true, exceptionPathsAnalysis, copyAnalysisResultOpt: null, pointsToAnalysisResultOpt, valueContentAnalysisResultOpt: null,
                  tryGetOrComputeAnalysisResult, parentControlFlowGraphOpt, interproceduralAnalysisDataOpt, interproceduralAnalysisPredicateOpt)
        {
        }

        internal static CopyAnalysisContext Create(
            AbstractValueDomain<CopyAbstractValue> valueDomain,
            WellKnownTypeProvider wellKnownTypeProvider,
            ControlFlowGraph controlFlowGraph,
            ISymbol owningSymbol,
            AnalyzerOptions analyzerOptions,
            InterproceduralAnalysisConfiguration interproceduralAnalysisConfig,
            bool pessimisticAnalysis,
            bool exceptionPathsAnalysis,
            PointsToAnalysisResult pointsToAnalysisResultOpt,
            Func<CopyAnalysisContext, CopyAnalysisResult> tryGetOrComputeAnalysisResult,
            InterproceduralAnalysisPredicate interproceduralAnalysisPredicateOpt)
        {
            return new CopyAnalysisContext(valueDomain, wellKnownTypeProvider, controlFlowGraph, owningSymbol, analyzerOptions,
                  interproceduralAnalysisConfig, pessimisticAnalysis, exceptionPathsAnalysis, pointsToAnalysisResultOpt, tryGetOrComputeAnalysisResult,
                  parentControlFlowGraphOpt: null, interproceduralAnalysisDataOpt: null, interproceduralAnalysisPredicateOpt);
        }

        public override CopyAnalysisContext ForkForInterproceduralAnalysis(
            IMethodSymbol invokedMethod,
            ControlFlowGraph invokedControlFlowGraph,
            IOperation operation,
            PointsToAnalysisResult pointsToAnalysisResultOpt,
            CopyAnalysisResult copyAnalysisResultOpt,
            ValueContentAnalysisResult valueContentAnalysisResultOpt,
            InterproceduralCopyAnalysisData interproceduralAnalysisData)
        {
            return new CopyAnalysisContext(ValueDomain, WellKnownTypeProvider, invokedControlFlowGraph, invokedMethod, AnalyzerOptions, InterproceduralAnalysisConfiguration,
                PessimisticAnalysis, ExceptionPathsAnalysis, pointsToAnalysisResultOpt, TryGetOrComputeAnalysisResult, ControlFlowGraph, interproceduralAnalysisData,
                InterproceduralAnalysisPredicateOpt);
        }

        protected override void ComputeHashCodePartsSpecific(Action<int> addPart)
        {
        }
    }
}
