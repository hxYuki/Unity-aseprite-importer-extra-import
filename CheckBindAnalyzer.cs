using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FindSymbols;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CheckBindAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor MissingMemberRule = new DiagnosticDescriptor(
        id: "SGA001",
        title: "Missing Required Member",
        messageFormat: "The class '{0}' is missing the required member '{1}'. {2}",
        category: "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingMethodCallRule = new DiagnosticDescriptor(
        id: "SGA010",
        title: "Unused Generated Method",
        messageFormat: "Generated method '{0}' in class '{1}' is not used.",
        category: "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(MissingMemberRule, MissingMethodCallRule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        //context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSymbolAction(symbolContext =>
        {
            var fieldSymbol = (IFieldSymbol)symbolContext.Symbol;
            var classSymbol = fieldSymbol.ContainingType;

            var attribute = fieldSymbol?.GetAttributes().FirstOrDefault(ad =>
                            ad.AttributeClass?.ToDisplayString() == "Assets.Extras.ShapeAnimation.BindAnimationPropertyAttribute");

            if (attribute != null)
            {
                if (IsMemberMissing("LoadAnimationData", classSymbol))
                {
                    var className = classSymbol.Name;
                    var diagnostic = Diagnostic.Create(MissingMemberRule, fieldSymbol.Locations[0], className, "LoadAnimationData", "Should be \"void LoadAnimationData(string unitName, string propertyName);\"");

                    symbolContext.ReportDiagnostic(diagnostic);
                }
            }
        }, SymbolKind.Field);

        context.RegisterSemanticModelAction(semanticModelContext =>
        {
            var semanticModel = semanticModelContext.SemanticModel;
            var syntaxTree = semanticModel.SyntaxTree;
            var root = syntaxTree.GetRoot();
            // 查找包含特性的字段的对象
            var fieldDeclarations = root.DescendantNodes().OfType<FieldDeclarationSyntax>();
            fieldDeclarations
                .SelectMany(d => d.Declaration.Variables)
                .Select(v => semanticModel.GetDeclaredSymbol(v) as IFieldSymbol)
                .Where(f => f.GetAttributes().Any(ad => ad.AttributeClass?.ToDisplayString() == "Assets.Extras.ShapeAnimation.BindAnimationPropertyAttribute"))
                .GroupBy(field => field.ContainingType)
                .Select(g => g.Key)
                .ToList().ForEach(classSymbol =>
                {
                    // 检查 InitAnimation、UpdateFrame 是否被调用
                    CheckMethodUsage(semanticModelContext, classSymbol, "InitAnimation");
                    CheckMethodUsage(semanticModelContext, classSymbol, "UpdateFrame");
                });
        });
    }

    public static bool IsMemberMissing(string requiredMember, INamedTypeSymbol classSymbol)
    {
        if (!classSymbol.GetMembers().Any(m => m.Name == requiredMember && m is IMethodSymbol))
        {
            return true;
        }
        return false;
    }

    private static void CheckMethodUsage(SemanticModelAnalysisContext context, INamedTypeSymbol classSymbol, string methodName)
    {
        var methodSymbol = classSymbol.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(m => m.Name == methodName);
        if (methodSymbol == null)
        {
            return;
        }
        var semanticModel = context.SemanticModel;
        var syntaxTree = context.SemanticModel.SyntaxTree;
        var methodCalled = false;

        var root = syntaxTree.GetRoot();

        var methodCalls = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var methodCall in methodCalls)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(methodCall);
            var calledMethodSymbol = symbolInfo.Symbol as IMethodSymbol;

            if (calledMethodSymbol != null && calledMethodSymbol.Equals(methodSymbol, SymbolEqualityComparer.Default))
            {
                methodCalled = true;
                break;
            }
        }

        if (!methodCalled)
        {
            Report();
        }

        void Report()
        {
            var diagnostic = Diagnostic.Create(MissingMethodCallRule, classSymbol.Locations[0], methodName, classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }


}
