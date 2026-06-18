
namespace RoslynCSharp
{
    /// <summary>
    /// Called by the compiler when compilation has completed.
    /// Allows for additional IL processing to occur after the compilation request.
    /// </summary>
    public interface ICompilationProcessor
    {
        // Properties
        /// <summary>
        /// Get the processor priority in the case that multiple processors are used.
        /// Default priority is 100 and implementation with a higher priority will run after those with a lower priority.
        /// </summary>
        int Priority { get; }

        // Methods
        /// <summary>
        /// Called by the compiler with the compiled assembly.
        /// </summary>
        /// <param name="assembly">The assembly that was compiled from the request</param>
        /// <returns>A modified/IL post processed compilation or the same assembly if no modification needs to take place</returns>
        AssemblySource OnPostProcess(AssemblySource assembly);
    }
}
