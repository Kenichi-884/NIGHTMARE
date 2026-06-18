using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;

namespace RoslynCSharp.ExecutionSecurity
{
    /// <summary>
    /// Used to post process syntax trees in order to inject execution security checks for loop statements.
    /// </summary>
    public sealed class ExecutionSecurityProcessor : IParserProcessor
    {
        // Private
        private readonly ExecutionSecuritySettings securitySettings;

        // Properties
        /// <summary>
        /// The priority of this processor.
        /// Should run quite late if not last
        /// </summary>
        public int Priority => 1000;

        // Constructor
        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="securitySettings">The execution security settings to use</param>
        public ExecutionSecurityProcessor(ExecutionSecuritySettings securitySettings)
        {
            this.securitySettings = securitySettings;
        }

        // Methods
        /// <summary>
        /// Called by the compilation pipeline when post processing should run on the provided syntax trees.
        /// </summary>
        /// <param name="syntaxTrees">The syntax trees to postprocess</param>
        /// <returns>The new syntax trees with security checks injected</returns>
        public SyntaxTree[] OnPostProcess(SyntaxTree[] syntaxTrees)
        {
            // Check for execution settings disabled
            if (securitySettings.SecurityMode == ExecutionSecurityMode.None)
                return syntaxTrees;

            // Get first compilation options
            CSharpParseOptions parseOptions = syntaxTrees.First().Options as CSharpParseOptions;

            // Create patcher
            ExecutionSecurityPatcher patcher = new(securitySettings);

            for (int i = 0; i < syntaxTrees.Length; i++)
            {
                // Visit the tree and insert security checks into looping statements
                syntaxTrees[i] = patcher.Visit(syntaxTrees[i].GetRoot())
                    .NormalizeWhitespace()
                    .SyntaxTree;
            }

            // Create the new tree
            SyntaxTree securityTree = CSharpSyntaxTree.Create(
                ExecutionSecurityBuilder.BuildExecutionSecurityNode(),
                parseOptions);

            // Build array with extra tree
            return syntaxTrees
                .Append(securityTree)
                .ToArray();
        }
    }
}
