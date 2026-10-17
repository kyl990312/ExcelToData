using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TableAutomationTool.UserEnum;

namespace TableAutomationTool.EnumGenerator;

/// <summary>
/// 프로그래머가 작성한 C# enum 소스 파일을 읽어 enum 정의를 뽑는다.
///
/// - 등록된 파일 전체를 하나의 컴파일 단위로 묶어 Roslyn 으로 의미 분석한다.
///   그래서 "B = A + 1", "Flag = 1 &lt;&lt; 3", 다른 파일 enum/const 참조 같은 값도 컴파일러와 같은 결과로 계산된다.
/// - UnityEngine 등 외부 참조는 없으므로 그런 using 이 있는 파일은 진단 에러가 나지만,
///   enum 멤버 값 계산에 쓰이지 않으면 무시한다. 값을 계산하지 못한 멤버가 있으면 실패로 처리한다.
/// - 중첩 enum 은 "Outer.Inner" 이름으로 등록한다.
/// - 멤버 값은 int 범위여야 한다(EnumDB 는 int 로 저장한다).
/// </summary>
public static class EnumSourceParser
{
    /// <summary>
    /// 소스 파일 하나에서 읽은 결과. SheetName 은 파일 이름(확장자 제외)이다.
    /// </summary>
    public sealed record ParsedFile(string FilePath, string SheetName, List<EnumDefinition> Enums);

    /// <summary>
    /// 등록된 enum 소스 파일들을 파싱한다.
    /// </summary>
    /// <param name="filePaths">C# 소스 파일 경로 목록</param>
    /// <param name="files">파일별 파싱 결과 (실패 시에도 가능한 만큼 채워진다)</param>
    /// <param name="errors">실패 사유 목록. 비어 있으면 성공이다.</param>
    /// <returns>성공 여부</returns>
    public static bool TryParse(IReadOnlyList<string> filePaths, out List<ParsedFile> files, out List<string> errors)
    {
        files = [];
        errors = [];

        var trees = new List<(string path, SyntaxTree tree)>();
        foreach (var rawPath in filePaths)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                errors.Add("SystemEnumFiles 에 빈 경로가 있습니다");
                continue;
            }

            var fullPath = Path.GetFullPath(rawPath);
            if (!File.Exists(fullPath))
            {
                errors.Add($"enum 소스 파일을 찾을 수 없습니다: {rawPath} ({fullPath})");
                continue;
            }

            if (trees.Any(t => string.Equals(t.path, fullPath, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add($"같은 enum 소스 파일이 두 번 등록되어 있습니다: {rawPath}");
                continue;
            }

            var text = File.ReadAllText(fullPath, Encoding.UTF8);
            trees.Add((fullPath, CSharpSyntaxTree.ParseText(text, path: fullPath)));
        }

        if (errors.Count > 0)
            return false;

        var compilation = CSharpCompilation.Create(
            "SystemEnums",
            trees.Select(t => t.tree),
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        foreach (var (path, tree) in trees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            var enums = new List<EnumDefinition>();

            foreach (var syntaxError in tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
                errors.Add($"구문 오류: {FormatDiagnostic(syntaxError)}");

            foreach (var declaration in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol)
                {
                    errors.Add($"enum 심볼을 해석할 수 없습니다: {declaration.Identifier.Text} ({Location(declaration)})");
                    continue;
                }

                var definition = ReadEnum(model, declaration, symbol, errors);
                if (definition != null)
                    enums.Add(definition);
            }

            files.Add(new ParsedFile(path, Path.GetFileNameWithoutExtension(path), enums));
        }

        return errors.Count == 0;
    }

    private static EnumDefinition? ReadEnum(SemanticModel model, EnumDeclarationSyntax declaration, INamedTypeSymbol symbol, List<string> errors)
    {
        var name = GetEnumName(symbol);
        var enumNamespace = symbol.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? ns.ToDisplayString()
            : string.Empty;

        var members = new List<(string member, int value)>();
        var hasError = false;

        foreach (var memberSyntax in declaration.Members)
        {
            var memberName = memberSyntax.Identifier.Text;

            if (model.GetDeclaredSymbol(memberSyntax) is not IFieldSymbol field || !field.HasConstantValue || field.ConstantValue == null)
            {
                var reasons = model.GetDiagnostics(memberSyntax.Span)
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(FormatDiagnostic)
                    .ToList();
                var reason = reasons.Count > 0 ? string.Join(" / ", reasons) : "상수 값을 계산할 수 없습니다";
                errors.Add($"{name}.{memberName} 값 계산 실패: {reason}");
                hasError = true;
                continue;
            }

            var raw = Convert.ToDecimal(field.ConstantValue);
            if (raw < int.MinValue || raw > int.MaxValue)
            {
                errors.Add($"{name}.{memberName} = {raw} 는 int 범위를 벗어납니다 ({Location(memberSyntax)})");
                hasError = true;
                continue;
            }

            members.Add((memberName, (int)raw));
        }

        if (hasError)
            return null;

        if (members.Count == 0)
        {
            errors.Add($"멤버가 없는 enum 입니다: {name} ({Location(declaration)})");
            return null;
        }

        return new EnumDefinition(name, enumNamespace, members);
    }

    /// <summary>
    /// 중첩 enum 이면 감싸는 타입 이름을 앞에 붙인다. 예) Outer.Inner
    /// </summary>
    private static string GetEnumName(INamedTypeSymbol symbol)
    {
        var parts = new List<string> { symbol.Name };
        for (var containing = symbol.ContainingType; containing != null; containing = containing.ContainingType)
            parts.Insert(0, containing.Name);
        return string.Join(".", parts);
    }

    private static string Location(SyntaxNode node)
    {
        var span = node.GetLocation().GetLineSpan();
        return $"{Path.GetFileName(span.Path)}:{span.StartLinePosition.Line + 1}";
    }

    private static string FormatDiagnostic(Diagnostic diagnostic)
    {
        var span = diagnostic.Location.GetLineSpan();
        return $"{Path.GetFileName(span.Path)}:{span.StartLinePosition.Line + 1} {diagnostic.Id} {diagnostic.GetMessage()}";
    }
}
