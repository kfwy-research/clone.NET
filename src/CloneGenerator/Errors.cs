using Microsoft.CodeAnalysis;

namespace CloneGenerator;

public static class Errors
{
    public static readonly DiagnosticDescriptor PartialRequiredError = new DiagnosticDescriptor(
        id: "CLONEE001",
        title: "PartialClassRequired",
        messageFormat: "clone require class '{0}' must be partial",
        category: "MyGeneratorUsage",
        defaultSeverity: DiagnosticSeverity.Error, // 设置为 Error 会导致编译失败
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnkownTypeError = new DiagnosticDescriptor(
        id: "CLONEE002",
        title: "UnknownType",
        messageFormat: "unknown type '{0}'",
        category: "MyGeneratorUsage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
