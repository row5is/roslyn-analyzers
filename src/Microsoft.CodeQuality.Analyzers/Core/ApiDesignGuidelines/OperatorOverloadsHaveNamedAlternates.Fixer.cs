// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA2225: Operator overloads have named alternates
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class OperatorOverloadsHaveNamedAlternatesFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(OperatorOverloadsHaveNamedAlternatesAnalyzer.RuleId);

        public override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SemanticModel semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxGenerator generator = context.Document.Project?.LanguageServices?.GetService<SyntaxGenerator>();
            if (semanticModel != null && generator != null)
            {
                string title = MicrosoftApiDesignGuidelinesAnalyzersResources.OperatorOverloadsHaveNamedAlternatesTitle;
                context.RegisterCodeFix(new MyCodeAction(title, ct => Fix(context, root, generator, semanticModel, ct), equivalenceKey: title), context.Diagnostics.First());
            }
        }

        private static async Task<Document> Fix(CodeFixContext context, SyntaxNode root, SyntaxGenerator generator, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            SyntaxNode node = root.FindNode(context.Span);
            Diagnostic diagnostic = context.Diagnostics.First();
            switch (diagnostic.Properties[OperatorOverloadsHaveNamedAlternatesAnalyzer.DiagnosticKindText])
            {
                case OperatorOverloadsHaveNamedAlternatesAnalyzer.AddAlternateText:
                    SyntaxNode methodDeclaration = generator.GetDeclaration(node, DeclarationKind.Operator) ?? generator.GetDeclaration(node, DeclarationKind.ConversionOperator);
                    var operatorOverloadSymbol = (IMethodSymbol)semanticModel.GetDeclaredSymbol(methodDeclaration, cancellationToken);
                    INamedTypeSymbol typeSymbol = operatorOverloadSymbol.ContainingType;

                    // For C# the following `typeDeclarationSyntax` and `typeDeclaration` nodes are identical, but for VB they're different so in
                    // an effort to keep this as language-agnostic as possible, the heavy-handed approach is used.
                    SyntaxNode typeDeclarationSyntax = typeSymbol.DeclaringSyntaxReferences.First().GetSyntax(cancellationToken);
                    SyntaxNode typeDeclaration = generator.GetDeclaration(typeDeclarationSyntax,
                        typeSymbol.TypeKind == TypeKind.Struct ? DeclarationKind.Struct : DeclarationKind.Class);

                    SyntaxNode addedMember;
                    IEnumerable<SyntaxNode> bodyStatements = generator.DefaultMethodBody(semanticModel.Compilation);
                    if (OperatorOverloadsHaveNamedAlternatesAnalyzer.IsPropertyExpected(operatorOverloadSymbol.Name))
                    {
                        // add a property
                        addedMember = generator.PropertyDeclaration(
                            name: OperatorOverloadsHaveNamedAlternatesAnalyzer.IsTrueText,
                            type: generator.TypeExpression(SpecialType.System_Boolean),
                            accessibility: Accessibility.Public,
                            modifiers: DeclarationModifiers.ReadOnly,
                            getAccessorStatements: bodyStatements);
                    }
                    else
                    {
                        // add a method
                        ExpectedMethodSignature expectedSignature = GetExpectedMethodSignature(operatorOverloadSymbol, semanticModel.Compilation);

                        if (expectedSignature.Name == "CompareTo" && operatorOverloadSymbol.ContainingType.TypeKind == TypeKind.Class)
                        {
                            var nullCheck = generator.IfStatement(
                                generator.InvocationExpression(
                                    generator.IdentifierName("ReferenceEquals"),
                                    generator.IdentifierName(expectedSignature.Parameters.First().Item1),
                                    generator.NullLiteralExpression()),
                                new[]
                                {
                                    generator.ReturnStatement(generator.LiteralExpression(1))
                                });

                            bodyStatements = new[] { nullCheck }.Concat(bodyStatements);
                        }

                        addedMember = generator.MethodDeclaration(
                            name: expectedSignature.Name,
                            parameters: expectedSignature.Parameters.Select(p => generator.ParameterDeclaration(p.Item1, generator.TypeExpression(p.Item2))),
                            returnType: generator.TypeExpression(expectedSignature.ReturnType),
                            accessibility: Accessibility.Public,
                            modifiers: expectedSignature.IsStatic ? DeclarationModifiers.Static : DeclarationModifiers.None,
                            statements: bodyStatements);
                    }

                    SyntaxNode newTypeDeclaration = generator.AddMembers(typeDeclaration, addedMember);
                    return context.Document.WithSyntaxRoot(root.ReplaceNode(typeDeclaration, newTypeDeclaration));
                case OperatorOverloadsHaveNamedAlternatesAnalyzer.FixVisibilityText:
                    SyntaxNode badVisibilityNode = generator.GetDeclaration(node, DeclarationKind.Method) ?? generator.GetDeclaration(node, DeclarationKind.Property);
                    ISymbol badVisibilitySymbol = semanticModel.GetDeclaredSymbol(badVisibilityNode, cancellationToken);
                    SymbolEditor symbolEditor = SymbolEditor.Create(context.Document);
                    ISymbol newSymbol = await symbolEditor.EditOneDeclarationAsync(badVisibilitySymbol,
                        (documentEditor, syntaxNode) => documentEditor.SetAccessibility(badVisibilityNode, Accessibility.Public), cancellationToken).ConfigureAwait(false);
                    Document newDocument = symbolEditor.GetChangedDocuments().Single();
                    SyntaxNode newRoot = await newDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                    return context.Document.WithSyntaxRoot(newRoot);
                default:
                    return context.Document;
            }
        }

        private static ExpectedMethodSignature GetExpectedMethodSignature(IMethodSymbol operatorOverloadSymbol, Compilation compilation)
        {
            var containingType = (ITypeSymbol)operatorOverloadSymbol.ContainingType;
            ITypeSymbol returnType = operatorOverloadSymbol.ReturnType;
            string expectedName = OperatorOverloadsHaveNamedAlternatesAnalyzer.GetExpectedAlternateMethodGroup(operatorOverloadSymbol.Name, returnType).AlternateMethod1;
            switch (operatorOverloadSymbol.Name)
            {
                case "op_GreaterThan":
                case "op_GreaterThanOrEqual":
                case "op_LessThan":
                case "op_LessThanOrEqual":
                    // e.g., public int CompareTo(MyClass other)
                    INamedTypeSymbol intType = compilation.GetSpecialType(SpecialType.System_Int32);
                    return new ExpectedMethodSignature(expectedName, intType, ImmutableArray.Create(Tuple.Create("other", containingType)), isStatic: false);
                case "op_Decrement":
                case "op_Increment":
                case "op_UnaryNegation":
                case "op_UnaryPlus":
                    // e.g., public static MyClass Decrement(MyClass item)
                    return new ExpectedMethodSignature(expectedName, returnType, ImmutableArray.Create(Tuple.Create("item", containingType)), isStatic: true);
                case "op_Implicit":
                    // e.g., public int ToInt32()
                    return new ExpectedMethodSignature(expectedName, returnType, ImmutableArray.Create<Tuple<string, ITypeSymbol>>(), isStatic: false);
                default:
                    // e.g., public static MyClass Add(MyClass left, MyClass right)
                    return new ExpectedMethodSignature(expectedName, returnType, ImmutableArray.Create(Tuple.Create("left", containingType), Tuple.Create("right", containingType)), isStatic: true);
            }
        }

        private class ExpectedMethodSignature
        {
            public string Name { get; }
            public ITypeSymbol ReturnType { get; }
            public IEnumerable<Tuple<string, ITypeSymbol>> Parameters { get; }
            public bool IsStatic { get; }

            public ExpectedMethodSignature(string name, ITypeSymbol returnType, IEnumerable<Tuple<string, ITypeSymbol>> parameters, bool isStatic)
            {
                Name = name;
                ReturnType = returnType;
                Parameters = parameters;
                IsStatic = isStatic;
            }
        }

        private class MyCodeAction : DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument, string equivalenceKey)
                : base(title, createChangedDocument, equivalenceKey)
            {
            }
        }
    }
}