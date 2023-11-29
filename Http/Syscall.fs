namespace Http

open System
open System.Buffers.Binary
open System.Net
open System.Runtime.InteropServices

type AddressFamily =
    /// aka IPv4
    | INET = 2s

[<RequireQualifiedAccess>]
module AddressFamily =
    let toInt (af : AddressFamily) = int af

type SocketType =
    | STREAM = 1

[<RequireQualifiedAccess>]
module SocketType =
    let toInt (s : SocketType) = int s

[<Struct ; StructLayout(LayoutKind.Sequential)>]
type SockAddrIPv4 =
    private
        {
            SaFamily : AddressFamily
            InPort : int16
            InAddr : uint32
            Padding : int64
        }

    static member Create (address : IPAddress) (port : int16) =
        {
            SaFamily = AddressFamily.INET
            InPort =
                if BitConverter.IsLittleEndian then
                    BinaryPrimitives.ReverseEndianness port
                else
                    port
            InAddr =
                // TODO: my computer is little-endian; does this need to be endian-aware?
                BitConverter.ToUInt32 (address.GetAddressBytes (), 0)
            Padding = 0
        }

[<RequireQualifiedAccess>]
module Syscall =
    type byteptr = nativeptr<byte>
    type sockptr = nativeptr<SockAddrIPv4>

    [<DllImport("libc", SetLastError = true)>]
    extern int socket(int domain, int typ, int protocol)

    [<DllImport("libc", SetLastError = true)>]
    extern int connect(int socket, sockptr addr, uint addrLen)

    [<DllImport("libc", SetLastError = true)>]
    extern int close(int fd)

    [<DllImport("libc", SetLastError = true)>]
    extern int read(int fd, byteptr buf, uint count)

    [<DllImport("libc", SetLastError = true)>]
    extern int write(int fd, byteptr buf, uint count)
