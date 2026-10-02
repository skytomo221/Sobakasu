using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class ExternCandidate
    {
        public string DisplayName { get; }
        public string ExternSignature { get; }
        public bool IsCallable { get; }
        public string RejectionReason { get; }
        public ExternCandidate(
            string displayName,
            string externSignature,
            bool isCallable,
            string rejectionReason)
        {
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            ExternSignature = externSignature ?? throw new ArgumentNullException(nameof(externSignature));
            IsCallable = isCallable;
            RejectionReason = rejectionReason ?? string.Empty;
        }

        public ExternCandidate(object discoveryMember, string externSignature, bool isCallable, string rejectionReason)
            : this(discoveryMember?.ToString() ?? "<unknown>", externSignature, isCallable, rejectionReason)
        {
        }
    }

}
