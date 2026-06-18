using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Diagnostics;

namespace RoslynCSharp.ExecutionSecurity
{
    internal static class ExecutionSecurityBuilder
    {
        // Private
        private static CSharpSyntaxNode cachedSecuritySyntax = null;

        // Public
        public const string ExecutionRootNamespace = "Trivial";
        public const string ExecutionSubNamespace = "ExecutionSecurity";
        public const string ExecutionNamespace = ExecutionRootNamespace + "." + ExecutionSubNamespace;

        public const string ExecutionTimeoutExceptionTypeName = "ExecutionTimeoutException";
        public const string ExecutionIterationExceptionTypeName = "ExecutionIterationException";
        public const string ExecutionTypeName = "ExecutionChecks";        
        public const string ExecutionTimeoutMethodName = "CheckExecutionTimeout";
        public const string ExecutionIterationMethodName = "CheckExecutionIterations";

        // Methods
        public static CSharpSyntaxNode BuildExecutionSecurityNode()
        {
            // Check for cached
            if(cachedSecuritySyntax != null)
                return cachedSecuritySyntax;

            // Create timeout exception
            ClassDeclarationSyntax timeoutException = BuildExecutionExceptionType(ExecutionTimeoutExceptionTypeName,
                "Execution was aborted due to timeout");

            // Create iteration exception
            ClassDeclarationSyntax iterationException = BuildExecutionExceptionType(ExecutionIterationExceptionTypeName,
                "Execution was aborted due to maximum iterations exceeded");

            // Create timeout method
            MethodDeclarationSyntax timeoutMethod = BuildExecutionSecurityTimeoutMethod();

            // Create iteration method
            MethodDeclarationSyntax iterationMethod = BuildExecutionSecurityIterationMethod();

            // Create main class
            ClassDeclarationSyntax executionSecurityType = SyntaxFactory.ClassDeclaration(ExecutionTypeName)
                .WithModifiers(SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.InternalKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .WithMembers(SyntaxFactory.List(new MemberDeclarationSyntax[]
                    {                        
                        timeoutMethod,
                        iterationMethod,
                    }));

            // Create namespace
            NamespaceDeclarationSyntax ns = SyntaxFactory.NamespaceDeclaration(
                SyntaxFactory.QualifiedName(
                    SyntaxFactory.IdentifierName(ExecutionRootNamespace),
                    SyntaxFactory.IdentifierName(ExecutionSubNamespace)))
                .WithMembers(SyntaxFactory.List(new MemberDeclarationSyntax[]
                {
                    timeoutException,
                    iterationException,
                    executionSecurityType,
                }));

            // Normalize white space
            ns = ns.NormalizeWhitespace();

            // Get compilation unit
            cachedSecuritySyntax = SyntaxFactory.CompilationUnit()
                .WithMembers(new SyntaxList<MemberDeclarationSyntax>(
                    ns));

            return cachedSecuritySyntax;
        }

        private static ClassDeclarationSyntax BuildExecutionExceptionType(string exceptionName, string exceptionMessage)
        {
            // Constructor
            ConstructorDeclarationSyntax constructor = SyntaxFactory.ConstructorDeclaration(exceptionName)
                .WithModifiers(SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.InternalKeyword)))
                .WithInitializer(SyntaxFactory.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer,
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(
                                SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, 
                                SyntaxFactory.Literal(exceptionMessage)))))))
                .WithBody(SyntaxFactory.Block());

            // Create class
            return SyntaxFactory.ClassDeclaration(exceptionName)
                .WithModifiers(SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.InternalKeyword)))
                .WithBaseList(SyntaxFactory.BaseList(
                    SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                        SyntaxFactory.SimpleBaseType(
                            SyntaxFactory.ParseTypeName(typeof(Exception).FullName)))))
                .WithMembers(
                    SyntaxFactory.List(new MemberDeclarationSyntax[]
                    {
                        constructor,
                    }));
        }

        private static MethodDeclarationSyntax BuildExecutionSecurityTimeoutMethod()
        {
            string timerName = "elapsedTimer";
            string timeoutName = "timeout";

            // Create timer statement
            IfStatementSyntax createTimerStatement = SyntaxFactory.IfStatement(
                SyntaxFactory.BinaryExpression(SyntaxKind.EqualsExpression,
                    SyntaxFactory.IdentifierName(timerName),
                    SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)),
                    
                SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName(timerName),
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.ParseTypeName(typeof(Stopwatch).FullName),
                                SyntaxFactory.IdentifierName(nameof(Stopwatch.StartNew)))))));

            // Check timeout statement
            IfStatementSyntax checkTimeoutStatement = SyntaxFactory.IfStatement(
                SyntaxFactory.BinaryExpression(SyntaxKind.GreaterThanExpression,
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName(timerName),
                            SyntaxFactory.IdentifierName(nameof(Stopwatch.Elapsed))),
                        SyntaxFactory.IdentifierName(nameof(Stopwatch.Elapsed.TotalSeconds))),
                    SyntaxFactory.IdentifierName(timeoutName)),

                SyntaxFactory.Block(new StatementSyntax[]
                {
                    // Stop the timer
                    SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName(timerName),
                                SyntaxFactory.IdentifierName(nameof(Stopwatch.Stop))))),

                    // Set timer to null
                    SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                            SyntaxFactory.IdentifierName(timerName),
                            SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression))),

                    // Return true
                    SyntaxFactory.ReturnStatement(
                        SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)),
                }));

            // Return statement
            ReturnStatementSyntax returnStatement = SyntaxFactory.ReturnStatement(
                SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression));

            // Build statements
            BlockSyntax body = SyntaxFactory.Block(new StatementSyntax[]
            {
                createTimerStatement,
                checkTimeoutStatement,
                returnStatement,
            });

            // Build parameters
            ParameterListSyntax parameterList = SyntaxFactory.ParameterList(
                SyntaxFactory.SeparatedList(new ParameterSyntax[]
                {
                    SyntaxFactory.Parameter(default,
                        SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.RefKeyword)),
                        SyntaxFactory.ParseTypeName(typeof(Stopwatch).FullName),
                        SyntaxFactory.Identifier(timerName), null),
                    SyntaxFactory.Parameter(default, default,
                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.FloatKeyword)),
                        SyntaxFactory.Identifier(timeoutName), null),
                }));

            // Create method
            MethodDeclarationSyntax method = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword)),
                    ExecutionTimeoutMethodName)
                .WithModifiers(SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)));

            // Build parameters
            return method
                .WithParameterList(parameterList)
                .WithBody(body);
        }

        private static MethodDeclarationSyntax BuildExecutionSecurityIterationMethod()
        {
            string iterationsName = "iterations";
            string maxIterationsName = "maxIterations";

            // Increment iterations
            ExpressionStatementSyntax incrementStatement = SyntaxFactory.ExpressionStatement(
                SyntaxFactory.PostfixUnaryExpression(SyntaxKind.PostIncrementExpression,
                    SyntaxFactory.IdentifierName(iterationsName)));

            // Check too many iterations statement
            IfStatementSyntax checkIterationsStatement = SyntaxFactory.IfStatement(
                SyntaxFactory.BinaryExpression(SyntaxKind.GreaterThanExpression,
                    SyntaxFactory.IdentifierName(iterationsName),
                    SyntaxFactory.IdentifierName(maxIterationsName)),

                SyntaxFactory.Block(new StatementSyntax[]
                {
                    // Return true
                    SyntaxFactory.ReturnStatement(
                        SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)),
                }));

            // Return statement
            ReturnStatementSyntax returnStatement = SyntaxFactory.ReturnStatement(
                SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression));

            // Build statements
            BlockSyntax body = SyntaxFactory.Block(new StatementSyntax[]
            {
                incrementStatement,
                checkIterationsStatement,
                returnStatement,
            });

            // Build parameters
            ParameterListSyntax parameterList = SyntaxFactory.ParameterList(
                SyntaxFactory.SeparatedList(new ParameterSyntax[]
                {
                    SyntaxFactory.Parameter(default,
                        SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.RefKeyword)),
                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)),
                        SyntaxFactory.Identifier(iterationsName), null),
                    SyntaxFactory.Parameter(default, default,
                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)),
                        SyntaxFactory.Identifier(maxIterationsName), null),
                }));

            // Create method
            MethodDeclarationSyntax method = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword)),
                    ExecutionIterationMethodName)
                .WithModifiers(SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)));

            // Build parameters
            return method
                .WithParameterList(parameterList)
                .WithBody(body);
        }
    }
}
