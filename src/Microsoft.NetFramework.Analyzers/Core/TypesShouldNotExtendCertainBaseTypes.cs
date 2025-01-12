// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetFramework.Analyzers
{
    /// <summary>
    /// CA1058: Types should not extend certain base types
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class TypesShouldNotExtendCertainBaseTypesAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1058";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesTitle), MicrosoftNetFrameworkAnalyzersResources.ResourceManager, typeof(MicrosoftNetFrameworkAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesDescription), MicrosoftNetFrameworkAnalyzersResources.ResourceManager, typeof(MicrosoftNetFrameworkAnalyzersResources));

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             "{0}",
                                                                             DiagnosticCategory.Design,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                             description: s_localizableDescription,
                                                                             helpLinkUri: "https://docs.microsoft.com/visualstudio/code-quality/ca1058-types-should-not-extend-certain-base-types",
                                                                             customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        private static readonly ImmutableDictionary<string, string> s_badBaseTypesToMessage = new Dictionary<string, string>
                                                    {
                                                        {"System.ApplicationException", MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemApplicationException},
                                                        {"System.Xml.XmlDocument", MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemXmlXmlDocument},
                                                        {"System.Collections.CollectionBase", MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsCollectionBase},
                                                        {"System.Collections.DictionaryBase", MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsDictionaryBase},
                                                        {"System.Collections.Queue", MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsQueue},
                                                        {"System.Collections.ReadOnlyCollectionBase", MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsReadOnlyCollectionBase},
                                                        {"System.Collections.SortedList", MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsSortedList},
                                                        {"System.Collections.Stack", MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsStack},
                                                    }.ToImmutableDictionary();

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterCompilationStartAction(AnalyzeCompilationStart);
        }

        private static void AnalyzeCompilationStart(CompilationStartAnalysisContext context)
        {
            ImmutableHashSet<INamedTypeSymbol> badBaseTypes = s_badBaseTypesToMessage.Keys
                                .Select(bt => context.Compilation.GetOrCreateTypeByMetadataName(bt))
                                .Where(bt => bt != null)
                                .ToImmutableHashSet();

            if (badBaseTypes.Count > 0)
            {
                context.RegisterSymbolAction((saContext) =>
                    {
                        var namedTypeSymbol = saContext.Symbol as INamedTypeSymbol;

                        if (namedTypeSymbol.BaseType != null &&
                            badBaseTypes.Contains(namedTypeSymbol.BaseType) &&
                            namedTypeSymbol.MatchesConfiguredVisibility(saContext.Options, Rule, saContext.CancellationToken))
                        {
                            string baseTypeName = namedTypeSymbol.BaseType.ToDisplayString();
                            Debug.Assert(s_badBaseTypesToMessage.ContainsKey(baseTypeName));
                            string message = string.Format(s_badBaseTypesToMessage[baseTypeName], namedTypeSymbol.ToDisplayString(), baseTypeName);
                            Diagnostic diagnostic = Diagnostic.Create(Rule, namedTypeSymbol.Locations.First(), namedTypeSymbol.Locations.Skip(1), message);
                            saContext.ReportDiagnostic(diagnostic);
                        }
                    }
                    , SymbolKind.NamedType);
            }
        }
    }
}