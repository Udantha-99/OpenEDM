using System;
using System.Collections.Generic;

namespace OpenEDMShellExtension.Core
{
    /// <summary>
    /// Immutable result object returned by every IO operation.
    /// Encapsulates success/failure state, messages, and affected file paths.
    /// </summary>
    public sealed class OperationResult
    {
        public bool Success { get; }
        public string Message { get; }
        public IReadOnlyList<string> AffectedFiles { get; }
        public IReadOnlyList<string> SkippedFiles { get; }
        public IReadOnlyList<string> Errors { get; }

        private OperationResult(
            bool success,
            string message,
            IReadOnlyList<string> affectedFiles,
            IReadOnlyList<string> skippedFiles,
            IReadOnlyList<string> errors)
        {
            Success = success;
            Message = message ?? string.Empty;
            AffectedFiles = affectedFiles ?? Array.Empty<string>();
            SkippedFiles = skippedFiles ?? Array.Empty<string>();
            Errors = errors ?? Array.Empty<string>();
        }

        public static OperationResult Ok(string message, List<string> affectedFiles)
        {
            return new OperationResult(true, message, affectedFiles?.AsReadOnly(), null, null);
        }

        public static OperationResult Partial(
            string message,
            List<string> affectedFiles,
            List<string> skippedFiles,
            List<string> errors)
        {
            return new OperationResult(
                false, message,
                affectedFiles?.AsReadOnly(),
                skippedFiles?.AsReadOnly(),
                errors?.AsReadOnly());
        }

        public static OperationResult Fail(string message)
        {
            return new OperationResult(false, message, null, null, new List<string> { message }.AsReadOnly());
        }

        public static OperationResult Fail(string message, List<string> errors)
        {
            return new OperationResult(false, message, null, null, errors?.AsReadOnly());
        }
    }
}
