using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Trivial.CodeSecurity;
using UnityEngine;

namespace RoslynCSharp
{
    /// <summary>
    /// An awaitable object that is returned by async compile operations so you can wait for completion in a coroutine as well as access progress and status information.
    /// Used to wait for an async operation to be completed with a result object.
    /// </summary>
    /// <typeparam name="T">The generic result type</typeparam>
    public class CompileAsync<T> : CompileAsync
    {
        // Private
        private CodeSecurityReport securityReport = null;
        private T result = default;
        private TaskCompletionSource<T> taskSource = null;

        // Properties
        /// <summary>
        /// Get the <see cref="System.Threading.Tasks.Task"/> for this async operation for use in C# async/await contexts.
        /// </summary>
        public new Task<T> Task
        {
            get
            {
                if (taskSource == null)
                {
                    // Create the awaitable task
                    taskSource = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

                    // Check for completed
                    if (IsDone == true)
                    {
                        if (isSuccessful == false)
                        {
                            taskSource.SetResult(result);
                            taskSource.SetException(new Exception(status));
                        }
                        else
                            taskSource.SetResult(result);
                    }
                }
                return taskSource.Task;
            }
        }

        /// <summary>
        /// Get the code security report for the loaded assembly if code validation was performed.
        /// </summary>
        public CodeSecurityReport SecurityReport
        {
            get { return securityReport; }
        }

        /// <summary>
        /// Get the generic result of the async operation. 
        /// </summary>
        public T Result
        {
            get { return result; }
        }

        // Constructor
        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="awaitable">Can this operation be awaited o the main thread, by blocking until the task has completed. This is only possible for requests that run on a background thread, and UnityWebRequest for example cannot support this</param>
        public CompileAsync(bool awaitable = true)
            : base(awaitable)
        {
        }

        // Methods
        /// <summary>
        /// Update the status message for this operation.
        /// Useful to show the current status if a failure occurs.
        /// </summary>
        /// <param name="status">The status message</param>
        protected internal new CompileAsync<T> UpdateStatus(string status)
        {
            base.UpdateStatus(status);
            return this;
        }

        /// <summary>
        /// Update the load progress for this operation.
        /// Calculates the progress as a value from 0-1 based on the input values.
        /// </summary>
        /// <param name="current">The current number of tasks that have been completed</param>
        /// <param name="total">The total number of tasks that should be completed</param>
        protected internal new CompileAsync<T> UpdateProgress(int current, int total)
        {
            base.UpdateProgress(current, total);
            return this;
        }

        /// <summary>
        /// Update the load progress for this operation.
        /// The specified progress value should be in the range of 0-1, and will be clamped if not.
        /// </summary>
        /// <param name="progress">The current progress value between 0-1</param>
        protected internal new CompileAsync<T> UpdateProgress(float progress)
        {
            base.UpdateProgress(progress);
            return this;
        }

        /// <summary>
        /// Reset the async operation.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            taskSource = null;
        }

        /// <summary>
        /// Complete the operation with an error status.
        /// This will cause <see cref="CompileAsync.IsDone"/> to become true and <see cref="CompileAsync.Progress"/> to become 1.
        /// </summary>
        /// <param name="status">The error message for the failure</param>
        /// <param name="compileResult">The compile result for the request</param>
        /// <param name="securityReport">The code security report if code security validation was performed</param>
        public void Error(string status, CompileResult compileResult, CodeSecurityReport securityReport)
        {
            this.securityReport = securityReport;
            this.result = default;
            base.Error(status, compileResult);

            // Check for task
            if (taskSource != null)
            {
                taskSource.SetResult(default);
                taskSource.SetException(new Exception(status));                
            }
        }

