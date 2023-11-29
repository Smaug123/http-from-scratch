namespace Http

open System
open System.Diagnostics
open System.Net
open System.Text

module Program =
    [<EntryPoint>]
    let main argv =
        let sw = Stopwatch.StartNew ()

        let ip =
            sw.Restart ()
            let dns = Dns.GetHostEntry "example.com"
            sw.Stop ()
            Printfn.time "DNS lookup:" sw.ElapsedMilliseconds
            dns.AddressList.[0]

        sw.Restart ()
        use sock = Sock.create AddressFamily.INET SocketType.STREAM
        sw.Stop ()
        Printfn.time "Socket create:" sw.ElapsedMilliseconds

        let addr = SockAddrIPv4.Create ip 80s
        sw.Restart ()
        Sock.connect sock addr
        sw.Stop ()
        Printfn.time "Socket connect:" sw.ElapsedMilliseconds

        let message =
            {
                HttpRequest.Request = RequestLine.OriginForm ("GET", "example.com", "/")
                Headers =
                    [
                        {
                            Name = "Connection"
                            Value = "close"
                        }
                    ]
                Body = Array.Empty<_> ()
            }
            |> fun r -> r.ToBytes ()

        do
            let mutable written = 0

            while written < message.Length do
                sw.Restart ()
                let write = Sock.write sock message written (message.Length - written |> uint)
                sw.Stop ()
                Printfn.time "Write:" sw.ElapsedMilliseconds
                Printfn.int "Bytes written:" write
                written <- written + write

        let response =
            let buffer = Array.zeroCreate<byte> 1024
            let mutable total = 0
            let mutable isDone = false
            let result = ResizeArray ()

            while not isDone do
                sw.Restart ()
                let read = Sock.read sock buffer 0u (uint buffer.Length)
                sw.Stop ()
                Printfn.time "Read:" sw.ElapsedMilliseconds
                Printfn.int "Bytes read:" read
                total <- total + read
                result.AddRange buffer.[0 .. read - 1]

                if read = 0 then
                    isDone <- true

            result.ToArray ()

        Console.WriteLine (System.Text.Encoding.ASCII.GetString response)

        0
