// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1068: CancellationToken parameters must come last.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class CancellationTokenParametersMustComeLastAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1068";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.CancellationTokenParametersMustComeLastTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.CancellationTokenParametersMustComeLastMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            RuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Design,
            DiagnosticHelpers.DefaultDiagnosticSeverity,
            isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultForVsixAndNuget);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                INamedTypeSymbol cancellationTokenType = compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingCancellationToken);
                if (cancellationTokenType != null)
                {
                    compilationContext.RegisterSymbolAction(symbolContext =>
                    {
                        var methodSymbol = (IMethodSymbol)symbolContext.Symbol;
                        if (methodSymbol.IsOverride ||
                            methodSymbol.IsImplementationOfAnyInterfaceMember())
                        {
                            return;
                        }

                        int last = methodSymbol.Parameters.Length - 1;
                        if (last >= 0 && methodSymbol.Parameters[last].IsParams)
                        {
                            last--;
                        }

                        // Skip optional parameters, UNLESS one of them is a CancellationToken
                        // AND it's not the last one.
                        if (last >= 0 && methodSymbol.Parameters[last].IsOptional
                            && !methodSymbol.Parameters[last].Type.Equals(cancellationTokenType))
                        {
                            last--;

                            while (last >= 0 && methodSymbol.Parameters[last].IsOptional)
                            {
                                if (methodSymbol.Parameters[last].Type.Equals(cancellationTokenType))
                                {
                                    symbolContext.ReportDiagnostic(Diagnostic.Create(
                                        Rule, methodSymbol.Locations.First(), methodSymbol.ToDisplayString()));
                                }

                                last--;
                            }
                        }

                        // Ignore multiple cancellation token parameters at the end of the parameter list.
                        while (last >= 0 && methodSymbol.Parameters[last].Type.Equals(cancellationTokenType))
                        {
                            last--;
                        }

                        // Ignore parameters passed by reference when they appear at the end of the parameter list.
                        while (last >= 0 && methodSymbol.Parameters[last].RefKind != RefKind.None)
                        {
                            last--;
                        }

                        for (int i = last - 1; i >= 0; i--)
                        {
                            ITypeSymbol parameterType = methodSymbol.Parameters[i].Type;
                            if (!parameterType.Equals(cancellationTokenType))
                            {
                                continue;
                            }

                            // Bail if the CancellationToken is the first parameter of an extension method.
                            if (i == 0 && methodSymbol.IsExtensionMethod)
                            {
                                continue;
                            }

                            symbolContext.ReportDiagnostic(Diagnostic.Create(
                                Rule, methodSymbol.Locations.First(), methodSymbol.ToDisplayString()));
                            break;
                        }
                    },
                    SymbolKind.Method);
                }
            });
        }
    }
}
