﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.AspNetCore.Server.Kestrel.Transport.Windows.Rio.Internal
{
    public struct RioRequestQueue
    {
#pragma warning disable 0169, 0649
        private IntPtr _handle;
#pragma warning restore 0169, 0649

        public void QueueSend(ref RioBufferSegment rioBuffer)
        {
            RioFunctions.QueueSend(this, ref rioBuffer);
        }

        public void CommitSend(CompletionPort sendCommitPort, ref RioBufferSegment rioBuffer, long requestCorrelation)
        {
            RioFunctions.CommitSend(this, ref rioBuffer, requestCorrelation);
            RioFunctions.PostQueuedCompletionStatus(sendCommitPort, 0, CompletionEventType.SendCommit, (IntPtr)this);
        }

        public void FlushSends()
        {
            RioFunctions.FlushSends(this);
        }

        public void Receive(ref RioBufferSegment rioBuffer)
        {
            RioFunctions.Receive(this, ref rioBuffer);
        }

        public static explicit operator IntPtr(RioRequestQueue queue)
        {
            return queue._handle;
        }

        public static explicit operator RioRequestQueue(IntPtr queue)
        {
            return new RioRequestQueue() { _handle = queue };
        }
    }
}
