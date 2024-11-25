using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SourceGen
{

    [Generator]
    public class PropertyBindGenerator : ISourceGenerator
    {
        const string AttributeText = @"
using System;
namespace Assets.Extras.ShapeAnimation
{
    /// <summary>
    /// Using BindAnimationProperty will create methods in your class:
    /// <code>
    /// void InitAnimation(string unit);
    /// void UpdateFrame(int i);
    /// </code>
    /// You need to call these when initializing and updating.
    /// And it relies on you providing a method to load animation data, like:
    /// <code>void LoadAnimationData(string unitName, string propertyName</code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    internal class BindAnimationPropertyAttribute : Attribute
    {
        public string PropertyName { get; }
        public BindAnimationPropertyAttribute() { }
        public BindAnimationPropertyAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }
    }
}";
        const string FileTemplate = @"
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Assets.Extras.ShapeAnimation;
using Assets.Scripts.Helpers;
using DG.Tweening;

namespace {Namespace}
{
    public partial class {ClassName}
    {
        {TweenHolders}

        void InitAnimation(string unit)
        {
            {Bindings}
        }

        void UpdateFrame(int i)
        {
            {TweenUpdates}
        }
    }
}";


        public void Execute(GeneratorExecutionContext context)
        {
            // 添加特性代码
            context.AddSource("ShapeAnimation.BindAnimationPropertyAttribute.g.cs", SourceText.From(AttributeText, Encoding.UTF8));

            // 获取语法树
            var syntaxTrees = context.Compilation.SyntaxTrees;

            // 用于存储类和其对应的绑定代码
            var classBindings = new Dictionary<INamedTypeSymbol, (List<string> tweenHolders, List<string> bindings, List<string> updates)>();

            // 遍历所有语法树
            foreach (var syntaxTree in syntaxTrees)
            {
                var semanticModel = context.Compilation.GetSemanticModel(syntaxTree);
                var root = syntaxTree.GetRoot();

                // 查找所有字段声明
                var fieldDeclarations = root.DescendantNodes().OfType<FieldDeclarationSyntax>();

                foreach (var fieldDeclaration in fieldDeclarations)
                {
                    foreach (var variable in fieldDeclaration.Declaration.Variables)
                    {
                        var fieldSymbol = semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;

                        // 检查字段是否具有 BindAnimationPropertyAttribute 特性
                        var attribute = fieldSymbol?.GetAttributes().FirstOrDefault(ad =>
                            ad.AttributeClass?.ToDisplayString() == "Assets.Extras.ShapeAnimation.BindAnimationPropertyAttribute");

                        if (attribute != null)
                        {
                            // 检查特性是否另外指定了属性名
                            var propertyName = attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "PropertyName").Value.Value as string;
                            // 如果没有指定属性名，则使用字段名
                            if (string.IsNullOrEmpty(propertyName))
                            {
                                propertyName = fieldSymbol.Name;
                            }

                            var classSymbol = fieldSymbol.ContainingType;
                            var fieldName = fieldSymbol.Name;

                            // 生成绑定代码
                            //var bindCode = BindTemplate.Replace("{FieldName}", fieldName);
                            var tweenHolder = $@"
        ShapeAnimation _{fieldName}Animation;";
                            var bindCode = $@"
            _{fieldName}Animation = LoadAnimationData(unit, ""{propertyName}"");";
                            var updateCode = $@"
            {fieldName} = _{fieldName}Animation.GetI(i, {fieldName});";

                            if (!classBindings.ContainsKey(classSymbol))
                            {
                                classBindings[classSymbol] = (new List<string>(), new List<string>(), new List<string>());
                            }

                            classBindings[classSymbol].tweenHolders.Add(tweenHolder);
                            classBindings[classSymbol].bindings.Add(bindCode);
                            classBindings[classSymbol].updates.Add(updateCode);
                        }
                    }
                }
            }


            // 生成每个类的代码
            foreach (var classBinding in classBindings)
            {
                var classSymbol = classBinding.Key;
                var className = classSymbol.Name;

                var holders = string.Join(Environment.NewLine, classBinding.Value.tweenHolders);
                var updates = string.Join(Environment.NewLine, classBinding.Value.updates);

                var namespaceName = classSymbol.ContainingNamespace.ToDisplayString();

                string bindings;
                if (CheckBindAnalyzer.IsMemberMissing("LoadAnimationData", classSymbol))
                {
                    bindings = "";
                }
                else
                {
                    bindings = string.Join(Environment.NewLine, classBinding.Value.bindings);
                }


                // 使用字符串模板生成类代码
                var source = FileTemplate
                    .Replace("{Namespace}", namespaceName)
                    .Replace("{ClassName}", className)
                    .Replace("{TweenHolders}", holders)
                    .Replace("{Bindings}", bindings)
                    .Replace("{TweenUpdates}", updates);

                // 添加生成的源代码
                context.AddSource($"{className}_BindAnimationProperty.g.cs", SourceText.From(source, Encoding.UTF8));

            }
        }


        public void Initialize(GeneratorInitializationContext context)
        {
            // 初始化代码
        }
    }
}
