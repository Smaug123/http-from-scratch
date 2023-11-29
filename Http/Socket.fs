#nowarn "9"

namespace Http

open System
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop

type Sock =
    private
        {
            FileDescriptor : int
        }

    member this.Close () =
        let result = Syscall.close this.FileDescriptor

        if result < 0 then
            let err = Marshal.GetLastWin32Error ()
            failwithf "failed to close: %i" err

    interface IDisposable with
        member this.Dispose () = this.Close ()

[<RequireQualifiedAccess>]
module Sock =
    let create (af : AddressFamily) (sock : SocketType) : Sock =
        let fd = Syscall.socket (AddressFamily.toInt af, SocketType.toInt sock, 0)

        if fd < 0 then
            let err = Marshal.GetLastWin32Error ()
            failwithf "failed to create: %i" err

        {
            FileDescriptor = fd
        }

    let write (sock : Sock) (buffer : byte[]) (offset : int) (count : uint) =
        use buffer = fixed buffer
        let result = Syscall.write (sock.FileDescriptor, NativePtr.add buffer offset, count)

        if result < 0 then
            let err = Marshal.GetLastWin32Error () |> enum<Errno>
            failwithf "failed to write: %O" err

        result

    let read (sock : Sock) (buffer : byte[]) (offset : uint) (count : uint) =
        use buffer = fixed buffer

        let result =
            Syscall.read (sock.FileDescriptor, NativePtr.add buffer (int offset), count)

        if result < 0 then
            let err = Marshal.GetLastWin32Error () |> enum<Errno>
            failwithf "failed to read: %O" err

        result

    type private ConnectionState =
        | NotStarted
        | Done
        | Interrupted

    let connect (sock : Sock) (addr : SockAddrIPv4) =
        use addr = fixed &addr
        let mutable isDone = ConnectionState.NotStarted

        while isDone <> ConnectionState.Done do
            let result =
                Syscall.connect (sock.FileDescriptor, addr, Marshal.SizeOf<SockAddrIPv4> () |> uint32)

            if result = 0 then
                isDone <- ConnectionState.Done
            elif result > 0 then
                failwithf "bad result from connect: %i" result
            else
            // Error case begins!

            let err = Marshal.GetLastWin32Error () |> enum<Errno>

            match isDone with
            | ConnectionState.Interrupted ->
                // It's OK to get "already connected", we were previously interrupted and we can't tell
                // how far the connection had got before we retried
                if err = Errno.EISCONN then
                    isDone <- ConnectionState.Done
                else
                    failwithf "failed to connect: %O" err
            | _ ->

            if err = Errno.EINTR then
                printfn "Retrying due to EINTR"
                isDone <- ConnectionState.Interrupted
            else
                failwithf "failed to connect: %O" err

    let close (sock : Sock) = sock.Close ()
