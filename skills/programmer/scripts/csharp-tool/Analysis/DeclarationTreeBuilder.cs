/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DeclarationTreeBuilder.cs
수정일 : 2026-09-17

# 설명
C# 선언을 재귀적으로 수집해 구조 트리와 선언 머리부를 만든다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// CompilationUnit에서 네임스페이스와 선언 트리를 수집한다.
    /// </summary>
    // ============================================================
    internal static class DeclarationTreeBuilder
    {

    #region 선언 수집

        // ------------------------------------------------------------
        /// <summary>
        /// 파일의 최상위 선언부터 모든 하위 선언을 재귀 수집한다.
        /// </summary>
        // ------------------------------------------------------------
        public static List<DeclarationDescriptor> Build(CompilationUnitSyntax root, SourceText sourceText)
        {
            List<DeclarationDescriptor> result = new();
            foreach (MemberDeclarationSyntax member in root.Members)
            {
                DeclarationDescriptor? declaration = CollectMember(member, null, sourceText);
                if (declaration != null)
                {
                    result.Add(declaration);
                }
            }

            return result;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 한 멤버 선언을 구조 정보로 바꾸고 하위 선언을 수집한다.
        /// </summary>
        // ------------------------------------------------------------
        private static DeclarationDescriptor? CollectMember(MemberDeclarationSyntax member, DeclarationDescriptor? parent, SourceText sourceText)
        {
            // 현재 노드를 먼저 하나의 선언 정보로 확정한 뒤 자식 탐색을 시작한다.
            DeclarationDescriptor? declaration = CreateDeclaration(member, parent, sourceText);
            if (declaration == null)
            {
                return null;
            }

            // 자식 수집은 현재 선언이 만들어진 뒤에만 수행해 부모 연결 순서를 고정한다.
            CollectChildren(member, declaration, sourceText);
            return declaration;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Roslyn 멤버를 Inspector가 사용하는 선언 종류 하나로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static DeclarationDescriptor? CreateDeclaration(MemberDeclarationSyntax member, DeclarationDescriptor? parent, SourceText sourceText)
        {
            return member switch
            {
                NamespaceDeclarationSyntax item                                                 => Create(item, "namespace", item.Name.ToString(), parent, sourceText),
                FileScopedNamespaceDeclarationSyntax item                                       => Create(item, "namespace", item.Name.ToString(), parent, sourceText),

                ClassDeclarationSyntax item                                                     => Create(item, "class", item.Identifier.ValueText, parent, sourceText),
                StructDeclarationSyntax item                                                    => Create(item, "struct", item.Identifier.ValueText, parent, sourceText),
                InterfaceDeclarationSyntax item                                                 => Create(item, "interface", item.Identifier.ValueText, parent, sourceText),
                EnumDeclarationSyntax item                                                      => Create(item, "enum", item.Identifier.ValueText, parent, sourceText),
                RecordDeclarationSyntax item when IsRecordStruct(item)                         => Create(item, "record struct", item.Identifier.ValueText, parent, sourceText),
                RecordDeclarationSyntax item                                                    => Create(item, "record", item.Identifier.ValueText, parent, sourceText),
                DelegateDeclarationSyntax item                                                  => Create(item, "delegate", item.Identifier.ValueText, parent, sourceText),

                FieldDeclarationSyntax item                                                     => Create(item, "field", GetFirstVariableName(item.Declaration), parent, sourceText),
                EventFieldDeclarationSyntax item                                                => Create(item, "event", GetFirstVariableName(item.Declaration), parent, sourceText),
                EventDeclarationSyntax item                                                     => Create(item, "event", item.Identifier.ValueText, parent, sourceText),
                PropertyDeclarationSyntax item                                                  => Create(item, "property", item.Identifier.ValueText, parent, sourceText),
                IndexerDeclarationSyntax item                                                   => Create(item, "indexer", "this", parent, sourceText),

                ConstructorDeclarationSyntax item                                               => Create(item, GetConstructorKind(item), item.Identifier.ValueText, parent, sourceText),
                DestructorDeclarationSyntax item                                                => Create(item, "destructor", item.Identifier.ValueText, parent, sourceText),
                MethodDeclarationSyntax item                                                    => Create(item, "method", item.Identifier.ValueText, parent, sourceText),
                OperatorDeclarationSyntax item                                                  => Create(item, "operator", item.OperatorToken.ValueText, parent, sourceText),
                ConversionOperatorDeclarationSyntax item                                        => Create(item, "conversion operator", item.Type.ToString(), parent, sourceText),

                _                                                                              => null,
            };
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 선언 종류에 맞는 직접 하위 선언을 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CollectChildren(MemberDeclarationSyntax member, DeclarationDescriptor declaration, SourceText sourceText)
        {
            switch (member)
            {
                case BaseNamespaceDeclarationSyntax namespaceDeclaration:
                    CollectMembers(namespaceDeclaration.Members, declaration, sourceText);
                    break;
                case TypeDeclarationSyntax typeDeclaration:
                    CollectMembers(typeDeclaration.Members, declaration, sourceText);
                    break;
                case EnumDeclarationSyntax enumDeclaration:
                    CollectEnumMembers(enumDeclaration, declaration, sourceText);
                    break;
                case BaseMethodDeclarationSyntax methodDeclaration when methodDeclaration.Body != null:
                    CollectLocalFunctions(methodDeclaration.Body, declaration, sourceText);
                    break;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 같은 부모를 공유하는 C# 멤버 목록을 원본 순서로 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CollectMembers(SyntaxList<MemberDeclarationSyntax> members, DeclarationDescriptor parent, SourceText sourceText)
        {
            foreach (MemberDeclarationSyntax member in members)
            {
                DeclarationDescriptor? child = CollectMember(member, parent, sourceText);
                AddChild(parent, child);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 열거형 멤버를 부모 열거형 아래에 원본 순서로 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CollectEnumMembers(EnumDeclarationSyntax declaration, DeclarationDescriptor parent, SourceText sourceText)
        {
            foreach (EnumMemberDeclarationSyntax member in declaration.Members)
            {
                DeclarationDescriptor child = Create(member, "enum member", member.Identifier.ValueText, parent, sourceText);
                parent.Children.Add(child);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// record 선언이 struct 형태인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool IsRecordStruct(RecordDeclarationSyntax record)
        {
            return record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 생성자 종류를 인스턴스 생성자와 정적 생성자로 구분한다.
        /// </summary>
        // ------------------------------------------------------------
        private static string GetConstructorKind(ConstructorDeclarationSyntax constructor)
        {
            return constructor.Modifiers.Any(SyntaxKind.StaticKeyword) ? "static constructor" : "constructor";
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 메서드 블록 안의 지역 함수를 부모 선언 아래에 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CollectLocalFunctions(SyntaxNode node, DeclarationDescriptor parent, SourceText sourceText)
        {
            foreach (SyntaxNode childNode in node.ChildNodes())
            {
                // 지역 함수는 구조 트리에 실제 자식 선언으로 추가하고, 그 본문 안의 지역 함수는 새 부모 아래에서 계속 찾는다.
                if (childNode is LocalFunctionStatementSyntax localFunction)
                {
                    DeclarationDescriptor child = Create(localFunction, "local function", localFunction.Identifier.ValueText, parent, sourceText);
                    parent.Children.Add(child);
                    if (localFunction.Body != null)
                    {
                        CollectLocalFunctions(localFunction.Body, child, sourceText);
                    }

                    continue;
                }

                // 람다는 기본 구조 출력 대상이 아니므로 내부 지역 함수를 현재 선언의 자식으로 끌어올리지 않는다.
                if (childNode is AnonymousFunctionExpressionSyntax)
                {
                    continue;
                }

                // 나머지 구문은 선언 소유권을 바꾸지 않고 현재 부모를 유지한 채 재귀 탐색한다.
                CollectLocalFunctions(childNode, parent, sourceText);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// null이 아닌 하위 선언을 부모에 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void AddChild(DeclarationDescriptor parent, DeclarationDescriptor? child)
        {
            if (child != null)
            {
                parent.Children.Add(child);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Roslyn 선언 하나를 공통 DeclarationDescriptor로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static DeclarationDescriptor Create(SyntaxNode node, string kind, string name, DeclarationDescriptor? parent, SourceText sourceText)
        {
            DeclarationDescriptor descriptor = new();
            descriptor.Kind = kind;
            descriptor.Name = name;
            descriptor.Header = BuildHeader(node, sourceText);
            descriptor.Accessibility = GetAccessibility(node, parent);
            descriptor.Modifiers.AddRange(GetModifiers(node).Select(token => token.ValueText));

            IEnumerable<string> attributes = node.ChildNodes()
                .OfType<AttributeListSyntax>()
                .Select(attribute => attribute.ToString());
            descriptor.Attributes.AddRange(attributes);

            descriptor.Range = SourceRange.FromLocation(node.GetLocation());
            descriptor.StartPosition = node.SpanStart;
            descriptor.EndPosition = node.Span.End;
            descriptor.Parent = parent;
            descriptor.Node = node;
            return descriptor;
        }

    #endregion

    #region 선언 머리부

        // ------------------------------------------------------------
        /// <summary>
        /// 선언 종류에 맞춰 본문과 긴 초기화식을 제외한 머리부를 만든다.
        /// </summary>
        // ------------------------------------------------------------
        private static string BuildHeader(SyntaxNode node, SourceText sourceText)
        {
            // 필드·이벤트 필드·enum 멤버·namespace는 일반 본문 경계보다 전용 표현을 쓰는 편이 구조를 더 정확히 보존한다.
            if (node is FieldDeclarationSyntax field)
            {
                return BuildFieldHeader(field.AttributeLists, field.Modifiers, field.Declaration, false);
            }

            if (node is EventFieldDeclarationSyntax eventField)
            {
                return BuildFieldHeader(eventField.AttributeLists, eventField.Modifiers, eventField.Declaration, true);
            }

            if (node is EnumMemberDeclarationSyntax enumMember)
            {
                return BuildEnumMemberHeader(enumMember);
            }

            if (node is BaseNamespaceDeclarationSyntax namespaceDeclaration)
            {
                return $"namespace {namespaceDeclaration.Name}";
            }

            // 나머지 선언은 본문이나 식 본문이 시작되기 직전까지만 잘라 Inspector가 볼 머리부를 만든다.
            int end = GetHeaderEnd(node);
            TextSpan headerSpan = TextSpan.FromBounds(node.SpanStart, Math.Max(node.SpanStart, end));
            string header = sourceText.ToString(headerSpan).Trim();

            // 자동 프로퍼티와 인덱서는 본문이 없으므로 접근자 키워드만 다시 붙여 선언 형태를 잃지 않게 한다.
            string accessors = GetAutoAccessorText(node);
            if (accessors.Length > 0)
            {
                header = $"{header} {{ {accessors} }}";
            }

            return TrimLineIndentation(header);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 열거형 멤버 이름과 선택적 상수 값을 구조 출력용 문자열로 만든다.
        /// </summary>
        // ------------------------------------------------------------
        private static string BuildEnumMemberHeader(EnumMemberDeclarationSyntax member)
        {
            string name = member.Identifier.ValueText;
            return member.EqualsValue == null ? name : $"{name} = {member.EqualsValue.Value}";
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 자동 프로퍼티와 인덱서의 접근자 목록을 한 줄 문자열로 만든다.
        /// </summary>
        // ------------------------------------------------------------
        private static string GetAutoAccessorText(SyntaxNode node)
        {
            AccessorListSyntax? accessorList = node switch
            {
                PropertyDeclarationSyntax property => property.AccessorList,
                IndexerDeclarationSyntax indexer => indexer.AccessorList,
                _ => null,
            };
            if (accessorList == null || !accessorList.Accessors.All(CSharpSyntaxFacts.IsAutoAccessor))
            {
                return string.Empty;
            }

            return string.Join(" ", accessorList.Accessors.Select(GetAccessorText));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 자동 접근자의 키워드를 구조 출력용 문자열로 만든다.
        /// </summary>
        // ------------------------------------------------------------
        private static string GetAccessorText(AccessorDeclarationSyntax accessor)
        {
            return $"{accessor.Keyword.ValueText};";
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 필드 선언에서 긴 초기화식을 빼고 형식과 변수 이름만 보존한다.
        /// </summary>
        // ------------------------------------------------------------
        private static string BuildFieldHeader(SyntaxList<AttributeListSyntax> attributes, SyntaxTokenList modifiers, VariableDeclarationSyntax declaration, bool isEvent)
        {
            StringBuilder builder = new();

            // 구조 출력에서도 특성과 modifier 순서를 원본 선언과 동일하게 유지한다.
            foreach (AttributeListSyntax attribute in attributes)
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(attribute.ToString());
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(string.Join(" ", modifiers.Select(token => token.ValueText)));
            if (modifiers.Count > 0)
            {
                builder.Append(' ');
            }

            // field-like event만 event 키워드를 보강하고, 긴 initializer는 제외한 형식·이름 골격만 남긴다.
            if (isEvent)
            {
                builder.Append("event ");
            }

            builder.Append(declaration.Type.ToString());
            builder.Append(' ');
            builder.Append(string.Join(", ", declaration.Variables.Select(variable => variable.Identifier.ValueText)));
            return builder.ToString();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 선언 본문이나 식 본문이 시작되기 직전 위치를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static int GetHeaderEnd(SyntaxNode node)
        {
            return node switch
            {
                BaseTypeDeclarationSyntax item when !item.OpenBraceToken.IsMissing => item.OpenBraceToken.SpanStart,
                BaseTypeDeclarationSyntax item => item.SemicolonToken.SpanStart,
                DelegateDeclarationSyntax item => item.SemicolonToken.SpanStart,
                BaseMethodDeclarationSyntax item when item.Body != null => item.Body.OpenBraceToken.SpanStart,
                BaseMethodDeclarationSyntax item when item.ExpressionBody != null => item.ExpressionBody.ArrowToken.SpanStart,
                BaseMethodDeclarationSyntax item => item.SemicolonToken.SpanStart,
                LocalFunctionStatementSyntax item when item.Body != null => item.Body.OpenBraceToken.SpanStart,
                LocalFunctionStatementSyntax item when item.ExpressionBody != null => item.ExpressionBody.ArrowToken.SpanStart,
                LocalFunctionStatementSyntax item => item.SemicolonToken.SpanStart,
                PropertyDeclarationSyntax item when item.AccessorList != null => item.AccessorList.OpenBraceToken.SpanStart,
                PropertyDeclarationSyntax item when item.ExpressionBody != null => item.ExpressionBody.ArrowToken.SpanStart,
                PropertyDeclarationSyntax item => item.SemicolonToken.SpanStart,
                IndexerDeclarationSyntax item when item.AccessorList != null => item.AccessorList.OpenBraceToken.SpanStart,
                IndexerDeclarationSyntax item when item.ExpressionBody != null => item.ExpressionBody.ArrowToken.SpanStart,
                IndexerDeclarationSyntax item => item.SemicolonToken.SpanStart,
                EventDeclarationSyntax item when item.AccessorList != null => item.AccessorList.OpenBraceToken.SpanStart,
                EventDeclarationSyntax item => item.SemicolonToken.SpanStart,
                _ => node.Span.End,
            };
        }

    #endregion

    #region 선언 속성

        // ------------------------------------------------------------
        /// <summary>
        /// 선언에 명시된 modifier 토큰 목록을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static SyntaxTokenList GetModifiers(SyntaxNode node)
        {
            return node switch
            {
                BaseTypeDeclarationSyntax item => item.Modifiers,
                DelegateDeclarationSyntax item => item.Modifiers,
                BaseMethodDeclarationSyntax item => item.Modifiers,
                LocalFunctionStatementSyntax item => item.Modifiers,
                BasePropertyDeclarationSyntax item => item.Modifiers,
                BaseFieldDeclarationSyntax item => item.Modifiers,
                _ => default,
            };
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 명시 modifier와 선언 문맥으로 접근 수준을 정규화한다.
        /// </summary>
        // ------------------------------------------------------------
        private static string GetAccessibility(SyntaxNode node, DeclarationDescriptor? parent)
        {
            // 명시적 인터페이스 구현은 C# 접근 제한자와 다른 배치 의미를 가지므로 가장 먼저 분리한다.
            if (IsExplicitInterfaceImplementation(node))
            {
                return "explicit";
            }

            // 명시 modifier 조합을 먼저 해석하고, 없을 때만 선언 문맥의 기본 접근 수준으로 내려간다.
            SyntaxTokenList modifiers = GetModifiers(node);
            bool isPublic = modifiers.Any(SyntaxKind.PublicKeyword);
            bool isPrivate = modifiers.Any(SyntaxKind.PrivateKeyword);
            bool isProtected = modifiers.Any(SyntaxKind.ProtectedKeyword);
            bool isInternal = modifiers.Any(SyntaxKind.InternalKeyword);
            if (isProtected && isInternal)
            {
                return isPrivate ? "private protected" : "protected internal";
            }

            if (isPublic)
            {
                return "public";
            }

            if (isProtected)
            {
                return "protected";
            }

            if (isInternal)
            {
                return "internal";
            }

            if (isPrivate)
            {
                return "private";
            }

            if (parent?.Kind == "interface")
            {
                return "public";
            }

            if (parent == null || parent.Kind == "namespace")
            {
                return "internal";
            }

            return "private";
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 선언이 접근 제한자를 갖지 않는 명시적 인터페이스 구현인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool IsExplicitInterfaceImplementation(SyntaxNode node)
        {
            return node switch
            {
                MethodDeclarationSyntax item => item.ExplicitInterfaceSpecifier != null,
                PropertyDeclarationSyntax item => item.ExplicitInterfaceSpecifier != null,
                IndexerDeclarationSyntax item => item.ExplicitInterfaceSpecifier != null,
                EventDeclarationSyntax item => item.ExplicitInterfaceSpecifier != null,
                _ => false,
            };
        }

    #endregion

    #region 머리부 정규화

        // ------------------------------------------------------------
        /// <summary>
        /// 여러 줄 머리부의 공통 앞 공백을 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        private static string TrimLineIndentation(string text)
        {
            string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
            return string.Join(Environment.NewLine, lines.Select(line => line.Trim())).Trim();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 필드 선언의 첫 변수 이름을 구조 식별자로 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static string GetFirstVariableName(VariableDeclarationSyntax declaration)
        {
            return declaration.Variables.Count == 0 ? string.Empty : declaration.Variables[0].Identifier.ValueText;
        }

    #endregion

    }

}
