// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    /// <summary>
    /// Analyzer for System.AppContext.SetSwitch invocations.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotSetSwitch : DiagnosticAnalyzer
    {
        internal static DiagnosticDescriptor DoNotDisableSchUseStrongCryptoRule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5361",
            typeof(MicrosoftNetCoreAnalyzersResources),
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotDisableSchUseStrongCrypto),
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotDisableSchUseStrongCryptoMessage),
            DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
            helpLinkUri: "https://docs.microsoft.com/visualstudio/code-quality/ca5361",
            descriptionResourceStringName: nameof(MicrosoftNetCoreAnalyzersResources.DoNotDisableSchUseStrongCryptoDescription),
            customTags: WellKnownDiagnosticTagsExtensions.DataflowAndTelemetry);
        internal static DiagnosticDescriptor DoNotDisableSpmSecurityProtocolsRule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5378",
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotDisableUsingServicePointManagerSecurityProtocolsTitle),
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotDisableUsingServicePointManagerSecurityProtocolsMessage),
            DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
            helpLinkUri: "https://docs.microsoft.com/visualstudio/code-quality/ca5378",
            customTags: WellKnownDiagnosticTagsExtensions.DataflowAndTelemetry);

        internal static ImmutableDictionary<string, (bool BadValue, DiagnosticDescriptor Rule)> BadSwitches =
            ImmutableDictionary.CreateRange(
                StringComparer.Ordinal,
                new[] {
                   ("Switch.System.Net.DontEnableSchUseStrongCrypto",
                        (true, DoNotDisableSchUseStrongCryptoRule)),
                   ("Switch.System.ServiceModel.DisableUsingServicePointManagerSecurityProtocols",
                        (true, DoNotDisableSpmSecurityProtocolsRule)),
                }.Select(
                    (o) => new KeyValuePair<string, (bool, DiagnosticDescriptor)>(o.Item1, o.Item2)));

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            DoNotDisableSchUseStrongCryptoRule,
            DoNotDisableSpmSecurityProtocolsRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
            {
                var compilation = compilationStartAnalysisContext.Compilation;
                var appContextTypeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemAppContext);

                if (appContextTypeSymbol == null)
                {
                    return;
                }

                var setSwitchMemberWithStringAndBoolParameter =
                    appContextTypeSymbol.GetMembers("SetSwitch").OfType<IMethodSymbol>().FirstOrDefault(
                        methodSymbol => methodSymbol.Parameters.Length == 2 &&
                                        methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                                        methodSymbol.Parameters[1].Type.SpecialType == SpecialType.System_Boolean);

                if (setSwitchMemberWithStringAndBoolParameter == null)
                {
                    return;
                }

                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationStartAnalysisContext.Compilation);

                compilationStartAnalysisContext.RegisterOperationAction(operationAnalysisContext =>
                {
                    var invocationOperation = (IInvocationOperation)operationAnalysisContext.Operation;
                    var methodSymbol = invocationOperation.TargetMethod;

                    if (!setSwitchMemberWithStringAndBoolParameter.Equals(methodSymbol))
                    {
                        return;
                    }

                    if (IsConfiguredToSkipAnalysis(DoNotDisableSchUseStrongCryptoRule, operationAnalysisContext) &&
                        IsConfiguredToSkipAnalysis(DoNotDisableSpmSecurityProtocolsRule, operationAnalysisContext))
                    {
                        return;
                    }

                    var values = invocationOperation.Arguments.Select(s => s.Value.ConstantValue).ToArray();

                    if (values[0].HasValue &&
                        values[1].HasValue)
                    {
                        if (values[0].Value is string switchName &&
                            BadSwitches.TryGetValue(switchName, out var pair) &&
                            pair.BadValue.Equals(values[1].Value) &&
                            !IsConfiguredToSkipAnalysis(pair.Rule, operationAnalysisContext))
                        {
                            operationAnalysisContext.ReportDiagnostic(
                                invocationOperation.CreateDiagnostic(
                                    pair.Rule,
                                    methodSymbol.Name));
                        }
                    }
                    else if (invocationOperation.TryGetEnclosingControlFlowGraph(out var cfg))
                    {
                        var valueContentResult = ValueContentAnalysis.TryGetOrComputeResult(
                            cfg,
                            operationAnalysisContext.ContainingSymbol,
                            operationAnalysisContext.Options,
                            wellKnownTypeProvider,
                            InterproceduralAnalysisConfiguration.Create(
                                operationAnalysisContext.Options,
                                SupportedDiagnostics,
                                InterproceduralAnalysisKind.None,   // Just looking for simple cases.
                                operationAnalysisContext.CancellationToken),
                            out _,
                            out _);

                        var switchNameValueContent = valueContentResult[
                            OperationKind.Argument,
                            invocationOperation.Arguments[0].Syntax];
                        var switchValueValueContent = valueContentResult[
                            OperationKind.Argument,
                            invocationOperation.Arguments[1].Syntax];

                        // Just check for simple cases with one possible literal value.
                        if (switchNameValueContent.TryGetSingleNonNullLiteral<string>(out var switchName) &&
                            switchValueValueContent.TryGetSingleNonNullLiteral<bool>(out var switchValue) &&
                            BadSwitches.TryGetValue(switchName, out var pair) &&
                            pair.BadValue.Equals(switchValue) &&
                            !IsConfiguredToSkipAnalysis(pair.Rule, operationAnalysisContext))
                        {
                            operationAnalysisContext.ReportDiagnostic(
                                invocationOperation.CreateDiagnostic(
                                    pair.Rule,
                                    invocationOperation.TargetMethod.Name));
                        }
                    }
                },
                OperationKind.Invocation);
            });
        }

        static bool IsConfiguredToSkipAnalysis(DiagnosticDescriptor rule, OperationAnalysisContext context)
            => context.ContainingSymbol.IsConfiguredToSkipAnalysis(context.Options, rule, context.Compilation, context.CancellationToken);
    }
}
