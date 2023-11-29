#nowarn "9"

namespace Http

open System
open System.Runtime.InteropServices
open System.Diagnostics
open System.Net
open System.Net.Sockets
open Microsoft.FSharp.NativeInterop

type AddressFamily =
    | INET = 2

type SocketType =
    | STREAM = 1

module Syscall =
    type byteptr = nativeptr<byte>
    [<DllImport ("libc", SetLastError=true)>]
    extern int socket (int domain, int typ, int protocol)

    [<DllImport ("libc", SetLastError=true)>]
    extern int connect (int socket, byteptr addr, uint addrLen)

    [<DllImport ("libc", SetLastError=true)>]
    extern int close (int fd)

    [<DllImport ("libc", SetLastError=true)>]
    extern int read (int fd, byteptr buf, uint count)

    [<DllImport ("libc", SetLastError=true)>]
    extern int write (int fd, byteptr buf, uint count)

type Sock =
    private
        {
            FileDescriptor : int
        }

    member this.Close () =
        let result = Syscall.close this.FileDescriptor
        if result < 0 then
            failwith "failed to close"

module Sock =
    let create (af : AddressFamily) (sock : SocketType) =
        let fd = Syscall.socket (int af, int sock, 0)
        if fd < 0 then
            failwith "failed to create"
        {
            FileDescriptor = fd
        }

    let write (sock : Sock) (buffer : byte []) (offset : int) (count : uint) =
        use buffer = fixed buffer
        let result = Syscall.write (sock.FileDescriptor, buffer, count)
        if result < 0 then
            failwith "failed to write"
        result

    let read (sock : Sock) (buffer : byte []) (offset : int) (count : uint) =
        use buffer = fixed buffer
        let result = Syscall.read (sock.FileDescriptor, buffer, count)
        if result < 0 then
            failwith "failed to read"
        result

    let close (sock : Sock) =
        sock.Close ()

type ConnectedSocket (sock : Socket, ep : IPEndPoint) =
    do
        sock.Connect ep
        ()
    interface IDisposable with
        member _.Dispose () =
            sock.Shutdown SocketShutdown.Both
            sock.Close ()

module Program =
    [<EntryPoint>]
    let main argv =
        let sw = Stopwatch.StartNew ()
        let ip =
            sw.Restart ()
            let dns = Dns.GetHostEntry "example.com"
            sw.Stop ()
            printfn "DNS lookup: %ims" sw.ElapsedMilliseconds
            dns.AddressList.[0]
        let ep = IPEndPoint (ip, 80)

        sw.Restart ()
        use client = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        sw.Stop ()
        printfn "Socket open: %ims" sw.ElapsedMilliseconds

        sw.Restart ()
        use _ = new ConnectedSocket (client, ep)
        sw.Stop ()
        printfn "Socket connect: %ims" sw.ElapsedMilliseconds

        let message =
            [
                "GET / HTTP/1.1"
                "Host: example.com"
                "Connection: close"
                ""
                ""
            ]
            |> String.concat "\r\n"
            |> System.Text.Encoding.ASCII.GetBytes

        do
            let mutable written = 0
            while written < message.Length do
                sw.Restart ()
                let write = client.Send (message, written, message.Length - written, SocketFlags.None)
                sw.Stop ()
                printfn "Took %ims to write %i bytes (of %i total)" sw.ElapsedMilliseconds write message.Length
                written <- written + write

        let response =
            let buffer = Array.zeroCreate<byte> 1024
            let mutable total = 0
            let mutable isDone = false
            let result = ResizeArray ()
            while not isDone do
                sw.Restart ()
                let read = client.Receive buffer
                sw.Stop ()
                printfn "Took %ims to read %i bytes" sw.ElapsedMilliseconds read
                total <- total + read
                result.AddRange buffer.[0..read-1]
                if read = 0 then
                    isDone <- true

            result.ToArray ()

        Console.WriteLine (System.Text.Encoding.ASCII.GetString response)

        0