        /// <summary>
        /// Complete the operation with the specified success status.
        /// This will cause <see cref="CompileAsync.IsDone"/> to become true and <see cref="CompileAsync.Progress"/> to become 1.
        /// </summary>
        /// <param name="success">Was the operation completed successfully</param>
        /// <param name="compileResult">The compile result for the request</param>
        /// <param name="securityReport">The code security report if code security validation was performed</param>
        /// <param name="result">An optional result object</param>
        public void Complete(bool success, CompileResult compileResult, CodeSecurityReport securityReport, T result)
        {
            this.securityReport = securityReport;
            this.result = result;
            base.Complete(success, compileResult);

            // Check for task
            if (taskSource != null)
                taskSource.SetResult(result);
        }
    }

    /// <summary>
    /// An awaitable object that is returned by async compile operations so you can wait for completion in a coroutine as well as access progress and status information.
    /// Used to wait until an async operation has been completed
    /// </summary>
    public class CompileAsync : IEnumerator
    {
        // Internal
        internal bool awaitable = true;

        // Private
        private TaskCompletionSource<CompileResult> taskSource = null;

        // Protected
        /// <summary>
        /// Was the operation successful or did something go wrong.
        /// </summary>
        protected bool isSuccessful = false;
        /// <summary>
        /// Get the current status of the async operation.
        /// </summary>
        protected string status = string.Empty;

        // Private
        private float progress = 0;
        private bool isDone = false;
        private CompileResult compileResult = null;

        // Properties
        /// <summary>
        /// Get the <see cref="System.Threading.Tasks.Task"/> for this async operation for use in C# async/await contexts.
        /// </summary>
        public Task<CompileResult> Task
        {
            get
            {
                if (taskSource == null)
                {
                    // Create the awaitable task
                    taskSource = new TaskCompletionSource<CompileResult>(TaskCreationOptions.RunContinuationsAsynchronously);

                    // Check for completed
                    if (IsDone == true)
                    {
                        if (isSuccessful == false)
                        {
                            taskSource.SetResult(compileResult);
                            taskSource.SetException(new Exception(status));
                        }
                        else
                            taskSource.SetResult(compileResult);
                    }
                }
                return taskSource.Task;
            }
        }

        /// <summary>
        /// Get the current status of the async operation.
        /// </summary>
        public string Status
        {
            get { return status; }
            protected internal set { status = value; }
        }

        /// <summary>
        /// Get the current progress of the async operation.
        /// This is a normalized value between 0-1.
        /// </summary>
        public float Progress
        {
            get { return progress; }
            protected internal set { progress = Mathf.Clamp01(value); }
        }

        /// <summary>
        /// Get the current progress percentage of the async operation.
        /// </summary>
        public int ProgressPercentage
        {
            get { return Mathf.RoundToInt(progress * 100f); }
        }

        /// <summary>
        /// Returns true if the async operation has finished or false if it is still running.
        /// </summary>
        public bool IsDone
        {
            get { return isDone; }
        }

        /// <summary>
        /// Get the compile result for this request.
        /// </summary>
        public CompileResult CompileResult
        {
            get { return compileResult; }
        }

        /// <summary>
        /// Returns true if the async operation completed successfully or false if an error occurred.
        /// </summary>
        public bool IsSuccessful
        {
            get { return isSuccessful; }
        }

        /// <summary>
        /// IEnumerator.Current implementation.
        /// </summary>
        public object Current
        {
            get { return null; }
        }

        // Constructor
        /// <summary>
        /// Create new operation.
        /// </summary>
        /// <param name="awaitable">Can this operation be awaited o the main thread, by blocking until the task has completed. This is only possible for requests that run on a background thread, and UnityWebRequest for example cannot support this</param>
        public CompileAsync(bool awaitable = true)
        {
            this.awaitable = awaitable;
        }

        // Methods
        /// <summary>
        /// Called when the async operation can perform some logic.
        /// </summary>
        protected virtual void UpdateTasks() { }

        /// <summary>
        /// IEnumerator.MoveNext() implementation.
        /// </summary>
        /// <returns>True if the enumerator advanced successfully or false if not</returns>
        public bool MoveNext()
        {
            if (IsDone == false)
            {
                // Advance the enumerator (continue waiting)
                return true;
            }

            // Task is finished
            return false;
        }

        /// <summary>
        /// IEnumerator.Reset() implementation.
        /// </summary>
        public virtual void Reset()
        {
            taskSource = null;
            status = string.Empty;
            progress = 0;
            isDone = false;
        }

        /// <summary>
        /// Block the main thread until the async operation has completed.
        /// Use with caution. This can cause an infinite loop if the async operation never completes, if the operation is not true async, or if the async operation relies on data from the main thread.
        /// </summary>
        /// <exception cref="TimeoutException">The await operation took longer that the specified timeout milliseconds, so was aborted to avoid infinite waiting</exception>
        public void Await(long msTimeout = 10000)
        {
            // Check for already done
            if (isDone == true)
                return;

            // Check for awaitable
            if (awaitable == false)
                throw new InvalidOperationException("Compile async await caller does not support waiting on the main thread. You should use the equivalent async API for this call");

            // Get current time
            DateTime start = DateTime.Now;

            // Wait until complete
            while (isDone == false)
            {
                // Check if we have timed out
                if (msTimeout > 0 && (DateTime.Now - start).TotalMilliseconds > msTimeout)
                    throw new TimeoutException("Compile async await call was aborted because the operation timed out");

                // Block the thread
                Thread.Sleep(1);
            }
        }

        /// <summary>
        /// Update the status message for this operation.
        /// Useful to show the current status if a failure occurs.
        /// </summary>
        /// <param name="status">The status message</param>
        protected internal CompileAsync UpdateStatus(string status)
        {
            this.status = status;

            if (this.status == null)
                this.status = string.Empty;

            return this;
        }

        /// <summary>
        /// Update the load progress for this operation.
        /// Calculates the progress as a value from 0-1 based on the input values.
        /// </summary>
        /// <param name="current">The current number of tasks that have been completed</param>
        /// <param name="total">The total number of tasks that should be completed</param>
        protected internal CompileAsync UpdateProgress(int current, int total)
        {
            this.progress = Mathf.InverseLerp(0, total, current);
            return this;
        }

        /// <summary>
        /// Update the load progress for this operation.
        /// The specified progress value should be in the range of 0-1, and will be clamped if not.
        /// </summary>
        /// <param name="progress">The current progress value between 0-1</param>
        protected internal CompileAsync UpdateProgress(float progress)
        {
            this.progress = Mathf.Clamp01(progress);
            return this;
        }

        /// <summary>
        /// Complete the operation with an error status.
        /// This will cause <see cref="IsDone"/> to become true and <see cref="Progress"/> to become 1.
        /// </summary>
        /// <param name="status">The error message for the failure</param>
        /// <param name="compileResult">The compile result for the request</param>
        public void Error(string status, CompileResult compileResult)
        {
            this.isSuccessful = false;
            this.status = status;
            this.progress = 1f;
            this.isDone = true;
            this.compileResult = compileResult;

            // Complete task
            if (taskSource != null)
            {
                taskSource.SetResult(null);
                taskSource.SetException(new Exception(status));
            }
        }

        /// <summary>
        /// Complete the operation with the specified success status.
        /// This will cause <see cref="IsDone"/> to become true and <see cref="Progress"/> to become 1.
        /// </summary>
        /// <param name="success">Was the operation completed successfully</param>
        /// <param name="compileResult">The compile result for the request</param>
        public void Complete(bool success, CompileResult compileResult)
        {
            this.isSuccessful = success;
            this.progress = 1f;
            this.isDone = true;
            this.compileResult = compileResult;

            // Complete the task
            if (taskSource != null)
                taskSource.SetResult(compileResult);
        }
    }
}
