// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

namespace Microsoft.AspNetCore.Server.Kestrel.Transport.WindowsRio.Internal
{
    internal unsafe static class RioFunctions
    {
        public static void Initalize()
        {
            // triggers the static .cctor
            NativeMethods.Initalize();
        }

        public static Event CreateEvent()
        {
            var @event = NativeMethods.WSACreateEvent();
            //if (vent.IsNull)
            //{
            //    var error = GetLastError();
            //    NativeMethods.WSACleanup();
            //    throw new Exception($"ERROR: WSACreateEvent returned {error}");
            //}

            return @event;
        }

        public static void CloseEvent(Event @event)
        {
            NativeMethods.WSACloseEvent(@event);
            //if (vent.IsNull)
            //{
            //    var error = GetLastError();
            //    NativeMethods.WSACleanup();
            //    throw new Exception($"ERROR: WSACreateEvent returned {error}");
            //}
        }

        public static RioRegisteredBuffer RegisterBuffer(IntPtr dataBuffer, uint dataLength)
        {
            return NativeMethods.RioRegisterBuffer(dataBuffer, dataLength);
        }

        public static void DeregisterBuffer(RioRegisteredBuffer buffer)
        {
            NativeMethods.RioDeregisterBuffer(buffer);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void QueueSend(RioRequestQueue socketQueue, ref RioBufferSegment rioBuffer)
        {
            ThrowIfErrored(NativeMethods.RioSend(socketQueue, ref rioBuffer, 1, RioSendFlags.Defer | RioSendFlags.DontNotify, -1), RioException.ActionType.Send);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Send(RioRequestQueue socketQueue, ref RioBufferSegment rioBuffer)
        {
            ThrowIfErrored(NativeMethods.RioSend(socketQueue, ref rioBuffer, 1, RioSendFlags.Defer, -1), RioException.ActionType.Send);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SendCommit(RioRequestQueue socketQueue, ref RioBufferSegment rioBuffer)
        {
            ThrowIfErrored(NativeMethods.RioSend(socketQueue, ref rioBuffer, 1, RioSendFlags.None, -1), RioException.ActionType.Send);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void FlushSends(RioRequestQueue socketQueue)
        {
            ThrowIfErrored(NativeMethods.RioSendCommit(socketQueue, (IntPtr)null, 0, RioSendFlags.CommitOnly, 0), RioException.ActionType.Send);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Receive(RioRequestQueue socketQueue, ref RioBufferSegment rioBuffer)
        {
            ThrowIfErrored(NativeMethods.RioReceive(socketQueue, ref rioBuffer, 1, RioReceiveFlags.None, 1), RioException.ActionType.Receive);
        }


        public static RioListenSocket CreateListenSocket()
        {
            var socket = NativeMethods.WSASocket(AddressFamilies.Internet, SocketType.Stream, Protocol.IpProtocolTcp, IntPtr.Zero, 0, SocketFlags.RegisteredIO);
            if (socket.IsNull)
            {
                var error = NativeMethods.WSAGetLastError();
                NativeMethods.WSACleanup();
                throw new Exception(string.Format("ERROR: WSASocket returned {0}", error));
            }

            return socket;
        }

        public static unsafe void SetTcpNodelay(RioListenSocket socket, bool enable)
        {
            const int TcpNodelay = 0x0001;
            const int IPPROTO_TCP = 6;

            var value = 0;
            if (enable)
            {
                value = -1;
            }
            NativeMethods.setsockopt(socket, IPPROTO_TCP, TcpNodelay, (char*)&value, 4);
        }


        public static unsafe void SetTcpNodelay(RioConnectedSocket socket, bool enable)
        {
            const int TcpNodelay = 0x0001;
            const int IPPROTO_TCP = 6;

            var value = 0;
            if (enable)
            {
                value = -1;
            }
            NativeMethods.setsockopt(socket, IPPROTO_TCP, TcpNodelay, (char*)&value, 4);
        }

        public static IPEndPoint GetSockIPEndPoint(RioConnectedSocket socket)
        {
            SockAddr socketAddress;
            int namelen = Marshal.SizeOf<SockAddr>();
            ThrowIfErrored(NativeMethods.getsockname(socket, out socketAddress, ref namelen), RioException.ActionType.GetPeerIPEndPoint);

            return socketAddress.GetIPEndPoint();
        }

        public static IPEndPoint GetSockIPEndPoint(RioListenSocket socket)
        {
            SockAddr socketAddress;
            int namelen = Marshal.SizeOf<SockAddr>();
            ThrowIfErrored(NativeMethods.getsockname(socket, out socketAddress, ref namelen), RioException.ActionType.GetPeerIPEndPoint);

            return socketAddress.GetIPEndPoint();
        }

        public static IPEndPoint GetPeerIPEndPoint(RioConnectedSocket socket)
        {
            SockAddr socketAddress;
            int namelen = Marshal.SizeOf<SockAddr>();
            ThrowIfErrored(NativeMethods.getpeername(socket, out socketAddress, ref namelen), RioException.ActionType.GetPeerIPEndPoint);

            return socketAddress.GetIPEndPoint();
        }

        public static RioConnectedSocket AcceptSocket(RioListenSocket socket)
        {
            var acceptSocket = NativeMethods.accept(socket, IntPtr.Zero, 0);

            if (acceptSocket.IsInvalid)
            {
                var error = NativeMethods.WSAGetLastError();
                NativeMethods.WSACleanup();
                throw new Exception($"accept failed with {error}");
            }

            return acceptSocket;
        }

        public static void BindSocket(RioListenSocket socket, IPEndPoint endPoint)
        {
            var socketAddress = IPEndPointExtensions.Serialize(endPoint);

            var result = NativeMethods.bind(socket, ref socketAddress.Buffer[0], socketAddress.Size);

            if (result != 0)
            {
                var error = NativeMethods.WSAGetLastError();
                NativeMethods.WSACleanup();
                throw new Exception($"bind failed with {error}");
            }
        }

        public static void Listen(RioListenSocket socket, int listenBacklog)
        {
            var result = NativeMethods.listen(socket, listenBacklog);

            if (result != 0)
            {
                var error = NativeMethods.WSAGetLastError();
                NativeMethods.WSACleanup();
                throw new Exception($"listen failed with {error}");
            }
        }

        public static void CloseSocket(RioListenSocket socket)
        {
            ThrowIfErrored(NativeMethods.closesocket(socket), RioException.ActionType.CloseSocket);
        }

        public static void CloseSocket(RioConnectedSocket socket)
        {
            ThrowIfErrored(NativeMethods.closesocket(socket), RioException.ActionType.CloseSocket);
        }

        public enum NotificationCompletionType : int
        {
            Polling = 0,
            EventCompletion = 1,
            IocpCompletion = 2
        }


        [StructLayout(LayoutKind.Sequential)]
        public struct NotificationEvent
        {
            public Event EventHandle;
            public bool NotifyReset;
            public IntPtr Filler;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NotificationCompletionEvent
        {
            public NotificationCompletionType Type;
            public NotificationEvent Event;
        }

        public static RioCompletionQueue CreateCompletionQueue(Event @event, uint queueSize)
        {
            var completionMethod = new NotificationCompletionEvent
            {
                Type = NotificationCompletionType.EventCompletion,
                Event = new NotificationEvent
                {
                    EventHandle = @event,
                    NotifyReset = true
                }
            };

            var completionQueue = NativeMethods.RioCreateCompletionQueueEvent(queueSize, completionMethod);

            if (completionQueue.IsNull)
            {
                var error = NativeMethods.WSAGetLastError();
                NativeMethods.WSACleanup();
                throw new Exception($"ERROR: RioCreateCompletionQueue returned {error}");
            }

            return completionQueue;
        }

        public static void CloseCompletionQueue(RioCompletionQueue completionQueue)
        {
            NativeMethods.RioCloseCompletionQueue(completionQueue);
        }

        public static RioRequestQueue CreateRequestQueue(
            RioConnectedSocket socket,
            RioCompletionQueue receiveQueue,
            RioCompletionQueue sendQueue,
            uint maxOutstandingSends
        )
        {

            var queue = NativeMethods.RioCreateRequestQueue(
                socket,
                maxOutstandingReceive: 1,
                maxReceiveDataBuffers: 1,
                maxOutstandingSend: maxOutstandingSends,
                maxSendDataBuffers: 1,
                receiveCq: receiveQueue,
                sendCq: sendQueue,
                connectionCorrelation: 0);

            if (queue.IsNull)
            {
                var error = NativeMethods.WSAGetLastError();
                NativeMethods.WSACleanup();
                throw new Exception($"ERROR: RioCreateCompletionQueue returned {error}");
            }

            return queue;
        }

        public static uint DequeueCompletions(RioCompletionQueue completionQueue, ref RioRequestResults results)
        {
            return NativeMethods.RioDequeueCompletion(completionQueue, ref results, RioRequestResults.MaxResults);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Notify(RioCompletionQueue completionQueue)
        {
            ThrowIfErrored(NativeMethods.RioNotify(completionQueue), RioException.ActionType.Notify);
        }

        public static void ResizeCompletionQueue(RioCompletionQueue completionQueue, uint queueSize)
        {
            ThrowIfErrored(NativeMethods.RioResizeCompletionQueue(completionQueue, queueSize), RioException.ActionType.ResizeCompletionQueue);
        }

        public static void ResizeRequestQueue(RioRequestQueue rq, uint maxOutstandingReceive, uint maxOutstandingSend)
        {
            ThrowIfErrored(NativeMethods.RioResizeRequestQueue(rq, maxOutstandingReceive, maxOutstandingSend), RioException.ActionType.ResizeRequestQueue);
        }


        public static void Shutdown(RioConnectedSocket s, SocketShutdown how)
        {
            ThrowIfErrored(NativeMethods.shutdown(s, how), RioException.ActionType.ResizeRequestQueue);
        }

        private static void ThrowIfErrored(int status, RioException.ActionType actionType)
        {
            // Note: method is explicitly small so the success case is easily inlined
            if (status != 0)
            {
                //WSAEINVAL
                //WSAEALREADY
                ThrowError(actionType);
            }
        }

        private static void ThrowIfErrored(bool success, RioException.ActionType actionType)
        {
            // Note: method is explicitly small so the success case is easily inlined
            if (!success)
            {
                ThrowError(actionType);
            }
        }
        private static void ThrowError(RioException.ActionType actionType)
        {
            // Note: only has one throw block so it will marked as "Does not return" by the jit
            // and not inlined into previous function, while also marking as a function
            // that does not need cpu register prep to call (see: https://github.com/dotnet/coreclr/pull/6103)
            throw GetError(actionType);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static RioException GetError(RioException.ActionType actionType)
        {
            var errorNo = NativeMethods.WSAGetLastError();

            string errorMessage;
            switch (errorNo)
            {
                case 10014: // WSAEFAULT
                    errorMessage = $"{actionType} failed: WSAEFAULT - The system detected an invalid pointer address in attempting to use a pointer argument in a call.";
                    break;
                case 10022: // WSAEINVAL
                    errorMessage = $"{actionType} failed: WSAEINVAL -  the SocketQueue parameter is not valid, the Flags parameter contains an value not valid for a send operation, or the integrity of the completion queue has been compromised.";
                    break;
                case 10055: // WSAENOBUFS
                    errorMessage = $"{actionType} failed: WSAENOBUFS - Sufficient memory could not be allocated, the I/O completion queue associated with the SocketQueue parameter is full.";
                    break;
                case 997: // WSA_IO_PENDING
                    errorMessage = $"{actionType} failed? WSA_IO_PENDING - The operation has been successfully initiated and the completion will be queued at a later time.";
                    break;
                case 995: // WSA_OPERATION_ABORTED
                    errorMessage = $"{actionType} failed. WSA_OPERATION_ABORTED - The operation has been canceled while the receive operation was pending.";
                    break;
                default:
                    errorMessage = $"{actionType} failed:  WSA error code {errorNo}";
                    break;
            }

            throw new RioException(errorMessage);
        }

        [Flags]
        private enum RioSendFlags : uint
        {
            None = 0x00000000,
            DontNotify = 0x00000001,
            Defer = 0x00000002,
            CommitOnly = 0x00000008
        }

        [Flags]
        private enum RioReceiveFlags : uint
        {
            None = 0x00000000,
            DontNotify = 0x00000001,
            Defer = 0x00000002,
            Waitall = 0x00000004,
            CommitOnly = 0x00000008
        }

        private enum SocketFlags : uint
        {
            Overlapped = 0x01,
            MultipointCRoot = 0x02,
            MultipointCLeaf = 0x04,
            MultipointDRoot = 0x08,
            MultipointDLeaf = 0x10,
            AccessSystemSecurity = 0x40,
            NoHandleInherit = 0x80,
            RegisteredIO = 0x100
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RioExtensionFunctionTable
        {
            public UInt32 Size;

            public IntPtr RIOReceive;
            public IntPtr RIOReceiveEx;
            public IntPtr RIOSend;
            public IntPtr RIOSendEx;
            public IntPtr RIOCloseCompletionQueue;
            public IntPtr RIOCreateCompletionQueue;
            public IntPtr RIOCreateRequestQueue;
            public IntPtr RIODequeueCompletion;
            public IntPtr RIODeregisterBuffer;
            public IntPtr RIONotify;
            public IntPtr RIORegisterBuffer;
            public IntPtr RIOResizeCompletionQueue;
            public IntPtr RIOResizeRequestQueue;
        }

        private enum AddressFamilies : short
        {
            Internet = 2,
        }

        private enum Protocol : short
        {
            IpProtocolTcp = 6,
        }

        private struct Version
        {
            public ushort Raw;

            public Version(byte major, byte minor)
            {
                Raw = major;
                Raw <<= 8;
                Raw += minor;
            }

            public byte Major
            {
                get
                {
                    ushort result = Raw;
                    result >>= 8;
                    return (byte)result;
                }
            }

            public byte Minor
            {
                get
                {
                    ushort result = Raw;
                    result &= 0x00FF;
                    return (byte)result;
                }
            }

            public override string ToString()
            {
                return string.Format("{0}.{1}", Major, Minor);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowsSocketsData
        {
            internal short Version;
            internal short HighVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)]
            internal string Description;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)]
            internal string SystemStatus;
            internal short MaxSockets;
            internal short MaxDatagramSize;
            internal IntPtr VendorInfo;
        }

        const string Kernel_32 = "Kernel32";
        const long INVALID_HANDLE_VALUE = -1;

        [DllImport(Kernel_32, SetLastError = true)]
        private static extern long GetLastError();

        private static class NativeDelegates
        {

            [SuppressUnmanagedCodeSecurity]
            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
            public delegate RioRegisteredBuffer RioRegisterBuffer([In] IntPtr dataBuffer, [In] UInt32 dataLength);

            [SuppressUnmanagedCodeSecurity]
            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
            public delegate void RioDeregisterBuffer([In] RioRegisteredBuffer bufferId);

            [SuppressUnmanagedCodeSecurity]
            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
            public delegate bool RioSend([In] RioRequestQueue socketQueue, [In] ref RioBufferSegment rioBuffer, [In] UInt32 dataBufferCount, [In] RioSendFlags flags, [In] long requestCorrelation);

            [SuppressUnmanagedCodeSecurity]
            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
            public delegate bool RioSendCommit([In] RioRequestQueue socketQueue, [In] IntPtr nullBufferSegment, [In] UInt32 dataBufferCount, [In] RioSendFlags flags, [In] long requestCorrelation);

            [SuppressUnmanagedCodeSecurity]
            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
            public delegate bool RioReceive([In] RioRequestQueue socketQueue, [In] ref RioBufferSegment rioBuffer, [In] UInt32 dataBufferCount, [In] RioReceiveFlags flags, [In] long requestCorrelation);

            [SuppressUnmanagedCodeSecurity]
            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
            public delegate RioCompletionQueue RioCreateCompletionQueueEvent([In] uint queueSize, [In] NotificationCompletionEvent notificationCompletion);

            [SuppressUnmanagedCodeSecurity]
            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
            public delegate void RioCloseCompletionQueue([In] RioCompletionQueue cq);

            [SuppressUnmanagedCodeSecurity]
            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
            public delegate RioRequestQueue RioCreateRequestQueue(
                [In] RioConnectedSocket socket,
                [In] UInt32 maxOutstandingReceive,
                [In] UInt32 maxReceiveDataBuffers,
                [In] UInt32 maxOutstandingSend,
                [In] UInt32 maxSendDataBuffers,
                [In] RioCompletionQueue receiveCq,
                [In] RioCompletionQueue sendCq,
                [In] long connectionCorrelation
            );

            [SuppressUnmanagedCodeSecurity]
            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
            public delegate uint RioDequeueCompletion([In] RioCompletionQueue cq, [In] ref RioRequestResults results, [In] uint resultArrayLength);

            [SuppressUnmanagedCodeSecurity]
            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
            public delegate Int32 RioNotify([In] RioCompletionQueue cq);

            [SuppressUnmanagedCodeSecurity]
            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
            public delegate bool RioResizeCompletionQueue([In] RioCompletionQueue cq, [In] UInt32 queueSize);

            [SuppressUnmanagedCodeSecurity]
            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
            public delegate bool RioResizeRequestQueue([In] RioRequestQueue rq, [In] UInt32 maxOutstandingReceive, [In] UInt32 maxOutstandingSend);

        }

        private static class NativeMethods
        {
            public static void Initalize()
            {
                // triggers the static .cctor
            }

            static unsafe NativeMethods()
            {
                var version = new Version(2, 2);
                var result = NativeMethods.WSAStartup((short)version.Raw, out var wsaData);
                if (result != SocketError.Success)
                {
                    var error = NativeMethods.WSAGetLastError();
                    throw new Exception(string.Format("ERROR: WSAStartup returned {0}", error));
                }

                var socket = CreateListenSocket();

                UInt32 dwBytes = 0;
                RioExtensionFunctionTable rio = new RioExtensionFunctionTable();
                Guid rioFunctionsTableId = new Guid("8509e081-96dd-4005-b165-9e2ee8c79e3f");
                const uint IocOut = 0x40000000;
                const uint IocIn = 0x80000000;
                const uint IOC_INOUT = IocIn | IocOut;
                const uint IocWs2 = 0x08000000;
                const uint SioGetMultipleExtensionFunctionPointer = IOC_INOUT | IocWs2 | 36;


                //int True = -1;

                //int result = NativeMethods.setsockopt(socket, IPPROTO_TCP, TcpNodelay, (char*)&True, 4);
                //if (result != 0)
                //{
                //    var error = WSAGetLastError();
                //    WSACleanup();
                //    throw new Exception($"ERROR: setsockopt TCP_NODELAY returned {error}");
                //}

                //result = WSAIoctlGeneral(socket, SioLoopbackFastPath,
                //                    &True, 4, null, 0,
                //                    out dwBytes, IntPtr.Zero, IntPtr.Zero);

                //if (result != 0)
                //{
                //    var error = WSAGetLastError();
                //    WSACleanup();
                //    throw new Exception($"ERROR: WSAIoctl SIO_LOOPBACK_FAST_PATH returned {error}");
                //}

                result = (SocketError)NativeMethods.WSAIoctl(socket, SioGetMultipleExtensionFunctionPointer,
                    ref rioFunctionsTableId, 16, ref rio,
                    sizeof(RioExtensionFunctionTable),
                    out dwBytes, IntPtr.Zero, IntPtr.Zero);

                if (result != SocketError.Success)
                {
                    var error = NativeMethods.WSAGetLastError();
                    NativeMethods.WSACleanup();
                    throw new Exception($"ERROR: RIOInitalize returned {error}");
                }

                RioRegisterBuffer = Marshal.GetDelegateForFunctionPointer<NativeDelegates.RioRegisterBuffer>(rio.RIORegisterBuffer);

                RioCreateCompletionQueueEvent = Marshal.GetDelegateForFunctionPointer<NativeDelegates.RioCreateCompletionQueueEvent>(rio.RIOCreateCompletionQueue);

                RioCreateRequestQueue = Marshal.GetDelegateForFunctionPointer<NativeDelegates.RioCreateRequestQueue>(rio.RIOCreateRequestQueue);

                RioNotify = Marshal.GetDelegateForFunctionPointer<NativeDelegates.RioNotify>(rio.RIONotify);
                RioDequeueCompletion = Marshal.GetDelegateForFunctionPointer<NativeDelegates.RioDequeueCompletion>(rio.RIODequeueCompletion);

                RioReceive = Marshal.GetDelegateForFunctionPointer<NativeDelegates.RioReceive>(rio.RIOReceive);
                RioSend = Marshal.GetDelegateForFunctionPointer<NativeDelegates.RioSend>(rio.RIOSend);
                RioSendCommit = Marshal.GetDelegateForFunctionPointer<NativeDelegates.RioSendCommit>(rio.RIOSend);

                RioCloseCompletionQueue = Marshal.GetDelegateForFunctionPointer<NativeDelegates.RioCloseCompletionQueue>(rio.RIOCloseCompletionQueue);
                RioDeregisterBuffer = Marshal.GetDelegateForFunctionPointer<NativeDelegates.RioDeregisterBuffer>(rio.RIODeregisterBuffer);
                RioResizeCompletionQueue = Marshal.GetDelegateForFunctionPointer<NativeDelegates.RioResizeCompletionQueue>(rio.RIOResizeCompletionQueue);
                RioResizeRequestQueue = Marshal.GetDelegateForFunctionPointer<NativeDelegates.RioResizeRequestQueue>(rio.RIOResizeRequestQueue);

                CloseSocket(socket);
            }

            private const string Ws232 = "WS2_32.dll";

            public static readonly NativeDelegates.RioRegisterBuffer RioRegisterBuffer;
            public static readonly NativeDelegates.RioCreateCompletionQueueEvent RioCreateCompletionQueueEvent;
            public static readonly NativeDelegates.RioCreateRequestQueue RioCreateRequestQueue;
            public static readonly NativeDelegates.RioReceive RioReceive;
            public static readonly NativeDelegates.RioSend RioSend;
            public static readonly NativeDelegates.RioSendCommit RioSendCommit;
            public static readonly NativeDelegates.RioNotify RioNotify;
            public static readonly NativeDelegates.RioCloseCompletionQueue RioCloseCompletionQueue;
            public static readonly NativeDelegates.RioDequeueCompletion RioDequeueCompletion;
            public static readonly NativeDelegates.RioDeregisterBuffer RioDeregisterBuffer;
            public static readonly NativeDelegates.RioResizeCompletionQueue RioResizeCompletionQueue;
            public static readonly NativeDelegates.RioResizeRequestQueue RioResizeRequestQueue;

            [SuppressUnmanagedCodeSecurity]
            [DllImport(Ws232, SetLastError = true)]
            public static extern Event WSACreateEvent();

            [SuppressUnmanagedCodeSecurity]
            [DllImport(Ws232, SetLastError = true)]
            public static extern void WSACloseEvent(Event @event);
            

            // Winsock functions

            [DllImport(Ws232, SetLastError = true)]
            public static extern int WSAIoctl(
                [In] RioListenSocket socket,
                [In] uint dwIoControlCode,
                [In] ref Guid lpvInBuffer,
                [In] uint cbInBuffer,
                [In, Out] ref RioExtensionFunctionTable lpvOutBuffer,
                [In] int cbOutBuffer,
                [Out] out uint lpcbBytesReturned,
                [In] IntPtr lpOverlapped,
                [In] IntPtr lpCompletionRoutine
            );

            [DllImport(Ws232, SetLastError = true, EntryPoint = "WSAIoctl")]
            private unsafe static extern int WSAIoctlGeneral(
              [In] IntPtr socket,
              [In] int dwIoControlCode,
              [In] int* lpvInBuffer,
              [In] uint cbInBuffer,
              [In] int* lpvOutBuffer,
              [In] int cbOutBuffer,
              [Out] out uint lpcbBytesReturned,
              [In] IntPtr lpOverlapped,
              [In] IntPtr lpCompletionRoutine
            );

            [DllImport(Ws232, SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = true, ThrowOnUnmappableChar = true)]
            internal static extern SocketError WSAStartup([In] short wVersionRequested, [Out] out WindowsSocketsData lpWindowsSocketsData);

            [DllImport(Ws232, SetLastError = true, CharSet = CharSet.Ansi)]
            public static extern RioListenSocket WSASocket([In] AddressFamilies af, [In] SocketType type, [In] Protocol protocol, [In] IntPtr lpProtocolInfo, [In] Int32 group, [In] SocketFlags dwFlags);

            [DllImport(Ws232, SetLastError = true)]
            public static extern ushort htons([In] ushort hostshort);

            [DllImport(Ws232, SetLastError = true, CharSet = CharSet.Ansi)]
            public static extern int bind(RioListenSocket s, ref byte name, int namelen);

            [DllImport(Ws232, SetLastError = true)]
            public static extern int listen(RioListenSocket s, int backlog);

            [DllImport(Ws232, SetLastError = true)]
            public unsafe static extern int setsockopt(RioListenSocket s, int level, int optname, char* optval, int optlen);

            [DllImport(Ws232, SetLastError = true)]
            public unsafe static extern int setsockopt(RioConnectedSocket s, int level, int optname, char* optval, int optlen);

            [DllImport(Ws232, SetLastError = true)]
            public static extern RioConnectedSocket accept(RioListenSocket s, IntPtr addr, int addrlen);

            [DllImport(Ws232)]
            public static extern Int32 WSAGetLastError();

            [DllImport(Ws232, SetLastError = true)]
            public static extern Int32 WSACleanup();

            [DllImport(Ws232, SetLastError = true)]
            public static extern int shutdown(RioConnectedSocket s, SocketShutdown how);

            [DllImport(Ws232, SetLastError = true)]
            public static extern int closesocket(RioListenSocket s);

            [DllImport(Ws232, SetLastError = true)]
            public static extern int closesocket(RioConnectedSocket s);

            [DllImport(Ws232, SetLastError = true)]
            public static extern int getsockname(RioListenSocket socket, out SockAddr socketAddress, ref int namelen);

            [DllImport(Ws232, SetLastError = true)]
            public static extern int getsockname(RioConnectedSocket socket, out SockAddr socketAddress, ref int namelen);

            [DllImport(Ws232, SetLastError = true)]
            public static extern int getpeername(RioConnectedSocket socket, out SockAddr socketAddress, ref int namelen);
        }
    }
}