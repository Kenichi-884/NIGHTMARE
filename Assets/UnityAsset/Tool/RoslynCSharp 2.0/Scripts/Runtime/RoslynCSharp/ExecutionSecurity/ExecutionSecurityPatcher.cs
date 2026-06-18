using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RoslynCSharp.ExecutionSecurity
{
    internal sealed class ExecutionSecurityPatcher : CSharpSyntaxRewriter
    {
        // Private
        private readonly ExecutionSecuritySettings securitySettings;

        // Constructor
        public ExecutionSecurityPatcher(ExecutionSecuritySettings securitySettings)
        {
            this.securitySettings = securitySettings;
        }

        // Methods
        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            // Get for loops
            IEnumerable<ForStatementSyntax> forStatements = node
                .DescendantNodes()
                .OfType<ForStatementSyntax>();

            // Check for any for
            foreach(ForStatementSyntax forStatement in forStatements)
            {
                // Insert for check
                node = InsertForStatementSecurityChecks(node, forStatement);
            }

            // Get foreach loops
            IEnumerable<ForEachStatementSyntax> foreachStatements = node
                .DescendantNodes()
                .OfType<ForEachStatementSyntax>();

            // Check foreach
            foreach(ForEachStatementSyntax foreachStatement in foreachStatements)
            {
                // Insert foreach check
                node = InsertForeachStatementSecurityChecks(node, foreachStatement);
            }

            // Get while loops
            IEnumerable<WhileStatementSyntax> whileStatements = node
                .DescendantNodes()
                .OfType<WhileStatementSyntax>();

            // Check for while
            foreach(WhileStatementSyntax whileStatement in whileStatements)
            {
                // Insert while check
                node = InsertWhileStatementSecurityChecks(node, whileStatement);
            }

            // Get do while loops
            IEnumerable<DoStatementSyntax> doStatements = node
                .DescendantNodes()
                .OfType<DoStatementSyntax>();

            // Check for do
            foreach(DoStatementSyntax doStatement in doStatements)
            {
                // Insert do check
                node = InsertDoStatementSecurityChecks(node, doStatement);
            }

            return node;
        }

        private MethodDeclarationSyntax InsertForStatementSecurityChecks(MethodDeclarationSyntax methodDeclaration, ForStatementSyntax forStatement)
        {
            // Timeout syntax
            LocalDeclarationStatementSyntax timeOutVariable = null;
            StatementSyntax timeOutCheck = null;

            // Iteration syntax
            LocalDeclarationStatementSyntax iterationVariable = null;
            StatementSyntax iterationCheck = null;

            // Check for supported options
            bool hasTimeout = (securitySettings.SecurityMode & ExecutionSecurityMode.ExecutionTimeout) != 0;
            bool hasIteration = (securitySettings.SecurityMode & ExecutionSecurityMode.ExecutionIterations) != 0;

            // Check for none - use input method with no changes required
            if (hasTimeout == false && hasIteration == false)
                return methodDeclaration;

            // Check for timeout
            if (hasTimeout == true)
            {
                // Build the timeout expression
                timeOutCheck = BuildTimeoutInvokeExpression(securitySettings.TimeoutSeconds, out timeOutVariable);                
            }

            // Check for iterations
            if (hasIteration == true)
            {
                // Build the iteration expression
                iterationCheck = BuildIterationInvokeExpression(securitySettings.TimeoutIterations, out iterationVariable);
            }

            // Create the statements
            SyntaxList<StatementSyntax> forStatements = forStatement.Statement is BlockSyntax block
                ? block.Statements
                : new SyntaxList<StatementSyntax>(new[] { forStatement.Statement });

            // Insert statements
            if (hasTimeout == true)
                forStatements = forStatements.Insert(0, timeOutCheck);

            if (hasIteration == true)
                forStatements = forStatements.Insert(0, iterationCheck);

            // Create new modified block
            BlockSyntax forBlock = SyntaxFactory.Block(forStatements);

            // Replace for
            methodDeclaration = methodDeclaration.ReplaceNode(forStatement,
                forStatement.WithStatement(forBlock));

            // Create method statements
            SyntaxList<StatementSyntax> methodStatements = methodDeclaration.Body.Statements;

            // Insert statements
            if(hasTimeout == true)
                methodStatements = methodStatements.Insert(0, timeOutVariable);

            if (hasIteration == true)
                methodStatements = methodStatements.Insert(0, iterationVariable);

            // Add the variable to the method
            methodDeclaration = methodDeclaration.WithBody(
                SyntaxFactory.Block(methodStatements));

            return methodDeclaration;
        }

        private MethodDeclarationSyntax InsertForeachStatementSecurityChecks(MethodDeclarationSyntax methodDeclaration, ForEachStatementSyntax foreachStatement)
        {
            // Timeout syntax
            LocalDeclarationStatementSyntax timeOutVariable = null;
            StatementSyntax timeOutCheck = null;

            // Iteration syntax
            LocalDeclarationStatementSyntax iterationVariable = null;
            StatementSyntax iterationCheck = null;

            // Check for supported options
            bool hasTimeout = (securitySettings.SecurityMode & ExecutionSecurityMode.ExecutionTimeout) != 0;
            bool hasIteration = (securitySettings.SecurityMode & ExecutionSecurityMode.ExecutionIterations) != 0;

            // Check for none - use input method with no changes required
            if (hasTimeout == false && hasIteration == false)
                return methodDeclaration;

            // Check for timeout
            if (hasTimeout == true)
            {
                // Build the timeout expression
                timeOutCheck = BuildTimeoutInvokeExpression(securitySettings.TimeoutSeconds, out timeOutVariable);
            }

            // Check for iterations
            if (hasIteration == true)
            {
                // Build the iteration expression
                iterationCheck = BuildIterationInvokeExpression(securitySettings.TimeoutIterations, out iterationVariable);
            }

            // Create the statements
            SyntaxList<StatementSyntax> foreachStatements = foreachStatement.Statement is BlockSyntax block
                ? block.Statements
                : new SyntaxList<StatementSyntax>(new[] { foreachStatement.Statement });

            // Insert statements
            if (hasTimeout == true)
                foreachStatements = foreachStatements.Insert(0, timeOutCheck);

            if (hasIteration == true)
                foreachStatements = foreachStatements.Insert(0, iterationCheck);

            // Create new modified block
            BlockSyntax foreachBlock = SyntaxFactory.Block(foreachStatements);

            // Replace foreach
            methodDeclaration = methodDeclaration.ReplaceNode(foreachStatement,
                foreachStatement.WithStatement(foreachBlock));

            // Create method statements
            SyntaxList<StatementSyntax> methodStatements = methodDeclaration.Body.Statements;

            // Insert statements
            if (hasTimeout == true)
                methodStatements = methodStatements.Insert(0, timeOutVariable);

            if (hasIteration == true)
                methodStatements = methodStatements.Insert(0, iterationVariable);

            // Add the variable to the method
            methodDeclaration = methodDeclaration.WithBody(
                SyntaxFactory.Block(methodStatements));

            return methodDeclaration;
        }

        private MethodDeclarationSyntax InsertWhileStatementSecurityChecks(MethodDeclarationSyntax methodDeclaration, WhileStatementSyntax whileStatement)
        {
            // Timeout syntax
            LocalDeclarationStatementSyntax timeOutVariable = null;
            StatementSyntax timeOutCheck = null;

            // Iteration syntax
            LocalDeclarationStatementSyntax iterationVariable = null;
            StatementSyntax iterationCheck = null;

            // Check for supported options
            bool hasTimeout = (securitySettings.SecurityMode & ExecutionSecurityMode.ExecutionTimeout) != 0;
            bool hasIteration = (securitySettings.SecurityMode & ExecutionSecurityMode.ExecutionIterations) != 0;

            // Check for none - use input method with no changes required
            if (hasTimeout == false && hasIteration == false)
                return methodDeclaration;

            // Check for timeout
            if (hasTimeout == true)
            {
                // Build the timeout expression
                timeOutCheck = BuildTimeoutInvokeExpression(securitySettings.TimeoutSeconds, out timeOutVariable);
            }

            // Check for iterations
            if (hasIteration == true)
            {
                // Build the iteration expression
                iterationCheck = BuildIterationInvokeExpression(securitySettings.TimeoutIterations, out iterationVariable);
            }

            // Create the statements
            SyntaxList<StatementSyntax> whileStatements = whileStatement.Statement is BlockSyntax block
                ? block.Statements
                : new SyntaxList<StatementSyntax>(new[] { whileStatement.Statement });

            // Insert statements
            if (hasTimeout == true)
                whileStatements = whileStatements.Insert(0, timeOutCheck);

            if (hasIteration == true)
                whileStatements = whileStatements.Insert(0, iterationCheck);

            // Create new modified block
            BlockSyntax whileBlock = SyntaxFactory.Block(whileStatements);

            // Replace while
            methodDeclaration = methodDeclaration.ReplaceNode(whileStatement,
                whileStatement.WithStatement(whileBlock));

            // Create method statements
            SyntaxList<StatementSyntax> methodStatements = methodDeclaration.Body.Statements;

            // Insert statements
            if (hasTimeout == true)
                methodStatements = methodStatements.Insert(0, timeOutVariable);

            if (hasIteration == true)
                methodStatements = methodStatements.Insert(0, iterationVariable);

            // Add the variable to the method
            methodDeclaration = methodDeclaration.WithBody(
                SyntaxFactory.Block(methodStatements));

            return methodDeclaration;
        }

        private MethodDeclarationSyntax InsertDoStatementSecurityChecks(MethodDeclarationSyntax methodDeclaration, DoStatementSyntax doStatement)
        {
            // Timeout syntax
            LocalDeclarationStatementSyntax timeOutVariable = null;
            StatementSyntax timeOutCheck = null;

            // Iteration syntax
            LocalDeclarationStatementSyntax iterationVariable = null;
            StatementSyntax iterationCheck = null;

            // Check for supported options
            bool hasTimeout = (securitySettings.SecurityMode & ExecutionSecurityMode.ExecutionTimeout) != 0;
            bool hasIteration = (securitySettings.SecurityMode & ExecutionSecurityMode.ExecutionIterations) != 0;

            // Check for none - use input method with no changes required
            if (hasTimeout == false && hasIteration == false)
                return methodDeclaration;

            // Check for timeout
            if (hasTimeout == true)
            {
                // Build the timeout expression
                timeOutCheck = BuildTimeoutInvokeExpression(securitySettings.TimeoutSeconds, out timeOutVariable);
            }

            // Check for iterations
            if (hasIteration == true)
            {
                // Build the iteration expression
                iterationCheck = BuildIterationInvokeExpression(securitySettings.TimeoutIterations, out iterationVariable);
            }

            // Create the statements
            SyntaxList<StatementSyntax> whileStatements = doStatement.Statement is BlockSyntax block
                ? block.Statements
                : new SyntaxList<StatementSyntax>(new[] { doStatement.Statement });

            // Insert statements
            if (hasTimeout == true)
                whileStatements = whileStatements.Insert(0, timeOutCheck);

            if (hasIteration == true)
                whileStatements = whileStatements.Insert(0, iterationCheck);

            // Create new modified block
            BlockSyntax whileBlock = SyntaxFactory.Block(whileStatements);

            // Replace while
            methodDeclaration = methodDeclaration.ReplaceNode(doStatement,
                doStatement.WithStatement(whileBlock));

            // Create method statements
            SyntaxList<StatementSyntax> methodStatements = methodDeclaration.Body.Statements;

            // Insert statements
            if (hasTimeout == true)
                methodStatements = methodStatements.Insert(0, timeOutVariable);

            if (hasIteration == true)
                methodStatements = methodStatements.Insert(0, iterationVariable);

            // Add the variable to the method
            methodDeclaration = methodDeclaration.WithBody(
                SyntaxFactory.Block(methodStatements));

            return methodDeclaration;
        }

        private StatementSyntax BuildTimeoutInvokeExpression(float timeout, out LocalDeclarationStatementSyntax localVariable)
        {
            // Generate variable name
            string variableName = GetVariableName();

            // Create local
            localVariable = BuildLocalVariableDeclaration(typeof(Stopwatch), variableName);

            // Create call
            InvocationExpressionSyntax invokeExpression = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ParseTypeName(ExecutionSecurityBuilder.ExecutionNamespace + "." + ExecutionSecurityBuilder.ExecutionTypeName),
                    SyntaxFactory.IdentifierName(ExecutionSecurityBuilder.ExecutionTimeoutMethodName)),
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SeparatedList(new[] {
                            SyntaxFactory.Argument(null, SyntaxFactory.Token(SyntaxKind.RefKeyword),
                                SyntaxFactory.IdentifierName(variableName)),
                            SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, 
                                SyntaxFactory.Literal(timeout))) })));

            // Create if statement
            IfStatementSyntax invokeCondition = SyntaxFactory.IfStatement(invokeExpression,
                    SyntaxFactory.ThrowStatement(
                            SyntaxFactory.ObjectCreationExpression(
                                SyntaxFactory.ParseTypeName(ExecutionSecurityBuilder.ExecutionNamespace + "." + ExecutionSecurityBuilder.ExecutionTimeoutExceptionTypeName),
                                    SyntaxFactory.ArgumentList(), null)));

            // Fix whitespace
            return invokeCondition.NormalizeWhitespace();
        }

        private StatementSyntax BuildIterationInvokeExpression(int maxIterations, out LocalDeclarationStatementSyntax localVariable)
        {
            // Generate variable name
            string variableName = GetVariableName();

            // Create local
            localVariable = BuildLocalVariableDeclaration(typeof(int), variableName);

            // Create call
            InvocationExpressionSyntax invokeExpression = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ParseTypeName(ExecutionSecurityBuilder.ExecutionNamespace + "." + ExecutionSecurityBuilder.ExecutionTypeName),
                    SyntaxFactory.IdentifierName(ExecutionSecurityBuilder.ExecutionIterationMethodName)),
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SeparatedList(new[] {
                            SyntaxFactory.Argument(null, SyntaxFactory.Token(SyntaxKind.RefKeyword),
                                SyntaxFactory.IdentifierName(variableName)),
                            SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,
                                SyntaxFactory.Literal(maxIterations))) })));

            // Create if statement
            IfStatementSyntax invokeCondition = SyntaxFactory.IfStatement(invokeExpression,
                    SyntaxFactory.ThrowStatement(
                            SyntaxFactory.ObjectCreationExpression(
                                SyntaxFactory.ParseTypeName(ExecutionSecurityBuilder.ExecutionNamespace + "." + ExecutionSecurityBuilder.ExecutionIterationExceptionTypeName),
                                    SyntaxFactory.ArgumentList(), null)));

            // Fix whitespace
            return invokeCondition.NormalizeWhitespace();
        }

        private LocalDeclarationStatementSyntax BuildLocalVariableDeclaration(Type variableType, string variableName)
        {
            // Assign expression
            ExpressionSyntax assignExpression = SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression);

            // Create variable declaration
            LocalDeclarationStatementSyntax localVariable = SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.ParseTypeName(variableType.FullName),
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.VariableDeclarator(variableName)
                            .WithInitializer(
                                SyntaxFactory.EqualsValueClause(assignExpression)))));

            // Fix whitespace
            return localVariable.NormalizeWhitespace();
        }

        private string GetVariableName()
        {
            return "_" + Guid.NewGuid().ToString()
                .Replace("-", "");
        }
    }
}
